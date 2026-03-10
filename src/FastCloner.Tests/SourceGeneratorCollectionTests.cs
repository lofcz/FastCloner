using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using FastCloner.SourceGenerator.Shared;
using System.Threading.Tasks;

namespace FastCloner.Tests;

/// <summary>
/// Simple mutable reference type for testing deep cloning
/// </summary>
[FastClonerClonable]
public class MutableItem
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

/// <summary>
/// Tests Stack<T> with complex elements - verifies LIFO order preservation
/// </summary>
[FastClonerClonable]
public class StackContainer
{
    public Stack<MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests Queue<T> with complex elements - verifies FIFO order preservation
/// </summary>
[FastClonerClonable]
public class QueueContainer
{
    public Queue<MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests LinkedList<T> with complex elements - verifies order preservation
/// </summary>
[FastClonerClonable]
public class LinkedListContainer
{
    public LinkedList<MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests SortedSet<T> - verifies sorted order is maintained
/// </summary>
[FastClonerClonable]
public class SortedSetContainer
{
    public SortedSet<int>? Numbers { get; set; }
}

/// <summary>
/// Tests SortedDictionary<K,V> with complex values
/// </summary>
[FastClonerClonable]
public class SortedDictionaryContainer
{
    public SortedDictionary<int, MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests SortedList<K,V> with complex values
/// </summary>
[FastClonerClonable]
public class SortedListContainer
{
    public SortedList<string, MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests ConcurrentStack<T> with complex elements
/// </summary>
[FastClonerClonable]
public class ConcurrentStackContainer
{
    public ConcurrentStack<MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests ConcurrentQueue<T> with complex elements
/// </summary>
[FastClonerClonable]
public class ConcurrentQueueContainer
{
    public ConcurrentQueue<MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests ConcurrentDictionary<K,V> with complex values
/// </summary>
[FastClonerClonable]
public class ConcurrentDictionaryContainer
{
    public ConcurrentDictionary<int, MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests ConcurrentBag<T> with complex elements
/// </summary>
[FastClonerClonable]
public class ConcurrentBagContainer
{
    public ConcurrentBag<MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests ImmutableList<T> with complex elements
/// </summary>
[FastClonerClonable]
public class ImmutableListContainer
{
    public ImmutableList<MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests ImmutableArray<T> with complex elements
/// </summary>
[FastClonerClonable]
public class ImmutableArrayContainer
{
    public ImmutableArray<MutableItem> Items { get; set; }
}

/// <summary>
/// Tests ImmutableStack<T> with complex elements - verifies LIFO order
/// </summary>
[FastClonerClonable]
public class ImmutableStackContainer
{
    public ImmutableStack<MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests ImmutableQueue<T> with complex elements - verifies FIFO order
/// </summary>
[FastClonerClonable]
public class ImmutableQueueContainer
{
    public ImmutableQueue<MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests ImmutableHashSet<T> with primitives (returns same reference optimization)
/// </summary>
[FastClonerClonable]
public class ImmutableHashSetPrimitiveContainer
{
    public ImmutableHashSet<int>? Numbers { get; set; }
}

/// <summary>
/// Tests ImmutableDictionary<K,V> with primitives (returns same reference optimization)
/// </summary>
[FastClonerClonable]
public class ImmutableDictionaryPrimitiveContainer
{
    public ImmutableDictionary<int, string>? Items { get; set; }
}

/// <summary>
/// Tests ImmutableDictionary<K,V> with complex values
/// </summary>
[FastClonerClonable]
public class ImmutableDictionaryComplexContainer
{
    public ImmutableDictionary<int, MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests ImmutableSortedSet<T>
/// </summary>
[FastClonerClonable]
public class ImmutableSortedSetContainer
{
    public ImmutableSortedSet<int>? Numbers { get; set; }
}

/// <summary>
/// Tests ImmutableSortedDictionary<K,V>
/// </summary>
[FastClonerClonable]
public class ImmutableSortedDictionaryContainer
{
    public ImmutableSortedDictionary<int, MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests ReadOnlyCollection<T> with complex elements
/// </summary>
[FastClonerClonable]
public class ReadOnlyCollectionContainer
{
    public ReadOnlyCollection<MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests ReadOnlyDictionary<K,V> with complex values
/// </summary>
[FastClonerClonable]
public class ReadOnlyDictionaryContainer
{
    public ReadOnlyDictionary<int, MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests ObservableCollection<T> with complex elements
/// </summary>
[FastClonerClonable]
public class ObservableCollectionContainer
{
    public ObservableCollection<MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests HashSet<T> with complex elements
/// </summary>
[FastClonerClonable]
public class HashSetContainer
{
    public HashSet<MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests jagged arrays with complex elements
/// </summary>
[FastClonerClonable]
public class JaggedArrayContainer
{
    public MutableItem[][]? Items { get; set; }
}

/// <summary>
/// Tests multi-dimensional arrays with complex elements
/// </summary>
[FastClonerClonable]
public class MultiDimArrayContainer
{
    public MutableItem[,]? Items { get; set; }
}

/// <summary>
/// Tests 3D arrays with primitives
/// </summary>
[FastClonerClonable]
public class ThreeDimArrayContainer
{
    public int[,,]? Numbers { get; set; }
}

/// <summary>
/// Tests nested collections (List of Stacks)
/// </summary>
[FastClonerClonable]
public class NestedCollectionContainer
{
    public List<Stack<MutableItem>>? StackList { get; set; }
}

/// <summary>
/// Tests Dictionary with collection values
/// </summary>
[FastClonerClonable]
public class DictionaryWithCollectionValuesContainer
{
    public Dictionary<string, List<MutableItem>>? Items { get; set; }
}

/// <summary>
/// Tests ICollection&lt;T&gt; with complex elements - verifies no index-based access is generated (#28)
/// </summary>
[FastClonerClonable]
public class ICollectionContainer
{
    public ICollection<MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests IEnumerable&lt;T&gt; with complex elements - verifies no .Count or index-based access is generated
/// </summary>
[FastClonerClonable]
public class IEnumerableContainer
{
    public IEnumerable<MutableItem>? Items { get; set; }
}

/// <summary>
/// Tests IReadOnlyCollection&lt;T&gt; with complex elements - verifies no index-based access is generated
/// </summary>
[FastClonerClonable]
public class IReadOnlyCollectionContainer
{
    public IReadOnlyCollection<MutableItem>? Items { get; set; }
}

/// <summary>
/// A concrete collection class that only implements IEnumerable&lt;T&gt;
/// </summary>
public class EnumerableOnlyCollection<T> : IEnumerable<T>
{
    private readonly List<T> _inner = [];
    public void Add(T item) => _inner.Add(item);
    public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Container whose property type is a concrete class that only implements IEnumerable&lt;T&gt;.
/// </summary>
[FastClonerClonable]
public class ConcreteEnumerableOnlyContainer
{
    public EnumerableOnlyCollection<MutableItem>? Items { get; set; }
}
public class SourceGeneratorCollectionTests
{
    [Test]
    [SourceGeneratorCompatible]
    public async Task Stack_With_Complex_Elements_Should_Preserve_LIFO_Order()
    {
        // Arrange
        StackContainer original = new StackContainer
        {
            Items = new Stack<MutableItem>()
        };
        original.Items.Push(new MutableItem { Id = 1, Name = "First" });
        original.Items.Push(new MutableItem { Id = 2, Name = "Second" });
        original.Items.Push(new MutableItem { Id = 3, Name = "Third" });

        // Act
        StackContainer clone = original.FastDeepClone();

        // Assert - verify LIFO order (3, 2, 1)
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items!.Count).IsEqualTo(3);

