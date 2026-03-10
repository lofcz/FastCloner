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
    private readonly record struct CacheIdentity(long ClearVersion, long ConfigCacheKey);

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

    #if MODERN_10
    private static readonly Lock sync = new Lock();
    #else
    private static readonly object sync = new object();
    #endif
    private static Func<T, FastCloneState, T>? cloner;
    private static bool isSafe;
    private static bool canUseNoTrackingState;
    private static FastClonerCache.TypeCloneMetadata? metadata;
    private static long version = -1;
    private static readonly ConcurrentDictionary<CacheIdentity, CacheEntry> versionedEntries = new();

    static ClonerCache()
    {
        FastClonerCache.RegisterVersionedCacheClearer(() => versionedEntries.Clear());
    }

    public static CacheEntry GetCurrent(FastClonerRuntimeConfig config)
    {
        long currentVersion = FastClonerCache.GetCacheVersion();

        if (config.CacheKey != 0)
        {
            CacheIdentity identity = new(currentVersion, config.CacheKey);
            return versionedEntries.GetOrAdd(identity, _ => Refresh(config, currentVersion));
        }

        if (Volatile.Read(ref version) != currentVersion)
        {
            lock (sync)
            {
                if (version != currentVersion)
                {
                    CacheEntry cacheEntry = Refresh(config, currentVersion);
                    cloner = cacheEntry.Cloner;
                    isSafe = cacheEntry.IsSafe;
                    canUseNoTrackingState = cacheEntry.CanUseNoTrackingState;
                    metadata = cacheEntry.Metadata;
                    Volatile.Write(ref version, currentVersion);
                }
            }
        }

        return new CacheEntry(cloner, isSafe, canUseNoTrackingState, metadata, currentVersion);
    }

    private static CacheEntry Refresh(FastClonerRuntimeConfig config, long currentVersion)
    {
        Type type = typeof(T);
        using FastClonerRuntimeConfigScope.Scope _ = FastClonerRuntimeConfigScope.Use(config);

        FastClonerCache.TypeCloneMetadata typeMetadata = FastClonerGenerator.GetTypeMetadata(type);
        bool computedIsSafe = FastClonerSafeTypes.CanReturnSameObject(type);
        bool computedCanUseNoTrackingState =
            !type.IsValueType &&
            typeMetadata is { CyclePolicy: FastClonerCache.CyclePolicy.None, HasBehaviorSensitiveMembers: false } &&
            !config.HasActiveTypeBehaviorOverrides;
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
                if (clonerObj is Func<object, FastCloneState, object> objectCloner)
                {
                    computedCloner = (obj, state) => (T)objectCloner(obj!, state);
                }
            }
        }

        return new CacheEntry(computedCloner, computedIsSafe, computedCanUseNoTrackingState, typeMetadata, currentVersion);
    }

    internal static int GetVersionedEntryCountForTesting() => versionedEntries.Count;
}

internal static class FastClonerCache
{
    private static long cacheVersion = 1;
    private static readonly ConcurrentQueue<Action> versionedCacheClearers = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long GetCacheVersion() => Interlocked.Read(ref cacheVersion);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long BumpCacheVersion() => Interlocked.Increment(ref cacheVersion);

