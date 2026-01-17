using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using FastCloner.SourceGenerator.Shared;
using NUnit.Framework;

namespace FastCloner.Tests;

#region Test Model Classes

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

#endregion

[TestFixture]
public class SourceGeneratorCollectionTests
{
    #region Stack Tests

    [Test]
    [SourceGeneratorCompatible]
    public void Stack_With_Complex_Elements_Should_Preserve_LIFO_Order()
    {
        // Arrange
        var original = new StackContainer
        {
            Items = new Stack<MutableItem>()
        };
        original.Items.Push(new MutableItem { Id = 1, Name = "First" });
        original.Items.Push(new MutableItem { Id = 2, Name = "Second" });
        original.Items.Push(new MutableItem { Id = 3, Name = "Third" });

        // Act
        var clone = original.FastDeepClone();

        // Assert - verify LIFO order (3, 2, 1)
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Items, Is.Not.Null);
        Assert.That(clone.Items, Is.Not.SameAs(original.Items));
        Assert.That(clone.Items!.Count, Is.EqualTo(3));

        var cloneItems = clone.Items.ToArray();
        Assert.That(cloneItems[0].Id, Is.EqualTo(3), "First pop should be 3 (LIFO)");
        Assert.That(cloneItems[1].Id, Is.EqualTo(2), "Second pop should be 2 (LIFO)");
        Assert.That(cloneItems[2].Id, Is.EqualTo(1), "Third pop should be 1 (LIFO)");

