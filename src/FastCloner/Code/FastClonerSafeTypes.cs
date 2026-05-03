using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
#if !MODERN
using System.Runtime.Serialization;
#endif
using System.Text;

namespace FastCloner.Code;

/// <summary>
/// Safe types are types, which can be copied without real cloning. e.g. simple structs or strings (it is immutable)
/// </summary>
internal static class FastClonerSafeTypes
{
    internal static readonly Dictionary<Type, bool> DefaultKnownTypes = new Dictionary<Type, bool>(64)
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

    private static ConcurrentDictionary<Type, bool> knownTypes = [];

    static FastClonerSafeTypes()
    {
        knownTypes = BuildKnownTypes();
    }

    private static ConcurrentDictionary<Type, bool> BuildKnownTypes()
    {
        ConcurrentDictionary<Type, bool> result = new ConcurrentDictionary<Type, bool>();
        
        foreach (KeyValuePair<Type, bool> x in DefaultKnownTypes)
        {
            result.TryAdd(x.Key, x.Value);
        }
        
        List<Type?> safeTypes =
        [
            Type.GetType("System.RuntimeType"),
            Type.GetType("System.RuntimeTypeHandle")
        ];

        foreach (Type x in safeTypes.OfType<Type>())
        {
            result.TryAdd(x, true);
        }

        return result;
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
        public const string SystemCollectionsFrozen = "System.Collections.Frozen.";
    }
    
    private static bool IsReflectionType(Type type)
    {
        if (type == typeof(AssemblyName))
        {
            return false;
        }
    
        return type.FullName?.StartsWith(TypePrefixes.SystemReflection) is true && Equals(type.GetTypeInfo().Assembly, typeof(PropertyInfo).GetTypeInfo().Assembly);
    }

    private static readonly ConcurrentDictionary<Type, FieldInfo[]> allTypeFieldsCache = new ConcurrentDictionary<Type, FieldInfo[]>();

    private static FieldInfo[] GetAllTypeFields(Type type)
    {
        return allTypeFieldsCache.TryGetValue(type, out FieldInfo[]? cached) ? cached : allTypeFieldsCache.GetOrAdd(type, BuildAllTypeFields);
    }

    private static FieldInfo[] BuildAllTypeFields(Type type)
    {
        Type? current = type;
        List<FieldInfo>? acc = null;
        FieldInfo[]? singleLevel = null;

        while (current is not null)
        {
            FieldInfo[] level = current.GetAllFields();
            if (level.Length > 0)
            {
                if (singleLevel is null)
                {
                    singleLevel = level;
                }
                else
                {
                    acc ??= [.. singleLevel];
                    acc.AddRange(level);
                }
            }

            current = current.BaseType;
        }

        if (acc is not null)
            return acc.ToArray();

        return singleLevel ?? [];
    }
    
    private static bool IsAnonymousType(Type type)
    {
        return FastClonerCache.GetOrAddAnonymousTypeStatus(type, t => 
            t.IsClass && t is { IsSealed: true, IsNotPublic: true }
                      && t.IsDefined(typeof(CompilerGeneratedAttribute), false)
                      && (t.Name.StartsWith("<>") || t.Name.StartsWith("VB$"))
                      && t.Name.Contains("AnonymousType"));
    }

    private static readonly HashSet<string> safeTypeExact = new HashSet<string>(StringComparer.Ordinal)
    {
        "System.Threading.Tasks.Task",
        "Microsoft.EntityFrameworkCore.Internal.ConcurrencyDetector"
    };

    private static readonly AhoCorasick safeTypePrefixes = new AhoCorasick([
        TypePrefixes.SystemRuntimeType,
        TypePrefixes.MicrosoftExtensions,
        TypePrefixes.SystemCollectionsFrozen,
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
            Type genericDef = type.GetGenericTypeDefinition();
            
            if (knownTypes.TryGetValue(genericDef, out bool isGenericSafe))
            {
                knownTypes.TryAdd(type, isGenericSafe);
                return isGenericSafe;
            }
        }

        if (typeof(Delegate).IsAssignableFrom(type))
        {
            knownTypes.TryAdd(type, true);
            return true;
        }
        
        string? fullName = type.FullName;

        if (fullName is null || IsSafeSystemType(type) || fullName.Contains("EqualityComparer") && IsSpecialEqualityComparer(fullName))
        {
            knownTypes.TryAdd(type, true);
            return true;
        }

        if (!IsAnonymousType(type) && !type.IsValueType())
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
        knownTypes = BuildKnownTypes();
    }
    