    internal static void RegisterVersionedCacheClearer(Action clearAction)
    {
        versionedCacheClearers.Enqueue(clearAction);
    }

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
    }

    internal sealed class TypeShape
    {
        public MemberInfo[] Members { get; init; } = [];
        public Dictionary<string, Type>? IgnoredEventDetails { get; init; }
        public Type[] CycleFieldTypes { get; init; } = [];
        public bool HasReadonlyFields { get; init; }
        public bool ContainsIgnoredMembers { get; init; }
        public bool HasDirectSelfReference { get; init; }
    }

    private static FastClonerRuntimeConfig? TryGetCurrentConfig() => FastClonerRuntimeConfigScope.TryGetCurrent();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetAmbientCacheKey()
    {
        FastClonerRuntimeConfig? current = TryGetCurrentConfig();
        return current?.CacheKey ?? FastCloner.GetPublishedCacheKey();
    }

    internal static bool HasTypeBehaviorOverrides
    {
        get
        {
            FastClonerRuntimeConfig? current = TryGetCurrentConfig();
            if (current is not null)
                return current.HasTypeBehaviorOverrides;

            return FastCloner.GetRuntimeConfigSnapshot().HasTypeBehaviorOverrides;
        }
    }

    internal static bool HasActiveTypeBehaviorOverrides
    {
        get
        {
            FastClonerRuntimeConfig? current = TryGetCurrentConfig();
            if (current is not null)
                return current.HasActiveTypeBehaviorOverrides;

            return FastCloner.GetRuntimeConfigSnapshot().HasActiveTypeBehaviorOverrides;
        }
    }

    internal static bool IsTypeIgnored(Type type)
    {
        FastClonerRuntimeConfig? current = TryGetCurrentConfig();
        if (current is not null)
            return current.IsTypeIgnored(type);

        return FastCloner.GetRuntimeConfigSnapshot().IsTypeIgnored(type);
    }

    internal static bool HasSafeTypeOverrides
    {
        get
        {
            FastClonerRuntimeConfig? current = TryGetCurrentConfig();
            if (current is not null)
                return current.HasSafeTypeOverrides;

            return FastCloner.GetRuntimeConfigSnapshot().HasSafeTypeOverrides;
        }
    }
    
    internal static bool IsTypeReference(Type type)
    {
        FastClonerRuntimeConfig? current = TryGetCurrentConfig();
        if (current is not null)
            return current.IsTypeReference(type);

        return FastCloner.GetRuntimeConfigSnapshot().IsTypeReference(type);
    }
    
    internal static CloneBehavior? GetTypeBehavior(Type type)
    {
        FastClonerRuntimeConfig? current = TryGetCurrentConfig();
        if (current is not null)
            return current.GetTypeBehavior(type);

        return FastCloner.GetRuntimeConfigSnapshot().GetTypeBehavior(type);
    }

    internal static void RecalculateTypeBehaviorState()
    {
        // Runtime config snapshots precompute this state.
    }
    
    private static readonly ClrCache<object?> classCache = new ClrCache<object?>();
    private static readonly ClrCache<TypeCloneMetadata> typeMetadataCache = new ClrCache<TypeCloneMetadata>();
    private static readonly ClrCache<TypeShape> typeShapeCache = new ClrCache<TypeShape>();
    private static readonly ClrCache<object?> structCache = new ClrCache<object?>();
    private static readonly ClrCache<object> deepClassToCache = new ClrCache<object>();
    private static readonly ClrCache<object> shallowClassToCache = new ClrCache<object>();
    private static readonly ConcurrentLazyCache<object> typeConvertCache = new ConcurrentLazyCache<object>();
    private static readonly GenericClrCache<TypeNameKey, object?> fieldCache = new GenericClrCache<TypeNameKey, object?>();
    private static readonly GenericClrCache<MemberInfo, CloneBehavior?> memberBehaviorCache = new GenericClrCache<MemberInfo, CloneBehavior?>();
    private static readonly ClrCache<CloneBehavior?> attributedTypeBehaviorCache = new ClrCache<CloneBehavior?>();
    private static readonly ClrCache<bool> immutableCollectionStatusCache = new ClrCache<bool>();
    private static readonly ClrCache<object> specialTypesCache = new ClrCache<object>();
    private static readonly ClrCache<bool> isTypeSafeHandleCache = new ClrCache<bool>();
    private static readonly ClrCache<bool> anonymousTypeStatusCache = new ClrCache<bool>();
    private static readonly ClrCache<bool> stableHashSemanticsCache = new ClrCache<bool>();
    private static readonly ClrCache<bool> canHaveCyclesCache = new ClrCache<bool>();
    private static readonly ClrCache<bool> valueTypeContainsReferencesCache = new ClrCache<bool>();
    private static readonly ClrCache<Type?> collectionPayloadTypeCache = new ClrCache<Type?>();
    private static readonly ClrCache<bool> compilerGeneratedTypeCache = new ClrCache<bool>();

    public static object? GetOrAddField(Type type, string name, Func<Type, object?> valueFactory)
        => fieldCache.GetOrAdd(new TypeNameKey(type, name), k => valueFactory(k.Type));
    public static object? GetOrAddClass(Type type, Func<Type, object?> valueFactory) => classCache.GetOrAdd(type, valueFactory);
    public static TypeCloneMetadata GetOrAddTypeMetadata(Type type, Func<Type, TypeCloneMetadata> valueFactory) => typeMetadataCache.GetOrAdd(type, valueFactory);
    public static TypeShape GetOrAddTypeShape(Type type, Func<Type, TypeShape> valueFactory) => typeShapeCache.GetOrAdd(type, valueFactory);
    public static object? GetOrAddStructAsObject(Type type, Func<Type, object?> valueFactory) => structCache.GetOrAdd(type, valueFactory);
    public static object GetOrAddDeepClassTo(Type type, Func<Type, object> valueFactory) => deepClassToCache.GetOrAdd(type, valueFactory);
    public static object GetOrAddShallowClassTo(Type type, Func<Type, object> valueFactory) => shallowClassToCache.GetOrAdd(type, valueFactory);
    public static T GetOrAddConvertor<T>(Type from, Type to, Func<Type, Type, T> valueFactory) => (T)typeConvertCache.GetOrAdd(from, to, (f, t) => valueFactory(f, t));
    public static CloneBehavior? GetOrAddMemberBehavior(MemberInfo memberInfo, Func<MemberInfo, CloneBehavior?> valueFactory) => memberBehaviorCache.GetOrAdd(memberInfo, valueFactory);
    public static CloneBehavior? GetOrAddAttributedTypeBehavior(Type type, Func<Type, CloneBehavior?> valueFactory)
        => attributedTypeBehaviorCache.GetOrAdd(type, valueFactory);
    public static bool GetOrAddImmutableCollectionStatus(Type type, Func<Type, bool> valueFactory)
        => immutableCollectionStatusCache.GetOrAdd(type, valueFactory);
    public static object GetOrAddSpecialType(Type type, Func<Type, object> valueFactory) => specialTypesCache.GetOrAdd(type, valueFactory);
    public static bool GetOrAddIsTypeSafeHandle(Type type, Func<Type, bool> valueFactory) => isTypeSafeHandleCache.GetOrAdd(type, valueFactory);
    public static bool GetOrAddAnonymousTypeStatus(Type type, Func<Type, bool> valueFactory) => anonymousTypeStatusCache.GetOrAdd(type, valueFactory);
    public static bool GetOrAddStableHashSemantics(Type type, Func<Type, bool> valueFactory) => stableHashSemanticsCache.GetOrAdd(type, valueFactory);
    public static bool GetOrAddCanHaveCycles(Type type, Func<Type, bool> valueFactory) => canHaveCyclesCache.GetOrAdd(type, valueFactory);
    public static bool GetOrAddValueTypeContainsReferences(Type type, Func<Type, bool> valueFactory) => valueTypeContainsReferencesCache.GetOrAdd(type, valueFactory);
    public static Type? GetOrAddCollectionPayloadType(Type type, Func<Type, Type?> valueFactory) => collectionPayloadTypeCache.GetOrAdd(type, valueFactory);
    public static bool GetOrAddCompilerGeneratedType(Type type, Func<Type, bool> valueFactory) => compilerGeneratedTypeCache.GetOrAdd(type, valueFactory);
    
    /// <summary>
    /// Clears the FastCloner cached reflection metadata.
    /// </summary>
    public static void ClearCache()
    {
        classCache.Clear();
        typeMetadataCache.Clear();
        typeShapeCache.Clear();
        structCache.Clear();
        deepClassToCache.Clear();
        shallowClassToCache.Clear();
        typeConvertCache.Clear();
        fieldCache.Clear();
        memberBehaviorCache.Clear();
        attributedTypeBehaviorCache.Clear();
        immutableCollectionStatusCache.Clear();
        specialTypesCache.Clear();
        isTypeSafeHandleCache.Clear();
        anonymousTypeStatusCache.Clear();
        stableHashSemanticsCache.Clear();
        canHaveCyclesCache.Clear();
        valueTypeContainsReferencesCache.Clear();
        collectionPayloadTypeCache.Clear();
        compilerGeneratedTypeCache.Clear();
        FastClonerSafeTypes.ClearKnownTypesCache();
        FastClonerExprGenerator.ClearAdaptiveDictionaryFactoryCache();

        foreach (Action clearAction in versionedCacheClearers)
        {
            clearAction();
        }

        BumpCacheVersion();
    }

    internal static void ClearVersionedConfigCaches()
    {
        classCache.ClearVersioned();
        typeMetadataCache.ClearVersioned();
        typeShapeCache.ClearVersioned();
        structCache.ClearVersioned();
        deepClassToCache.ClearVersioned();
        shallowClassToCache.ClearVersioned();
        typeConvertCache.ClearVersioned();
        fieldCache.ClearVersioned();
        memberBehaviorCache.ClearVersioned();
        attributedTypeBehaviorCache.ClearVersioned();
        immutableCollectionStatusCache.ClearVersioned();
        specialTypesCache.ClearVersioned();
        isTypeSafeHandleCache.ClearVersioned();
        anonymousTypeStatusCache.ClearVersioned();
        stableHashSemanticsCache.ClearVersioned();
        canHaveCyclesCache.ClearVersioned();
        valueTypeContainsReferencesCache.ClearVersioned();
        collectionPayloadTypeCache.ClearVersioned();
        compilerGeneratedTypeCache.ClearVersioned();
        FastClonerSafeTypes.ClearVersionedKnownTypesCache();
        FastClonerExprGenerator.ClearAdaptiveDictionaryFactoryCache();

        foreach (Action clearAction in versionedCacheClearers)
        {
            clearAction();
        }
    }

    internal static int GetVersionedCacheEntryCountForTesting()
    {
        return classCache.VersionedCount +
               typeMetadataCache.VersionedCount +
               typeShapeCache.VersionedCount +
               structCache.VersionedCount +
               deepClassToCache.VersionedCount +
               shallowClassToCache.VersionedCount +
               typeConvertCache.VersionedCount +
               fieldCache.VersionedCount +
               memberBehaviorCache.VersionedCount +
               attributedTypeBehaviorCache.VersionedCount +
               immutableCollectionStatusCache.VersionedCount +
               specialTypesCache.VersionedCount +
               isTypeSafeHandleCache.VersionedCount +
               anonymousTypeStatusCache.VersionedCount +
               stableHashSemanticsCache.VersionedCount +
               canHaveCyclesCache.VersionedCount +
               valueTypeContainsReferencesCache.VersionedCount +
               collectionPayloadTypeCache.VersionedCount +
               compilerGeneratedTypeCache.VersionedCount;
    }

    internal static int GetClonerCacheVersionedEntryCountForTesting<T>() => ClonerCache<T>.GetVersionedEntryCountForTesting();

    private readonly struct ConfigTypeKey(long cacheKey, IntPtr typeHandle) : IEquatable<ConfigTypeKey>
    {
        public long CacheKey { get; } = cacheKey;
        public IntPtr TypeHandle { get; } = typeHandle;

        public bool Equals(ConfigTypeKey other)
        {
            return CacheKey == other.CacheKey && TypeHandle == other.TypeHandle;
        }

        public override bool Equals(object? obj)
        {
            return obj is ConfigTypeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (CacheKey.GetHashCode() * 397) ^ TypeHandle.GetHashCode();
            }
        }
    }

    private sealed class ClrCache<TValue>
    {
        private readonly ConcurrentDictionary<IntPtr, TValue> defaultCache = new ConcurrentDictionary<IntPtr, TValue>();
        private readonly ConcurrentDictionary<ConfigTypeKey, TValue> versionedCache = new ConcurrentDictionary<ConfigTypeKey, TValue>();
        public int VersionedCount => versionedCache.Count;

        public TValue GetOrAdd(Type type, Func<Type, TValue> valueFactory)
        {
            IntPtr handle = type.TypeHandle.Value;
            long cacheKey = GetAmbientCacheKey();
            if (cacheKey == 0)
            {
                return defaultCache.TryGetValue(handle, out TValue? existing)
                    ? existing
                    : defaultCache.GetOrAdd(handle, _ => valueFactory(type));
            }

            return versionedCache.GetOrAdd(new ConfigTypeKey(cacheKey, handle), _ => valueFactory(type));
        }

        public void Clear()
        {
            defaultCache.Clear();
            versionedCache.Clear();
        }

        public void ClearVersioned() => versionedCache.Clear();
    }

    private readonly struct ConfigGenericKey<TKey>(long cacheKey, TKey key) : IEquatable<ConfigGenericKey<TKey>> where TKey : notnull
    {
        public long CacheKey { get; } = cacheKey;
        public TKey Key { get; } = key;

        public bool Equals(ConfigGenericKey<TKey> other)
        {
            return CacheKey == other.CacheKey && EqualityComparer<TKey>.Default.Equals(Key, other.Key);
        }

        public override bool Equals(object? obj)
        {
            return obj is ConfigGenericKey<TKey> other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (CacheKey.GetHashCode() * 397) ^ EqualityComparer<TKey>.Default.GetHashCode(Key);
            }
        }
    }

    private sealed class GenericClrCache<TKey, TValue> where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, TValue> defaultCache = new ConcurrentDictionary<TKey, TValue>();
        private readonly ConcurrentDictionary<ConfigGenericKey<TKey>, TValue> versionedCache = new ConcurrentDictionary<ConfigGenericKey<TKey>, TValue>();
        public int VersionedCount => versionedCache.Count;
        
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            long cacheKey = GetAmbientCacheKey();
            if (cacheKey == 0)
                return defaultCache.GetOrAdd(key, valueFactory);

            return versionedCache.GetOrAdd(new ConfigGenericKey<TKey>(cacheKey, key), _ => valueFactory(key));
        }

        public void Clear()
        {
            defaultCache.Clear();
            versionedCache.Clear();
        }

        public void ClearVersioned() => versionedCache.Clear();
    }

    private readonly struct TypeNameKey(Type type, string name) : IEquatable<TypeNameKey>
    {
        public Type Type { get; } = type;
        public string Name { get; } = name;

        public bool Equals(TypeNameKey other)
        {
            return ReferenceEquals(Type, other.Type) && Name == other.Name;
        }

        public override bool Equals(object? obj)
        {
            return obj is TypeNameKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (RuntimeHelpers.GetHashCode(Type) * 397) ^ Name.GetHashCode();
            }
        }
    }
    
    private sealed class ConcurrentLazyCache<TValue>
    {
#if MODERN
        private readonly ConcurrentDictionary<(IntPtr, IntPtr), TValue> defaultCache = new ConcurrentDictionary<(IntPtr, IntPtr), TValue>();
        private readonly ConcurrentDictionary<(long CacheKey, IntPtr From, IntPtr To), TValue> versionedCache = new ConcurrentDictionary<(long CacheKey, IntPtr From, IntPtr To), TValue>();
        public int VersionedCount => versionedCache.Count;

        public TValue GetOrAdd(Type from, Type to, Func<Type, Type, TValue> valueFactory)
        {
            long cacheKey = GetAmbientCacheKey();
            if (cacheKey == 0)
            {
                (IntPtr, IntPtr) key = (from.TypeHandle.Value, to.TypeHandle.Value);
                return defaultCache.GetOrAdd(key, _ => valueFactory(from, to));
            }

            return versionedCache.GetOrAdd((cacheKey, from.TypeHandle.Value, to.TypeHandle.Value), _ => valueFactory(from, to));
        }

        public void Clear()
        {
            defaultCache.Clear();
            versionedCache.Clear();
        }

        public void ClearVersioned() => versionedCache.Clear();
#else
        private readonly ConcurrentDictionary<Tuple<IntPtr, IntPtr>, TValue> defaultCache = new ConcurrentDictionary<Tuple<IntPtr, IntPtr>, TValue>();
        private readonly ConcurrentDictionary<Tuple<long, IntPtr, IntPtr>, TValue> versionedCache = new ConcurrentDictionary<Tuple<long, IntPtr, IntPtr>, TValue>();
        public int VersionedCount => versionedCache.Count;
        
        public TValue GetOrAdd(Type from, Type to, Func<Type, Type, TValue> valueFactory)
        {
            long cacheKey = GetAmbientCacheKey();
            if (cacheKey == 0)
            {
                Tuple<IntPtr, IntPtr> key = Tuple.Create(from.TypeHandle.Value, to.TypeHandle.Value);
                return defaultCache.TryGetValue(key, out TValue? cached) ? cached : defaultCache.GetOrAdd(key, _ => valueFactory(from, to));
            }

            Tuple<long, IntPtr, IntPtr> versionedKey = Tuple.Create(cacheKey, from.TypeHandle.Value, to.TypeHandle.Value);
            return versionedCache.TryGetValue(versionedKey, out TValue? versionedCached)
                ? versionedCached
                : versionedCache.GetOrAdd(versionedKey, _ => valueFactory(from, to));
        }

        public void Clear()
        {
            defaultCache.Clear();
            versionedCache.Clear();
        }

        public void ClearVersioned() => versionedCache.Clear();
#endif
    }
}
