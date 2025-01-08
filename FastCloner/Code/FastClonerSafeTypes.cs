using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace FastCloner.Code;

/// <summary>
/// Safe types are types, which can be copied without real cloning. e.g. simple structs or strings (it is immutable)
/// </summary>
internal static class FastClonerSafeTypes
{
    private static readonly ConcurrentDictionary<Type, bool> knownTypes = new ConcurrentDictionary<Type, bool>
    {
        // Primitives
        [typeof(byte)] = true,
        [typeof(short)] = true,
        [typeof(ushort)] = true,
        [typeof(int)] = true,
        [typeof(uint)] = true,
        [typeof(long)] = true,
        [typeof(ulong)] = true,
        [typeof(float)] = true,
        [typeof(double)] = true,
        [typeof(decimal)] = true,
        [typeof(string)] = true,
        [typeof(char)] = true,
        [typeof(bool)] = true,
        [typeof(sbyte)] = true,
        [typeof(nint)] = true,
        [typeof(nuint)] = true,
        [typeof(Guid)] = true,
        [typeof(Rune)] = true,
        
        // Time-related types
        [typeof(TimeSpan)] = true,
        [typeof(TimeZoneInfo)] = true,
        [typeof(DateTime)] = true,
        [typeof(DateTimeOffset)] = true,
        [typeof(DateOnly)] = true,
        [typeof(TimeOnly)] = true,
        
        // Numeric types
        [typeof(Half)] = true,
        [typeof(Int128)] = true,
        [typeof(UInt128)] = true,
        
        // Others
        [typeof(DBNull)] = true,
        [StringComparer.Ordinal.GetType()] = true,
        [StringComparer.OrdinalIgnoreCase.GetType()] = true,
        [StringComparer.InvariantCulture.GetType()] = true,
        [StringComparer.InvariantCultureIgnoreCase.GetType()] = true
    };

    static FastClonerSafeTypes()
    {
        List<Type?> safeTypes =
        [
            Type.GetType("System.RuntimeType"),
            Type.GetType("System.RuntimeTypeHandle")
        ];

        foreach (Type? x in safeTypes.OfType<Type>())
        {
            knownTypes.TryAdd(x, true);
        }
    }

    private static bool CanReturnSameType(Type type, HashSet<Type>? processingTypes)
    {
        if (knownTypes.TryGetValue(type, out bool isSafe))
        {
            return isSafe;
        }

        if (typeof(Delegate).IsAssignableFrom(type))
        {
            knownTypes.TryAdd(type, false);
            return false;
        }
        
        // enums are safe
        // pointers (e.g. int*) are unsafe, but we cannot do anything with it except blind copy
        if (type.IsEnum() || type.IsPointer)
        {
            knownTypes.TryAdd(type, true);
            return true;
        }
        
        if (type.FullName is null)
        {
            knownTypes.TryAdd(type, true);
            return true;
        }

        if (type.FullName.StartsWith("System.Reflection.") && type.Assembly == typeof(PropertyInfo).Assembly)
        {
            knownTypes.TryAdd(type, true);
            return true;
        }

        // these types are serious native resources, it is better not to clone it
        if (type.IsSubclassOf(typeof(System.Runtime.ConstrainedExecution.CriticalFinalizerObject)))
        {
            knownTypes.TryAdd(type, true);
            return true;
        }

        // Better not to do anything with COM
        if (type.IsCOMObject)
        {
            knownTypes.TryAdd(type, true);
            return true;
        }

        if (type.FullName.StartsWith("System.RuntimeType"))
        {
            knownTypes.TryAdd(type, true);
            return true;
        }

        if (type.FullName.StartsWith("System.Reflection.") && Equals(type.GetTypeInfo().Assembly, typeof(PropertyInfo).GetTypeInfo().Assembly))
        {
            knownTypes.TryAdd(type, true);
            return true;
        }

        if (type.IsSubclassOfTypeByName("CriticalFinalizerObject"))
        {
            knownTypes.TryAdd(type, true);
            return true;
        }

        // better not to touch ms dependency injection
        if (type.FullName.StartsWith("Microsoft.Extensions.DependencyInjection."))
        {
            knownTypes.TryAdd(type, true);
            return true;
        }

        if (type.FullName == "Microsoft.EntityFrameworkCore.Internal.ConcurrencyDetector")
        {
            knownTypes.TryAdd(type, true);
            return true;
        }

        // default comparers should not be cloned due possible comparison EqualityComparer<T>.Default == comparer
        if (type.FullName.Contains("EqualityComparer"))
        {
            if (type.FullName.StartsWith("System.Collections.Generic.GenericEqualityComparer`")
                || type.FullName.StartsWith("System.Collections.Generic.ObjectEqualityComparer`")
                || type.FullName.StartsWith("System.Collections.Generic.EnumEqualityComparer`")
                || type.FullName.StartsWith("System.Collections.Generic.NullableEqualityComparer`")
                || type.FullName == "System.Collections.Generic.ByteEqualityComparer")
            {
                knownTypes.TryAdd(type, true);
                return true;
            }
        }

        // classes are always unsafe (we should copy it fully to count references)
        if (!type.IsValueType())
        {
            knownTypes.TryAdd(type, false);
            return false;
        }

        processingTypes ??= [];

        // structs cannot have a loops, but check it anyway
        processingTypes.Add(type);

        List<FieldInfo> fi = [];
        Type? tp = type;
        do
        {
            fi.AddRange(tp.GetAllFields());
            tp = tp.BaseType();
        }
        while (tp != null);

        foreach (FieldInfo fieldInfo in fi)
        {
            // type loop
            Type fieldType = fieldInfo.FieldType;
            if (processingTypes.Contains(fieldType))
                continue;

            // not safe and not not safe. we need to go deeper
            if (!CanReturnSameType(fieldType, processingTypes))
            {
                knownTypes.TryAdd(type, false);
                return false;
            }
        }

        knownTypes.TryAdd(type, true);
        return true;
    }

    public static bool CanReturnSameObject(Type type) => CanReturnSameType(type, null);
}