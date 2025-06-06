namespace FastCloner.Code;

using System.Runtime.CompilerServices;

internal sealed class FastCloneState
{
    private MiniDictionary? loops;
    private readonly object[] baseFromTo = new object[6];
    private int idx;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? GetKnownRef(object from)
    {
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

    private sealed class MiniDictionary
    {
        private struct Entry(int hashCode, int next, object key, object value)
        {
            public readonly int HashCode = hashCode;
            public int Next = next;
            public readonly object Key = key;
            public readonly object Value = value;
        }

        private int[]? buckets;
        private Entry[] entries;
        private int count;
        private const int DefaultCapacity = 7;

        public MiniDictionary() : this(DefaultCapacity)
        {
        }

        public MiniDictionary(int capacity)
        {
            if (capacity > 0)
                Initialize(capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object? FindEntry(object key)
        {
            if (buckets is null)
            {
                return null;
            }

            int hashCode = RuntimeHelpers.GetHashCode(key) & 0x7FFFFFFF;
            int bucketIndex = hashCode % buckets.Length;
            
            Entry[] entriesLocal = entries;
            for (int i = buckets[bucketIndex]; i >= 0; i = entriesLocal[i].Next)
            {
                ref readonly Entry entry = ref entriesLocal[i];
                if (entry.HashCode == hashCode && ReferenceEquals(entry.Key, key))
                    return entry.Value;
            }

            return null;
        }

        // Hash Table Primes, stabilizes to ~1.2x increase
        private static readonly int[] primes =
        [
            3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
            1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
            17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
            187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
            1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369
        ];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetPrime(int min)
        {
            int left = 0;
            int right = primes.Length - 1;
            
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                if (primes[mid] >= min)
                {
                    right = mid - 1;
                }
                else
                {
                    left = mid + 1;
                }
            }
            
            return left < primes.Length ? primes[left] : GeneratePrime(min);
        }

        private static int GeneratePrime(int min)
        {
            for (int i = min | 1; i < int.MaxValue; i += 2)
            {
                if (IsPrime(i) && (i - 1) % 101 is not 0)
                {
                    return i;
                }
            }
            
            return min;
        }

        private static bool IsPrime(int candidate)
        {
            if ((candidate & 1) is 0)
                return candidate is 2;
            
            int limit = (int)Math.Sqrt(candidate);
            for (int divisor = 3; divisor <= limit; divisor += 2)
            {
                if (candidate % divisor is 0)
                    return false;
            }
            return true;
        }

        private static int ExpandPrime(int oldSize)
        {
            int newSize = 2 * oldSize;
            if ((uint)newSize > 0x7FEFFFFD && 0x7FEFFFFD > oldSize)
                return 0x7FEFFFFD;
            return GetPrime(newSize);
        }

        private void Initialize(int size)
        {
            buckets = new int[size];
#if MODERN
            Array.Fill(buckets, -1);
#else
            buckets.Fill(-1);
#endif
            entries = new Entry[size];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(object key, object value)
        {
            if (buckets is null)
            {
                Initialize(GetPrime(DefaultCapacity));
            }

            int hashCode = RuntimeHelpers.GetHashCode(key) & 0x7FFFFFFF;
            int[] localBuckets = buckets!;
            int targetBucket = hashCode % localBuckets.Length;
            Entry[] localEntries = entries;

            if (count == localEntries.Length)
            {
                Resize();
                localBuckets = buckets!;
                localEntries = entries;
                targetBucket = hashCode % localBuckets.Length;
            }

            int index = count++;
            localEntries[index] = new Entry(hashCode, localBuckets[targetBucket], key, value);
            localBuckets[targetBucket] = index;
        }

        private void Resize() => Resize(ExpandPrime(count));

        private void Resize(int newSize)
        {
            int[] newBuckets = new int[newSize];
#if MODERN
            Array.Fill(newBuckets, -1);
#else
            newBuckets.Fill(-1);
#endif
            
            Entry[] newEntries = new Entry[newSize];
            Array.Copy(entries, newEntries, count);

            for (int i = 0; i < count; i++)
            {
                ref Entry entry = ref newEntries[i];
                
                if (entry.HashCode < 0)
                {
                    continue;
                }

                int bucket = entry.HashCode % newSize;
                entry.Next = newBuckets[bucket];
                newBuckets[bucket] = i;
            }

            buckets = newBuckets;
            entries = newEntries;
        }
    }
}
