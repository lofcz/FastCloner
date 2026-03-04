using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FastCloner.Code;

/// <summary>
/// Specifies how a type should be handled during cloning.
/// </summary>
public enum CloneBehavior
{
    /// <summary>
    /// Perform deep cloning (default behavior).
    /// </summary>
    Clone,
    
    /// <summary>
    /// Return the same instance without cloning (for immutable/safe types).
    /// </summary>
    Reference,
    
    /// <summary>
    /// Perform shallow cloning (MemberwiseClone).
    /// </summary>
    Shallow,

    /// <summary>
    /// Skip cloning, return default.
    /// </summary>
    Ignore
}

/// <summary>
/// Generic static cache that leverages JIT specialization to avoid dictionary lookups
/// when the compile-time type T matches the runtime type.
/// </summary>
internal static class ClonerCache<T>
{
    internal readonly struct CacheEntry(
        Func<T, FastCloneState, T>? cloner,
        bool isSafe,
        bool canUseNoTrackingState,
        FastClonerCache.TypeCloneMetadata? metadata,
        long version)
    {
        public Func<T, FastCloneState, T>? Cloner { get; } = cloner;
        public bool IsSafe { get; } = isSafe;
        public bool CanUseNoTrackingState { get; } = canUseNoTrackingState;
        public FastClonerCache.TypeCloneMetadata? Metadata { get; } = metadata;
        public long Version { get; } = version;
    }

    private static readonly object sync = new object();
    private static Func<T, FastCloneState, T>? cloner;
    private static bool isSafe;
    private static bool canUseNoTrackingState;
    private static FastClonerCache.TypeCloneMetadata? metadata;
    private static long version = -1;

    public static CacheEntry GetCurrent()
    {
        long currentVersion = FastClonerCache.GetCacheVersion();
        if (Volatile.Read(ref version) != currentVersion)
        {
            lock (sync)
            {
                if (version != currentVersion)
                {
                    Refresh(currentVersion);
                }
            }
        }

        return new CacheEntry(cloner, isSafe, canUseNoTrackingState, metadata, currentVersion);
    }

    private static void Refresh(long currentVersion)
    {
        Type type = typeof(T);
        FastClonerCache.TypeCloneMetadata typeMetadata = FastClonerGenerator.GetTypeMetadata(type);
        bool computedIsSafe = FastClonerSafeTypes.CanReturnSameObject(type);
        bool computedCanUseNoTrackingState =
            !type.IsValueType &&
            typeMetadata.CyclePolicy == FastClonerCache.CyclePolicy.None &&
            !typeMetadata.HasBehaviorSensitiveMembers &&
            !FastClonerCache.HasActiveTypeBehaviorOverrides;
        Func<T, FastCloneState, T>? computedCloner = null;

        if (!computedIsSafe)
        {
            object? clonerObj = FastClonerExprGenerator.GenerateClonerInternal(type, type.IsValueType);
            if (type.IsValueType)
            {
                computedCloner = clonerObj as Func<T, FastCloneState, T>;
            }
            else
            {
                Func<object, FastCloneState, object>? objectCloner = clonerObj as Func<object, FastCloneState, object>;
                if (objectCloner is not null)
                {
                    computedCloner = (obj, state) => (T)objectCloner(obj!, state);
                }
            }
        }

        cloner = computedCloner;
        isSafe = computedIsSafe;
        canUseNoTrackingState = computedCanUseNoTrackingState;
        metadata = typeMetadata;
        Volatile.Write(ref version, currentVersion);
    }
}

internal static class FastClonerCache
{
    private static long cacheVersion = 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long GetCacheVersion() => Interlocked.Read(ref cacheVersion);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long BumpCacheVersion() => Interlocked.Increment(ref cacheVersion);

    internal enum CollectionCloneStrategy
    {
        None = 0,
        MemberwiseFast = 1,
        SpecializedRebuild = 2,
        Hybrid = 3
    }