    /// <summary>
    /// Determines whether GetHashCode() result won't change after deep cloning.
    /// </summary>
    internal static bool HasStableHashSemantics(Type type)
    {
        HashSet<Type>? stack = probeStack;
        if (stack is not null && stack.Contains(type))
        {
            return false;
        }

        return FastClonerCache.GetOrAddStableHashSemantics(type, ComputeStableHashSemanticsWithCycleGuard);
    }

    [ThreadStatic]
    private static HashSet<Type>? probeStack;

    private static bool ComputeStableHashSemanticsWithCycleGuard(Type type)
    {
        HashSet<Type> stack = probeStack ??= [];
        stack.Add(type);
        try
        {
            return CalculateHasStableHashSemantics(type);
        }
        finally
        {
            stack.Remove(type);
        }
    }
    
    private static bool CalculateHasStableHashSemantics(Type type)
    {
        if (type.IsPrimitive)
            return true;
        
        if (type == typeof(string))
            return true;
        
        if (type.IsEnum)
            return true;
        
        if (DefaultKnownTypes.ContainsKey(type))
            return true;

        if (type.IsDefined(typeof(FastClonerStableHashAttribute), inherit: true))
            return true;
        
        if (type.IsValueType && ValueTypeHasNoReferenceFields(type))
            return true;

        MethodInfo? getHashCodeMethod = type.GetMethod(
            "GetHashCode",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null);

        if (getHashCodeMethod is null)
            return false;

        if (!type.IsValueType && getHashCodeMethod.DeclaringType == typeof(object))
            return false;

        return ProbeOverriddenHashIsValueBased(type);
    }

    private static bool ValueTypeHasNoReferenceFields(Type type, HashSet<Type>? visited = null)
    {
        if (!type.IsValueType)
            return false;

        if (type.IsPrimitive || type.IsEnum)
            return true;

        visited ??= [];
        if (!visited.Add(type))
            return true;

        foreach (FieldInfo f in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            Type ft = f.FieldType;
            if (!ft.IsValueType)
                return false;

            if (ft.IsPrimitive || ft.IsEnum)
                continue;

            if (!ValueTypeHasNoReferenceFields(ft, visited))
                return false;
        }

        return true;
    }

    [SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize")]
    private static bool ProbeOverriddenHashIsValueBased(Type type)
    {
        if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters
            || type.IsArray || type.IsPointer || type.IsByRef || type == typeof(string))
        {
            return false;
        }

        try
        {
            object instance1 = CreateUninitialized(type);
            object instance2 = CreateUninitialized(type);

            if (!type.IsValueType)
            {
                GC.SuppressFinalize(instance1);
                GC.SuppressFinalize(instance2);
            }
            
            foreach (FieldInfo field in GetAllTypeFields(type))
            {
                if (field.IsStatic)
                    continue;

                Type ft = field.FieldType;
                if (ft.IsValueType)
                    continue;

                if (!CanProbeAllocate(ft))
                    continue;

                if (HasStableHashSemantics(ft))
                {
                    object? sample = TryCreateProbeSample(ft);
                    if (sample is null)
                        continue;

                    TrySetField(field, instance1, sample);
                    TrySetField(field, instance2, sample);
                }
                else
                {
                    object? a = TryCreateProbeSample(ft);
                    object? b = TryCreateProbeSample(ft);
                    if (a is null || b is null)
                        continue;

                    TrySetField(field, instance1, a);
                    TrySetField(field, instance2, b);
                }
            }

            int hash1 = instance1.GetHashCode();
            int hash2 = instance2.GetHashCode();

            return hash1 == hash2;
        }
        catch
        {
            return false;
        }
    }

    private static bool CanProbeAllocate(Type type)
    {
        return type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false, IsArray: false, IsPointer: false, IsByRef: false, IsCOMObject: false };
    }

    [SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize")]
    private static object? TryCreateProbeSample(Type type)
    {
        if (type == typeof(string))
            return string.Empty;

        try
        {
            object inst = CreateUninitialized(type);
            GC.SuppressFinalize(inst);
            return inst;
        }
        catch
        {
            return null;
        }
    }

    private static void TrySetField(FieldInfo field, object target, object value)
    {
        try
        {
            field.SetValue(target, value);
        }
        catch
        {
            // init-only/readonly with strict runtime enforcement: leave field as default.
        }
    }

    private static object CreateUninitialized(Type type)
    {
#if MODERN
        return RuntimeHelpers.GetUninitializedObject(type);
#else
        return FormatterServices.GetUninitializedObject(type);
#endif
    }
}
