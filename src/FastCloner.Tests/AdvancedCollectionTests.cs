using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using FastCloner.SourceGenerator.Shared;
using System.Threading.Tasks;

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
public class AdvancedCollectionTests
{
    [Test]
    public async Task TestAdvancedCollections()
    {
        AdvancedCollections original = new AdvancedCollections
        {
            Observable = [1, 2, 3],
            ReadOnly = new ReadOnlyCollection<int>(new List<int> { 4, 5, 6 }),
            ImmutableList = ImmutableList.Create(7, 8, 9),
            ImmutableArray = [10, 11, 12],
            ImmutableQueue = ImmutableQueue.Create(13, 14, 15),
            ImmutableStack = ImmutableStack.Create(16, 17, 18),
            ImmutableDict = ImmutableDictionary.Create<int, int>().Add(1, 100),
            ReadOnlyDict = new ReadOnlyDictionary<int, int>(new Dictionary<int, int> { { 2, 200 } })
        };

        AdvancedCollections clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone).IsNotSameReferenceAs(original);

        // Verify Observable
        await Assert.That(clone.Observable).IsNotNull();
        await Assert.That(clone.Observable).IsNotSameReferenceAs(original.Observable);
        await Assert.That(clone.Observable).IsEquivalentTo(original.Observable);

        // Verify ReadOnly
        await Assert.That(clone.ReadOnly).IsNotNull();
        await Assert.That(clone.ReadOnly).IsNotSameReferenceAs(original.ReadOnly);
        await Assert.That(clone.ReadOnly).IsEquivalentTo(original.ReadOnly);

        // Verify ImmutableList
        await Assert.That(clone.ImmutableList).IsNotNull();
        // Optimization: Immutable collections with immutable elements return the same reference
        // since there's nothing mutable to clone
        await Assert.That(clone.ImmutableList).IsSameReferenceAs(original.ImmutableList);
        await Assert.That(clone.ImmutableList).IsEquivalentTo(original.ImmutableList);

        // Verify ImmutableArray (Struct)
        await Assert.That(clone.ImmutableArray.IsDefault).IsFalse();
        await Assert.That(clone.ImmutableArray).IsEquivalentTo(original.ImmutableArray);

        // Verify ImmutableQueue
        await Assert.That(clone.ImmutableQueue).IsNotNull();
        // Optimization: Immutable collections with immutable elements return same reference
        await Assert.That(clone.ImmutableQueue).IsSameReferenceAs(original.ImmutableQueue);
        await Assert.That(clone.ImmutableQueue.ToArray()).IsEquivalentTo(original.ImmutableQueue.ToArray());

        // Verify ImmutableStack
        await Assert.That(clone.ImmutableStack).IsNotNull();
        // Optimization: Immutable collections with immutable elements return same reference
        await Assert.That(clone.ImmutableStack).IsSameReferenceAs(original.ImmutableStack);
        await Assert.That(clone.ImmutableStack.ToArray()).IsEquivalentTo(original.ImmutableStack.ToArray());

        // Verify ImmutableDict
        await Assert.That(clone.ImmutableDict).IsNotNull();
        // Optimization: Immutable dictionary with immutable keys and values returns same reference
        await Assert.That(clone.ImmutableDict).IsSameReferenceAs(original.ImmutableDict);
        await Assert.That(clone.ImmutableDict[1]).IsEqualTo(100);

        // Verify ReadOnlyDict
        await Assert.That(clone.ReadOnlyDict).IsNotNull();
        await Assert.That(clone.ReadOnlyDict).IsNotSameReferenceAs(original.ReadOnlyDict);
        await Assert.That(clone.ReadOnlyDict[2]).IsEqualTo(200);
    }
}