        MutableItem[] cloneItems = clone.Items.ToArray();
        await Assert.That(cloneItems[0].Id).IsEqualTo(3).Because("First pop should be 3 (LIFO)");
        await Assert.That(cloneItems[1].Id).IsEqualTo(2).Because("Second pop should be 2 (LIFO)");
        await Assert.That(cloneItems[2].Id).IsEqualTo(1).Because("Third pop should be 1 (LIFO)");

        // Verify deep clone - modifying clone shouldn't affect original
        cloneItems[0].Name = "Modified";
        await Assert.That(original.Items.Peek().Name).IsEqualTo("Third");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Stack_Empty_Should_Clone_Correctly()
    {
        StackContainer original = new StackContainer { Items = new Stack<MutableItem>() };
        StackContainer clone = original.FastDeepClone();
        
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items!.Count).IsEqualTo(0);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Queue_With_Complex_Elements_Should_Preserve_FIFO_Order()
    {
        // Arrange
        QueueContainer original = new QueueContainer
        {
            Items = new Queue<MutableItem>()
        };
        original.Items.Enqueue(new MutableItem { Id = 1, Name = "First" });
        original.Items.Enqueue(new MutableItem { Id = 2, Name = "Second" });
        original.Items.Enqueue(new MutableItem { Id = 3, Name = "Third" });

        // Act
        QueueContainer clone = original.FastDeepClone();

        // Assert - verify FIFO order (1, 2, 3)
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items!.Count).IsEqualTo(3);