        // Verify deep clone - modifying clone shouldn't affect original
        cloneItems[0].Name = "Modified";
        Assert.That(original.Items.Peek().Name, Is.EqualTo("Third"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Stack_Empty_Should_Clone_Correctly()
    {
        var original = new StackContainer { Items = new Stack<MutableItem>() };
        var clone = original.FastDeepClone();
        
        Assert.That(clone!.Items, Is.Not.Null);
        Assert.That(clone.Items!.Count, Is.EqualTo(0));
    }

    #endregion

    #region Queue Tests

    [Test]
    [SourceGeneratorCompatible]
    public void Queue_With_Complex_Elements_Should_Preserve_FIFO_Order()
    {
        // Arrange
        var original = new QueueContainer
        {
            Items = new Queue<MutableItem>()
        };
        original.Items.Enqueue(new MutableItem { Id = 1, Name = "First" });
        original.Items.Enqueue(new MutableItem { Id = 2, Name = "Second" });
        original.Items.Enqueue(new MutableItem { Id = 3, Name = "Third" });

        // Act
        var clone = original.FastDeepClone();

        // Assert - verify FIFO order (1, 2, 3)
        Assert.That(clone!.Items, Is.Not.Null);
        Assert.That(clone.Items!.Count, Is.EqualTo(3));

        var cloneItems = clone.Items.ToArray();
        Assert.That(cloneItems[0].Id, Is.EqualTo(1), "First dequeue should be 1 (FIFO)");
        Assert.That(cloneItems[1].Id, Is.EqualTo(2), "Second dequeue should be 2 (FIFO)");
        Assert.That(cloneItems[2].Id, Is.EqualTo(3), "Third dequeue should be 3 (FIFO)");

        // Verify deep clone
        cloneItems[0].Name = "Modified";
        Assert.That(original.Items.Peek().Name, Is.EqualTo("First"));
    }

    #endregion

    #region LinkedList Tests

    [Test]
    [SourceGeneratorCompatible]
    public void LinkedList_With_Complex_Elements_Should_Preserve_Order()
    {
        // Arrange
        var original = new LinkedListContainer
        {
            Items = new LinkedList<MutableItem>()
        };
        original.Items.AddLast(new MutableItem { Id = 1, Name = "First" });
        original.Items.AddLast(new MutableItem { Id = 2, Name = "Second" });
        original.Items.AddLast(new MutableItem { Id = 3, Name = "Third" });

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone!.Items, Is.Not.Null);
        Assert.That(clone.Items!.Count, Is.EqualTo(3));
        Assert.That(clone.Items.First!.Value.Id, Is.EqualTo(1));
        Assert.That(clone.Items.Last!.Value.Id, Is.EqualTo(3));

        // Verify deep clone
        clone.Items.First.Value.Name = "Modified";
        Assert.That(original.Items.First!.Value.Name, Is.EqualTo("First"));
    }

    #endregion

    #region Sorted Collection Tests

    [Test]
    [SourceGeneratorCompatible]
    public void SortedSet_Should_Maintain_Sorted_Order()
    {
        // Arrange - insert out of order
        var original = new SortedSetContainer
        {
            Numbers = new SortedSet<int> { 3, 1, 4, 1, 5, 9, 2, 6 }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert - should be sorted: 1, 2, 3, 4, 5, 6, 9
        Assert.That(clone!.Numbers, Is.Not.Null);
        Assert.That(clone.Numbers!.ToArray(), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 9 }));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void SortedDictionary_Should_Maintain_Key_Order_And_Deep_Clone_Values()
    {
        // Arrange
        var original = new SortedDictionaryContainer
        {
            Items = new SortedDictionary<int, MutableItem>
            {
                { 3, new MutableItem { Id = 3, Name = "Three" } },
                { 1, new MutableItem { Id = 1, Name = "One" } },
                { 2, new MutableItem { Id = 2, Name = "Two" } }
            }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert - keys should be in order: 1, 2, 3
        Assert.That(clone!.Items, Is.Not.Null);
        var keys = clone.Items!.Keys.ToArray();
        Assert.That(keys, Is.EqualTo(new[] { 1, 2, 3 }));

        // Verify deep clone
        clone.Items[1].Name = "Modified";
        Assert.That(original.Items[1].Name, Is.EqualTo("One"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void SortedList_Should_Maintain_Key_Order_And_Deep_Clone_Values()
    {
        // Arrange
        var original = new SortedListContainer
        {
            Items = new SortedList<string, MutableItem>
            {
                { "charlie", new MutableItem { Id = 3, Name = "Charlie" } },
                { "alpha", new MutableItem { Id = 1, Name = "Alpha" } },
                { "bravo", new MutableItem { Id = 2, Name = "Bravo" } }
            }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert - keys should be in order: alpha, bravo, charlie
        Assert.That(clone!.Items, Is.Not.Null);
        var keys = clone.Items!.Keys.ToArray();
        Assert.That(keys, Is.EqualTo(new[] { "alpha", "bravo", "charlie" }));

        // Verify deep clone
        clone.Items["alpha"].Name = "Modified";
        Assert.That(original.Items["alpha"].Name, Is.EqualTo("Alpha"));
    }

    #endregion

    #region Concurrent Collection Tests

    [Test]
    [SourceGeneratorCompatible]
    public void ConcurrentStack_With_Complex_Elements_Should_Preserve_Order()
    {
        // Arrange
        var original = new ConcurrentStackContainer
        {
            Items = new ConcurrentStack<MutableItem>()
        };
        original.Items.Push(new MutableItem { Id = 1, Name = "First" });
        original.Items.Push(new MutableItem { Id = 2, Name = "Second" });
        original.Items.Push(new MutableItem { Id = 3, Name = "Third" });

        // Act
        var clone = original.FastDeepClone();

        // Assert - LIFO order
        Assert.That(clone!.Items, Is.Not.Null);
        var cloneItems = clone.Items!.ToArray();
        Assert.That(cloneItems[0].Id, Is.EqualTo(3));
        Assert.That(cloneItems[1].Id, Is.EqualTo(2));
        Assert.That(cloneItems[2].Id, Is.EqualTo(1));

        // Verify deep clone
        cloneItems[0].Name = "Modified";
        original.Items.TryPeek(out var originalTop);
        Assert.That(originalTop!.Name, Is.EqualTo("Third"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void ConcurrentQueue_With_Complex_Elements_Should_Preserve_Order()
    {
        // Arrange
        var original = new ConcurrentQueueContainer
        {
            Items = new ConcurrentQueue<MutableItem>()
        };
        original.Items.Enqueue(new MutableItem { Id = 1, Name = "First" });
        original.Items.Enqueue(new MutableItem { Id = 2, Name = "Second" });
        original.Items.Enqueue(new MutableItem { Id = 3, Name = "Third" });

        // Act
        var clone = original.FastDeepClone();

        // Assert - FIFO order
        Assert.That(clone!.Items, Is.Not.Null);
        var cloneItems = clone.Items!.ToArray();
        Assert.That(cloneItems[0].Id, Is.EqualTo(1));
        Assert.That(cloneItems[1].Id, Is.EqualTo(2));
        Assert.That(cloneItems[2].Id, Is.EqualTo(3));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void ConcurrentDictionary_With_Complex_Values_Should_Deep_Clone()
    {
        // Arrange
        var original = new ConcurrentDictionaryContainer
        {
            Items = new ConcurrentDictionary<int, MutableItem>()
        };
        original.Items[1] = new MutableItem { Id = 1, Name = "One" };
        original.Items[2] = new MutableItem { Id = 2, Name = "Two" };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone!.Items, Is.Not.Null);
        Assert.That(clone.Items!.Count, Is.EqualTo(2));
        Assert.That(clone.Items[1].Name, Is.EqualTo("One"));

        // Verify deep clone
        clone.Items[1].Name = "Modified";
        Assert.That(original.Items[1].Name, Is.EqualTo("One"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void ConcurrentBag_With_Complex_Elements_Should_Clone()
    {
        // Arrange
        var original = new ConcurrentBagContainer
        {
            Items = new ConcurrentBag<MutableItem>
            {
                new MutableItem { Id = 1, Name = "One" },
                new MutableItem { Id = 2, Name = "Two" },
                new MutableItem { Id = 3, Name = "Three" }
            }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert - ConcurrentBag doesn't guarantee order, just check contents
        Assert.That(clone!.Items, Is.Not.Null);
        Assert.That(clone.Items!.Count, Is.EqualTo(3));
        
        var cloneIds = clone.Items.Select(x => x.Id).OrderBy(x => x).ToArray();
        Assert.That(cloneIds, Is.EqualTo(new[] { 1, 2, 3 }));

        // Verify deep clone
        var firstCloneItem = clone.Items.First();
        var originalId = firstCloneItem.Id;
        firstCloneItem.Name = "Modified";
        
        var originalItem = original.Items.First(x => x.Id == originalId);
        Assert.That(originalItem.Name, Is.Not.EqualTo("Modified"));
    }

    #endregion

    #region Immutable Collection Tests

    [Test]
    [SourceGeneratorCompatible]
    public void ImmutableList_With_Complex_Elements_Should_Deep_Clone()
    {
        // Arrange
        var original = new ImmutableListContainer
        {
            Items = ImmutableList.Create(
                new MutableItem { Id = 1, Name = "One" },
                new MutableItem { Id = 2, Name = "Two" }
            )
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone!.Items, Is.Not.Null);
        Assert.That(clone.Items!.Count, Is.EqualTo(2));
        Assert.That(clone.Items[0].Id, Is.EqualTo(1));

        // Verify deep clone
        clone.Items[0].Name = "Modified";
        Assert.That(original.Items[0].Name, Is.EqualTo("One"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void ImmutableArray_With_Complex_Elements_Should_Deep_Clone()
    {
        // Arrange
        var original = new ImmutableArrayContainer
        {
            Items = ImmutableArray.Create(
                new MutableItem { Id = 1, Name = "One" },
                new MutableItem { Id = 2, Name = "Two" }
            )
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone!.Items.Length, Is.EqualTo(2));
        Assert.That(clone.Items[0].Id, Is.EqualTo(1));

        // Verify deep clone
        clone.Items[0].Name = "Modified";
        Assert.That(original.Items[0].Name, Is.EqualTo("One"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void ImmutableStack_With_Complex_Elements_Should_Preserve_Order()
    {
        // Arrange - Push creates stack in reverse order (last push = top)
        var original = new ImmutableStackContainer
        {
            Items = ImmutableStack<MutableItem>.Empty
                .Push(new MutableItem { Id = 1, Name = "First" })
                .Push(new MutableItem { Id = 2, Name = "Second" })
                .Push(new MutableItem { Id = 3, Name = "Third" })
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert - LIFO: 3 should be on top
        Assert.That(clone!.Items, Is.Not.Null);
        var cloneArray = clone.Items!.ToArray();
        Assert.That(cloneArray[0].Id, Is.EqualTo(3), "Top should be 3");
        Assert.That(cloneArray[1].Id, Is.EqualTo(2));
        Assert.That(cloneArray[2].Id, Is.EqualTo(1));

        // Verify deep clone
        cloneArray[0].Name = "Modified";
        Assert.That(original.Items.Peek().Name, Is.EqualTo("Third"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void ImmutableQueue_With_Complex_Elements_Should_Preserve_Order()
    {
        // Arrange
        var original = new ImmutableQueueContainer
        {
            Items = ImmutableQueue<MutableItem>.Empty
                .Enqueue(new MutableItem { Id = 1, Name = "First" })
                .Enqueue(new MutableItem { Id = 2, Name = "Second" })
                .Enqueue(new MutableItem { Id = 3, Name = "Third" })
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert - FIFO order
        Assert.That(clone!.Items, Is.Not.Null);
        var cloneArray = clone.Items!.ToArray();
        Assert.That(cloneArray[0].Id, Is.EqualTo(1), "First should be 1 (FIFO)");
        Assert.That(cloneArray[1].Id, Is.EqualTo(2));
        Assert.That(cloneArray[2].Id, Is.EqualTo(3));

        // Verify deep clone
        cloneArray[0].Name = "Modified";
        Assert.That(original.Items.Peek().Name, Is.EqualTo("First"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void ImmutableHashSet_With_Primitives_Returns_Same_Reference()
    {
        // Arrange
        var original = new ImmutableHashSetPrimitiveContainer
        {
            Numbers = ImmutableHashSet.Create(1, 2, 3, 4, 5)
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert - optimization: should return same reference for immutable with immutable elements
        Assert.That(clone!.Numbers, Is.Not.Null);
        Assert.That(clone.Numbers, Is.SameAs(original.Numbers), 
            "Immutable collection with immutable elements should return same reference");
    }

    [Test]
    [SourceGeneratorCompatible]
    public void ImmutableDictionary_With_Primitives_Returns_Same_Reference()
    {
        // Arrange
        var original = new ImmutableDictionaryPrimitiveContainer
        {
            Items = ImmutableDictionary.Create<int, string>()
                .Add(1, "One")
                .Add(2, "Two")
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert - optimization: should return same reference
        Assert.That(clone!.Items, Is.Not.Null);
        Assert.That(clone.Items, Is.SameAs(original.Items),
            "Immutable dictionary with immutable keys and values should return same reference");
    }

    [Test]
    [SourceGeneratorCompatible]
    public void ImmutableDictionary_With_Complex_Values_Should_Deep_Clone()
    {
        // Arrange
        var original = new ImmutableDictionaryComplexContainer
        {
            Items = ImmutableDictionary.Create<int, MutableItem>()
                .Add(1, new MutableItem { Id = 1, Name = "One" })
                .Add(2, new MutableItem { Id = 2, Name = "Two" })
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone!.Items, Is.Not.Null);
        Assert.That(clone.Items!.Count, Is.EqualTo(2));

        // Verify deep clone - values should be different instances
        clone.Items[1].Name = "Modified";
        Assert.That(original.Items[1].Name, Is.EqualTo("One"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void ImmutableSortedSet_Should_Maintain_Order()
    {
        // Arrange
        var original = new ImmutableSortedSetContainer
        {
            Numbers = ImmutableSortedSet.Create(5, 3, 1, 4, 2)
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone!.Numbers, Is.Not.Null);
        Assert.That(clone.Numbers!.ToArray(), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
        
        // Should be same reference since primitives
        Assert.That(clone.Numbers, Is.SameAs(original.Numbers));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void ImmutableSortedDictionary_Should_Maintain_Order_And_Deep_Clone()
    {
        // Arrange
        var original = new ImmutableSortedDictionaryContainer
        {
            Items = ImmutableSortedDictionary.Create<int, MutableItem>()
                .Add(3, new MutableItem { Id = 3, Name = "Three" })
                .Add(1, new MutableItem { Id = 1, Name = "One" })
                .Add(2, new MutableItem { Id = 2, Name = "Two" })
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone!.Items, Is.Not.Null);
        var keys = clone.Items!.Keys.ToArray();
        Assert.That(keys, Is.EqualTo(new[] { 1, 2, 3 }));

        // Verify deep clone
        clone.Items[1].Name = "Modified";
        Assert.That(original.Items[1].Name, Is.EqualTo("One"));
    }

    #endregion

    #region ReadOnly Collection Tests

    [Test]
    [SourceGeneratorCompatible]
    public void ReadOnlyCollection_With_Complex_Elements_Should_Deep_Clone()
    {
        // Arrange
        var original = new ReadOnlyCollectionContainer
        {
            Items = new ReadOnlyCollection<MutableItem>(new List<MutableItem>
            {
                new MutableItem { Id = 1, Name = "One" },
                new MutableItem { Id = 2, Name = "Two" }
            })
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone!.Items, Is.Not.Null);
        Assert.That(clone.Items, Is.Not.SameAs(original.Items));
        Assert.That(clone.Items!.Count, Is.EqualTo(2));

        // Verify deep clone
        clone.Items[0].Name = "Modified";
        Assert.That(original.Items[0].Name, Is.EqualTo("One"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void ReadOnlyDictionary_With_Complex_Values_Should_Deep_Clone()
    {
        // Arrange
        var original = new ReadOnlyDictionaryContainer
        {
            Items = new ReadOnlyDictionary<int, MutableItem>(new Dictionary<int, MutableItem>
            {
                { 1, new MutableItem { Id = 1, Name = "One" } },
                { 2, new MutableItem { Id = 2, Name = "Two" } }
            })
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone!.Items, Is.Not.Null);
        Assert.That(clone.Items, Is.Not.SameAs(original.Items));

        // Verify deep clone
        clone.Items[1].Name = "Modified";
        Assert.That(original.Items[1].Name, Is.EqualTo("One"));
    }

    #endregion

    #region Observable Collection Tests

    [Test]
    [SourceGeneratorCompatible]
    public void ObservableCollection_With_Complex_Elements_Should_Deep_Clone()
    {
        // Arrange
        var original = new ObservableCollectionContainer
        {
            Items = new ObservableCollection<MutableItem>
            {
                new MutableItem { Id = 1, Name = "One" },
                new MutableItem { Id = 2, Name = "Two" }
            }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone!.Items, Is.Not.Null);
        Assert.That(clone.Items, Is.Not.SameAs(original.Items));
        Assert.That(clone.Items!.Count, Is.EqualTo(2));

        // Verify order preserved
        Assert.That(clone.Items[0].Id, Is.EqualTo(1));
        Assert.That(clone.Items[1].Id, Is.EqualTo(2));

        // Verify deep clone
        clone.Items[0].Name = "Modified";
        Assert.That(original.Items[0].Name, Is.EqualTo("One"));
    }

    #endregion

    #region HashSet Tests

    [Test]
    [SourceGeneratorCompatible]
    public void HashSet_With_Complex_Elements_Should_Deep_Clone()
    {
        // Arrange
        var item1 = new MutableItem { Id = 1, Name = "One" };
        var item2 = new MutableItem { Id = 2, Name = "Two" };
        var original = new HashSetContainer
        {
            Items = new HashSet<MutableItem> { item1, item2 }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone!.Items, Is.Not.Null);
        Assert.That(clone.Items!.Count, Is.EqualTo(2));

        // Verify deep clone - get an item and modify it
        var cloneItem = clone.Items.First(x => x.Id == 1);
        cloneItem.Name = "Modified";
        Assert.That(item1.Name, Is.EqualTo("One"));
    }

    #endregion

    #region Array Tests

    [Test]
    [SourceGeneratorCompatible]
    public void JaggedArray_With_Complex_Elements_Should_Deep_Clone()
    {
        // Arrange
        var original = new JaggedArrayContainer
        {
            Items = new[]
            {
                new[] { new MutableItem { Id = 1, Name = "1-1" }, new MutableItem { Id = 2, Name = "1-2" } },
                new[] { new MutableItem { Id = 3, Name = "2-1" } }
            }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone!.Items, Is.Not.Null);
        Assert.That(clone.Items, Is.Not.SameAs(original.Items));
        Assert.That(clone.Items!.Length, Is.EqualTo(2));
        Assert.That(clone.Items[0].Length, Is.EqualTo(2));
        Assert.That(clone.Items[1].Length, Is.EqualTo(1));

        // Verify deep clone
        clone.Items[0][0].Name = "Modified";
        Assert.That(original.Items[0][0].Name, Is.EqualTo("1-1"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void MultiDimArray_With_Complex_Elements_Should_Deep_Clone()
    {
        // Arrange
        var original = new MultiDimArrayContainer
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
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone!.Items, Is.Not.Null);
        Assert.That(clone.Items, Is.Not.SameAs(original.Items));
        Assert.That(clone.Items!.GetLength(0), Is.EqualTo(2));
        Assert.That(clone.Items.GetLength(1), Is.EqualTo(3));
        Assert.That(clone.Items[1, 2].Id, Is.EqualTo(6));

        // Verify deep clone
        clone.Items[0, 0].Name = "Modified";
        Assert.That(original.Items[0, 0].Name, Is.EqualTo("0-0"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void ThreeDimArray_With_Primitives_Should_Clone()
    {
        // Arrange
        var original = new ThreeDimArrayContainer
        {
            Numbers = new int[2, 3, 4]
        };
        for (int i = 0; i < 2; i++)
            for (int j = 0; j < 3; j++)
                for (int k = 0; k < 4; k++)
                    original.Numbers[i, j, k] = i * 100 + j * 10 + k;

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone!.Numbers, Is.Not.Null);
        Assert.That(clone.Numbers, Is.Not.SameAs(original.Numbers));
        Assert.That(clone.Numbers![1, 2, 3], Is.EqualTo(123));

        // Verify independence
        clone.Numbers[0, 0, 0] = 999;
        Assert.That(original.Numbers[0, 0, 0], Is.EqualTo(0));
    }

    #endregion

    #region Nested Collection Tests

    [Test]
    [SourceGeneratorCompatible]
    public void NestedCollections_List_Of_Stacks_Should_Deep_Clone()
    {
        // Arrange
        var stack1 = new Stack<MutableItem>();
        stack1.Push(new MutableItem { Id = 1, Name = "S1-1" });
        stack1.Push(new MutableItem { Id = 2, Name = "S1-2" });

        var stack2 = new Stack<MutableItem>();
        stack2.Push(new MutableItem { Id = 3, Name = "S2-1" });

        var original = new NestedCollectionContainer
        {
            StackList = new List<Stack<MutableItem>> { stack1, stack2 }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone!.StackList, Is.Not.Null);
        Assert.That(clone.StackList!.Count, Is.EqualTo(2));
        Assert.That(clone.StackList[0].Count, Is.EqualTo(2));
        Assert.That(clone.StackList[1].Count, Is.EqualTo(1));

        // Verify LIFO order preserved in stacks
        Assert.That(clone.StackList[0].Peek().Id, Is.EqualTo(2), "Top of stack1 should be 2");

        // Verify deep clone
        clone.StackList[0].Peek().Name = "Modified";
        Assert.That(stack1.Peek().Name, Is.EqualTo("S1-2"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Dictionary_With_List_Values_Should_Deep_Clone()
    {
        // Arrange
        var original = new DictionaryWithCollectionValuesContainer
        {
            Items = new Dictionary<string, List<MutableItem>>
            {
                { "group1", new List<MutableItem> 
                    { 
                        new MutableItem { Id = 1, Name = "G1-1" },
                        new MutableItem { Id = 2, Name = "G1-2" }
                    } 
                },
                { "group2", new List<MutableItem>
                    {
                        new MutableItem { Id = 3, Name = "G2-1" }
                    }
                }
            }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone!.Items, Is.Not.Null);
        Assert.That(clone.Items!.Count, Is.EqualTo(2));
        Assert.That(clone.Items["group1"].Count, Is.EqualTo(2));
        Assert.That(clone.Items["group2"].Count, Is.EqualTo(1));

        // Verify deep clone of nested items
        clone.Items["group1"][0].Name = "Modified";
        Assert.That(original.Items["group1"][0].Name, Is.EqualTo("G1-1"));
    }

    #endregion

    #region Null Handling Tests

    [Test]
    [SourceGeneratorCompatible]
    public void Null_Collections_Should_Remain_Null()
    {
        // Arrange
        var original = new StackContainer { Items = null };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone!.Items, Is.Null);
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Collection_With_Null_Elements_Should_Clone_Correctly()
    {
        // Arrange
        var original = new QueueContainer
        {
            Items = new Queue<MutableItem>()
        };
        original.Items.Enqueue(new MutableItem { Id = 1, Name = "One" });
        original.Items.Enqueue(null!);
        original.Items.Enqueue(new MutableItem { Id = 3, Name = "Three" });

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone!.Items!.Count, Is.EqualTo(3));
        var cloneArray = clone.Items.ToArray();
        Assert.That(cloneArray[0].Id, Is.EqualTo(1));
        Assert.That(cloneArray[1], Is.Null);
        Assert.That(cloneArray[2].Id, Is.EqualTo(3));
    }

    #endregion
}
