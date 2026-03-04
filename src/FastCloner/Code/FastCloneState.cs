using System.Reflection;
using System.Runtime.CompilerServices;

namespace FastCloner.Code;

internal sealed class FastCloneState
{
    internal static readonly PropertyInfo UseWorkListProp = typeof(FastCloneState).GetProperty("UseWorkList")!;

    private const int MaxPoolSize = 16;
    [ThreadStatic]
    private static FastCloneState?[]? _pool;
    [ThreadStatic]
    private static int _poolCount;

    [ThreadStatic]
    private static FastCloneState? _simpleState;

    public bool TrackReferences { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FastCloneState GetSimpleState()
    {
        return _simpleState ?? CreateSimpleState();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static FastCloneState CreateSimpleState()
    {
        FastCloneState state = new FastCloneState(trackReferences: false);
        _simpleState = state;
        return state;
    }

    public static FastCloneState Rent()
    {
        FastCloneState?[]? pool = _pool;
        if (pool != null && _poolCount > 0)
        {
            int index = --_poolCount;
            FastCloneState? state = pool[index];
            pool[index] = null;
            if (state != null)
                return state;
        }
        return new FastCloneState();
    }

    public static void Return(FastCloneState state)
    {
        state.Reset();

        FastCloneState?[] pool = _pool ??= new FastCloneState[MaxPoolSize];
        if (_poolCount < MaxPoolSize)
        {
            pool[_poolCount++] = state;
        }
    }

    private void Reset()
    {
        for (int i = 0; i < idx; i++)
        {
            baseFromTo[i] = null!;
            baseFromTo[i + 3] = null!;
        }
        idx = 0;

        if (loops is { } localLoops)
        {
            if (localLoops.Capacity > 4096)
                loops = null;
            else
                localLoops.Clear();
        }

        workCount = 0;
        UseWorkList = false;
        callDepth = 0;
        metadata1 = null;
        metadata2 = null;
        metadataTypeHandle1 = default;
        metadataTypeHandle2 = default;
    }

    private FastCloneState(bool trackReferences = true)
    {
        TrackReferences = trackReferences;
    }

    private MiniDictionary? loops;
    private readonly object[] baseFromTo = new object[6];
    private int idx;
    private WorkItem[]? workItems;
    private int workCount;
    public bool UseWorkList { get; set; }
    private int callDepth;
    private RuntimeTypeHandle metadataTypeHandle1;
    private FastClonerCache.TypeCloneMetadata? metadata1;
    private RuntimeTypeHandle metadataTypeHandle2;
    private FastClonerCache.TypeCloneMetadata? metadata2;

    private readonly struct WorkItem(object from, object to, Type type)
    {
        public readonly object From = from;
        public readonly object To = to;
        public readonly Type Type = type;
    }

    public object? GetKnownRef(object from)
    {
        if (!TrackReferences)
            return null;

        return idx switch
        {
            1 when ReferenceEquals(from, baseFromTo[0]) => baseFromTo[3],
            2 when ReferenceEquals(from, baseFromTo[0]) => baseFromTo[3],
            2 when ReferenceEquals(from, baseFromTo[1]) => baseFromTo[4],
            3 when ReferenceEquals(from, baseFromTo[0]) => baseFromTo[3],
            3 when ReferenceEquals(from, baseFromTo[1]) => baseFromTo[4],
            3 when ReferenceEquals(from, baseFromTo[2]) => baseFromTo[5],
            _ => loops?.FindEntry(from)
        };
    }

    public void AddKnownRef(object from, object to)
    {
        if (!TrackReferences)
            return;

        if (idx < 3)
        {
            baseFromTo[idx] = from;
            baseFromTo[idx + 3] = to;
            idx++;
            return;
        }

        loops ??= new MiniDictionary();
        loops.Insert(from, to);
    }

    public void EnsureKnownRefCapacity(int totalExpectedReferences)
    {
        if (!TrackReferences || totalExpectedReferences <= 3)
            return;

        int expectedInDictionary = totalExpectedReferences - 3;
        if (expectedInDictionary <= 0)
            return;

        if (loops is null)
        {
            loops = new MiniDictionary(expectedInDictionary);
            return;
        }

        loops.EnsureCapacity(expectedInDictionary);
    }

    public void EnqueueProcess(object from, object to, Type type)
    {
        if (!TrackReferences)
            return;

        WorkItem[] local = workItems ??= new WorkItem[16];
        if (workCount == local.Length)
        {
            int newSize = local.Length * 2;
            WorkItem[] resized = new WorkItem[newSize];
            Array.Copy(local, resized, workCount);
            workItems = local = resized;
        }

        local[workCount++] = new WorkItem(from, to, type);
    }

    public void EnsureWorkQueueCapacity(int expectedAdditionalItems)
    {
        if (!TrackReferences || expectedAdditionalItems <= 0)
            return;

        int needed = workCount + expectedAdditionalItems;
        WorkItem[] local = workItems ??= new WorkItem[Math.Max(16, expectedAdditionalItems)];
        if (needed <= local.Length)
            return;

        int newSize = local.Length;
        while (newSize < needed)
            newSize *= 2;

        WorkItem[] resized = new WorkItem[newSize];
        Array.Copy(local, resized, workCount);
        workItems = resized;
    }

    public bool TryPop(out object from, out object to, out Type type)
    {
        if (!TrackReferences)
        {
            from = null!;
            to = null!;
            type = null!;
            return false;
        }

        if (workCount == 0)
        {
            from = null!;
            to = null!;
            type = null!;
            return false;
        }

        WorkItem wi = workItems![--workCount];
        from = wi.From;
        to = wi.To;
        type = wi.Type;
        return true;
    }

    public int IncrementDepth() => ++callDepth;

    public void DecrementDepth()
    {
        if (callDepth > 0) callDepth--;
    }

    public bool TryGetCachedTypeMetadata(Type type, out FastClonerCache.TypeCloneMetadata metadata)
    {
        RuntimeTypeHandle handle = type.TypeHandle;
        FastClonerCache.TypeCloneMetadata? cached1 = metadata1;
        if (cached1 is not null && metadataTypeHandle1.Equals(handle))
        {
            metadata = cached1;
            return true;
        }

        FastClonerCache.TypeCloneMetadata? cached2 = metadata2;
        if (cached2 is not null && metadataTypeHandle2.Equals(handle))
        {
            metadataTypeHandle2 = metadataTypeHandle1;
            metadata2 = cached1;
            metadataTypeHandle1 = handle;
            metadata1 = cached2;
            metadata = cached2;
            return true;
        }

        metadata = null!;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CacheTypeMetadata(Type type, FastClonerCache.TypeCloneMetadata metadata)
    {
        RuntimeTypeHandle handle = type.TypeHandle;
        if (metadataTypeHandle1.Equals(handle))
        {
            metadata1 = metadata;
            return;
        }

        metadataTypeHandle2 = metadataTypeHandle1;
        metadata2 = metadata1;
        metadataTypeHandle1 = handle;
        metadata1 = metadata;
    }

    private sealed class MiniDictionary
    {
        private struct Entry(int hashCode, nint next, object key, object value)
        {
            public readonly int HashCode = hashCode;
            public nint Next = next;
            public readonly object Key = key;
            public readonly object Value = value;
        }

        private const int DefaultCapacity = 8;

        private nint[] buckets;
        private Entry[] entries;
        private int count;
        private int bucketMask;
        public int Capacity => entries.Length;

        public MiniDictionary() : this(DefaultCapacity) { }

        public MiniDictionary(int capacity)
        {
            int size = RoundUpToPowerOf2(capacity < DefaultCapacity ? DefaultCapacity : capacity);
            Initialize(size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int RoundUpToPowerOf2(int value)
        {
            --value;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }

        public object? FindEntry(object key)
        {
            int hashCode = RuntimeHelpers.GetHashCode(key) & 0x7FFFFFFF;
            nint bucketIndex = hashCode & bucketMask;

            Entry[] entriesLocal = entries;
            for (nint i = buckets[bucketIndex]; i >= 0; i = entriesLocal[i].Next)
            {
                ref readonly Entry entry = ref entriesLocal[i];
                if (entry.HashCode == hashCode && ReferenceEquals(entry.Key, key))
                    return entry.Value;
            }

            return null;
        }

        private void Initialize(int size)
        {
            buckets = new nint[size];
#if MODERN
            Array.Fill(buckets, (nint)(-1));
#else
            for (int i = 0; i < buckets.Length; i++)
                buckets[i] = -1;
#endif
            entries = new Entry[size];
            bucketMask = size - 1;
        }

        public void Clear()
        {
            if (count == 0)
                return;

            Array.Clear(entries, 0, count);
#if MODERN
            Array.Fill(buckets, (nint)(-1));
#else
            for (int i = 0; i < buckets.Length; i++)
                buckets[i] = -1;
#endif
            count = 0;
        }

        public void EnsureCapacity(int capacity)
        {
            if (capacity <= entries.Length)
                return;
            Resize(RoundUpToPowerOf2(capacity));
        }

        public void Insert(object key, object value)
        {
            int hashCode = RuntimeHelpers.GetHashCode(key) & 0x7FFFFFFF;
            nint[] localBuckets = buckets;
            Entry[] localEntries = entries;

            if (count == localEntries.Length)
            {
                Resize();
                localBuckets = buckets;
                localEntries = entries;
            }

            nint targetBucket = hashCode & bucketMask;
            nint index = count++;
            localEntries[index] = new Entry(hashCode, localBuckets[targetBucket], key, value);
            localBuckets[targetBucket] = index;
        }

        private void Resize() => Resize(entries.Length * 2);

        private void Resize(int newSize)
        {
            nint[] newBuckets = new nint[newSize];
#if MODERN
            Array.Fill(newBuckets, (nint)(-1));
#else
            for (int i = 0; i < newBuckets.Length; i++)
                newBuckets[i] = -1;
#endif

            Entry[] newEntries = new Entry[newSize];
            Array.Copy(entries, newEntries, count);

            int newMask = newSize - 1;
            for (int i = 0; i < count; i++)
            {
                ref Entry entry = ref newEntries[i];
                nint bucket = entry.HashCode & newMask;
                entry.Next = newBuckets[bucket];
                newBuckets[bucket] = i;
            }

            buckets = newBuckets;
            entries = newEntries;
            bucketMask = newMask;
        }
    }
}