        MutableItem[] cloneItems = clone.Items.ToArray();
        await Assert.That(cloneItems[0].Id).IsEqualTo(1).Because("First dequeue should be 1 (FIFO)");
        await Assert.That(cloneItems[1].Id).IsEqualTo(2).Because("Second dequeue should be 2 (FIFO)");
        await Assert.That(cloneItems[2].Id).IsEqualTo(3).Because("Third dequeue should be 3 (FIFO)");

        // Verify deep clone
        cloneItems[0].Name = "Modified";
        await Assert.That(original.Items.Peek().Name).IsEqualTo("First");
    }
    
    [Test]
    [SourceGeneratorCompatible]
    public async Task LinkedList_With_Complex_Elements_Should_Preserve_Order()
    {
        // Arrange
        LinkedListContainer original = new LinkedListContainer
        {
            Items = []
        };
        original.Items.AddLast(new MutableItem { Id = 1, Name = "First" });
        original.Items.AddLast(new MutableItem { Id = 2, Name = "Second" });
        original.Items.AddLast(new MutableItem { Id = 3, Name = "Third" });

        // Act
        LinkedListContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items!.Count).IsEqualTo(3);
        await Assert.That(clone.Items.First!.Value.Id).IsEqualTo(1);
        await Assert.That(clone.Items.Last!.Value.Id).IsEqualTo(3);

        // Verify deep clone
        clone.Items.First.Value.Name = "Modified";
        await Assert.That(original.Items.First!.Value.Name).IsEqualTo("First");
    }
    
