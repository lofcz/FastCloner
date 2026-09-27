using System.Collections;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace FastCloner.Tests;

/// <summary>
/// A non-generic class that implements ISet&lt;string&gt; directly.
/// Exercises the code path where type.GetGenericArguments() returns an empty array
/// but IsSetType() matches via the interface.
/// </summary>
public class StringSet : ISet<string>
{
    private readonly HashSet<string> _inner = new();

    public int Count => _inner.Count;
    public bool IsReadOnly => false;

    public bool Add(string item) => _inner.Add(item);
    public void Clear() => _inner.Clear();
    public bool Contains(string item) => _inner.Contains(item);
    public void CopyTo(string[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);
    public void ExceptWith(IEnumerable<string> other) => _inner.ExceptWith(other);
    public IEnumerator<string> GetEnumerator() => _inner.GetEnumerator();
    public void IntersectWith(IEnumerable<string> other) => _inner.IntersectWith(other);
    public bool IsProperSubsetOf(IEnumerable<string> other) => _inner.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<string> other) => _inner.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<string> other) => _inner.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<string> other) => _inner.IsSupersetOf(other);
    public bool Overlaps(IEnumerable<string> other) => _inner.Overlaps(other);
    public bool Remove(string item) => _inner.Remove(item);
    public bool SetEquals(IEnumerable<string> other) => _inner.SetEquals(other);
    public void SymmetricExceptWith(IEnumerable<string> other) => _inner.SymmetricExceptWith(other);
    public void UnionWith(IEnumerable<string> other) => _inner.UnionWith(other);
    void ICollection<string>.Add(string item) => _inner.Add(item);
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class CollectionTests
{
    [Test]
    [Arguments(typeof(List<int>), false)]
    [Arguments(typeof(List<string>), false)]
    [Arguments(typeof(Queue<int>), false)]
    [Arguments(typeof(Stack<int>), false)]
    [Arguments(typeof(int[]), false)]
    [Arguments(typeof(List<object>), true)]
    [Arguments(typeof(Dictionary<int, int>), true)]
    [Arguments(typeof(ConcurrentDictionary<string, DateTime?>), true)]
    [Arguments(typeof(HashSet<string>), true)]
    public async Task Collection_CyclePolicy_Should_KeepOnlyProvenPayloadFastPaths(Type type, bool expected)
    {
        // Act
        bool actual = CanHaveCycles(type);

        // Assert
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    public async Task Collection_CyclePolicy_Should_NotCachePayloadForUnsupportedShape()
    {
        // Arrange
        Type type = typeof(PayloadLookupCollection);
        bool payloadFactoryCalled = false;

        // Act
        CanHaveCycles(type);
        Code.FastClonerCache.GetOrAddCollectionPayloadType(type, _ =>
        {
            payloadFactoryCalled = true;
            return typeof(int);
        });

        // Assert
        await Assert.That(payloadFactoryCalled).IsTrue();
    }

    [Test]
    public async Task Collection_WithInternalStorageCycle_Should_PreserveCycle()
    {
        // Arrange
        CollectionWithStorage original = new();
        await Assert.That(CanHaveCycles(original.GetType())).IsTrue();

        // Act
        var clone = original.DeepClone();

        // Assert
        await Assert.That(clone.Storage).IsNotSameReferenceAs(original.Storage);
        await Assert.That(clone.Storage.References[0]).IsSameReferenceAs(clone.Storage.References);
    }

    [Test]
    public async Task ConcurrentDictionary_WithInternalCycle_Should_PreserveReferenceTracking()
    {
        // Arrange
        ConcurrentDictionary<string, DateTime?> original = new(StringComparer.OrdinalIgnoreCase);
        original["first"] = null;
        // Check the classifier first so a regression fails instead of overflowing the test process.
        await Assert.That(CanHaveCycles(original.GetType())).IsTrue();

        // Act
        var clone = original.DeepClone();
        clone["second"] = null;

        // Assert
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.ContainsKey("FIRST")).IsTrue();
        await Assert.That(original.Count).IsEqualTo(1);
        await Assert.That(clone.Count).IsEqualTo(2);
    }

    [Test]
    public async Task DerivedList_WithInternalStorageCycle_Should_PreserveCycle()
    {
        // Arrange
        DerivedListWithStorage original = new() { 1, 2 };
        await Assert.That(CanHaveCycles(original.GetType())).IsTrue();

        // Act
        var clone = original.DeepClone();

        // Assert
        await Assert.That(clone).IsEquivalentTo(new[] { 1, 2 });
        await Assert.That(clone.Storage).IsNotSameReferenceAs(original.Storage);
        await Assert.That(clone.Storage.References[0]).IsSameReferenceAs(clone.Storage.References);
    }

    [Test]
    public async Task Dictionary_WithKeyReferencingDictionary_Should_PreserveCycle()
    {
        // Arrange
        Dictionary<CyclicDictionaryKey, int> original = new();
        CyclicDictionaryKey key = new() { Owner = original };
        original[key] = 1;
        await Assert.That(CanHaveCycles(original.GetType())).IsTrue();

        // Act
        var clone = original.DeepClone();
        var clonedKey = clone.Keys.Single();

        // Assert
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clonedKey).IsNotSameReferenceAs(key);
        await Assert.That(clonedKey.Owner).IsSameReferenceAs(clone);
        await Assert.That(clone[clonedKey]).IsEqualTo(1);
    }

    [Test]
    public async Task Dictionary_WithMaterializedViews_Should_BindViewsToClone()
    {
        // Arrange
        var original = Enumerable.Range(0, 100).ToDictionary(i => i, i => i);
        var originalKeys = original.Keys;
        var originalValues = original.Values;
        await Assert.That(CanHaveCycles(original.GetType())).IsTrue();

        // Act
        var clone = original.DeepClone();
        clone.Add(100, 100);

        // Assert
        await Assert.That(clone.Keys.Count).IsEqualTo(101);
        await Assert.That(clone.Values.Count).IsEqualTo(101);
        await Assert.That(originalKeys.Count).IsEqualTo(100);
        await Assert.That(originalValues.Count).IsEqualTo(100);
    }

    [Test]
    public async Task HashSet_WithComparerReferencingSet_Should_PreserveCycle()
    {
        // Arrange
        OwnerComparer comparer = new();
        HashSet<string> original = new(comparer) { "first" };
        comparer.Owner = original;
        await Assert.That(CanHaveCycles(original.GetType())).IsTrue();

        // Act
        var clone = original.DeepClone();

        // Assert
        await Assert.That(clone.Comparer).IsNotSameReferenceAs(comparer);
        await Assert.That(((OwnerComparer)clone.Comparer).Owner).IsSameReferenceAs(clone);
        await Assert.That(clone.Contains("FIRST")).IsTrue();
    }

    [Test]
    public async Task PriorityQueue_Should_Be_Deep_Cloned_Correctly()
    {
        PriorityQueue<string, int> original = new PriorityQueue<string, int>();
        original.Enqueue("Low", 10);
        original.Enqueue("High", 1);
        original.Enqueue("Medium", 5);

        PriorityQueue<string, int> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(3);

        // Verify order
        await Assert.That(clone.Dequeue()).IsEqualTo("High");
        await Assert.That(clone.Dequeue()).IsEqualTo("Medium");
        await Assert.That(clone.Dequeue()).IsEqualTo("Low");

        // Original should remain untouched
        await Assert.That(original.Count).IsEqualTo(3);
        await Assert.That(original.Dequeue()).IsEqualTo("High");
    }

    [Test]
    public async Task Stack_Should_Be_Deep_Cloned_Correctly()
    {
        Stack<int> original = new Stack<int>();
        original.Push(1);
        original.Push(2);
        original.Push(3);

        Stack<int> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(3);

        // Verify order (LIFO)
        await Assert.That(clone.Pop()).IsEqualTo(3);
        await Assert.That(clone.Pop()).IsEqualTo(2);
        await Assert.That(clone.Pop()).IsEqualTo(1);

        // Original should remain untouched
        await Assert.That(original.Count).IsEqualTo(3);
        await Assert.That(original.Peek()).IsEqualTo(3);
    }

    [Test]
    public async Task Queue_Should_Be_Deep_Cloned_Correctly()
    {
        Queue<int> original = new Queue<int>();
        original.Enqueue(1);
        original.Enqueue(2);
        original.Enqueue(3);

        Queue<int> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(3);

        // Verify order (FIFO)
        await Assert.That(clone.Dequeue()).IsEqualTo(1);
        await Assert.That(clone.Dequeue()).IsEqualTo(2);
        await Assert.That(clone.Dequeue()).IsEqualTo(3);

        // Original should remain untouched
        await Assert.That(original.Count).IsEqualTo(3);
        await Assert.That(original.Peek()).IsEqualTo(1);
    }

    [Test]
    public async Task ConcurrentStack_Should_Be_Deep_Cloned_Correctly()
    {
        ConcurrentStack<int> original = new ConcurrentStack<int>();
        original.Push(1);
        original.Push(2);
        original.Push(3);

        ConcurrentStack<int> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(3);

        // Verify order (LIFO)
        int result;
        await Assert.That(clone.TryPop(out result)).IsTrue();
        await Assert.That(result).IsEqualTo(3);

        await Assert.That(clone.TryPop(out result)).IsTrue();
        await Assert.That(result).IsEqualTo(2);

        await Assert.That(clone.TryPop(out result)).IsTrue();
        await Assert.That(result).IsEqualTo(1);

        // Original should remain untouched
        await Assert.That(original.Count).IsEqualTo(3);
        await Assert.That(original.TryPeek(out result)).IsTrue();
        await Assert.That(result).IsEqualTo(3);
    }
    
    [Test]
    public async Task ConcurrentQueue_Should_Be_Deep_Cloned_Correctly()
    {
        ConcurrentQueue<int> original = new ConcurrentQueue<int>();
        original.Enqueue(1);
        original.Enqueue(2);
        original.Enqueue(3);

        ConcurrentQueue<int> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(3);

        // Verify order (FIFO)
        int result;
        await Assert.That(clone.TryDequeue(out result)).IsTrue();
        await Assert.That(result).IsEqualTo(1);

        await Assert.That(clone.TryDequeue(out result)).IsTrue();
        await Assert.That(result).IsEqualTo(2);

        await Assert.That(clone.TryDequeue(out result)).IsTrue();
        await Assert.That(result).IsEqualTo(3);

        // Original should remain untouched
        await Assert.That(original.Count).IsEqualTo(3);
        await Assert.That(original.TryPeek(out result)).IsTrue();
        await Assert.That(result).IsEqualTo(1);
    }

    [Test]
    public async Task BlockingCollection_Should_Be_Deep_Cloned_Correctly()
    {
        BlockingCollection<int> original = new BlockingCollection<int>();
        original.Add(1);
        original.Add(2);
        original.Add(3);

        BlockingCollection<int> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(3);

        // Verify order (FIFO by default)
        await Assert.That(clone.Take()).IsEqualTo(1);
        await Assert.That(clone.Take()).IsEqualTo(2);
        await Assert.That(clone.Take()).IsEqualTo(3);

        // Original should remain untouched
        await Assert.That(original.Count).IsEqualTo(3);
    }

    [Test]
    public async Task LinkedList_Should_Be_Deep_Cloned_Correctly()
    {
        LinkedList<int> original = new LinkedList<int>();
        original.AddLast(1);
        original.AddLast(2);
        original.AddLast(3);

        LinkedList<int> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(3);

        // Verify order
        await Assert.That(clone.First.Value).IsEqualTo(1);
        await Assert.That(clone.First.Next.Value).IsEqualTo(2);
        await Assert.That(clone.Last.Value).IsEqualTo(3);

        // Original should remain untouched
        await Assert.That(original.Count).IsEqualTo(3);
    }

    [Test]
    public async Task NonGenericSetImplementingISet_Should_Be_Deep_Cloned_Correctly()
    {
        StringSet original = new StringSet();
        original.Add("alpha");
        original.Add("beta");
        original.Add("gamma");

        StringSet clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(3);
        await Assert.That(clone.Contains("alpha")).IsTrue();
        await Assert.That(clone.Contains("beta")).IsTrue();
        await Assert.That(clone.Contains("gamma")).IsTrue();

        // Mutating clone should not affect original
        clone.Add("delta");
        await Assert.That(clone.Count).IsEqualTo(4);
        await Assert.That(original.Count).IsEqualTo(3);
    }

    [Test]
    public async Task NonGenericSetInsideObject_Should_Be_Deep_Cloned_Correctly()
    {
        ObjectWithNonGenericSet original = new ObjectWithNonGenericSet
        {
            Name = "test",
            Tags = new StringSet()
        };
        original.Tags.Add("a");
        original.Tags.Add("b");

        ObjectWithNonGenericSet clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Tags).IsNotSameReferenceAs(original.Tags);
        await Assert.That(clone.Tags.Count).IsEqualTo(2);
        await Assert.That(clone.Tags.Contains("a")).IsTrue();
        await Assert.That(clone.Tags.Contains("b")).IsTrue();

        clone.Tags.Add("c");
        await Assert.That(clone.Tags.Count).IsEqualTo(3);
        await Assert.That(original.Tags.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GenericSetWhereElementIsNotFirstTypeArg_Should_Be_Deep_Cloned_Correctly()
    {
        // TTag=List<int> (mutable, no stable hash semantics) forces the iterate-and-clone
        // path rather than the memberwise fast path, exposing the wrong element type.
        TaggedSet<List<int>, string> original = new TaggedSet<List<int>, string>
        {
            Tag = new List<int> { 1, 2, 3 }
        };
        original.Add("one");
        original.Add("two");
        original.Add("three");

        TaggedSet<List<int>, string> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Tag).IsNotSameReferenceAs(original.Tag);
        await Assert.That(clone.Tag).IsEquivalentTo(original.Tag);
        await Assert.That(clone.Count).IsEqualTo(3);
        await Assert.That(clone.Contains("one")).IsTrue();
        await Assert.That(clone.Contains("two")).IsTrue();
        await Assert.That(clone.Contains("three")).IsTrue();

        // Mutating clone should not affect original
        clone.Add("four");
        clone.Tag = new List<int> { 99 };
        await Assert.That(clone.Count).IsEqualTo(4);
        await Assert.That(original.Count).IsEqualTo(3);
        await Assert.That(original.Tag).IsEquivalentTo(new List<int> { 1, 2, 3 });
    }

    private sealed class CyclicStorage
    {
        public object[] References = new object[1];
        public CyclicStorage() => References[0] = References;
    }

    private sealed class DerivedListWithStorage : List<int>
    {
        public CyclicStorage Storage = new();
    }

    private sealed class CollectionWithStorage : IEnumerable<int>
    {
        public CyclicStorage Storage = new();
        public IEnumerator<int> GetEnumerator() => Enumerable.Empty<int>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class PayloadLookupCollection : IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator() => Enumerable.Empty<int>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class OwnerComparer : IEqualityComparer<string>
    {
        public HashSet<string>? Owner;
        public bool Equals(string? x, string? y) => StringComparer.OrdinalIgnoreCase.Equals(x, y);
        public int GetHashCode(string obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
    }

    private static bool CanHaveCycles(Type type) => (bool)typeof(Code.FastClonerGenerator)
        .GetMethod("CalculateCanHaveCycles", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
        .Invoke(null, [type])!;

    private sealed class CyclicDictionaryKey
    {
        public required Dictionary<CyclicDictionaryKey, int> Owner { get; init; }
    }
}

public class ObjectWithNonGenericSet
{
    public string Name { get; set; } = "";
    public StringSet Tags { get; set; } = new();
}

/// <summary>
/// A generic set where the ISet element type is NOT the first generic parameter.
/// Exercises the case where type.GetGenericArguments()[0] != the ISet&lt;T&gt; element type.
/// </summary>
public class TaggedSet<TTag, TElement> : ISet<TElement>
{
    private readonly HashSet<TElement> _inner = new();
    public TTag? Tag { get; set; }

    public int Count => _inner.Count;
    public bool IsReadOnly => false;

    public bool Add(TElement item) => _inner.Add(item);
    public void Clear() => _inner.Clear();
    public bool Contains(TElement item) => _inner.Contains(item);
    public void CopyTo(TElement[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);
    public void ExceptWith(IEnumerable<TElement> other) => _inner.ExceptWith(other);
    public IEnumerator<TElement> GetEnumerator() => _inner.GetEnumerator();
    public void IntersectWith(IEnumerable<TElement> other) => _inner.IntersectWith(other);
    public bool IsProperSubsetOf(IEnumerable<TElement> other) => _inner.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<TElement> other) => _inner.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<TElement> other) => _inner.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<TElement> other) => _inner.IsSupersetOf(other);
    public bool Overlaps(IEnumerable<TElement> other) => _inner.Overlaps(other);
    public bool Remove(TElement item) => _inner.Remove(item);
    public bool SetEquals(IEnumerable<TElement> other) => _inner.SetEquals(other);
    public void SymmetricExceptWith(IEnumerable<TElement> other) => _inner.SymmetricExceptWith(other);
    public void UnionWith(IEnumerable<TElement> other) => _inner.UnionWith(other);
    void ICollection<TElement>.Add(TElement item) => _inner.Add(item);
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
