using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
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
        [typeof(Complex)] = true,
        
        // Others
        [typeof(DBNull)] = true,
        [StringComparer.Ordinal.GetType()] = true,
        [StringComparer.OrdinalIgnoreCase.GetType()] = true,
        [StringComparer.InvariantCulture.GetType()] = true,
        [StringComparer.InvariantCultureIgnoreCase.GetType()] = true,
        [typeof(Range)] = true,
        [typeof(Index)] = true
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
    
    private static bool IsSpecialEqualityComparer(string fullName) => fullName switch
    {
        _ when fullName.StartsWith("System.Collections.Generic.GenericEqualityComparer`") => true,
        _ when fullName.StartsWith("System.Collections.Generic.ObjectEqualityComparer`") => true,
        _ when fullName.StartsWith("System.Collections.Generic.EnumEqualityComparer`") => true,
        _ when fullName.StartsWith("System.Collections.Generic.NullableEqualityComparer`") => true,
        "System.Collections.Generic.ByteEqualityComparer" => true,
        _ => false
    };
    
    private static class TypePrefixes
    {
        public const string SystemReflection = "System.Reflection.";
        public const string SystemRuntimeType = "System.RuntimeType";
        public const string MicrosoftExtensions = "Microsoft.Extensions.DependencyInjection.";
    }

    private static readonly Assembly propertyInfoAssembly = typeof(PropertyInfo).Assembly;
    private static bool IsReflectionType(Type type) => type.FullName?.StartsWith(TypePrefixes.SystemReflection) is true && Equals(type.GetTypeInfo().Assembly, typeof(PropertyInfo).GetTypeInfo().Assembly);

    private static IEnumerable<FieldInfo> GetAllTypeFields(Type type)
    {
        Type? currentType = type;
    
        while (currentType is not null)
        {
            foreach (FieldInfo field in currentType.GetAllFields())
            {
                yield return field;
            }
            
            currentType = currentType.BaseType();
        }
    }
    
    private static bool IsSafeSystemType(Type type)
    {
        if (type.IsEnum() || type.IsPointer)
            return true;

        if (type.IsCOMObject)
            return true;

        if (type.FullName is null)
            return true;

        if (IsReflectionType(type))
            return true;

        if (type.IsSubclassOf(typeof(System.Runtime.ConstrainedExecution.CriticalFinalizerObject)))
            return true;

        if (type.FullName.StartsWith(TypePrefixes.SystemRuntimeType))
            return true;

        if (type.FullName.StartsWith(TypePrefixes.MicrosoftExtensions))
            return true;

        if (type.FullName is "Microsoft.EntityFrameworkCore.Internal.ConcurrencyDetector")
            return true;

        return false;
    }
    
    private static bool CanReturnSameType(Type type, HashSet<Type>? processingTypes = null)
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

        if (IsSafeSystemType(type) || type.FullName?.Contains("EqualityComparer") == true && IsSpecialEqualityComparer(type.FullName))
        {
            knownTypes.TryAdd(type, true);
            return true;
        }

        if (!type.IsValueType())
        {
            knownTypes.TryAdd(type, false);
            return false;
        }

        processingTypes ??= [];

        if (!processingTypes.Add(type))
        {
            return true;
        }

        foreach (FieldInfo fieldInfo in GetAllTypeFields(type))
        {
            Type fieldType = fieldInfo.FieldType;
            
            if (processingTypes.Contains(fieldType))
            {
                continue;
            }

            if (CanReturnSameType(fieldType, processingTypes))
            {
                continue;
            }
            
            knownTypes.TryAdd(type, false);
            return false;
        }

        knownTypes.TryAdd(type, true);
        return true;
    }

    public static bool CanReturnSameObject(Type type) => CanReturnSameType(type);
}