    [Test]
    [SourceGeneratorCompatible]
    public async Task SortedSet_Should_Maintain_Sorted_Order()
    {
        // Arrange - insert out of order
        SortedSetContainer original = new SortedSetContainer
        {
            Numbers = [3, 1, 4, 1, 5, 9, 2, 6]
        };

        // Act
        SortedSetContainer clone = original.FastDeepClone();

        // Assert - should be sorted: 1, 2, 3, 4, 5, 6, 9
        await Assert.That(clone!.Numbers).IsNotNull();
        await Assert.That(clone.Numbers!.ToArray().SequenceEqual([1, 2, 3, 4, 5, 6, 9])).IsTrue();
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task SortedDictionary_Should_Maintain_Key_Order_And_Deep_Clone_Values()
    {
        // Arrange
        SortedDictionaryContainer original = new SortedDictionaryContainer
        {
            Items = new SortedDictionary<int, MutableItem>
            {
                { 3, new MutableItem { Id = 3, Name = "Three" } },
                { 1, new MutableItem { Id = 1, Name = "One" } },
                { 2, new MutableItem { Id = 2, Name = "Two" } }
            }
        };

        // Act
        SortedDictionaryContainer clone = original.FastDeepClone();

        // Assert - keys should be in order: 1, 2, 3
        await Assert.That(clone!.Items).IsNotNull();
        int[] keys = clone.Items!.Keys.ToArray();
        await Assert.That(keys.SequenceEqual([1, 2, 3])).IsTrue();

        // Verify deep clone
        clone.Items[1].Name = "Modified";
        await Assert.That(original.Items[1].Name).IsEqualTo("One");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task SortedList_Should_Maintain_Key_Order_And_Deep_Clone_Values()
    {
        // Arrange
        SortedListContainer original = new SortedListContainer
        {
            Items = new SortedList<string, MutableItem>
            {
                { "charlie", new MutableItem { Id = 3, Name = "Charlie" } },
                { "alpha", new MutableItem { Id = 1, Name = "Alpha" } },
                { "bravo", new MutableItem { Id = 2, Name = "Bravo" } }
            }
        };

        // Act
        SortedListContainer clone = original.FastDeepClone();

        // Assert - keys should be in order: alpha, bravo, charlie
        await Assert.That(clone!.Items).IsNotNull();
        string[] keys = clone.Items!.Keys.ToArray();
        await Assert.That(keys.SequenceEqual(new[] { "alpha", "bravo", "charlie" })).IsTrue();

        // Verify deep clone
        clone.Items["alpha"].Name = "Modified";
        await Assert.That(original.Items["alpha"].Name).IsEqualTo("Alpha");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ConcurrentStack_With_Complex_Elements_Should_Preserve_Order()
    {
        // Arrange
        ConcurrentStackContainer original = new ConcurrentStackContainer
        {
            Items = new ConcurrentStack<MutableItem>()
        };
        original.Items.Push(new MutableItem { Id = 1, Name = "First" });
        original.Items.Push(new MutableItem { Id = 2, Name = "Second" });
        original.Items.Push(new MutableItem { Id = 3, Name = "Third" });

        // Act
        ConcurrentStackContainer clone = original.FastDeepClone();

        // Assert - LIFO order
        await Assert.That(clone!.Items).IsNotNull();
        MutableItem[] cloneItems = clone.Items!.ToArray();
        await Assert.That(cloneItems[0].Id).IsEqualTo(3);
        await Assert.That(cloneItems[1].Id).IsEqualTo(2);
        await Assert.That(cloneItems[2].Id).IsEqualTo(1);

        // Verify deep clone
        cloneItems[0].Name = "Modified";
        original.Items.TryPeek(out MutableItem? originalTop);
        await Assert.That(originalTop!.Name).IsEqualTo("Third");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ConcurrentQueue_With_Complex_Elements_Should_Preserve_Order()
    {
        // Arrange
        ConcurrentQueueContainer original = new ConcurrentQueueContainer
        {
            Items = new ConcurrentQueue<MutableItem>()
        };
        original.Items.Enqueue(new MutableItem { Id = 1, Name = "First" });
        original.Items.Enqueue(new MutableItem { Id = 2, Name = "Second" });
        original.Items.Enqueue(new MutableItem { Id = 3, Name = "Third" });

        // Act
        ConcurrentQueueContainer clone = original.FastDeepClone();

        // Assert - FIFO order
        await Assert.That(clone!.Items).IsNotNull();
        MutableItem[] cloneItems = clone.Items!.ToArray();
        await Assert.That(cloneItems[0].Id).IsEqualTo(1);
        await Assert.That(cloneItems[1].Id).IsEqualTo(2);
        await Assert.That(cloneItems[2].Id).IsEqualTo(3);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ConcurrentDictionary_With_Complex_Values_Should_Deep_Clone()
    {
        // Arrange
        ConcurrentDictionaryContainer original = new ConcurrentDictionaryContainer
        {
            Items = new ConcurrentDictionary<int, MutableItem>()
        };
        original.Items[1] = new MutableItem { Id = 1, Name = "One" };
        original.Items[2] = new MutableItem { Id = 2, Name = "Two" };

        // Act
        ConcurrentDictionaryContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items!.Count).IsEqualTo(2);
        await Assert.That(clone.Items[1].Name).IsEqualTo("One");

        // Verify deep clone
        clone.Items[1].Name = "Modified";
        await Assert.That(original.Items[1].Name).IsEqualTo("One");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ConcurrentBag_With_Complex_Elements_Should_Clone()
    {
        // Arrange
        ConcurrentBagContainer original = new ConcurrentBagContainer
        {
            Items =
            [
                new MutableItem { Id = 1, Name = "One" },
                new MutableItem { Id = 2, Name = "Two" },
                new MutableItem { Id = 3, Name = "Three" }
            ]
        };

        // Act
        ConcurrentBagContainer clone = original.FastDeepClone();

        // Assert - ConcurrentBag doesn't guarantee order, just check contents
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items!.Count).IsEqualTo(3);

        int[] cloneIds = clone.Items.Select(x => x.Id).OrderBy(x => x).ToArray();
        await Assert.That(cloneIds).IsEquivalentTo([1, 2, 3]);

        // Verify deep clone
        MutableItem firstCloneItem = clone.Items.First();
        int originalId = firstCloneItem.Id;
        firstCloneItem.Name = "Modified";
        
        MutableItem originalItem = original.Items.First(x => x.Id == originalId);
        await Assert.That(originalItem.Name).IsNotEqualTo("Modified");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ImmutableList_With_Complex_Elements_Should_Deep_Clone()
    {
        // Arrange
        ImmutableListContainer original = new ImmutableListContainer
        {
            Items = ImmutableList.Create(
                new MutableItem { Id = 1, Name = "One" },
                new MutableItem { Id = 2, Name = "Two" }
            )
        };

        // Act
        ImmutableListContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items!.Count).IsEqualTo(2);
        await Assert.That(clone.Items[0].Id).IsEqualTo(1);

        // Verify deep clone
        clone.Items[0].Name = "Modified";
        await Assert.That(original.Items[0].Name).IsEqualTo("One");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ImmutableArray_With_Complex_Elements_Should_Deep_Clone()
    {
        // Arrange
        ImmutableArrayContainer original = new ImmutableArrayContainer
        {
            Items =
            [
                new MutableItem { Id = 1, Name = "One" },
                new MutableItem { Id = 2, Name = "Two" }
            ]
        };

        // Act
        ImmutableArrayContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone!.Items.Length).IsEqualTo(2);
        await Assert.That(clone.Items[0].Id).IsEqualTo(1);

        // Verify deep clone
        clone.Items[0].Name = "Modified";
        await Assert.That(original.Items[0].Name).IsEqualTo("One");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ImmutableStack_With_Complex_Elements_Should_Preserve_Order()
    {
        // Arrange - Push creates stack in reverse order (last push = top)
        ImmutableStackContainer original = new ImmutableStackContainer
        {
            Items = ImmutableStack<MutableItem>.Empty
                .Push(new MutableItem { Id = 1, Name = "First" })
                .Push(new MutableItem { Id = 2, Name = "Second" })
                .Push(new MutableItem { Id = 3, Name = "Third" })
        };

        // Act
        ImmutableStackContainer clone = original.FastDeepClone();

        // Assert - LIFO: 3 should be on top
        await Assert.That(clone!.Items).IsNotNull();
        MutableItem[] cloneArray = clone.Items!.ToArray();
        await Assert.That(cloneArray[0].Id).IsEqualTo(3).Because("Top should be 3");
        await Assert.That(cloneArray[1].Id).IsEqualTo(2);
        await Assert.That(cloneArray[2].Id).IsEqualTo(1);

        // Verify deep clone
        cloneArray[0].Name = "Modified";
        await Assert.That(original.Items.Peek().Name).IsEqualTo("Third");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ImmutableQueue_With_Complex_Elements_Should_Preserve_Order()
    {
        // Arrange
        ImmutableQueueContainer original = new ImmutableQueueContainer
        {
            Items = ImmutableQueue<MutableItem>.Empty
                .Enqueue(new MutableItem { Id = 1, Name = "First" })
                .Enqueue(new MutableItem { Id = 2, Name = "Second" })
                .Enqueue(new MutableItem { Id = 3, Name = "Third" })
        };

        // Act
        ImmutableQueueContainer clone = original.FastDeepClone();

        // Assert - FIFO order
        await Assert.That(clone!.Items).IsNotNull();
        MutableItem[] cloneArray = clone.Items!.ToArray();
        await Assert.That(cloneArray[0].Id).IsEqualTo(1).Because("First should be 1 (FIFO)");
        await Assert.That(cloneArray[1].Id).IsEqualTo(2);
        await Assert.That(cloneArray[2].Id).IsEqualTo(3);

        // Verify deep clone
        cloneArray[0].Name = "Modified";
        await Assert.That(original.Items.Peek().Name).IsEqualTo("First");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ImmutableHashSet_With_Primitives_Returns_Same_Reference()
    {
        // Arrange
        ImmutableHashSetPrimitiveContainer original = new ImmutableHashSetPrimitiveContainer
        {
            Numbers = ImmutableHashSet.Create(1, 2, 3, 4, 5)
        };

        // Act
        ImmutableHashSetPrimitiveContainer clone = original.FastDeepClone();

        // Assert - optimization: should return same reference for immutable with immutable elements
        await Assert.That(clone!.Numbers).IsNotNull();
        await Assert.That(clone.Numbers).IsSameReferenceAs(original.Numbers).Because("Immutable collection with immutable elements should return same reference");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ImmutableDictionary_With_Primitives_Returns_Same_Reference()
    {
        // Arrange
        ImmutableDictionaryPrimitiveContainer original = new ImmutableDictionaryPrimitiveContainer
        {
            Items = ImmutableDictionary.Create<int, string>()
                .Add(1, "One")
                .Add(2, "Two")
        };

        // Act
        ImmutableDictionaryPrimitiveContainer clone = original.FastDeepClone();

        // Assert - optimization: should return same reference
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items).IsSameReferenceAs(original.Items).Because("Immutable dictionary with immutable keys and values should return same reference");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ImmutableDictionary_With_Complex_Values_Should_Deep_Clone()
    {
        // Arrange
        ImmutableDictionaryComplexContainer original = new ImmutableDictionaryComplexContainer
        {
            Items = ImmutableDictionary.Create<int, MutableItem>()
                .Add(1, new MutableItem { Id = 1, Name = "One" })
                .Add(2, new MutableItem { Id = 2, Name = "Two" })
        };

        // Act
        ImmutableDictionaryComplexContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items!.Count).IsEqualTo(2);

        // Verify deep clone - values should be different instances
        clone.Items[1].Name = "Modified";
        await Assert.That(original.Items[1].Name).IsEqualTo("One");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ImmutableSortedSet_Should_Maintain_Order()
    {
        // Arrange
        ImmutableSortedSetContainer original = new ImmutableSortedSetContainer
        {
            Numbers = ImmutableSortedSet.Create(5, 3, 1, 4, 2)
        };

        // Act
        ImmutableSortedSetContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone!.Numbers).IsNotNull();
        await Assert.That(clone.Numbers!.ToArray().SequenceEqual([1, 2, 3, 4, 5])).IsTrue();

        // Should be same reference since primitives
        await Assert.That(clone.Numbers).IsSameReferenceAs(original.Numbers);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ImmutableSortedDictionary_Should_Maintain_Order_And_Deep_Clone()
    {
        // Arrange
        ImmutableSortedDictionaryContainer original = new ImmutableSortedDictionaryContainer
        {
            Items = ImmutableSortedDictionary.Create<int, MutableItem>()
                .Add(3, new MutableItem { Id = 3, Name = "Three" })
                .Add(1, new MutableItem { Id = 1, Name = "One" })
                .Add(2, new MutableItem { Id = 2, Name = "Two" })
        };

        // Act
        ImmutableSortedDictionaryContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone!.Items).IsNotNull();
        int[] keys = clone.Items!.Keys.ToArray();
        await Assert.That(keys.SequenceEqual([1, 2, 3])).IsTrue();

        // Verify deep clone
        clone.Items[1].Name = "Modified";
        await Assert.That(original.Items[1].Name).IsEqualTo("One");
    }
    
    [Test]
    [SourceGeneratorCompatible]
    public async Task ReadOnlyCollection_With_Complex_Elements_Should_Deep_Clone()
    {
        // Arrange
        ReadOnlyCollectionContainer original = new ReadOnlyCollectionContainer
        {
            Items = new ReadOnlyCollection<MutableItem>(new List<MutableItem>
            {
                new MutableItem { Id = 1, Name = "One" },
                new MutableItem { Id = 2, Name = "Two" }
            })
        };

        // Act
        ReadOnlyCollectionContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items!.Count).IsEqualTo(2);

        // Verify deep clone
        clone.Items[0].Name = "Modified";
        await Assert.That(original.Items[0].Name).IsEqualTo("One");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ReadOnlyDictionary_With_Complex_Values_Should_Deep_Clone()
    {
        // Arrange
        ReadOnlyDictionaryContainer original = new ReadOnlyDictionaryContainer
        {
            Items = new ReadOnlyDictionary<int, MutableItem>(new Dictionary<int, MutableItem>
            {
                { 1, new MutableItem { Id = 1, Name = "One" } },
                { 2, new MutableItem { Id = 2, Name = "Two" } }
            })
        };

        // Act
        ReadOnlyDictionaryContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);

        // Verify deep clone
        clone.Items[1].Name = "Modified";
        await Assert.That(original.Items[1].Name).IsEqualTo("One");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ObservableCollection_With_Complex_Elements_Should_Deep_Clone()
    {
        // Arrange
        ObservableCollectionContainer original = new ObservableCollectionContainer
        {
            Items =
            [
                new MutableItem { Id = 1, Name = "One" },
                new MutableItem { Id = 2, Name = "Two" }
            ]
        };

        // Act
        ObservableCollectionContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items!.Count).IsEqualTo(2);

        // Verify order preserved
        await Assert.That(clone.Items[0].Id).IsEqualTo(1);
        await Assert.That(clone.Items[1].Id).IsEqualTo(2);

        // Verify deep clone
        clone.Items[0].Name = "Modified";
        await Assert.That(original.Items[0].Name).IsEqualTo("One");
    }
    
    [Test]
    [SourceGeneratorCompatible]
    public async Task HashSet_With_Complex_Elements_Should_Deep_Clone()
    {
        // Arrange
        MutableItem item1 = new MutableItem { Id = 1, Name = "One" };
        MutableItem item2 = new MutableItem { Id = 2, Name = "Two" };
        HashSetContainer original = new HashSetContainer
        {
            Items = [item1, item2]
        };

        // Act
        HashSetContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items!.Count).IsEqualTo(2);

        // Verify deep clone - get an item and modify it
        MutableItem cloneItem = clone.Items.First(x => x.Id == 1);
        cloneItem.Name = "Modified";
        await Assert.That(item1.Name).IsEqualTo("One");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task JaggedArray_With_Complex_Elements_Should_Deep_Clone()
    {
        // Arrange
        JaggedArrayContainer original = new JaggedArrayContainer
        {
            Items =
            [
                [new MutableItem { Id = 1, Name = "1-1" }, new MutableItem { Id = 2, Name = "1-2" }],
                [new MutableItem { Id = 3, Name = "2-1" }]
            ]
        };

        // Act
        JaggedArrayContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items!.Length).IsEqualTo(2);
        await Assert.That(clone.Items[0].Length).IsEqualTo(2);
        await Assert.That(clone.Items[1].Length).IsEqualTo(1);

        // Verify deep clone
        clone.Items[0][0].Name = "Modified";
        await Assert.That(original.Items[0][0].Name).IsEqualTo("1-1");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task MultiDimArray_With_Complex_Elements_Should_Deep_Clone()
    {
        // Arrange
        MultiDimArrayContainer original = new MultiDimArrayContainer
        {
            Items = new MutableItem[2, 3]
        };
        original.Items[0, 0] = new MutableItem { Id = 1, Name = "0-0" };
        original.Items[0, 1] = new MutableItem { Id = 2, Name = "0-1" };
        original.Items[0, 2] = new MutableItem { Id = 3, Name = "0-2" };
        original.Items[1, 0] = new MutableItem { Id = 4, Name = "1-0" };
        original.Items[1, 1] = new MutableItem { Id = 5, Name = "1-1" };
        original.Items[1, 2] = new MutableItem { Id = 6, Name = "1-2" };

        // Act
        MultiDimArrayContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items!.GetLength(0)).IsEqualTo(2);
        await Assert.That(clone.Items.GetLength(1)).IsEqualTo(3);
        await Assert.That(clone.Items[1, 2].Id).IsEqualTo(6);

        // Verify deep clone
        clone.Items[0, 0].Name = "Modified";
        await Assert.That(original.Items[0, 0].Name).IsEqualTo("0-0");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ThreeDimArray_With_Primitives_Should_Clone()
    {
        // Arrange
        ThreeDimArrayContainer original = new ThreeDimArrayContainer
        {
            Numbers = new int[2, 3, 4]
        };
        for (int i = 0; i < 2; i++)
            for (int j = 0; j < 3; j++)
                for (int k = 0; k < 4; k++)
                    original.Numbers[i, j, k] = i * 100 + j * 10 + k;

        // Act
        ThreeDimArrayContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone!.Numbers).IsNotNull();
        await Assert.That(clone.Numbers).IsNotSameReferenceAs(original.Numbers);
        await Assert.That(clone.Numbers![1, 2, 3]).IsEqualTo(123);

        // Verify independence
        clone.Numbers[0, 0, 0] = 999;
        await Assert.That(original.Numbers[0, 0, 0]).IsEqualTo(0);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task NestedCollections_List_Of_Stacks_Should_Deep_Clone()
    {
        // Arrange
        Stack<MutableItem> stack1 = new Stack<MutableItem>();
        stack1.Push(new MutableItem { Id = 1, Name = "S1-1" });
        stack1.Push(new MutableItem { Id = 2, Name = "S1-2" });

        Stack<MutableItem> stack2 = new Stack<MutableItem>();
        stack2.Push(new MutableItem { Id = 3, Name = "S2-1" });

        NestedCollectionContainer original = new NestedCollectionContainer
        {
            StackList = [stack1, stack2]
        };

        // Act
        NestedCollectionContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone!.StackList).IsNotNull();
        await Assert.That(clone.StackList!.Count).IsEqualTo(2);
        await Assert.That(clone.StackList[0].Count).IsEqualTo(2);
        await Assert.That(clone.StackList[1].Count).IsEqualTo(1);

        // Verify LIFO order preserved in stacks
        await Assert.That(clone.StackList[0].Peek().Id).IsEqualTo(2).Because("Top of stack1 should be 2");

        // Verify deep clone
        clone.StackList[0].Peek().Name = "Modified";
        await Assert.That(stack1.Peek().Name).IsEqualTo("S1-2");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Dictionary_With_List_Values_Should_Deep_Clone()
    {
        // Arrange
        DictionaryWithCollectionValuesContainer original = new DictionaryWithCollectionValuesContainer
        {
            Items = new Dictionary<string, List<MutableItem>>
            {
                { "group1", [
                        new MutableItem { Id = 1, Name = "G1-1" },
                        new MutableItem { Id = 2, Name = "G1-2" }
                    ]
                },
                { "group2", [new MutableItem { Id = 3, Name = "G2-1" }]
                }
            }
        };

        // Act
        DictionaryWithCollectionValuesContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items!.Count).IsEqualTo(2);
        await Assert.That(clone.Items["group1"].Count).IsEqualTo(2);
        await Assert.That(clone.Items["group2"].Count).IsEqualTo(1);

        // Verify deep clone of nested items
        clone.Items["group1"][0].Name = "Modified";
        await Assert.That(original.Items["group1"][0].Name).IsEqualTo("G1-1");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Null_Collections_Should_Remain_Null()
    {
        // Arrange
        StackContainer original = new StackContainer { Items = null };

        // Act
        StackContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone!.Items).IsNull();
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Collection_With_Null_Elements_Should_Clone_Correctly()
    {
        // Arrange
        QueueContainer original = new QueueContainer
        {
            Items = new Queue<MutableItem>()
        };
        original.Items.Enqueue(new MutableItem { Id = 1, Name = "One" });
        original.Items.Enqueue(null!);
        original.Items.Enqueue(new MutableItem { Id = 3, Name = "Three" });

        // Act
        QueueContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone!.Items!.Count).IsEqualTo(3);
        MutableItem[] cloneArray = clone.Items.ToArray();
        await Assert.That(cloneArray[0].Id).IsEqualTo(1);
        await Assert.That(cloneArray[1]).IsNull();
        await Assert.That(cloneArray[2].Id).IsEqualTo(3);
    }
    
    [Test]
    [SourceGeneratorCompatible]
    public async Task ICollection_With_Complex_Elements_Should_Deep_Clone()
    {
        ICollectionContainer original = new ICollectionContainer
        {
            Items = new List<MutableItem>
            {
                new MutableItem { Id = 1, Name = "One" },
                new MutableItem { Id = 2, Name = "Two" },
                new MutableItem { Id = 3, Name = "Three" }
            }
        };

        ICollectionContainer clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items!.Count).IsEqualTo(3);

        List<MutableItem> cloneList = clone.Items.ToList();
        await Assert.That(cloneList[0].Id).IsEqualTo(1);
        await Assert.That(cloneList[1].Id).IsEqualTo(2);
        await Assert.That(cloneList[2].Id).IsEqualTo(3);

        cloneList[0].Name = "Modified";
        await Assert.That(original.Items.First().Name).IsEqualTo("One");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ICollection_Null_Should_Remain_Null()
    {
        ICollectionContainer original = new ICollectionContainer { Items = null };
        ICollectionContainer clone = original.FastDeepClone();
        await Assert.That(clone!.Items).IsNull();
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task IEnumerable_With_Complex_Elements_Should_Deep_Clone()
    {
        IEnumerableContainer original = new IEnumerableContainer
        {
            Items = new List<MutableItem>
            {
                new MutableItem { Id = 10, Name = "Alpha" },
                new MutableItem { Id = 20, Name = "Beta" }
            }
        };

        IEnumerableContainer clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);

        List<MutableItem> cloneList = clone.Items!.ToList();
        await Assert.That(cloneList.Count).IsEqualTo(2);
        await Assert.That(cloneList[0].Id).IsEqualTo(10);
        await Assert.That(cloneList[1].Id).IsEqualTo(20);

        cloneList[0].Name = "Modified";
        await Assert.That(original.Items.First().Name).IsEqualTo("Alpha");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task IReadOnlyCollection_With_Complex_Elements_Should_Deep_Clone()
    {
        IReadOnlyCollectionContainer original = new IReadOnlyCollectionContainer
        {
            Items = new List<MutableItem>
            {
                new MutableItem { Id = 100, Name = "X" },
                new MutableItem { Id = 200, Name = "Y" },
                new MutableItem { Id = 300, Name = "Z" }
            }
        };

        IReadOnlyCollectionContainer clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items!.Count).IsEqualTo(3);

        List<MutableItem> cloneList = clone.Items.ToList();
        await Assert.That(cloneList[0].Id).IsEqualTo(100);
        await Assert.That(cloneList[2].Id).IsEqualTo(300);

        cloneList[0].Name = "Modified";
        await Assert.That(original.Items.First().Name).IsEqualTo("X");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ConcreteClass_Only_IEnumerable_Should_Deep_Clone()
    {
        ConcreteEnumerableOnlyContainer original = new ConcreteEnumerableOnlyContainer
        {
            Items =
            [
                new MutableItem { Id = 1, Name = "One" },
                new MutableItem { Id = 2, Name = "Two" }
            ]
        };

        ConcreteEnumerableOnlyContainer clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);

        List<MutableItem> cloneList = clone.Items!.ToList();
        await Assert.That(cloneList.Count).IsEqualTo(2);
        await Assert.That(cloneList[0].Id).IsEqualTo(1);
        await Assert.That(cloneList[1].Id).IsEqualTo(2);

        cloneList[0].Name = "Modified";
        await Assert.That(original.Items.First().Name).IsEqualTo("One");
    }
}