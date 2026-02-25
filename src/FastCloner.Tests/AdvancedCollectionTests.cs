using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using FastCloner.SourceGenerator.Shared;
using NUnit.Framework;

namespace FastCloner.Tests;

[FastClonerClonable]
public class AdvancedCollections
{
    public ObservableCollection<int> Observable { get; set; }
    public ReadOnlyCollection<int> ReadOnly { get; set; }
    public ImmutableList<int> ImmutableList { get; set; }
    public ImmutableArray<int> ImmutableArray { get; set; }
    public ImmutableQueue<int> ImmutableQueue { get; set; }
    public ImmutableStack<int> ImmutableStack { get; set; }
    public ImmutableDictionary<int, int> ImmutableDict { get; set; }
    public ReadOnlyDictionary<int, int> ReadOnlyDict { get; set; }
}

[TestFixture]
public class AdvancedCollectionTests
{
    [Test]
    public void TestAdvancedCollections()
    {
        AdvancedCollections original = new AdvancedCollections
        {
            Observable = new ObservableCollection<int> { 1, 2, 3 },
            ReadOnly = new ReadOnlyCollection<int>(new List<int> { 4, 5, 6 }),
            ImmutableList = ImmutableList.Create(7, 8, 9),
            ImmutableArray = ImmutableArray.Create(10, 11, 12),
            ImmutableQueue = ImmutableQueue.Create(13, 14, 15),
            ImmutableStack = ImmutableStack.Create(16, 17, 18),
            ImmutableDict = ImmutableDictionary.Create<int, int>().Add(1, 100),
            ReadOnlyDict = new ReadOnlyDictionary<int, int>(new Dictionary<int, int> { { 2, 200 } })
        };

        AdvancedCollections clone = original.FastDeepClone();

        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.Not.SameAs(original));

        // Verify Observable
        Assert.That(clone.Observable, Is.Not.Null);
        Assert.That(clone.Observable, Is.Not.SameAs(original.Observable));
        Assert.That(clone.Observable, Is.EqualTo(original.Observable));

        // Verify ReadOnly
        Assert.That(clone.ReadOnly, Is.Not.Null);
        Assert.That(clone.ReadOnly, Is.Not.SameAs(original.ReadOnly));
        Assert.That(clone.ReadOnly, Is.EqualTo(original.ReadOnly));

        // Verify ImmutableList
        Assert.That(clone.ImmutableList, Is.Not.Null);
        // Optimization: Immutable collections with immutable elements return the same reference
        // since there's nothing mutable to clone
        Assert.That(clone.ImmutableList, Is.SameAs(original.ImmutableList));
        Assert.That(clone.ImmutableList, Is.EqualTo(original.ImmutableList));

        // Verify ImmutableArray (Struct)
        Assert.That(clone.ImmutableArray.IsDefault, Is.False);
        Assert.That(clone.ImmutableArray, Is.EqualTo(original.ImmutableArray));

        // Verify ImmutableQueue
        Assert.That(clone.ImmutableQueue, Is.Not.Null);
        // Optimization: Immutable collections with immutable elements return same reference
        Assert.That(clone.ImmutableQueue, Is.SameAs(original.ImmutableQueue));
        Assert.That(clone.ImmutableQueue.ToArray(), Is.EqualTo(original.ImmutableQueue.ToArray()));

        // Verify ImmutableStack
        Assert.That(clone.ImmutableStack, Is.Not.Null);
        // Optimization: Immutable collections with immutable elements return same reference
        Assert.That(clone.ImmutableStack, Is.SameAs(original.ImmutableStack));
        Assert.That(clone.ImmutableStack.ToArray(), Is.EqualTo(original.ImmutableStack.ToArray()));

        // Verify ImmutableDict
        Assert.That(clone.ImmutableDict, Is.Not.Null);
        // Optimization: Immutable dictionary with immutable keys and values returns same reference
        Assert.That(clone.ImmutableDict, Is.SameAs(original.ImmutableDict));
        Assert.That(clone.ImmutableDict[1], Is.EqualTo(100));

        // Verify ReadOnlyDict
        Assert.That(clone.ReadOnlyDict, Is.Not.Null);
        Assert.That(clone.ReadOnlyDict, Is.Not.SameAs(original.ReadOnlyDict));
        Assert.That(clone.ReadOnlyDict[2], Is.EqualTo(200));
    }
}