    internal enum CloneExecutionMode
    {
        SafeReturn = 0,
        MemberwiseThenPatch = 1,
        RebuildCollection = 2
    }

    internal enum CyclePolicy
    {
        None = 0,
        TrackReferences = 1,
        Worklist = 2
    }

    internal sealed class TypeCloneMetadata
    {
        public Type Type { get; set; } = typeof(object);
        public bool IsSafe { get; set; }
        public bool CanHaveCycles { get; set; }
        public bool CanSkipReferenceTracking { get; set; }
        public bool HasDirectSelfReference { get; set; }
        public bool HasBehaviorSensitiveMembers { get; set; }
        public bool RequiresSpecializedCloner { get; set; }
        public CollectionCloneStrategy CollectionStrategy { get; set; }
        public CloneExecutionMode ExecutionMode { get; set; }
        public CyclePolicy CyclePolicy { get; set; }
        public Func<object, FastCloneState, object>? RecursiveCloner { get; set; }
        public Func<object, FastCloneState, object>? WorklistCloner { get; set; }
    }

    internal static readonly ConcurrentDictionary<Type, CloneBehavior> TypeBehaviors = [];
    internal static volatile bool HasTypeBehaviorOverrides;
    internal static volatile bool HasActiveTypeBehaviorOverrides;

    internal static bool IsTypeIgnored(Type type)
    {
        return HasActiveTypeBehaviorOverrides &&
               TypeBehaviors.TryGetValue(type, out CloneBehavior behavior) &&
               behavior == CloneBehavior.Ignore;
    }

    internal static volatile bool HasSafeTypeOverrides;
    
    internal static bool IsTypeReference(Type type)
    {
        return HasActiveTypeBehaviorOverrides &&
               TypeBehaviors.TryGetValue(type, out CloneBehavior behavior) &&
               behavior == CloneBehavior.Reference;
    }
    
    internal static CloneBehavior? GetTypeBehavior(Type type)
    {
        return TypeBehaviors.TryGetValue(type, out CloneBehavior behavior) ? behavior : null;
    }

    internal static void RecalculateTypeBehaviorState()
    {
        HasTypeBehaviorOverrides = !TypeBehaviors.IsEmpty;
        HasActiveTypeBehaviorOverrides = HasTypeBehaviorOverrides && !global::FastCloner.FastCloner.DisableOptionalFeatures;
        HasSafeTypeOverrides = CalculateHasSafeTypeOverrides();
    }

    private static bool CalculateHasSafeTypeOverrides()
    {
        if (!HasTypeBehaviorOverrides)
            return false;

        foreach (KeyValuePair<Type, CloneBehavior> kvp in TypeBehaviors)
        {
            if (kvp.Value == CloneBehavior.Ignore && FastClonerSafeTypes.DefaultKnownTypes.ContainsKey(kvp.Key))
                return true;
        }

        return false;
    }
    
