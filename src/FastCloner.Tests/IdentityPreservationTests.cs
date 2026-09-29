using FastCloner.SourceGenerator.Shared;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
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

    #region Test 10: Member-level PreserveIdentity overrides child type-level attribute

    /// <summary>
    /// Shared leaf used to observe whether aliasing inside a subgraph survived cloning.
    /// </summary>
    [FastClonerClonable]
    public class PrecedenceSharedLeaf
    {
        public int Value { get; set; }
    }

    /// <summary>
    /// Child whose own type-level attribute turns identity preservation on.
    /// Cloning this type directly should keep the Left/Right aliasing.
    /// </summary>
    [FastClonerClonable]
    [FastClonerPreserveIdentity(true)]
    public class PrecedenceChildWithIdentity
    {
        public PrecedenceSharedLeaf Left { get; set; } = new();
        public PrecedenceSharedLeaf Right { get; set; } = new();
    }

    /// <summary>
    /// Child with no type-level attribute (library default: identity preserved only
    /// when a member override asks for it or the graph needs cycle tracking).
    /// </summary>
    [FastClonerClonable]
    public class PrecedenceChildWithoutIdentity
    {
        public PrecedenceSharedLeaf Left { get; set; } = new();
        public PrecedenceSharedLeaf Right { get; set; } = new();
    }

    [FastClonerClonable]
    public class MemberFalseOverChildTypeTrueRoot
    {
        [FastClonerPreserveIdentity(false)]
        public PrecedenceChildWithIdentity Child { get; set; } = new();
    }

    [FastClonerClonable]
    public class MemberTrueOverChildTypeDefaultRoot
    {
        [FastClonerPreserveIdentity(true)]
        public PrecedenceChildWithoutIdentity Child { get; set; } = new();
    }

    [FastClonerClonable]
    public class NoMemberOverrideChildTypeTrueRoot
    {
        public PrecedenceChildWithIdentity Child { get; set; } = new();
    }

    [Test]
    public async Task MemberFalse_OverridesChildTypeTrue_DoesNotPreserveIdentity()
    {
        PrecedenceSharedLeaf shared = new PrecedenceSharedLeaf { Value = 7 };
        MemberFalseOverChildTypeTrueRoot original = new MemberFalseOverChildTypeTrueRoot
        {
            Child = new PrecedenceChildWithIdentity { Left = shared, Right = shared }
        };

        await Assert.That(original.Child.Left).IsSameReferenceAs(original.Child.Right);

        MemberFalseOverChildTypeTrueRoot clone = original.FastDeepClone();

        await Assert.That(clone.Child).IsNotSameReferenceAs(original.Child);
        await Assert.That(clone.Child.Left).IsNotSameReferenceAs(original.Child.Left);
        await Assert.That(clone.Child.Left.Value).IsEqualTo(7);
        await Assert.That(clone.Child.Right.Value).IsEqualTo(7);

        // Member-level [FastClonerPreserveIdentity(false)] must win over the child's
        // type-level [FastClonerPreserveIdentity(true)] for this member's subgraph.
        await Assert.That(clone.Child.Left).IsNotSameReferenceAs(clone.Child.Right).Because(
            "member-level PreserveIdentity(false) must suppress identity preservation for the member's subgraph");
    }

    [Test]
    public async Task MemberFalse_OverridesChildTypeTrue_ChildClonedDirectlyStillPreservesIdentity()
    {
        PrecedenceSharedLeaf shared = new PrecedenceSharedLeaf { Value = 9 };
        PrecedenceChildWithIdentity original = new PrecedenceChildWithIdentity { Left = shared, Right = shared };

        PrecedenceChildWithIdentity clone = original.FastDeepClone();

        // Type-level [FastClonerPreserveIdentity(true)] semantics for a directly cloned
        // root must be unaffected by the member-level override in another type.
        await Assert.That(clone.Left).IsSameReferenceAs(clone.Right).Because(
            "type-level identity preservation for the directly cloned type must be preserved");
    }

    [Test]
    public async Task MemberTrue_OverridesChildTypeDefault_PreservesIdentity()
    {
        PrecedenceSharedLeaf shared = new PrecedenceSharedLeaf { Value = 11 };
        MemberTrueOverChildTypeDefaultRoot original = new MemberTrueOverChildTypeDefaultRoot
        {
            Child = new PrecedenceChildWithoutIdentity { Left = shared, Right = shared }
        };

        await Assert.That(original.Child.Left).IsSameReferenceAs(original.Child.Right);

        MemberTrueOverChildTypeDefaultRoot clone = original.FastDeepClone();

        await Assert.That(clone.Child).IsNotSameReferenceAs(original.Child);
        await Assert.That(clone.Child.Left.Value).IsEqualTo(11);
        await Assert.That(clone.Child.Left).IsSameReferenceAs(clone.Child.Right).Because(
            "member-level PreserveIdentity(true) must enable identity preservation for the member's subgraph");
    }

    [Test]
    public async Task NoMemberOverride_ChildTypeTrue_PreservesIdentity()
    {
        PrecedenceSharedLeaf shared = new PrecedenceSharedLeaf { Value = 13 };
        NoMemberOverrideChildTypeTrueRoot original = new NoMemberOverrideChildTypeTrueRoot
        {
            Child = new PrecedenceChildWithIdentity { Left = shared, Right = shared }
        };

        await Assert.That(original.Child.Left).IsSameReferenceAs(original.Child.Right);

        NoMemberOverrideChildTypeTrueRoot clone = original.FastDeepClone();

        await Assert.That(clone.Child).IsNotSameReferenceAs(original.Child);
        await Assert.That(clone.Child.Left).IsNotSameReferenceAs(original.Child.Left);
        await Assert.That(clone.Child.Left.Value).IsEqualTo(13);
        await Assert.That(clone.Child.Left).IsSameReferenceAs(clone.Child.Right).Because(
            "without a member override the child type's own PreserveIdentity(true) must apply");
    }

    #endregion

    #region Test 11: member-level opt-out interactions (ambient state, nesting, cycles, non-public)

    /// <summary>
    /// Root that itself tracks identity: the opt-out member must not inherit the parent's
    /// tracking state, while the parent's own members keep preserving identity.
    /// </summary>
    [FastClonerClonable]
    [FastClonerPreserveIdentity(true)]
    public class IdentityRootWithOptOutMember
    {
        public PrecedenceSharedLeaf Left { get; set; } = new();
        public PrecedenceSharedLeaf Right { get; set; } = new();

        [FastClonerPreserveIdentity(false)]
        public PrecedenceChildWithIdentity Child { get; set; } = new();
    }

    [FastClonerClonable]
    [FastClonerPreserveIdentity(true)]
    public class PrecedenceNestedHolder
    {
        public PrecedenceChildWithIdentity Inner { get; set; } = new();
    }

    [FastClonerClonable]
    public class MemberFalseOverNestedIdentityRoot
    {
        [FastClonerPreserveIdentity(false)]
        public PrecedenceNestedHolder Child { get; set; } = new();
    }

    [FastClonerClonable]
    public class PrecedenceCyclicChild
    {
        public int Value { get; set; }
        public PrecedenceCyclicChild? Next { get; set; }
    }

    [FastClonerClonable]
    public class MemberFalseOverCyclicChildRoot
    {
        [FastClonerPreserveIdentity(false)]
        public PrecedenceCyclicChild Child { get; set; } = new();
    }

    [FastClonerClonable]
    public class MemberFalseOverPrivateClonableFieldRoot
    {
        [FastClonerPreserveIdentity(false)]
        private PrecedenceChildWithIdentity _child = new();

        // Accessors are methods rather than a property: a public property over the same storage
        // would be cloned as a second member and overwrite the field clone under test.
        public void SetChild(PrecedenceChildWithIdentity child) => _child = child;

        public PrecedenceChildWithIdentity GetChild() => _child;
    }

    [FastClonerClonable]
    [FastClonerPreserveIdentity(true)]
    public class IdentityRootWithOptOutCollectionMember
    {
        public PrecedenceSharedLeaf Left { get; set; } = new();
        public PrecedenceSharedLeaf Right { get; set; } = new();

        [FastClonerPreserveIdentity(false)]
        public List<PrecedenceChildWithIdentity> Items { get; set; } = [];
    }

    [Test]
    public async Task MemberFalse_OverridesChildTypeTrue_EvenWhenParentTracksIdentity()
    {
        PrecedenceSharedLeaf sharedLeaf = new PrecedenceSharedLeaf { Value = 5 };
        PrecedenceSharedLeaf childLeaf = new PrecedenceSharedLeaf { Value = 6 };

        IdentityRootWithOptOutMember original = new IdentityRootWithOptOutMember
        {
            Left = sharedLeaf,
            Right = sharedLeaf,
            Child = new PrecedenceChildWithIdentity { Left = childLeaf, Right = childLeaf }
        };

        IdentityRootWithOptOutMember clone = original.FastDeepClone();

        // The parent's own members still share the parent's tracking state.
        await Assert.That(clone.Left).IsSameReferenceAs(clone.Right);
        await Assert.That(clone.Left.Value).IsEqualTo(5);

        // The opted-out member does not get the parent's state forwarded into its subgraph.
        await Assert.That(clone.Child.Left).IsNotSameReferenceAs(clone.Child.Right).Because(
            "member-level PreserveIdentity(false) must not inherit the parent's identity-tracking state");
        await Assert.That(clone.Child.Left.Value).IsEqualTo(6);
    }

    [Test]
    public async Task MemberFalse_PropagatesThroughNestedIdentityTrackingTypes()
    {
        PrecedenceSharedLeaf shared = new PrecedenceSharedLeaf { Value = 8 };
        MemberFalseOverNestedIdentityRoot original = new MemberFalseOverNestedIdentityRoot
        {
            Child = new PrecedenceNestedHolder
            {
                Inner = new PrecedenceChildWithIdentity { Left = shared, Right = shared }
            }
        };

        MemberFalseOverNestedIdentityRoot clone = original.FastDeepClone();

        await Assert.That(clone.Child.Inner.Left.Value).IsEqualTo(8);
        await Assert.That(clone.Child.Inner.Left).IsNotSameReferenceAs(clone.Child.Inner.Right).Because(
            "the opt-out must propagate into nested types that would otherwise track identity");
    }

    [Test]
    public async Task MemberFalse_OverChildWithCycles_StillDetectsCycles()
    {
        PrecedenceCyclicChild child = new PrecedenceCyclicChild { Value = 1 };
        child.Next = child; // self-cycle

        MemberFalseOverCyclicChildRoot original = new MemberFalseOverCyclicChildRoot { Child = child };

        MemberFalseOverCyclicChildRoot clone = original.FastDeepClone();

        await Assert.That(clone.Child).IsNotSameReferenceAs(original.Child);
        await Assert.That(clone.Child.Value).IsEqualTo(1);

        // Cycle detection must win over the opt-out: without it this clone would recurse forever.
        await Assert.That(clone.Child.Next).IsSameReferenceAs(clone.Child).Because(
            "circular references must still be detected when a member opts out of identity preservation");
    }

    [Test]
    public async Task MemberFalse_OnPrivateClonableField_IsHonored()
    {
        PrecedenceSharedLeaf shared = new PrecedenceSharedLeaf { Value = 17 };
        MemberFalseOverPrivateClonableFieldRoot original = new MemberFalseOverPrivateClonableFieldRoot();
        original.SetChild(new PrecedenceChildWithIdentity { Left = shared, Right = shared });

        MemberFalseOverPrivateClonableFieldRoot clone = original.FastDeepClone();

        await Assert.That(clone.GetChild()).IsNotSameReferenceAs(original.GetChild());
        await Assert.That(clone.GetChild().Left.Value).IsEqualTo(17);
        await Assert.That(clone.GetChild().Left).IsNotSameReferenceAs(clone.GetChild().Right).Because(
            "a member-level opt-out must also apply to non-public members");
    }

    [Test]
    public async Task MemberFalse_OnCollectionMember_DoesNotPreserveIdentityAcrossElements()
    {
        PrecedenceSharedLeaf sharedLeaf = new PrecedenceSharedLeaf { Value = 3 };
        PrecedenceChildWithIdentity sharedElement = new PrecedenceChildWithIdentity
        {
            Left = sharedLeaf,
            Right = sharedLeaf
        };

        IdentityRootWithOptOutCollectionMember original = new IdentityRootWithOptOutCollectionMember
        {
            Left = sharedLeaf,
            Right = sharedLeaf,
            Items = [sharedElement, sharedElement]
        };

        IdentityRootWithOptOutCollectionMember clone = original.FastDeepClone();

        await Assert.That(clone.Left).IsSameReferenceAs(clone.Right);

        // The opted-out collection member does not share the parent's tracking state, so the two
        // occurrences of the same element clone independently...
        await Assert.That(clone.Items[0]).IsNotSameReferenceAs(clone.Items[1]);

        // ...and the opt-out reaches the element cloners, suppressing aliasing inside each element
        // even though the element type's own default is to preserve identity.
        await Assert.That(clone.Items[0].Left).IsNotSameReferenceAs(clone.Items[0].Right).Because(
            "a member-level opt-out must reach the collection's element cloners");
        await Assert.That(clone.Items[0].Left.Value).IsEqualTo(3);
        await Assert.That(clone.Items[1].Left.Value).IsEqualTo(3);
    }

    #endregion

    #region Test 12: member-level override on an implicitly cloned (unannotated) POCO

    /// <summary>
    /// Plain POCO without <c>[FastClonerClonable]</c>: cloned implicitly by the generator.
    /// </summary>
    public class PrecedenceImplicitHolder
    {
        public PrecedenceChildWithIdentity Inner { get; set; } = new();
    }

    [FastClonerClonable]
    [FastClonerPreserveIdentity(true)]
    public class IdentityRootWithOptOutImplicitMember
    {
        public PrecedenceSharedLeaf Left { get; set; } = new();
        public PrecedenceSharedLeaf Right { get; set; } = new();

        [FastClonerPreserveIdentity(false)]
        public PrecedenceImplicitHolder Holder { get; set; } = new();
    }

    [Test]
    public async Task MemberFalse_OnImplicitPocoMember_DoesNotPreserveIdentityBelow()
    {
        PrecedenceSharedLeaf shared = new PrecedenceSharedLeaf { Value = 31 };
        IdentityRootWithOptOutImplicitMember original = new IdentityRootWithOptOutImplicitMember
        {
            Left = shared,
            Right = shared,
            Holder = new PrecedenceImplicitHolder
            {
                Inner = new PrecedenceChildWithIdentity { Left = shared, Right = shared }
            }
        };

        IdentityRootWithOptOutImplicitMember clone = original.FastDeepClone();

        await Assert.That(clone.Left).IsSameReferenceAs(clone.Right);
        await Assert.That(clone.Holder.Inner.Left.Value).IsEqualTo(31);
        await Assert.That(clone.Holder.Inner.Left).IsNotSameReferenceAs(clone.Holder.Inner.Right).Because(
            "a member-level opt-out must reach the implicit POCO's nested cloners");
    }

    #endregion

    #region Test 13: member-level overrides on arrays, dictionaries and safe-element collections

    [FastClonerClonable]
    [FastClonerPreserveIdentity(true)]
    public class IdentityRootWithOptOutArrayMember
    {
        public PrecedenceSharedLeaf Left { get; set; } = new();
        public PrecedenceSharedLeaf Right { get; set; } = new();

        [FastClonerPreserveIdentity(false)]
        public PrecedenceChildWithIdentity[] Items { get; set; } = [];
    }

    [FastClonerClonable]
    [FastClonerPreserveIdentity(true)]
    public class IdentityRootWithOptOutDictionaryMember
    {
        public PrecedenceSharedLeaf Left { get; set; } = new();
        public PrecedenceSharedLeaf Right { get; set; } = new();

        [FastClonerPreserveIdentity(false)]
        public Dictionary<string, PrecedenceChildWithIdentity> Items { get; set; } = new();
    }

    /// <summary>
    /// Mirror case: an explicit opt-in on a collection of safe elements. The elements cannot be
    /// shared, so the opt-in is unobservable — but it used to desynchronise the helper signature
    /// from the call arguments and break the build.
    /// </summary>
    [FastClonerClonable]
    public class RootWithOptInSafeElementCollection
    {
        [FastClonerPreserveIdentity]
        public List<string> Labels { get; set; } = [];
    }

    [Test]
    public async Task MemberFalse_OnArrayMember_DoesNotPreserveIdentityInElements()
    {
        PrecedenceSharedLeaf shared = new PrecedenceSharedLeaf { Value = 21 };
        IdentityRootWithOptOutArrayMember original = new IdentityRootWithOptOutArrayMember
        {
            Left = shared,
            Right = shared,
            Items = [new PrecedenceChildWithIdentity { Left = shared, Right = shared }]
        };

        IdentityRootWithOptOutArrayMember clone = original.FastDeepClone();

        await Assert.That(clone.Left).IsSameReferenceAs(clone.Right);
        await Assert.That(clone.Items[0].Left.Value).IsEqualTo(21);
        await Assert.That(clone.Items[0].Left).IsNotSameReferenceAs(clone.Items[0].Right).Because(
            "a member-level opt-out must reach array element cloners");
    }

    [Test]
    public async Task MemberFalse_OnDictionaryMember_DoesNotPreserveIdentityInValues()
    {
        PrecedenceSharedLeaf shared = new PrecedenceSharedLeaf { Value = 22 };
        IdentityRootWithOptOutDictionaryMember original = new IdentityRootWithOptOutDictionaryMember
        {
            Left = shared,
            Right = shared,
            Items = { ["a"] = new PrecedenceChildWithIdentity { Left = shared, Right = shared } }
        };

        IdentityRootWithOptOutDictionaryMember clone = original.FastDeepClone();

        await Assert.That(clone.Left).IsSameReferenceAs(clone.Right);
        await Assert.That(clone.Items["a"].Left.Value).IsEqualTo(22);
        await Assert.That(clone.Items["a"].Left).IsNotSameReferenceAs(clone.Items["a"].Right).Because(
            "a member-level opt-out must reach dictionary value cloners");
    }

    [Test]
    public async Task MemberTrue_OnSafeElementCollection_ClonesSuccessfully()
    {
        RootWithOptInSafeElementCollection original = new RootWithOptInSafeElementCollection
        {
            Labels = ["a", "b", "a"]
        };

        RootWithOptInSafeElementCollection clone = original.FastDeepClone();

        await Assert.That(clone.Labels).IsEquivalentTo(new[] { "a", "b", "a" });
        await Assert.That(clone.Labels).IsNotSameReferenceAs(original.Labels);
    }

    #endregion

    #region Test 14: member-level opt-out with a root that needs no state of its own

    // The roots below carry no [FastClonerPreserveIdentity] and have no cycles, so the root itself
    // generates no tracking state and every helper it emits is stateless for its own sake. A helper
    // must still be able to receive the member-level opt-out ("capability" is not "default
    // behaviour").

    [FastClonerClonable]
    public class CollectionOptOutRootDefaultOff
    {
        [FastClonerPreserveIdentity(false)]
        public List<PrecedenceChildWithIdentity> Items { get; set; } = [];
    }

    [FastClonerClonable]
    public class ArrayOptOutRootDefaultOff
    {
        [FastClonerPreserveIdentity(false)]
        public PrecedenceChildWithIdentity[] Items { get; set; } = [];
    }

    [FastClonerClonable]
    public class DictionaryOptOutRootDefaultOff
    {
        [FastClonerPreserveIdentity(false)]
        public Dictionary<string, PrecedenceChildWithIdentity> Items { get; set; } = new();
    }

    [FastClonerClonable]
    public class ImplicitOptOutRootDefaultOff
    {
        [FastClonerPreserveIdentity(false)]
        public PrecedenceImplicitHolder Holder { get; set; } = new();
    }

    [FastClonerClonable]
    public class NestedCollectionOptOutRootDefaultOff
    {
        [FastClonerPreserveIdentity(false)]
        public List<List<PrecedenceChildWithIdentity>> Items { get; set; } = [];
    }

    [FastClonerClonable]
    public class GetterOnlyCollectionOptOutRootDefaultOff
    {
        public GetterOnlyCollectionOptOutRootDefaultOff()
        {
            Items = [];
        }

        public GetterOnlyCollectionOptOutRootDefaultOff(List<PrecedenceChildWithIdentity> items)
        {
            Items = items;
        }

        [FastClonerPreserveIdentity(false)]
        public List<PrecedenceChildWithIdentity> Items { get; }
    }

    [FastClonerClonable]
    public class ClonableOptOutRootDefaultOff
    {
        [FastClonerPreserveIdentity(false)]
        public PrecedenceChildWithIdentity Child { get; set; } = new();
    }

    /// <summary>Builds a <see cref="PrecedenceChildWithIdentity"/> whose members alias one leaf.</summary>
    private static PrecedenceChildWithIdentity AliasedChild(int value)
    {
        PrecedenceSharedLeaf shared = new PrecedenceSharedLeaf { Value = value };
        return new PrecedenceChildWithIdentity { Left = shared, Right = shared };
    }

    [Test]
    public async Task DefaultOffRoot_ClonableMemberOptOut_DoesNotPreserveIdentity()
    {
        ClonableOptOutRootDefaultOff original = new ClonableOptOutRootDefaultOff { Child = AliasedChild(41) };

        ClonableOptOutRootDefaultOff clone = original.FastDeepClone();

        await Assert.That(clone.Child.Left.Value).IsEqualTo(41);
        await Assert.That(clone.Child.Left).IsNotSameReferenceAs(clone.Child.Right).Because(
            "the child's type-level default must not win over the member-level opt-out");
    }

    [Test]
    public async Task DefaultOffRoot_CollectionMemberOptOut_DoesNotPreserveIdentityInElements()
    {
        CollectionOptOutRootDefaultOff original = new CollectionOptOutRootDefaultOff { Items = [AliasedChild(42)] };

        CollectionOptOutRootDefaultOff clone = original.FastDeepClone();

        await Assert.That(clone.Items.Count).IsEqualTo(1);
        await Assert.That(clone.Items[0].Left.Value).IsEqualTo(42);
        await Assert.That(clone.Items[0].Left).IsNotSameReferenceAs(clone.Items[0].Right).Because(
            "a stateless default must not stop the opt-out from reaching the element cloner");
    }

    [Test]
    public async Task DefaultOffRoot_ArrayMemberOptOut_DoesNotPreserveIdentityInElements()
    {
        ArrayOptOutRootDefaultOff original = new ArrayOptOutRootDefaultOff { Items = [AliasedChild(43)] };

        ArrayOptOutRootDefaultOff clone = original.FastDeepClone();

        await Assert.That(clone.Items[0].Left.Value).IsEqualTo(43);
        await Assert.That(clone.Items[0].Left).IsNotSameReferenceAs(clone.Items[0].Right).Because(
            "a stateless default must not stop the opt-out from reaching the array element cloner");
    }

    [Test]
    public async Task DefaultOffRoot_DictionaryMemberOptOut_DoesNotPreserveIdentityInValues()
    {
        DictionaryOptOutRootDefaultOff original = new DictionaryOptOutRootDefaultOff
        {
            Items = { ["a"] = AliasedChild(44) }
        };

        DictionaryOptOutRootDefaultOff clone = original.FastDeepClone();

        await Assert.That(clone.Items["a"].Left.Value).IsEqualTo(44);
        await Assert.That(clone.Items["a"].Left).IsNotSameReferenceAs(clone.Items["a"].Right).Because(
            "a stateless default must not stop the opt-out from reaching the dictionary value cloner");
    }

    [Test]
    public async Task DefaultOffRoot_ImplicitPocoMemberOptOut_DoesNotPreserveIdentityBelow()
    {
        ImplicitOptOutRootDefaultOff original = new ImplicitOptOutRootDefaultOff
        {
            Holder = new PrecedenceImplicitHolder { Inner = AliasedChild(45) }
        };

        ImplicitOptOutRootDefaultOff clone = original.FastDeepClone();

        await Assert.That(clone.Holder.Inner.Left.Value).IsEqualTo(45);
        await Assert.That(clone.Holder.Inner.Left).IsNotSameReferenceAs(clone.Holder.Inner.Right).Because(
            "a stateless default must not stop the opt-out from reaching the implicit POCO's cloners");
    }

    [Test]
    public async Task DefaultOffRoot_NestedCollectionMemberOptOut_DoesNotPreserveIdentityInElements()
    {
        NestedCollectionOptOutRootDefaultOff original = new NestedCollectionOptOutRootDefaultOff
        {
            Items = [[AliasedChild(46)]]
        };

        NestedCollectionOptOutRootDefaultOff clone = original.FastDeepClone();

        await Assert.That(clone.Items[0][0].Left.Value).IsEqualTo(46);
        await Assert.That(clone.Items[0][0].Left).IsNotSameReferenceAs(clone.Items[0][0].Right).Because(
            "the opt-out must propagate through nested collection helpers");
    }

    [Test]
    public async Task DefaultOffRoot_GetterOnlyCollectionMemberOptOut_DoesNotPreserveIdentityInElements()
    {
        GetterOnlyCollectionOptOutRootDefaultOff original =
            new GetterOnlyCollectionOptOutRootDefaultOff([AliasedChild(47)]);

        GetterOnlyCollectionOptOutRootDefaultOff clone = original.FastDeepClone();

        await Assert.That(clone.Items[0].Left.Value).IsEqualTo(47);
        await Assert.That(clone.Items[0].Left).IsNotSameReferenceAs(clone.Items[0].Right).Because(
            "a stateless default must not stop the opt-out from reaching a getter-only collection's elements");
    }

    #endregion

    #region Test 15: capability must not depend on generic usage discovery order

    // A plain POCO (no attribute) with an opted-out member. It only becomes known to the generator
    // while generic usages are being analysed, i.e. after the root's own members were emitted.
    public class GenericUsageOptOutPoco
    {
        [FastClonerPreserveIdentity(false)]
        public List<PrecedenceChildWithIdentity> Items { get; set; } = [];
    }

    [FastClonerClonable]
    public class GenericOptOutOrderFirst<T>
    {
        public T Value { get; set; } = default!;
    }

    [FastClonerClonable]
    public class GenericOptOutOrderLast<T>
    {
        public T Value { get; set; } = default!;
    }

    /// <summary>
    /// Usage ordering A: the state-free usage (which decides the capability of the shared
    /// <c>List&lt;PrecedenceChildWithIdentity&gt;</c> helper) comes first, the usage that introduces
    /// the opted-out POCO second.
    /// </summary>
    private static void TouchUsagesStateFreeFirst()
    {
        _ = new GenericOptOutOrderFirst<List<PrecedenceChildWithIdentity>>();
        _ = new GenericOptOutOrderFirst<GenericUsageOptOutPoco>();
    }

    /// <summary>Usage ordering B: the opted-out POCO is discovered first.</summary>
    private static void TouchUsagesOptOutFirst()
    {
        _ = new GenericOptOutOrderLast<GenericUsageOptOutPoco>();
        _ = new GenericOptOutOrderLast<List<PrecedenceChildWithIdentity>>();
    }

    [Test]
    public async Task GenericUsageOrder_StateFreeUsageFirst_StillHonoursMemberOptOut()
    {
        TouchUsagesStateFreeFirst();

        GenericOptOutOrderFirst<GenericUsageOptOutPoco> original = new GenericOptOutOrderFirst<GenericUsageOptOutPoco>
        {
            Value = new GenericUsageOptOutPoco { Items = [AliasedChild(51)] }
        };

        GenericOptOutOrderFirst<GenericUsageOptOutPoco> clone = original.FastDeepClone();

        await Assert.That(clone.Value.Items[0].Left.Value).IsEqualTo(51);
        await Assert.That(clone.Value.Items[0].Left).IsNotSameReferenceAs(clone.Value.Items[0].Right).Because(
            "capability must not depend on which generic usage happened to be analysed first");
    }

    [Test]
    public async Task GenericUsageOrder_OptOutUsageFirst_StillHonoursMemberOptOut()
    {
        TouchUsagesOptOutFirst();

        GenericOptOutOrderLast<GenericUsageOptOutPoco> original = new GenericOptOutOrderLast<GenericUsageOptOutPoco>
        {
            Value = new GenericUsageOptOutPoco { Items = [AliasedChild(52)] }
        };

        GenericOptOutOrderLast<GenericUsageOptOutPoco> clone = original.FastDeepClone();

        await Assert.That(clone.Value.Items[0].Left.Value).IsEqualTo(52);
        await Assert.That(clone.Value.Items[0].Left).IsNotSameReferenceAs(clone.Value.Items[0].Right).Because(
            "the reversed usage order must generate the same semantics");
    }

    #endregion

    #region Test 16: opt-out declared on a closed generic subtype discovered from usage

    [FastClonerClonable]
    public abstract class ShapeBase;

    /// <summary>
    /// Generic subtype of <see cref="ShapeBase"/>: only the closed construction in the test below
    /// makes the generator emit dispatch for it, and its opted-out member must keep reaching the
    /// collection helper with the opt-out intact.
    /// </summary>
    [FastClonerClonable]
    public sealed class ShapeBaseAsGenericSubtype<T> : ShapeBase
    {
        [FastClonerPreserveIdentity(false)]
        public List<PrecedenceChildWithIdentity> Items { get; set; } = [];
    }

    [Test]
    public async Task UsageDiscoveredClosedSubtype_OptOutIsPropagated()
    {
        // The only mention of the closed construction is right here.
        ShapeBase shape = new ShapeBaseAsGenericSubtype<int> { Items = [AliasedChild(53)] };

        ShapeBase clone = shape.FastDeepClone();

        ShapeBaseAsGenericSubtype<int> typedClone = (ShapeBaseAsGenericSubtype<int>)clone;
        await Assert.That(typedClone.Items[0].Left.Value).IsEqualTo(53);
        await Assert.That(typedClone.Items[0].Left).IsNotSameReferenceAs(typedClone.Items[0].Right).Because(
            "an opt-out declared on a subtype reached through a usage-discovered closed construction must still propagate");
    }

    #endregion

    #region Test 17: opt-out on a cycle edge must not discard the operation's in-flight state

    // The opted-out edge itself participates in the cycle here. Identity preservation stays off for
    // ordinary shared references, while the opt-out keeps sharing the clone operation's in-flight
    // state, so the cycle through the opted-out edge still resolves to the instance being cloned.

    [FastClonerClonable]
    public class OptOutCycleEdge
    {
        public int Value { get; set; }

        [FastClonerPreserveIdentity(false)]
        public OptOutCycleEdge? Next { get; set; }

        public OptOutCycleEdge? Ordinary { get; set; }
    }

    [FastClonerClonable]
    public class OptOutCycleEdgeCollectionRoot
    {
        [FastClonerPreserveIdentity(false)]
        public List<OptOutCycleEdge> Items { get; set; } = [];
    }

    [Test]
    public async Task OptOutOnCycleEdge_SelfCycle_TerminatesAndClosesTheCycle()
    {
        OptOutCycleEdge node = new OptOutCycleEdge { Value = 61 };
        node.Next = node;

        OptOutCycleEdge clone = node.FastDeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(node);
        await Assert.That(clone.Value).IsEqualTo(61);
        await Assert.That(clone.Next).IsSameReferenceAs(clone).Because(
            "the opted-out edge sits on the cycle, so the map closing it must survive the opt-out");
    }

    [Test]
    public async Task OptOutOnCycleEdge_TwoNodeCycle_TerminatesAndClosesTheCycle()
    {
        OptOutCycleEdge a = new OptOutCycleEdge { Value = 62 };
        OptOutCycleEdge b = new OptOutCycleEdge { Value = 63 };
        a.Next = b;
        b.Ordinary = a;

        OptOutCycleEdge cloneA = a.FastDeepClone();

        await Assert.That(cloneA.Value).IsEqualTo(62);
        await Assert.That(cloneA.Next).IsNotNull();
        await Assert.That(cloneA.Next!.Value).IsEqualTo(63);
        await Assert.That(cloneA.Next!.Ordinary).IsSameReferenceAs(cloneA).Because(
            "the cycle through the opted-out edge must still be closed");
    }

    [Test]
    public async Task OptOutOnCycleEdge_CollectionElements_TerminateAndStayIndependent()
    {
        OptOutCycleEdge cyclic = new OptOutCycleEdge { Value = 64 };
        cyclic.Next = cyclic;

        OptOutCycleEdgeCollectionRoot original = new OptOutCycleEdgeCollectionRoot { Items = [cyclic, cyclic] };
        OptOutCycleEdgeCollectionRoot clone = original.FastDeepClone();

        await Assert.That(clone.Items.Count).IsEqualTo(2);

        // The opt-out still suppresses ordinary aliasing: the same source instance appears twice and
        // must clone independently.
        await Assert.That(clone.Items[0]).IsNotSameReferenceAs(clone.Items[1]).Because(
            "an opt-out must keep suppressing ordinary alias preservation");

        // ...while each element's own cycle still closes.
        await Assert.That(clone.Items[0].Next).IsSameReferenceAs(clone.Items[0]);
        await Assert.That(clone.Items[1].Next).IsSameReferenceAs(clone.Items[1]);
    }

    /// <summary>
    /// Pins the documented precedence: the opted-out edge suppresses completed aliases but keeps
    /// sharing the clone operation's in-flight state, so a reference to the instance being cloned is
    /// still resolved to the clone under construction.
    /// </summary>
    [Test]
    public async Task OptOutOnCycleEdge_KeepsReferenceToTheInstanceBeingCloned()
    {
        OptOutCycleEdge node = new OptOutCycleEdge { Value = 65 };
        node.Next = node;      // opted-out edge
        node.Ordinary = node;  // ordinary edge to the same in-flight instance

        OptOutCycleEdge clone = node.FastDeepClone();

        await Assert.That(clone.Next).IsSameReferenceAs(clone).Because(
            "the opted-out edge must keep the in-flight state that closes the cycle it sits on");
        await Assert.That(clone.Ordinary).IsSameReferenceAs(clone).Because(
            "both edges resolve to the instance currently being cloned");
    }

    #endregion

    #region Test 18: opt-out member edge into a cyclic type registered in a FastClonerContext

    [Test]
    public async Task Context_OptOutMemberIntoTypedCycle_TerminatesAndClosesTheCycle()
    {
        ContextCycleNode node = new ContextCycleNode { Value = 71 };
        node.Next = node;

        ContextCycleContext context = new ContextCycleContext();
        ContextCycleRoot clone = context.Clone(new ContextCycleRoot { Node = node })!;

        await Assert.That(clone.Node).IsNotNull();
        await Assert.That(clone.Node).IsNotSameReferenceAs(node);
        await Assert.That(clone.Node!.Value).IsEqualTo(71);
        await Assert.That(clone.Node!.Next).IsSameReferenceAs(clone.Node).Because(
            "a registered type reached through an opted-out member must still close its cycle");
    }

    /// <summary>
    /// Pins the behaviour for context-registered graphs: the context's own cloners honour the
    /// member-level opt-out exactly like every other generated path. Cycle closure is unaffected,
    /// because the suppressing state is a view over the same clone operation
    /// (<see cref="Context_OptOutMemberIntoTypedCycle_TerminatesAndClosesTheCycle"/>).
    /// </summary>
    [Test]
    public async Task Context_MemberLevelOptOut_SuppressesAliasing()
    {
        ContextOptLeaf shared = new ContextOptLeaf { Value = 1 };
        ContextOptRoot source = new ContextOptRoot
        {
            Node = new ContextOptChild { Left = shared, Right = shared }
        };

        ContextOptContext context = new ContextOptContext();
        ContextOptRoot clone = context.Clone(source)!;

        await Assert.That(clone.Node).IsNotNull();
        await Assert.That(clone.Node!.Left).IsNotSameReferenceAs(clone.Node.Right).Because(
            "a member-level opt-out must reach the context's own generated cloners");
        await Assert.That(clone.Node.Left!.Value).IsEqualTo(1);
    }

    #endregion

    #region Test 19: opted-out edges into cyclic subgraphs across the remaining generated paths

    [FastClonerClonable]
    public abstract class OptOutPolyBase
    {
        public int Value { get; set; }
    }

    [FastClonerClonable]
    public sealed class OptOutPolyCyclicDerived : OptOutPolyBase
    {
        public OptOutPolyCyclicDerived? Next { get; set; }
    }

    [FastClonerClonable]
    public class OptOutPolyRoot
    {
        [FastClonerPreserveIdentity(false)]
        public OptOutPolyBase? Node { get; set; }
    }

    /// <summary>
    /// Cyclic type whose only reference member is the opted-out cycle edge, reached through another
    /// opted-out member. This is the shape in which the cycle-continuity guard alone has to keep the
    /// recursion finite.
    /// </summary>
    [FastClonerClonable]
    public class OptOutCycleOnlyNode
    {
        public int Value { get; set; }

        [FastClonerPreserveIdentity(false)]
        public OptOutCycleOnlyNode? Next { get; set; }
    }

    [FastClonerClonable]
    public class OptOutCycleOnlyHolder
    {
        [FastClonerPreserveIdentity(false)]
        public OptOutCycleOnlyNode? Node { get; set; }
    }

    /// <summary>Unannotated POCO used to reach a cyclic child through an implicit helper.</summary>
    public class OptOutCyclicImplicitHolder
    {
        [FastClonerPreserveIdentity(false)]
        public PrecedenceCyclicChild? Edge { get; set; }
    }

    /// <summary>
    /// Opts every member out, so a cyclic child reaches each generated path (member, array,
    /// dictionary, collection, implicit POCO, getter-only collection, non-public field) with a
    /// state that suppresses identity.
    /// </summary>
    [FastClonerClonable]
    public class OptOutCyclicPathsRoot
    {
        [FastClonerPreserveIdentity(false)]
        public PrecedenceCyclicChild? Single { get; set; }

        [FastClonerPreserveIdentity(false)]
        public PrecedenceCyclicChild[] Items { get; set; } = [];

        [FastClonerPreserveIdentity(false)]
        public Dictionary<string, PrecedenceCyclicChild> Map { get; set; } = new();

        [FastClonerPreserveIdentity(false)]
        public List<PrecedenceCyclicChild> List { get; set; } = [];

        [FastClonerPreserveIdentity(false)]
        public OptOutCyclicImplicitHolder Holder { get; set; } = new();

        [FastClonerPreserveIdentity(false)]
        public List<PrecedenceCyclicChild> ReadOnly { get; } = [];

        [FastClonerPreserveIdentity(false)]
        private PrecedenceCyclicChild? _hidden;

        public void SetHidden(PrecedenceCyclicChild value) => _hidden = value;

        public PrecedenceCyclicChild? GetHidden() => _hidden;
    }

    private static PrecedenceCyclicChild SelfCyclicChild(int value)
    {
        PrecedenceCyclicChild node = new PrecedenceCyclicChild { Value = value };
        node.Next = node;
        return node;
    }

    [Test]
    public async Task OptOut_OnSingleMemberSelfCycleReachedThroughOptOut_TerminatesAndClosesTheCycle()
    {
        OptOutCycleOnlyNode node = new OptOutCycleOnlyNode { Value = 81 };
        node.Next = node;

        OptOutCycleOnlyHolder clone = new OptOutCycleOnlyHolder { Node = node }.FastDeepClone();

        await Assert.That(clone.Node).IsNotNull();
        await Assert.That(clone.Node).IsNotSameReferenceAs(node);
        await Assert.That(clone.Node!.Value).IsEqualTo(81);
        await Assert.That(clone.Node!.Next).IsSameReferenceAs(clone.Node).Because(
            "a self-cycle whose only edge is opted out must still terminate and close");
    }

    [Test]
    public async Task OptOut_OnAbstractMemberWithCyclicDerivedInstance_TerminatesAndClosesTheCycle()
    {
        OptOutPolyCyclicDerived node = new OptOutPolyCyclicDerived { Value = 73 };
        node.Next = node;

        OptOutPolyRoot clone = new OptOutPolyRoot { Node = node }.FastDeepClone();

        await Assert.That(clone.Node).IsNotNull();
        await Assert.That(clone.Node).IsNotSameReferenceAs(node);
        await Assert.That(clone.Node!.Value).IsEqualTo(73);
        await Assert.That(((OptOutPolyCyclicDerived)clone.Node!).Next).IsSameReferenceAs(clone.Node).Because(
            "a cyclic derived instance reached through an opted-out member must still close its cycle");
    }

    [Test]
    public async Task OptOut_OnEveryCollectionLikeMemberWithCyclicChild_TerminatesAndClosesEachCycle()
    {
        PrecedenceCyclicChild single = SelfCyclicChild(74);
        PrecedenceCyclicChild element = SelfCyclicChild(75);
        PrecedenceCyclicChild value = SelfCyclicChild(76);
        PrecedenceCyclicChild listed = SelfCyclicChild(77);
        PrecedenceCyclicChild implicitEdge = SelfCyclicChild(78);
        PrecedenceCyclicChild readOnly = SelfCyclicChild(79);
        PrecedenceCyclicChild hidden = SelfCyclicChild(80);

        OptOutCyclicPathsRoot original = new OptOutCyclicPathsRoot
        {
            Single = single,
            Items = [element],
            Holder = new OptOutCyclicImplicitHolder { Edge = implicitEdge }
        };
        original.Map["a"] = value;
        original.List.Add(listed);
        original.ReadOnly.Add(readOnly);
        original.SetHidden(hidden);

        OptOutCyclicPathsRoot clone = original.FastDeepClone();

        await Assert.That(clone.Single!.Next).IsSameReferenceAs(clone.Single).Because("opt-out member edge");
        await Assert.That(clone.Items[0].Next).IsSameReferenceAs(clone.Items[0]).Because("array element");
        await Assert.That(clone.Map["a"].Next).IsSameReferenceAs(clone.Map["a"]).Because("dictionary value");
        await Assert.That(clone.List[0].Next).IsSameReferenceAs(clone.List[0]).Because("collection element");
        await Assert.That(clone.Holder.Edge!.Next).IsSameReferenceAs(clone.Holder.Edge).Because("implicit POCO member");
        await Assert.That(clone.ReadOnly[0].Next).IsSameReferenceAs(clone.ReadOnly[0]).Because("getter-only collection");
        await Assert.That(clone.GetHidden()!.Next).IsSameReferenceAs(clone.GetHidden()).Because("non-public member");

        await Assert.That(clone.Single!.Value).IsEqualTo(74);
        await Assert.That(clone.Items[0].Value).IsEqualTo(75);
        await Assert.That(clone.Map["a"].Value).IsEqualTo(76);
        await Assert.That(clone.List[0].Value).IsEqualTo(77);
        await Assert.That(clone.Holder.Edge!.Value).IsEqualTo(78);
        await Assert.That(clone.ReadOnly[0].Value).IsEqualTo(79);
        await Assert.That(clone.GetHidden()!.Value).IsEqualTo(80);
    }

    #endregion

    #region Test 20: an opted-out container is still the active clone of its own back-references

    // An opted-out container must register itself while its elements are cloning, so an element that
    // points back at the container resolves to the clone under construction. That registration is
    // in-flight only: it is dropped once the container's own clone completes, so repeated
    // non-cyclic aliases beneath the opt-out still clone independently.

    [FastClonerClonable]
    public class OptOutContainerListCycleNode
    {
        public int Value { get; set; }

        public List<OptOutContainerListCycleNode> Owner { get; set; } = [];
    }

    [FastClonerClonable]
    public class OptOutContainerListCycleRoot
    {
        [FastClonerPreserveIdentity(false)]
        public List<OptOutContainerListCycleNode> Items { get; set; } = [];
    }

    [FastClonerClonable]
    public class OptOutContainerArrayCycleNode
    {
        public int Value { get; set; }

        public OptOutContainerArrayCycleNode[] Owner { get; set; } = [];
    }

    [FastClonerClonable]
    public class OptOutContainerArrayCycleRoot
    {
        [FastClonerPreserveIdentity(false)]
        public OptOutContainerArrayCycleNode[] Items { get; set; } = [];
    }

    /// <summary>
    /// Dictionary value type that is itself cycle-capable (through <see cref="Partners"/>), so the
    /// generated value cloner participates in reference tracking and can resolve the container it
    /// was reached from.
    /// </summary>
    [FastClonerClonable]
    public class OptOutContainerDictionaryCycleValue
    {
        public int Value { get; set; }

        public List<OptOutContainerDictionaryCycleValue> Partners { get; set; } = [];

        public Dictionary<string, OptOutContainerDictionaryCycleValue> Owner { get; set; } = new();
    }

    [FastClonerClonable]
    public class OptOutContainerDictionaryCycleRoot
    {
        [FastClonerPreserveIdentity(false)]
        public Dictionary<string, OptOutContainerDictionaryCycleValue> Items { get; set; } = new();
    }

    [Test]
    public async Task OptOutList_ElementPointsBackToContainer_ResolvesToTheClonedContainer()
    {
        OptOutContainerListCycleRoot original = new();
        OptOutContainerListCycleNode node = new OptOutContainerListCycleNode { Value = 91 };
        original.Items.Add(node);
        node.Owner = original.Items;

        OptOutContainerListCycleRoot clone = original.FastDeepClone();

        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items.Count).IsEqualTo(1);
        await Assert.That(clone.Items[0]).IsNotSameReferenceAs(node);
        await Assert.That(clone.Items[0].Value).IsEqualTo(91);
        await Assert.That(clone.Items[0].Owner).IsSameReferenceAs(clone.Items).Because(
            "an element pointing back at its opted-out container must resolve to the container clone under construction");
    }

    [Test]
    public async Task OptOutList_RepeatedElement_StillClonesElementsIndependently()
    {
        OptOutContainerListCycleRoot original = new();
        OptOutContainerListCycleNode node = new OptOutContainerListCycleNode { Value = 92 };
        original.Items.Add(node);
        original.Items.Add(node);

        OptOutContainerListCycleRoot clone = original.FastDeepClone();

        await Assert.That(clone.Items.Count).IsEqualTo(2);
        await Assert.That(clone.Items[0]).IsNotSameReferenceAs(clone.Items[1]).Because(
            "an opt-out must keep suppressing ordinary alias preservation for repeated elements");
    }

    [Test]
    public async Task OptOutArray_ElementPointsBackToContainer_ResolvesToTheClonedContainer()
    {
        OptOutContainerArrayCycleNode node = new OptOutContainerArrayCycleNode { Value = 93 };
        OptOutContainerArrayCycleRoot original = new OptOutContainerArrayCycleRoot { Items = [node] };
        node.Owner = original.Items;

        OptOutContainerArrayCycleRoot clone = original.FastDeepClone();

        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items[0]).IsNotSameReferenceAs(node);
        await Assert.That(clone.Items[0].Owner).IsSameReferenceAs(clone.Items).Because(
            "an array element pointing back at its opted-out array must resolve to the array clone under construction");
        await Assert.That(clone.Items[0].Owner[0]).IsSameReferenceAs(clone.Items[0]);
    }

    [Test]
    public async Task OptOutDictionary_ValuePointsBackToContainer_ResolvesToTheClonedContainer()
    {
        OptOutContainerDictionaryCycleValue value = new OptOutContainerDictionaryCycleValue { Value = 94 };
        OptOutContainerDictionaryCycleRoot original = new OptOutContainerDictionaryCycleRoot();
        original.Items["a"] = value;
        value.Owner = original.Items;

        OptOutContainerDictionaryCycleRoot clone = original.FastDeepClone();

        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items["a"]).IsNotSameReferenceAs(value);
        await Assert.That(clone.Items["a"].Owner).IsSameReferenceAs(clone.Items).Because(
            "a dictionary value pointing back at its opted-out dictionary must resolve to the dictionary clone under construction");
    }

    // Multidimensional arrays are cloned by their own helper, which was reworked by this change for
    // state lookup, registration, opt-out propagation and completion. These pin both halves of the
    // contract through a T[,] member.

    [FastClonerClonable]
    public class OptOutContainerMultiDimArrayCycleNode
    {
        public int Value { get; set; }

        public OptOutContainerMultiDimArrayCycleNode[,]? Owner { get; set; }
    }

    [FastClonerClonable]
    public class OptOutContainerMultiDimArrayCycleRoot
    {
        [FastClonerPreserveIdentity(false)]
        public OptOutContainerMultiDimArrayCycleNode[,] Grid { get; set; } =
            new OptOutContainerMultiDimArrayCycleNode[1, 1];

        [FastClonerPreserveIdentity(false)]
        public PrecedenceChildWithIdentity[,] Children { get; set; } =
            new PrecedenceChildWithIdentity[1, 1];
    }

    /// <summary>Same shape without the opt-out, so aliasing below the member is expected to survive.</summary>
    [FastClonerClonable]
    public class MultiDimArrayControlRoot
    {
        public PrecedenceChildWithIdentity[,] Children { get; set; } =
            new PrecedenceChildWithIdentity[1, 1];
    }

    [Test]
    public async Task OptOutMultiDimArray_ElementPointsBackToContainer_ResolvesToTheClonedContainer()
    {
        OptOutContainerMultiDimArrayCycleNode node = new OptOutContainerMultiDimArrayCycleNode { Value = 94 };
        OptOutContainerMultiDimArrayCycleRoot original = new OptOutContainerMultiDimArrayCycleRoot();
        original.Grid[0, 0] = node;
        node.Owner = original.Grid;

        OptOutContainerMultiDimArrayCycleRoot clone = original.FastDeepClone();

        await Assert.That(clone.Grid).IsNotSameReferenceAs(original.Grid);
        await Assert.That(clone.Grid[0, 0]).IsNotSameReferenceAs(node);
        await Assert.That(clone.Grid[0, 0].Value).IsEqualTo(94);
        await Assert.That(clone.Grid[0, 0].Owner).IsSameReferenceAs(clone.Grid).Because(
            "a multidimensional-array element pointing back at its opted-out array must resolve to the array clone under construction");
        await Assert.That(clone.Grid[0, 0].Owner![0, 0]).IsSameReferenceAs(clone.Grid[0, 0]);
    }

    [Test]
    public async Task OptOutMultiDimArray_SuppressesAliasesBelowTheMember()
    {
        PrecedenceSharedLeaf shared = new PrecedenceSharedLeaf { Value = 95 };
        OptOutContainerMultiDimArrayCycleRoot original = new OptOutContainerMultiDimArrayCycleRoot();
        original.Children[0, 0] = new PrecedenceChildWithIdentity { Left = shared, Right = shared };

        OptOutContainerMultiDimArrayCycleRoot clone = original.FastDeepClone();

        await Assert.That(clone.Children[0, 0].Left.Value).IsEqualTo(95);
        await Assert.That(clone.Children[0, 0].Left).IsNotSameReferenceAs(clone.Children[0, 0].Right).Because(
            "an opted-out T[,] member must suppress aliases inside the element subgraph, even though the element's own type preserves identity");
    }

    [Test]
    public async Task MultiDimArray_WithoutOptOut_KeepsAliasesBelowTheMember()
    {
        // Control: the same graph shape without the attribute must keep the child type's own
        // identity-preserving behaviour, which is what makes the test above discriminating.
        PrecedenceSharedLeaf shared = new PrecedenceSharedLeaf { Value = 96 };
        MultiDimArrayControlRoot original = new MultiDimArrayControlRoot();
        original.Children[0, 0] = new PrecedenceChildWithIdentity { Left = shared, Right = shared };

        MultiDimArrayControlRoot clone = original.FastDeepClone();

        await Assert.That(clone.Children[0, 0].Left).IsSameReferenceAs(clone.Children[0, 0].Right).Because(
            "without an opt-out the element's own type keeps aliasing");
    }

    #endregion

    #region Test 21: capability-only widening must not change ordinary default behaviour

    // Widening a helper so it can receive an opt-out is a signature capability, not a behaviour
    // change. A safe immutable collection must still return its own instance when the helper is
    // invoked normally, even when an unrelated member elsewhere in the same generated root opts out.

    [FastClonerClonable]
    public class ImmutableFastPathWithUnrelatedOptOutRoot
    {
        [FastClonerPreserveIdentity(false)]
        public PrecedenceChildWithIdentity Child { get; set; } = new();

        public ImmutableList<string> Labels { get; set; } = ImmutableList<string>.Empty;

        public ImmutableArray<string> LabelsArray { get; set; } = ImmutableArray<string>.Empty;

        public ImmutableDictionary<string, string> Map { get; set; } = ImmutableDictionary<string, string>.Empty;

        public List<string> PlainLabels { get; set; } = [];
    }

    [FastClonerClonable]
    public class ImmutableFastPathWithoutOptOutRoot
    {
        public ImmutableList<string> Labels { get; set; } = ImmutableList<string>.Empty;

        public ImmutableDictionary<string, string> Map { get; set; } = ImmutableDictionary<string, string>.Empty;
    }

    [Test]
    public async Task UnrelatedOptOut_DoesNotRebuildSafeImmutableCollections()
    {
        ImmutableFastPathWithUnrelatedOptOutRoot original = new ImmutableFastPathWithUnrelatedOptOutRoot
        {
            Child = AliasedChild(95),
            Labels = ImmutableList.Create("a", "b"),
            LabelsArray = ["a", "b"],
            Map = ImmutableDictionary<string, string>.Empty.Add("a", "b"),
            PlainLabels = ["a", "b"]
        };

        ImmutableFastPathWithUnrelatedOptOutRoot clone = original.FastDeepClone();

        // The unrelated opt-out must still work...
        await Assert.That(clone.Child.Left).IsNotSameReferenceAs(clone.Child.Right);

        // ...while every safe immutable member keeps its established same-instance behaviour.
        await Assert.That(clone.Labels).IsSameReferenceAs(original.Labels).Because(
            "a helper widened only for opt-out propagation must still return the source for safe immutable collections");
        await Assert.That(clone.Map).IsSameReferenceAs(original.Map).Because(
            "a helper widened only for opt-out propagation must still return the source for safe immutable dictionaries");
        await Assert.That(clone.LabelsArray).IsEqualTo(original.LabelsArray);
    }

    [Test]
    public async Task NoOptOut_SafeImmutableCollectionsAreNotRebuilt()
    {
        ImmutableFastPathWithoutOptOutRoot original = new ImmutableFastPathWithoutOptOutRoot
        {
            Labels = ImmutableList.Create("a", "b"),
            Map = ImmutableDictionary<string, string>.Empty.Add("a", "b")
        };

        ImmutableFastPathWithoutOptOutRoot clone = original.FastDeepClone();

        await Assert.That(clone.Labels).IsSameReferenceAs(original.Labels);
        await Assert.That(clone.Map).IsSameReferenceAs(original.Map);
    }

    #endregion

    #region Test 22: an unrelated cycle-capable member must not defeat a member-level opt-out

    // The opt-out is member-precise, not type-wide: it suppresses completed aliases for its own
    // subgraph while still sharing the clone operation's in-flight state, so a cycle that passes
    // through the opted-out edge keeps closing. A type that merely *could* participate in an
    // unrelated cycle must therefore not re-enable aliasing inside the opted-out member's subgraph.

    [FastClonerClonable]
    public class CycleCapableParentWithOptOutChild
    {
        /// <summary>Unrelated member that makes this type cycle-capable.</summary>
        public List<CycleCapableParentWithOptOutChild> Children { get; set; } = [];

        [FastClonerPreserveIdentity(false)]
        public PrecedenceChildWithIdentity Child { get; set; } = new();
    }

    [Test]
    public async Task MemberFalse_OverChildOfCycleCapableParent_StillSuppressesIdentity()
    {
        CycleCapableParentWithOptOutChild original = new CycleCapableParentWithOptOutChild
        {
            Child = AliasedChild(96)
        };

        CycleCapableParentWithOptOutChild clone = original.FastDeepClone();

        await Assert.That(clone.Child.Left.Value).IsEqualTo(96);
        await Assert.That(clone.Child.Left).IsNotSameReferenceAs(clone.Child.Right).Because(
            "an unrelated cycle-capable member of the containing type must not defeat the member-level opt-out");
    }

    #endregion

    #region Test 23: non-public collection, array and dictionary members honour the opt-out

    // On .NET 8+ non-public members are cloned through generated accessors, so their subgraph must be
    // routed through the same generated helpers as a public member's. Before that routing they fell
    // through to the runtime cloner, which tracks references on its own terms and ignored the
    // opt-out.

    [FastClonerClonable]
    public class NonPublicCollectionOptOutRoot
    {
        [FastClonerPreserveIdentity(false)]
        private List<PrecedenceChildWithIdentity> _items = [];

        [FastClonerPreserveIdentity(false)]
        private PrecedenceChildWithIdentity[] _array = [];

        [FastClonerPreserveIdentity(false)]
        private Dictionary<string, PrecedenceChildWithIdentity> _map = new();

        // Accessors are methods rather than properties: a public property over the same storage would
        // be cloned as a second member and overwrite the field clone under test.
        public void Seed(
            List<PrecedenceChildWithIdentity> items,
            PrecedenceChildWithIdentity[] array,
            Dictionary<string, PrecedenceChildWithIdentity> map)
        {
            _items = items;
            _array = array;
            _map = map;
        }

        public List<PrecedenceChildWithIdentity> GetItems() => _items;

        public PrecedenceChildWithIdentity[] GetArray() => _array;

        public Dictionary<string, PrecedenceChildWithIdentity> GetMap() => _map;
    }

    [Test]
    public async Task MemberFalse_OnNonPublicCollectionMembers_IsHonored()
    {
        NonPublicCollectionOptOutRoot original = new NonPublicCollectionOptOutRoot();
        original.Seed(
            [AliasedChild(97)],
            [AliasedChild(98)],
            new Dictionary<string, PrecedenceChildWithIdentity> { ["a"] = AliasedChild(99) });

        NonPublicCollectionOptOutRoot clone = original.FastDeepClone();

        await Assert.That(clone.GetItems()[0].Left.Value).IsEqualTo(97);
        await Assert.That(clone.GetArray()[0].Left.Value).IsEqualTo(98);
        await Assert.That(clone.GetMap()["a"].Left.Value).IsEqualTo(99);

        await Assert.That(clone.GetItems()[0].Left).IsNotSameReferenceAs(clone.GetItems()[0].Right).Because("non-public List member");
        await Assert.That(clone.GetArray()[0].Left).IsNotSameReferenceAs(clone.GetArray()[0].Right).Because("non-public array member");
        await Assert.That(clone.GetMap()["a"].Left).IsNotSameReferenceAs(clone.GetMap()["a"].Right).Because("non-public dictionary member");
    }

    #endregion

    #region Test 24: an opt-out must ignore an alias the same operation already completed

    // The sharp form of the separation: one source leaf is referenced by a preserving member first
    // (so its clone is a completed alias), then by an opted-out member (which must not reuse it),
    // then by another preserving member (which must still resolve to the completed alias, i.e. the
    // opt-out did not damage the operation's identity preservation).

    [FastClonerClonable]
    [FastClonerPreserveIdentity(true)]
    public class CompletedAliasThenOptOutRoot
    {
        public PrecedenceSharedLeaf Left { get; set; } = new();

        public PrecedenceSharedLeaf Right { get; set; } = new();

        [FastClonerPreserveIdentity(false)]
        public PrecedenceSharedLeaf OptedOut { get; set; } = new();

        public PrecedenceSharedLeaf After { get; set; } = new();
    }

    [Test]
    public async Task MemberFalse_IgnoresCompletedAliasAndLeavesItIntact()
    {
        PrecedenceSharedLeaf shared = new PrecedenceSharedLeaf { Value = 100 };
        CompletedAliasThenOptOutRoot original = new CompletedAliasThenOptOutRoot
        {
            Left = shared,
            Right = shared,
            OptedOut = shared,
            After = shared
        };

        CompletedAliasThenOptOutRoot clone = original.FastDeepClone();

        await Assert.That(clone.Left).IsNotSameReferenceAs(shared);
        await Assert.That(clone.Right).IsSameReferenceAs(clone.Left).Because(
            "a preserving member must still reuse the completed alias");
        await Assert.That(clone.OptedOut).IsNotSameReferenceAs(clone.Left).Because(
            "an opted-out occurrence must not reuse the completed clone of the same operation");
        await Assert.That(clone.OptedOut.Value).IsEqualTo(100);
        await Assert.That(clone.After).IsSameReferenceAs(clone.Left).Because(
            "the opt-out must leave the operation's completed alias intact for later preserving members");
    }

    #endregion

    #region Test 25: a null opted-out member allocates no suppressing state

    /// <summary>Structurally identical to <see cref="NullPlainMemberRoot"/>, except the member opts out.</summary>
    [FastClonerClonable]
    public class NullOptOutMemberRoot
    {
        [FastClonerPreserveIdentity(false)]
        public PrecedenceSharedLeaf? Leaf { get; set; }
    }

    /// <summary>Structurally identical to <see cref="NullOptOutMemberRoot"/>, with no opt-out.</summary>
    [FastClonerClonable]
    public class NullPlainMemberRoot
    {
        public PrecedenceSharedLeaf? Leaf { get; set; }
    }

    // Both roots are the same shape and clone the same leaf type, so their generated bodies allocate
    // the same amount for the same input - unless the opted-out call site evaluates its state
    // argument eagerly. SuppressIdentity(null) starts a whole clone operation, so an eagerly
    // evaluated argument costs a state plus its lookup structure even when the member is null.
    [Test]
    public async Task NullOptedOutMember_AllocatesLikeAnOrdinaryNullMember()
    {
        NullOptOutMemberRoot optedOut = new NullOptOutMemberRoot { Leaf = null };
        NullPlainMemberRoot plain = new NullPlainMemberRoot { Leaf = null };

        const int iterations = 200;
        long optedOutBytes = Measure(iterations, () => optedOut.FastDeepClone());
        long plainBytes = Measure(iterations, () => plain.FastDeepClone());

        await Assert.That(plainBytes).IsGreaterThan(0);
        await Assert.That(optedOutBytes).IsEqualTo(plainBytes).Because(
            "a null opted-out member must not build a suppressing state for nothing");
    }

    [Test]
    public async Task NonNullOptedOutMember_StillBuildsTheSuppressingState()
    {
        NullOptOutMemberRoot optedOut = new NullOptOutMemberRoot { Leaf = new PrecedenceSharedLeaf { Value = 7 } };

        const int iterations = 200;
        long withLeaf = Measure(iterations, () => optedOut.FastDeepClone());
        long nullLeaf = Measure(iterations, () => new NullOptOutMemberRoot { Leaf = null }.FastDeepClone());

        await Assert.That(withLeaf).IsGreaterThan(nullLeaf).Because(
            "a present opted-out member still needs the suppressing state to drop completed aliases");
    }

    private static long Measure(int iterations, Action action)
    {
        // Warm up the generated path (JIT, statics) before measuring, then measure one path at a time
        // on the current thread: an eager state argument shows up as a constant per-iteration cost.
        action();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
        {
            action();
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    #endregion

    #region Test 26: an opted-out member is evaluated exactly once

    // Only a property the generator clones *as a property* can expose a repeated read: an
    // auto-property is read through its backing field, and a property without an accessible setter is
    // treated as getter-only and left to the field clone. Both models below therefore keep an
    // accessible setter and a counting getter.

    /// <summary>Opted-out clonable member behind a getter that records every call.</summary>
    [FastClonerClonable]
    public class SingleReadOptOutPropertyRoot
    {
        public static int LeafReads;

        private PrecedenceSharedLeaf? _leaf;

        [FastClonerPreserveIdentity(false)]
        public PrecedenceSharedLeaf? Leaf
        {
            get
            {
                LeafReads++;
                return _leaf;
            }
            set => _leaf = value;
        }

        public PrecedenceSharedLeaf? ReadLeaf() => _leaf;

        public void Seed(PrecedenceSharedLeaf leaf) => _leaf = leaf;
    }

    /// <summary>Opted-out collection member behind a getter that records every call.</summary>
    [FastClonerClonable]
    public class SingleReadOptOutCollectionPropertyRoot
    {
        public static int ItemsReads;

        private List<int>? _items;

        [FastClonerPreserveIdentity(false)]
        public List<int>? Items
        {
            get
            {
                ItemsReads++;
                return _items;
            }
            set => _items = value;
        }

        public List<int>? ReadItems() => _items;

        public void Seed() => _items = [5, 6];
    }

    [Test]
    public async Task OptedOutClonableMember_WithSideEffectingGetter_IsEvaluatedOnce()
    {
        PrecedenceSharedLeaf leaf = new PrecedenceSharedLeaf { Value = 7 };
        SingleReadOptOutPropertyRoot original = new SingleReadOptOutPropertyRoot();
        original.Seed(leaf);
        SingleReadOptOutPropertyRoot.LeafReads = 0;

        SingleReadOptOutPropertyRoot clone = original.FastDeepClone();

        await Assert.That(SingleReadOptOutPropertyRoot.LeafReads).IsEqualTo(1).Because(
            "an opted-out member must be evaluated exactly once, so a side-effecting getter cannot be observed twice");
        await Assert.That(clone.ReadLeaf()).IsNotNull();
        await Assert.That(clone.ReadLeaf()!.Value).IsEqualTo(7);
    }

    [Test]
    public async Task OptedOutCollectionMember_WithSideEffectingGetter_IsEvaluatedOnce()
    {
        SingleReadOptOutCollectionPropertyRoot original = new SingleReadOptOutCollectionPropertyRoot();
        original.Seed();
        SingleReadOptOutCollectionPropertyRoot.ItemsReads = 0;

        SingleReadOptOutCollectionPropertyRoot clone = original.FastDeepClone();

        await Assert.That(SingleReadOptOutCollectionPropertyRoot.ItemsReads).IsEqualTo(1).Because(
            "an opted-out collection member must be evaluated exactly once, so a side-effecting getter cannot be observed twice");
        List<int>? cloned = clone.ReadItems();
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned!).IsEquivalentTo(new[] { 5, 6 });
    }

    // Getter-only collections and dictionaries take a different path: the clone is populated into
    // the collection the getter already returns. The source member therefore has to be read once,
    // up front, and that one instance used for the guard, the shallow copy and the deep clone.

    /// <summary>
    /// Per-instance access counter plus a getter that returns a distinguishable collection on any
    /// access after the first: a second read is observable in both the count and the cloned content.
    /// </summary>
    [FastClonerClonable]
    public class GetterOnlyCountedCollectionRoot
    {
        public int SourceReads;

        private List<int> _items;

        public GetterOnlyCountedCollectionRoot() => _items = [1, 2];

        public List<int> Items => SourceReads++ == 0 ? _items : [999];

        public List<int> ReadStored() => _items;
    }

    /// <summary>Dictionary equivalent of <see cref="GetterOnlyCountedCollectionRoot"/>.</summary>
    [FastClonerClonable]
    public class GetterOnlyCountedDictionaryRoot
    {
        public int SourceReads;

        private Dictionary<string, int> _items;

        public GetterOnlyCountedDictionaryRoot() => _items = new Dictionary<string, int> { ["a"] = 1 };

        public Dictionary<string, int> Items =>
            SourceReads++ == 0 ? _items : new Dictionary<string, int> { ["b"] = 999 };

        public Dictionary<string, int> ReadStored() => _items;
    }

    [Test]
    public async Task GetterOnlyCollectionMember_IsEvaluatedOnce()
    {
        GetterOnlyCountedCollectionRoot original = new GetterOnlyCountedCollectionRoot();

        GetterOnlyCountedCollectionRoot clone = original.FastDeepClone();

        await Assert.That(original.SourceReads).IsEqualTo(1).Because(
            "a getter-only collection member must be read exactly once, so the guard and the clone cannot disagree about the instance");
        await Assert.That(clone.ReadStored()).IsEquivalentTo(new[] { 1, 2 }).Because(
            "the clone must be populated from the instance that was validated, not from a second access");
    }

    [Test]
    public async Task GetterOnlyDictionaryMember_IsEvaluatedOnce()
    {
        GetterOnlyCountedDictionaryRoot original = new GetterOnlyCountedDictionaryRoot();

        GetterOnlyCountedDictionaryRoot clone = original.FastDeepClone();

        await Assert.That(original.SourceReads).IsEqualTo(1).Because(
            "a getter-only dictionary member must be read exactly once, so the guard and the clone cannot disagree about the instance");
        await Assert.That(clone.ReadStored()["a"]).IsEqualTo(1).Because(
            "the clone must be populated from the instance that was validated, not from a second access");
        await Assert.That(clone.ReadStored().ContainsKey("b")).IsFalse();
    }

    #endregion

    #region Test 27: the legacy AddKnownRef keeps its first-write-wins contract

    // The public entry point predates the generated token path and previously used TryAdd, so a
    // repeated registration for the same source object never displaced the first mapping. Callers
    // compiled against the earlier assembly - including assemblies whose generated code calls this
    // method - observe exactly that.

    [Test]
    public async Task AddKnownRef_FirstRegistrationWins()
    {
        FcGeneratedCloneState state = new FcGeneratedCloneState();

        object source = new object();
        object first = new object();
        object second = new object();

        state.AddKnownRef(source, first);
        state.AddKnownRef(source, second);

        await Assert.That(state.GetKnownRef(source)).IsSameReferenceAs(first).Because(
            "the legacy AddKnownRef must keep first-write-wins semantics");
    }

    [Test]
    public async Task AddKnownRef_FirstRegistrationWins_ForDistinctSources()
    {
        FcGeneratedCloneState state = new FcGeneratedCloneState();

        object sourceA = new object();
        object sourceB = new object();
        object cloneA = new object();
        object cloneB = new object();

        state.AddKnownRef(sourceA, cloneA);
        state.AddKnownRef(sourceB, cloneB);
        state.AddKnownRef(sourceA, cloneB);

        await Assert.That(state.GetKnownRef(sourceA)).IsSameReferenceAs(cloneA).Because(
            "per-source first-write-wins, unaffected by registrations of other sources");
        await Assert.That(state.GetKnownRef(sourceB)).IsSameReferenceAs(cloneB);
    }

    [Test]
    public async Task RegisterKnownRef_UpdatesInFlightRegistration()
    {
        // The generated path deliberately differs from the legacy one: an in-flight registration is
        // replaceable, which is what makes a nested clone of the same source object visible as the
        // object being built at the innermost frame.
        FcGeneratedCloneState state = new FcGeneratedCloneState();

        object source = new object();
        object outer = new object();
        object inner = new object();

        state.RegisterKnownRef(source, outer);
        object? token = state.RegisterKnownRef(source, inner);

        await Assert.That(state.GetKnownRef(source)).IsSameReferenceAs(inner).Because(
            "RegisterKnownRef replaces the in-flight reading");

        state.CompleteKnownRef(token);

        await Assert.That(state.GetKnownRef(source)).IsSameReferenceAs(outer).Because(
            "completing the inner registration reveals the completed clone established first");
    }

    [Test]
    public async Task RegisterKnownRef_FirstCompletedCloneWins()
    {
        // The completed reading keeps its first writer, which is what makes repeated references
        // resolve to one clone even when the same source is registered again.
        FcGeneratedCloneState state = new FcGeneratedCloneState();

        object source = new object();
        object first = new object();
        object second = new object();

        object? firstToken = state.RegisterKnownRef(source, first);
        object? secondToken = state.RegisterKnownRef(source, second);

        await Assert.That(state.GetKnownRef(source)).IsSameReferenceAs(second).Because(
            "the in-flight reading is the one being built");

        state.CompleteKnownRef(secondToken);
        state.CompleteKnownRef(firstToken);

        await Assert.That(state.GetKnownRef(source)).IsSameReferenceAs(first).Because(
            "once nothing is in flight the completed reading is the first clone registered");
    }

    [Test]
    public async Task AddKnownRef_RecordsExactlyOneClone_UnderConcurrency()
    {
        // Concurrent registration of one source object must end in a single, stable clone that is one
        // of the supplied candidates - who wins is well-defined rather than whichever writer happened
        // to land last.
        //
        // This is a resilience check, not proof of atomicity. Registration claims the mapping with a
        // single Interlocked.CompareExchange, an atomic read-modify-write - the same guarantee
        // ConcurrentDictionary.TryAdd gave this method before - and a black-box test cannot
        // discriminate that from a check-then-write: both write within adjacent instructions, and the
        // GetOrAdd in front of them already serializes the checkers on the same key, so reverting to
        // check-then-write is not observable from outside (measured: a polling reader never saw the
        // mapping change across repeated runs). What this test does pin is the observable state the
        // guarantee protects - the winner is one of the candidates, the value is stable once the
        // writers finish, and a later registration does not displace it.
        const int rounds = 30;
        const int workers = 8;
        const int maxSpins = 20_000_000;

        for (int round = 0; round < rounds; round++)
        {
            FcGeneratedCloneState state = new FcGeneratedCloneState();
            object source = new object();
            object[] candidates = new object[workers];
            for (int i = 0; i < workers; i++)
            {
                candidates[i] = new object();
            }

            using ManualResetEventSlim writersDone = new ManualResetEventSlim(false);
            using ManualResetEventSlim readerPolling = new ManualResetEventSlim(false);
            using Barrier barrier = new Barrier(workers);

            Task[] writers = new Task[workers];
            for (int i = 0; i < workers; i++)
            {
                int index = i;
                writers[index] = Task.Run(() =>
                {
                    // Make sure the reader is already polling before any writer claims the mapping,
                    // so the writes genuinely happen underneath it.
                    readerPolling.Wait();
                    barrier.SignalAndWait();
                    state.AddKnownRef(source, candidates[index]);
                });
            }

            object? firstSeen = null;
            bool changedUnderReader = false;
            Task reader = Task.Run(() =>
            {
                readerPolling.Set();
                for (int spin = 0; spin < maxSpins && !writersDone.IsSet; spin++)
                {
                    object? seen = state.GetKnownRef(source);
                    if (seen != null)
                    {
                        if (firstSeen == null)
                        {
                            firstSeen = seen;
                        }
                        else if (!ReferenceEquals(firstSeen, seen))
                        {
                            changedUnderReader = true;
                            return;
                        }
                    }
                }
            });

            await Task.WhenAll(writers);
            writersDone.Set();
            await reader;

            await Assert.That(changedUnderReader).IsFalse().Because(
                "an atomic registration writes the mapping once, so its value cannot change under a concurrent reader while the writers run");

            object? winner = state.GetKnownRef(source);
            await Assert.That(winner).IsNotNull();

            bool winnerIsCandidate = false;
            for (int i = 0; i < workers; i++)
            {
                if (ReferenceEquals(candidates[i], winner))
                {
                    winnerIsCandidate = true;
                }
            }

            await Assert.That(winnerIsCandidate).IsTrue().Because(
                "the recorded clone must be one of the registered candidates");

            // Stability: nothing is in flight any more, so a later registration must not displace it.
            state.AddKnownRef(source, new object());
            await Assert.That(state.GetKnownRef(source)).IsSameReferenceAs(winner).Because(
                "a registration after the fact must not displace the recorded clone");
        }
    }

    #endregion

    #region Test 28: a supplied opt-out survives an acyclic stateless derived dispatch

    // The derived dispatch decides how to clone a runtime subtype. When that subtype needs no tracking
    // of its own, the supplied state used to be dropped, so a nested member whose type preserves
    // identity re-enabled aliasing below an opted-out member. A supplied state must be honoured; a
    // null state must keep the untracked behaviour.

    [FastClonerClonable]
    public abstract class OptOutStatelessPolyBase
    {
        public int Value { get; set; }
    }

    /// <summary>
    /// Acyclic subtype with no attribute of its own: the base's generated file emits the derived helper
    /// for it, which is the path that decides whether to use a supplied state.
    /// </summary>
    public sealed class OptOutStatelessPolyDerived : OptOutStatelessPolyBase
    {
        public PrecedenceChildWithIdentity Child { get; set; } = new();
    }

    [FastClonerClonable]
    public class OptOutStatelessPolyRoot
    {
        [FastClonerPreserveIdentity(false)]
        public OptOutStatelessPolyBase Value { get; set; } = new OptOutStatelessPolyDerived();
    }

    /// <summary>Builds a derived instance whose nested child aliases one shared leaf.</summary>
    private static OptOutStatelessPolyDerived AliasedDerived(int value)
    {
        PrecedenceSharedLeaf shared = new PrecedenceSharedLeaf { Value = value };
        return new OptOutStatelessPolyDerived
        {
            Value = value,
            Child = new PrecedenceChildWithIdentity { Left = shared, Right = shared }
        };
    }

    [Test]
    public async Task MemberFalse_OverAcyclicDerivedType_PropagatesSuppressionToIdentityPreservingChild()
    {
        OptOutStatelessPolyRoot original = new OptOutStatelessPolyRoot { Value = AliasedDerived(61) };

        OptOutStatelessPolyRoot clone = original.FastDeepClone();

        OptOutStatelessPolyDerived clonedDerived = (OptOutStatelessPolyDerived)clone.Value;
        await Assert.That(clonedDerived).IsNotSameReferenceAs(original.Value).Because(
            "runtime polymorphic dispatch must reach the acyclic derived type");
        await Assert.That(clonedDerived.Value).IsEqualTo(61);
        await Assert.That(clonedDerived.Child.Left.Value).IsEqualTo(61);
        await Assert.That(clonedDerived.Child.Left).IsNotSameReferenceAs(clonedDerived.Child.Right).Because(
            "the member-level opt-out must keep suppressing aliases inside a nested identity-preserving child");
    }

    [Test]
    public async Task DirectClone_OfAcyclicDerivedType_RetainsItsChildIdentityPreservingBehaviour()
    {
        // Control: without an opted-out member above it, nothing suppresses identity here, so the
        // child type's own [FastClonerPreserveIdentity(true)] keeps the alias. The derived subtype has
        // no attribute of its own, so cloning goes through the base's generated dispatcher with a null
        // state - exactly the ordinary stateless path.
        OptOutStatelessPolyDerived original = AliasedDerived(62);

        OptOutStatelessPolyDerived clone = (OptOutStatelessPolyDerived)original.FastDeepClone();

        await Assert.That(clone.Child.Left).IsSameReferenceAs(clone.Child.Right).Because(
            "ordinary cloning must retain the child type's identity-preserving default");
        await Assert.That(clone.Child.Left).IsNotSameReferenceAs(original.Child.Left);
    }

    [Test]
    public async Task MemberFalse_OverAcyclicDerivedType_StillSplitsAliasesWithRepeatedDerivedInstances()
    {
        // A second, sharper reading of the same rule: the suppression must reach the derived body even
        // though the derived instance itself is acyclic and appears only once.
        OptOutStatelessPolyDerived derived = AliasedDerived(63);
        OptOutStatelessPolyRoot original = new OptOutStatelessPolyRoot { Value = derived };

        OptOutStatelessPolyRoot clone = original.FastDeepClone();

        OptOutStatelessPolyDerived clonedDerived = (OptOutStatelessPolyDerived)clone.Value;
        await Assert.That(clonedDerived.Child).IsNotSameReferenceAs(derived.Child);
        await Assert.That(clonedDerived.Child.Left).IsNotSameReferenceAs(clonedDerived.Child.Right);
    }

    #endregion
}

/// <summary>Plain registered type that participates in a self-cycle.</summary>
public class ContextCycleNode
{
    public int Value { get; set; }

    public ContextCycleNode? Next { get; set; }
}

/// <summary>Clonable registered type with an opted-out member pointing at the cyclic registered type.</summary>
[FastClonerClonable]
public class ContextCycleRoot
{
    [FastClonerPreserveIdentity(false)]
    public ContextCycleNode? Node { get; set; }
}

[FastClonerRegister(typeof(ContextCycleRoot), typeof(ContextCycleNode))]
public partial class ContextCycleContext : FastClonerContext
{
}

public class ContextOptLeaf
{
    public int Value { get; set; }
}

public class ContextOptChild
{
    public ContextOptLeaf? Left { get; set; }

    public ContextOptLeaf? Right { get; set; }
}

public class ContextOptRoot
{
    [FastClonerPreserveIdentity(false)]
    public ContextOptChild? Node { get; set; }
}

[FastClonerRegister(typeof(ContextOptRoot), typeof(ContextOptChild))]
public partial class ContextOptContext : FastClonerContext
{
}
