using FastCloner.SourceGenerator.Shared;

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
    public void SimpleTree_ClonesCorrectly_NoStateNeeded()
    {
        var original = new SimpleTreeRoot
        {
            Name = "Root",
            Child = new SimpleTreeChild { Value = 42 }
        };
        
        var clone = original.FastDeepClone();
        
        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone.Name, Is.EqualTo("Root"));
        Assert.That(clone.Child, Is.Not.SameAs(original.Child));
        Assert.That(clone.Child.Value, Is.EqualTo(42));
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
    public void MultiplePaths_PreservesIdentity_WhenSameInstanceShared()
    {
        var sharedNode = new SharedNode { Value = 100 };
        var original = new MultiPathRoot
        {
            Name = "MultiPath",
            PathA = new PathA { Shared = sharedNode },
            PathB = new PathB { Shared = sharedNode }
        };
        
        // Verify original has shared identity
        Assert.That(original.PathA.Shared, Is.SameAs(original.PathB.Shared));
        
        var clone = original.FastDeepClone();
        
        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone.PathA.Shared, Is.Not.SameAs(original.PathA.Shared));
        Assert.That(clone.PathB.Shared, Is.Not.SameAs(original.PathB.Shared));
        
        // Key assertion: clone should preserve the shared identity
        Assert.That(clone.PathA.Shared, Is.SameAs(clone.PathB.Shared), 
            "Identity should be preserved: both paths should clone to the same instance");
        Assert.That(clone.PathA.Shared.Value, Is.EqualTo(100));
    }
    
    [Test]
    public void MultiplePaths_CreatesSeparateClones_WhenDifferentInstances()
    {
        var original = new MultiPathRoot
        {
            Name = "MultiPath",
            PathA = new PathA { Shared = new SharedNode { Value = 1 } },
            PathB = new PathB { Shared = new SharedNode { Value = 2 } }
        };
        
        // Verify original has different instances
        Assert.That(original.PathA.Shared, Is.Not.SameAs(original.PathB.Shared));
        
        var clone = original.FastDeepClone();
        
        // Clone should also have different instances
        Assert.That(clone.PathA.Shared, Is.Not.SameAs(clone.PathB.Shared));
        Assert.That(clone.PathA.Shared.Value, Is.EqualTo(1));
        Assert.That(clone.PathB.Shared.Value, Is.EqualTo(2));
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
    public void DuplicateProperties_PreservesIdentity_WhenSameInstance()
    {
        var sharedChild = new DuplicateChild { Id = 1, Data = "Shared" };
        var original = new DuplicatePropsRoot
        {
            Name = "DuplicateProps",
            Child1 = sharedChild,
            Child2 = sharedChild
        };
        
        // Verify original has shared identity
        Assert.That(original.Child1, Is.SameAs(original.Child2));
        
        var clone = original.FastDeepClone();
        
        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone.Child1, Is.Not.SameAs(original.Child1));
        
        // Key assertion: clone should preserve the shared identity
        Assert.That(clone.Child1, Is.SameAs(clone.Child2),
            "Identity should be preserved: both properties should reference the same cloned instance");
        Assert.That(clone.Child1.Id, Is.EqualTo(1));
        Assert.That(clone.Child1.Data, Is.EqualTo("Shared"));
    }
    
    #endregion
    
    #region Test 4: Collection with Non-Safe Elements - State needed (with PreserveIdentity)
    
    [FastClonerClonable]
    [FastClonerPreserveIdentity] // Required for identity preservation in collections
    public class CollectionRoot
    {
        public string Name { get; set; } = "";
        public List<CollectionItem> Items { get; set; } = new();
    }
    
    [FastClonerClonable]
    public class CollectionItem
    {
        public int Id { get; set; }
        public string Description { get; set; } = "";
    }
    
    [Test]
    public void Collection_PreservesIdentity_WhenSameInstanceAppearsTwice()
    {
        var sharedItem = new CollectionItem { Id = 1, Description = "Shared" };
        var original = new CollectionRoot
        {
            Name = "CollectionTest",
            Items = new List<CollectionItem> { sharedItem, new CollectionItem { Id = 2 }, sharedItem }
        };
        
        // Verify original: first and third items are the same instance
        Assert.That(original.Items[0], Is.SameAs(original.Items[2]));
        Assert.That(original.Items[0], Is.Not.SameAs(original.Items[1]));
        
        var clone = original.FastDeepClone();
        
        Assert.That(clone.Items.Count, Is.EqualTo(3));
        Assert.That(clone.Items[0], Is.Not.SameAs(original.Items[0]));
        
        // Key assertion: clone should preserve identity
        Assert.That(clone.Items[0], Is.SameAs(clone.Items[2]),
            "Identity should be preserved: same original instance should clone to same cloned instance");
        Assert.That(clone.Items[0], Is.Not.SameAs(clone.Items[1]));
        Assert.That(clone.Items[0].Description, Is.EqualTo("Shared"));
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
    public void CircularReference_HandlesCorrectly()
    {
        var node1 = new CircularNode { Id = 1, Name = "First" };
        var node2 = new CircularNode { Id = 2, Name = "Second" };
        
        // Create a cycle: node1 -> node2 -> node1
        node1.Next = node2;
        node2.Next = node1;
        
        var clone = node1.FastDeepClone();
        
        Assert.That(clone, Is.Not.SameAs(node1));
        Assert.That(clone.Id, Is.EqualTo(1));
        Assert.That(clone.Next, Is.Not.Null);
        Assert.That(clone.Next!.Id, Is.EqualTo(2));
        
        // Verify cycle is preserved correctly
        Assert.That(clone.Next.Next, Is.SameAs(clone),
            "Circular reference should be preserved: clone.Next.Next should be the same as clone");
    }
    
    [Test]
    public void SelfReference_HandlesCorrectly()
    {
        var node = new CircularNode { Id = 1, Name = "Self" };
        node.Next = node; // Self-reference
        
        var clone = node.FastDeepClone();
        
        Assert.That(clone, Is.Not.SameAs(node));
        Assert.That(clone.Id, Is.EqualTo(1));
        
        // Verify self-reference is preserved
        Assert.That(clone.Next, Is.SameAs(clone),
            "Self-reference should be preserved: clone.Next should be the same as clone");
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
    public void DeepGraph_PreservesIdentity_AtMultipleLevels()
    {
        var sharedLeaf = new DeepGraphLeaf { Data = "SharedLeaf" };
        var sharedLevel1 = new DeepGraphLevel1 { Value = 10, Leaf = sharedLeaf };
        
        var original = new DeepGraphRoot
        {
            Name = "DeepGraph",
            Level1A = sharedLevel1,
            Level1B = sharedLevel1
        };
        
        // Verify original has shared identity at multiple levels
        Assert.That(original.Level1A, Is.SameAs(original.Level1B));
        Assert.That(original.Level1A.Leaf, Is.SameAs(original.Level1B.Leaf));
        
        var clone = original.FastDeepClone();
        
        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone.Level1A, Is.Not.SameAs(original.Level1A));
        
        // Key assertions: clone should preserve identity at all levels
        Assert.That(clone.Level1A, Is.SameAs(clone.Level1B),
            "Identity at Level1 should be preserved");
        Assert.That(clone.Level1A.Leaf, Is.SameAs(clone.Level1B.Leaf),
            "Identity at Leaf level should be preserved (same Level1 means same Leaf)");
        Assert.That(clone.Level1A.Leaf.Data, Is.EqualTo("SharedLeaf"));
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
    public void TypeWithPreserveIdentity_PreservesIdentity()
    {
        var shared = new SharedItem { Id = 42 };
        var original = new TypeWithPreserveIdentity
        {
            Name = "Test",
            Item1 = shared,
            Item2 = shared
        };
        
        Assert.That(original.Item1, Is.SameAs(original.Item2));
        
        var clone = original.FastDeepClone();
        
        Assert.That(clone.Item1, Is.Not.SameAs(original.Item1));
        Assert.That(clone.Item1, Is.SameAs(clone.Item2),
            "With [FastClonerPreserveIdentity], shared references should be preserved");
    }
    
    #endregion
    
    #region Test 8: PreserveIdentity attribute on member
    
    [FastClonerClonable]
    public class TypeWithMemberPreserveIdentity
    {
        public string Name { get; set; } = "";
        
        [FastClonerPreserveIdentity]
        public List<MemberItem> Items { get; set; } = new();
    }
    
    [FastClonerClonable]
    public class MemberItem
    {
        public int Value { get; set; }
    }
    
    [Test]
    public void MemberWithPreserveIdentity_PreservesIdentityInCollection()
    {
        var shared = new MemberItem { Value = 100 };
        var original = new TypeWithMemberPreserveIdentity
        {
            Name = "Test",
            Items = new List<MemberItem> { shared, new MemberItem { Value = 200 }, shared }
        };
        
        Assert.That(original.Items[0], Is.SameAs(original.Items[2]));
        
        var clone = original.FastDeepClone();
        
        Assert.That(clone.Items[0], Is.Not.SameAs(original.Items[0]));
        Assert.That(clone.Items[0], Is.SameAs(clone.Items[2]),
            "With [FastClonerPreserveIdentity] on member, shared references should be preserved");
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
    public void TypeWithoutPreserveIdentity_DoesNotPreserveIdentity()
    {
        var shared = new NoIdentityItem { Id = 42 };
        var original = new TypeWithoutPreserveIdentity
        {
            Name = "Test",
            Item1 = shared,
            Item2 = shared
        };
        
        Assert.That(original.Item1, Is.SameAs(original.Item2));
        
        var clone = original.FastDeepClone();
        
        // Without PreserveIdentity, both items are cloned separately (faster, but loses identity)
        // This is the expected behavior for performance - users opt-in to identity preservation
        Assert.That(clone.Item1.Id, Is.EqualTo(42));
        Assert.That(clone.Item2.Id, Is.EqualTo(42));
        // Note: We don't assert they're different because cycles still require tracking
        // The key difference is we don't track identity for performance when not needed
    }
    
    #endregion
}
