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
    internal static readonly Dictionary<Type, bool> DefaultKnownTypes = new Dictionary<Type, bool>(34)
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
#if MODERN
        [typeof(Rune)] = true,
#endif

        // Time-related types
        [typeof(TimeSpan)] = true,
        [typeof(TimeZoneInfo)] = true,
        [typeof(DateTime)] = true,
        [typeof(DateTimeOffset)] = true,
#if MODERN
        [typeof(DateOnly)] = true,
        [typeof(TimeOnly)] = true,
#endif

        // Numeric types
 #if MODERN
        [typeof(Half)] = true,
        [typeof(Int128)] = true,
        [typeof(UInt128)] = true,
        [typeof(Complex)] = true,
#endif
        // Others
        [typeof(DBNull)] = true,
        [StringComparer.Ordinal.GetType()] = true,
        [StringComparer.OrdinalIgnoreCase.GetType()] = true,
        [StringComparer.InvariantCulture.GetType()] = true,
        [StringComparer.InvariantCultureIgnoreCase.GetType()] = true,
        [typeof(WeakReference)] = true,
        [typeof(WeakReference<>)] = true,
        [typeof(CancellationTokenSource)] = true,
#if MODERN
        [typeof(Range)] = true,
        [typeof(Index)] = true
#endif
    };

    private static readonly ConcurrentDictionary<Type, bool> knownTypes = [];

    static FastClonerSafeTypes()
    {
        InitializeKnownTypes();
    }

    private static void InitializeKnownTypes()
    {
        foreach (KeyValuePair<Type, bool> x in DefaultKnownTypes)
        {
            knownTypes.TryAdd(x.Key, x.Value);
        }
        
        List<Type?> safeTypes =
        [
            Type.GetType("System.RuntimeType"),
            Type.GetType("System.RuntimeTypeHandle")
        ];

        foreach (Type x in safeTypes.OfType<Type>())
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
        "System.Collections.Generic.StringEqualityComparer" => true,
        _ => false
    };
    
    private static class TypePrefixes
    {
        public const string SystemReflection = "System.Reflection.";
        public const string SystemRuntimeType = "System.RuntimeType";
        public const string MicrosoftExtensions = "Microsoft.Extensions.DependencyInjection.";
    }

    private static readonly Assembly propertyInfoAssembly = typeof(PropertyInfo).Assembly;
    
    private static bool IsReflectionType(Type type)
    {
        if (type == typeof(AssemblyName))
        {
            return false;
        }
    
        return type.FullName?.StartsWith(TypePrefixes.SystemReflection) is true && Equals(type.GetTypeInfo().Assembly, typeof(PropertyInfo).GetTypeInfo().Assembly);
    }

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
    
    private static readonly HashSet<string> safeTypeExact = new HashSet<string>(StringComparer.Ordinal)
    {
        "System.Threading.Tasks.Task",
        "Microsoft.EntityFrameworkCore.Internal.ConcurrencyDetector"
    };

    private static readonly AhoCorasick safeTypePrefixes = new AhoCorasick([
        TypePrefixes.SystemRuntimeType,
        TypePrefixes.MicrosoftExtensions,
        "System.Threading.Tasks.Task`"
    ]);

    private static bool IsSafeSystemType(Type type)
    {
        if (type.IsEnum() || type.IsPointer)
            return true;

        if (type.IsCOMObject)
            return true;

        string? fullName = type.FullName;

        if (fullName is null)
            return true;

        if (safeTypeExact.Contains(fullName))
            return true;

        if (safeTypePrefixes.ContainsAnyPattern(fullName))
            return true;

        if (IsReflectionType(type))
            return true;

        if (type.IsSubclassOf(typeof(System.Runtime.ConstrainedExecution.CriticalFinalizerObject)))
            return true;

        return false;
    }
    
    private static bool CanReturnSameType(Type type, HashSet<Type>? processingTypes = null)
    {
        if (FastClonerCache.IsTypeReference(type))
        {
            return true;
        }
        
        if (knownTypes.TryGetValue(type, out bool isSafe))
        {
            return isSafe;
        }
        
        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            if (knownTypes.TryGetValue(genericDef, out bool isGenericSafe))
            {
                knownTypes.TryAdd(type, isGenericSafe);
                return isGenericSafe;
            }
        }

        if (typeof(Delegate).IsAssignableFrom(type))
        {
            knownTypes.TryAdd(type, false);
            return false;
        }
        
        string? fullName = type.FullName;

        if (fullName is null || IsSafeSystemType(type) || fullName.Contains("EqualityComparer") && IsSpecialEqualityComparer(fullName))
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
    
    internal static void ClearKnownTypesCache()
    {
        knownTypes.Clear();
        InitializeKnownTypes();
    }
}
