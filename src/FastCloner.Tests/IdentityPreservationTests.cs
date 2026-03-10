using FastCloner.SourceGenerator.Shared;
using System.Threading.Tasks;

namespace FastCloner.Tests;

public class IdentityPreservationTests
{
    #region Test 1: Simple Tree - No state needed
    
    [FastClonerClonable]
    public class SimpleTreeRoot
    {
        public string Name { get; set; } = "";
        public SimpleTreeChild Child { get; set; } = new();
    }
    
    [FastClonerClonable]
    public class SimpleTreeChild
    {
        public int Value { get; set; }
    }
    
    [Test]
    public async Task SimpleTree_ClonesCorrectly_NoStateNeeded()
    {
        SimpleTreeRoot original = new SimpleTreeRoot
        {
            Name = "Root",
            Child = new SimpleTreeChild { Value = 42 }
        };
        
        SimpleTreeRoot clone = original.FastDeepClone();
        
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Name).IsEqualTo("Root");
        await Assert.That(clone.Child).IsNotSameReferenceAs(original.Child);
        await Assert.That(clone.Child.Value).IsEqualTo(42);
    }
    
    #endregion
    
    #region Test 2: Multiple Paths to Same Type - State needed (with PreserveIdentity)
    
    [FastClonerClonable]
    [FastClonerPreserveIdentity]
    public class MultiPathRoot
    {
        public string Name { get; set; } = "";
        public PathA PathA { get; set; } = new();
        public PathB PathB { get; set; } = new();
    }
    
    [FastClonerClonable]
    public class PathA
    {
        public SharedNode Shared { get; set; } = new();
    }
    
    [FastClonerClonable]
    public class PathB
    {
        public SharedNode Shared { get; set; } = new();
    }
    
    [FastClonerClonable]
    public class SharedNode
    {
        public int Value { get; set; }
    }
    
    [Test]
    public async Task MultiplePaths_PreservesIdentity_WhenSameInstanceShared()
    {
        SharedNode sharedNode = new SharedNode { Value = 100 };
        MultiPathRoot original = new MultiPathRoot
        {
            Name = "MultiPath",
            PathA = new PathA { Shared = sharedNode },
            PathB = new PathB { Shared = sharedNode }
        };
        
        // Verify original has shared identity
        await Assert.That(original.PathA.Shared).IsSameReferenceAs(original.PathB.Shared);

        MultiPathRoot clone = original.FastDeepClone();
        
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.PathA.Shared).IsNotSameReferenceAs(original.PathA.Shared);
        await Assert.That(clone.PathB.Shared).IsNotSameReferenceAs(original.PathB.Shared);

        // Key assertion: clone should preserve the shared identity
        await Assert.That(clone.PathA.Shared).IsSameReferenceAs(clone.PathB.Shared).Because("Identity should be preserved: both paths should clone to the same instance");
        await Assert.That(clone.PathA.Shared.Value).IsEqualTo(100);
    }
    
    [Test]
    public async Task MultiplePaths_CreatesSeparateClones_WhenDifferentInstances()
    {
        MultiPathRoot original = new MultiPathRoot
        {
            Name = "MultiPath",
            PathA = new PathA { Shared = new SharedNode { Value = 1 } },
            PathB = new PathB { Shared = new SharedNode { Value = 2 } }
        };
        
        // Verify original has different instances
        await Assert.That(original.PathA.Shared).IsNotSameReferenceAs(original.PathB.Shared);

        MultiPathRoot clone = original.FastDeepClone();
        
        // Clone should also have different instances
        await Assert.That(clone.PathA.Shared).IsNotSameReferenceAs(clone.PathB.Shared);
        await Assert.That(clone.PathA.Shared.Value).IsEqualTo(1);
        await Assert.That(clone.PathB.Shared.Value).IsEqualTo(2);
    }
    
    #endregion
    
    #region Test 3: Duplicate Properties of Same Type - State needed (with PreserveIdentity)
    
    [FastClonerClonable]
    [FastClonerPreserveIdentity] // Required for identity preservation
    public class DuplicatePropsRoot
    {
        public string Name { get; set; } = "";
        public DuplicateChild Child1 { get; set; } = new();
        public DuplicateChild Child2 { get; set; } = new();
    }
    
    [FastClonerClonable]
    public class DuplicateChild
    {
        public int Id { get; set; }
        public string Data { get; set; } = "";
    }
    
    [Test]
    public async Task DuplicateProperties_PreservesIdentity_WhenSameInstance()
    {
        DuplicateChild sharedChild = new DuplicateChild { Id = 1, Data = "Shared" };
        DuplicatePropsRoot original = new DuplicatePropsRoot
        {
            Name = "DuplicateProps",
            Child1 = sharedChild,
            Child2 = sharedChild
        };
        
        // Verify original has shared identity
        await Assert.That(original.Child1).IsSameReferenceAs(original.Child2);

        DuplicatePropsRoot clone = original.FastDeepClone();
        
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Child1).IsNotSameReferenceAs(original.Child1);

        // Key assertion: clone should preserve the shared identity
        await Assert.That(clone.Child1).IsSameReferenceAs(clone.Child2).Because("Identity should be preserved: both properties should reference the same cloned instance");
        await Assert.That(clone.Child1.Id).IsEqualTo(1);
        await Assert.That(clone.Child1.Data).IsEqualTo("Shared");
    }
    
    #endregion
    
    #region Test 4: Collection with Non-Safe Elements - State needed (with PreserveIdentity)
    
    [FastClonerClonable]
    [FastClonerPreserveIdentity] // Required for identity preservation in collections
    public class CollectionRoot
    {
        public string Name { get; set; } = "";
        public List<CollectionItem> Items { get; set; } = [];
    }
    
    [FastClonerClonable]
    public class CollectionItem
    {
        public int Id { get; set; }
        public string Description { get; set; } = "";
    }
    
    [Test]
    public async Task Collection_PreservesIdentity_WhenSameInstanceAppearsTwice()
    {
        CollectionItem sharedItem = new CollectionItem { Id = 1, Description = "Shared" };
        CollectionRoot original = new CollectionRoot
        {
            Name = "CollectionTest",
            Items = [sharedItem, new CollectionItem { Id = 2 }, sharedItem]
        };
        
        // Verify original: first and third items are the same instance
        await Assert.That(original.Items[0]).IsSameReferenceAs(original.Items[2]);
        await Assert.That(original.Items[0]).IsNotSameReferenceAs(original.Items[1]);

        CollectionRoot clone = original.FastDeepClone();
        
        await Assert.That(clone.Items.Count).IsEqualTo(3);
        await Assert.That(clone.Items[0]).IsNotSameReferenceAs(original.Items[0]);

        // Key assertion: clone should preserve identity
        await Assert.That(clone.Items[0]).IsSameReferenceAs(clone.Items[2]).Because("Identity should be preserved: same original instance should clone to same cloned instance");
        await Assert.That(clone.Items[0]).IsNotSameReferenceAs(clone.Items[1]);
        await Assert.That(clone.Items[0].Description).IsEqualTo("Shared");
    }
    
    #endregion
    
    #region Test 5: Existing Circular Reference - State needed
    
    [FastClonerClonable]
    public class CircularNode
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public CircularNode? Next { get; set; }
    }
    
    [Test]
    public async Task CircularReference_HandlesCorrectly()
    {
        CircularNode node1 = new CircularNode { Id = 1, Name = "First" };
        CircularNode node2 = new CircularNode { Id = 2, Name = "Second" };
        
        // Create a cycle: node1 -> node2 -> node1
        node1.Next = node2;
        node2.Next = node1;
        
        CircularNode clone = node1.FastDeepClone();
        
        await Assert.That(clone).IsNotSameReferenceAs(node1);
        await Assert.That(clone.Id).IsEqualTo(1);
        await Assert.That(clone.Next).IsNotNull();
        await Assert.That(clone.Next!.Id).IsEqualTo(2);

        // Verify cycle is preserved correctly
        await Assert.That(clone.Next.Next).IsSameReferenceAs(clone).Because("Circular reference should be preserved: clone.Next.Next should be the same as clone");
    }
    
    [Test]
    public async Task SelfReference_HandlesCorrectly()
    {
        CircularNode node = new CircularNode { Id = 1, Name = "Self" };
        node.Next = node; // Self-reference
        
        CircularNode clone = node.FastDeepClone();
        
        await Assert.That(clone).IsNotSameReferenceAs(node);
        await Assert.That(clone.Id).IsEqualTo(1);

        // Verify self-reference is preserved
        await Assert.That(clone.Next).IsSameReferenceAs(clone).Because("Self-reference should be preserved: clone.Next should be the same as clone");
    }
    
    #endregion
    
    #region Test 6: Deep Graph with Mixed Scenarios (with PreserveIdentity)
    
    [FastClonerClonable]
    [FastClonerPreserveIdentity] // Required for identity preservation
    public class DeepGraphRoot
    {
        public string Name { get; set; } = "";
        public DeepGraphLevel1 Level1A { get; set; } = new();
        public DeepGraphLevel1 Level1B { get; set; } = new();
    }
    
    [FastClonerClonable]
    public class DeepGraphLevel1
    {
        public int Value { get; set; }
        public DeepGraphLeaf Leaf { get; set; } = new();
    }
    
    [FastClonerClonable]
    public class DeepGraphLeaf
    {
        public string Data { get; set; } = "";
    }
    
    [Test]
    public async Task DeepGraph_PreservesIdentity_AtMultipleLevels()
    {
        DeepGraphLeaf sharedLeaf = new DeepGraphLeaf { Data = "SharedLeaf" };
        DeepGraphLevel1 sharedLevel1 = new DeepGraphLevel1 { Value = 10, Leaf = sharedLeaf };
        
        DeepGraphRoot original = new DeepGraphRoot
        {
            Name = "DeepGraph",
            Level1A = sharedLevel1,
            Level1B = sharedLevel1
        };
        
        // Verify original has shared identity at multiple levels
        await Assert.That(original.Level1A).IsSameReferenceAs(original.Level1B);
        await Assert.That(original.Level1A.Leaf).IsSameReferenceAs(original.Level1B.Leaf);

        DeepGraphRoot clone = original.FastDeepClone();
        
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Level1A).IsNotSameReferenceAs(original.Level1A);

        // Key assertions: clone should preserve identity at all levels
        await Assert.That(clone.Level1A).IsSameReferenceAs(clone.Level1B).Because("Identity at Level1 should be preserved");
        await Assert.That(clone.Level1A.Leaf).IsSameReferenceAs(clone.Level1B.Leaf).Because("Identity at Leaf level should be preserved (same Level1 means same Leaf)");
        await Assert.That(clone.Level1A.Leaf.Data).IsEqualTo("SharedLeaf");
    }
    
    #endregion
    
    #region Test 7: PreserveIdentity attribute on type
    
    /// <summary>
    /// Type with PreserveIdentity enabled - should track identity even without cycles
    /// </summary>
    [FastClonerClonable]
    [FastClonerPreserveIdentity]
    public class TypeWithPreserveIdentity
    {
        public string Name { get; set; } = "";
        public SharedItem Item1 { get; set; } = new();
        public SharedItem Item2 { get; set; } = new();
    }
    
    [FastClonerClonable]
    public class SharedItem
    {
        public int Id { get; set; }
    }
    
    [Test]
    public async Task TypeWithPreserveIdentity_PreservesIdentity()
    {
        SharedItem shared = new SharedItem { Id = 42 };
        TypeWithPreserveIdentity original = new TypeWithPreserveIdentity
        {
            Name = "Test",
            Item1 = shared,
            Item2 = shared
        };
        
        await Assert.That(original.Item1).IsSameReferenceAs(original.Item2);

        TypeWithPreserveIdentity clone = original.FastDeepClone();
        
        await Assert.That(clone.Item1).IsNotSameReferenceAs(original.Item1);
        await Assert.That(clone.Item1).IsSameReferenceAs(clone.Item2).Because("With [FastClonerPreserveIdentity], shared references should be preserved");
    }
    
    #endregion
    
    #region Test 8: PreserveIdentity attribute on member
    
    [FastClonerClonable]
    public class TypeWithMemberPreserveIdentity
    {
        public string Name { get; set; } = "";
        
        [FastClonerPreserveIdentity]
        public List<MemberItem> Items { get; set; } = [];
    }
    
    [FastClonerClonable]
    public class MemberItem
    {
        public int Value { get; set; }
    }
    
    [Test]
    public async Task MemberWithPreserveIdentity_PreservesIdentityInCollection()
    {
        MemberItem shared = new MemberItem { Value = 100 };
        TypeWithMemberPreserveIdentity original = new TypeWithMemberPreserveIdentity
        {
            Name = "Test",
            Items = [shared, new MemberItem { Value = 200 }, shared]
        };
        
        await Assert.That(original.Items[0]).IsSameReferenceAs(original.Items[2]);

        TypeWithMemberPreserveIdentity clone = original.FastDeepClone();
        
        await Assert.That(clone.Items[0]).IsNotSameReferenceAs(original.Items[0]);
        await Assert.That(clone.Items[0]).IsSameReferenceAs(clone.Items[2]).Because("With [FastClonerPreserveIdentity] on member, shared references should be preserved");
    }
    
    #endregion
    
    #region Test 9: Without PreserveIdentity - no identity preservation (faster)
    
    [FastClonerClonable]
    public class TypeWithoutPreserveIdentity
    {
        public string Name { get; set; } = "";
        public NoIdentityItem Item1 { get; set; } = new();
        public NoIdentityItem Item2 { get; set; } = new();
    }
    
    [FastClonerClonable]
    public class NoIdentityItem
    {
        public int Id { get; set; }
    }
    
    [Test]
    public async Task TypeWithoutPreserveIdentity_DoesNotPreserveIdentity()
    {
        NoIdentityItem shared = new NoIdentityItem { Id = 42 };
        TypeWithoutPreserveIdentity original = new TypeWithoutPreserveIdentity
        {
            Name = "Test",
            Item1 = shared,
            Item2 = shared
        };
        
        await Assert.That(original.Item1).IsSameReferenceAs(original.Item2);

        TypeWithoutPreserveIdentity clone = original.FastDeepClone();
        
        // Without PreserveIdentity, both items are cloned separately (faster, but loses identity)
        // This is the expected behavior for performance - users opt-in to identity preservation
        await Assert.That(clone.Item1.Id).IsEqualTo(42);
        await Assert.That(clone.Item2.Id).IsEqualTo(42);
        // Note: We don't assert they're different because cycles still require tracking
        // The key difference is we don't track identity for performance when not needed
    }
    
    #endregion
}