    private static readonly ClrCache<object?> classCache = new ClrCache<object?>();
    private static readonly ClrCache<TypeCloneMetadata> typeMetadataCache = new ClrCache<TypeCloneMetadata>();
    private static readonly ClrCache<object?> structCache = new ClrCache<object?>();
    private static readonly ClrCache<object> deepClassToCache = new ClrCache<object>();
    private static readonly ClrCache<object> shallowClassToCache = new ClrCache<object>();
    private static readonly ConcurrentLazyCache<object> typeConvertCache = new ConcurrentLazyCache<object>();
    private static readonly GenericClrCache<Tuple<Type, string>, object?> fieldCache = new GenericClrCache<Tuple<Type, string>, object?>();
    private static readonly ClrCache<Dictionary<string, Type>> ignoredEventInfoCache = new ClrCache<Dictionary<string, Type>>();
    private static readonly ClrCache<List<MemberInfo>> allMembersCache = new ClrCache<List<MemberInfo>>();
    private static readonly GenericClrCache<MemberInfo, CloneBehavior?> memberBehaviorCache = new GenericClrCache<MemberInfo, CloneBehavior?>();
    private static readonly ClrCache<bool> typeContainsIgnoredMembersCache = new ClrCache<bool>();
    private static readonly ClrCache<object> specialTypesCache = new ClrCache<object>();
    private static readonly ClrCache<bool> isTypeSafeHandleCache = new ClrCache<bool>();
    private static readonly ClrCache<bool> anonymousTypeStatusCache = new ClrCache<bool>();
    private static readonly ClrCache<bool> stableHashSemanticsCache = new ClrCache<bool>();
    private static readonly ClrCache<bool> canHaveCyclesCache = new ClrCache<bool>();
    private static readonly ClrCache<bool> valueTypeContainsReferencesCache = new ClrCache<bool>();
    private static readonly ClrCache<Type[]> cycleFieldTypesCache = new ClrCache<Type[]>();
    private static readonly ClrCache<Type?> collectionPayloadTypeCache = new ClrCache<Type?>();
    private static readonly ClrCache<bool> compilerGeneratedTypeCache = new ClrCache<bool>();

    public static object? GetOrAddField(Type type, string name, Func<Type, object?> valueFactory) => fieldCache.GetOrAdd(new Tuple<Type, string>(type, name), k => valueFactory(k.Item1));
    public static object? GetOrAddClass(Type type, Func<Type, object?> valueFactory) => classCache.GetOrAdd(type, valueFactory);
    public static TypeCloneMetadata GetOrAddTypeMetadata(Type type, Func<Type, TypeCloneMetadata> valueFactory) => typeMetadataCache.GetOrAdd(type, valueFactory);
    public static object? GetOrAddStructAsObject(Type type, Func<Type, object?> valueFactory) => structCache.GetOrAdd(type, valueFactory);
    public static object GetOrAddDeepClassTo(Type type, Func<Type, object> valueFactory) => deepClassToCache.GetOrAdd(type, valueFactory);
    public static object GetOrAddShallowClassTo(Type type, Func<Type, object> valueFactory) => shallowClassToCache.GetOrAdd(type, valueFactory);
    public static T GetOrAddConvertor<T>(Type from, Type to, Func<Type, Type, T> valueFactory) => (T)typeConvertCache.GetOrAdd(from, to, (f, t) => valueFactory(f, t));
    public static Dictionary<string, Type> GetOrAddIgnoredEventInfo(Type type, Func<Type, Dictionary<string, Type>> valueFactory) => ignoredEventInfoCache.GetOrAdd(type, valueFactory);
    public static List<MemberInfo> GetOrAddAllMembers(Type type, Func<Type, List<MemberInfo>> valueFactory) => allMembersCache.GetOrAdd(type, valueFactory);
    public static CloneBehavior? GetOrAddMemberBehavior(MemberInfo memberInfo, Func<MemberInfo, CloneBehavior?> valueFactory) => memberBehaviorCache.GetOrAdd(memberInfo, valueFactory);
    public static bool GetOrAddTypeContainsIgnoredMembers(Type type, Func<Type, bool> valueFactory)
    {
        return typeContainsIgnoredMembersCache.GetOrAdd(type, valueFactory);
    }
    public static object GetOrAddSpecialType(Type type, Func<Type, object> valueFactory) => specialTypesCache.GetOrAdd(type, valueFactory);
    public static bool GetOrAddIsTypeSafeHandle(Type type, Func<Type, bool> valueFactory) => isTypeSafeHandleCache.GetOrAdd(type, valueFactory);
    public static bool GetOrAddAnonymousTypeStatus(Type type, Func<Type, bool> valueFactory) => anonymousTypeStatusCache.GetOrAdd(type, valueFactory);
    public static bool GetOrAddStableHashSemantics(Type type, Func<Type, bool> valueFactory) => stableHashSemanticsCache.GetOrAdd(type, valueFactory);
    public static bool GetOrAddCanHaveCycles(Type type, Func<Type, bool> valueFactory) => canHaveCyclesCache.GetOrAdd(type, valueFactory);
    public static bool GetOrAddValueTypeContainsReferences(Type type, Func<Type, bool> valueFactory) => valueTypeContainsReferencesCache.GetOrAdd(type, valueFactory);
    public static Type[] GetOrAddCycleFieldTypes(Type type, Func<Type, Type[]> valueFactory) => cycleFieldTypesCache.GetOrAdd(type, valueFactory);
    public static Type? GetOrAddCollectionPayloadType(Type type, Func<Type, Type?> valueFactory) => collectionPayloadTypeCache.GetOrAdd(type, valueFactory);
    public static bool GetOrAddCompilerGeneratedType(Type type, Func<Type, bool> valueFactory) => compilerGeneratedTypeCache.GetOrAdd(type, valueFactory);
    
    /// <summary>
    /// Clears the FastCloner cached reflection metadata.
    /// </summary>
    public static void ClearCache()
    {
        classCache.Clear();
        typeMetadataCache.Clear();
        structCache.Clear();
        deepClassToCache.Clear();
        shallowClassToCache.Clear();
        typeConvertCache.Clear();
        fieldCache.Clear();
        ignoredEventInfoCache.Clear();
        allMembersCache.Clear();
        memberBehaviorCache.Clear();
        typeContainsIgnoredMembersCache.Clear();
        specialTypesCache.Clear();
        isTypeSafeHandleCache.Clear();
        anonymousTypeStatusCache.Clear();
        stableHashSemanticsCache.Clear();
        canHaveCyclesCache.Clear();
        valueTypeContainsReferencesCache.Clear();
        cycleFieldTypesCache.Clear();
        collectionPayloadTypeCache.Clear();
        compilerGeneratedTypeCache.Clear();
        BumpCacheVersion();
    }
    
    internal sealed class ClrCache<TValue>
    {
        private readonly ConcurrentDictionary<IntPtr, TValue> cache = new ConcurrentDictionary<IntPtr, TValue>();
        
        public TValue GetOrAdd(Type type, Func<Type, TValue> valueFactory)
        {
            IntPtr handle = type.TypeHandle.Value;
            if (cache.TryGetValue(handle, out TValue? existing))
                return existing;

            return cache.GetOrAdd(handle, _ => valueFactory(type));
        }

        public void Clear() => cache.Clear();
    }
    
    private sealed class GenericClrCache<TKey, TValue> where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, TValue> cache = new ConcurrentDictionary<TKey, TValue>();
        
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            return cache.GetOrAdd(key, valueFactory);
        }

        public void Clear() => cache.Clear();
    }
    
    private sealed class ConcurrentLazyCache<TValue>
    {
#if MODERN
        private readonly ConcurrentDictionary<(IntPtr, IntPtr), TValue> cache = new ConcurrentDictionary<(IntPtr, IntPtr), TValue>();

        public TValue GetOrAdd(Type from, Type to, Func<Type, Type, TValue> valueFactory)
        {
            (IntPtr, IntPtr) key = (from.TypeHandle.Value, to.TypeHandle.Value);
            return cache.GetOrAdd(key, _ => valueFactory(from, to));
        }

        public void Clear() => cache.Clear();
#else
        private readonly ConcurrentDictionary<Tuple<IntPtr, IntPtr>, TValue> cache = new ConcurrentDictionary<Tuple<IntPtr, IntPtr>, TValue>();
        
        public TValue GetOrAdd(Type from, Type to, Func<Type, Type, TValue> valueFactory)
        {
            Tuple<IntPtr, IntPtr> key = Tuple.Create(from.TypeHandle.Value, to.TypeHandle.Value);
            return cache.TryGetValue(key, out TValue? cached) ? cached : cache.GetOrAdd(key, _ => valueFactory(from, to));
        }

        public void Clear() => cache.Clear();
#endif
    }
}
