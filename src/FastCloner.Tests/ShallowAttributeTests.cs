using FastCloner.Code;
using FastCloner.SourceGenerator.Shared;

namespace FastCloner.Tests;

/// <summary>
/// Tests for the [FastClonerShallow] attribute which marks members for shallow cloning
/// instead of deep cloning. This is useful for parent references and shared state.
/// Tests cover both reflection-based and source-generated (AOT) cloning.
/// </summary>
[TestFixture]
public class ShallowAttributeTests
{
    #region Test Classes for Reflection-based Cloning

    /// <summary>
    /// A parent class that is referenced by children but should not be deep cloned.
    /// </summary>
    public class ParentObject
    {
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
        public List<ChildWithShallowParent> Children { get; set; } = [];
    }

    /// <summary>
    /// A child class that has a shallow-cloned reference to its parent.
    /// </summary>
    public class ChildWithShallowParent
    {
        [FastClonerShallow]
        public ParentObject? Parent { get; set; }
        
        public string ChildName { get; set; } = string.Empty;
        public int ChildId { get; set; }
    }

    /// <summary>
    /// Class with mix of shallow and deep cloned members.
    /// </summary>
    public class MixedCloneClass
    {
        [FastClonerShallow]
        public SharedState? SharedData { get; set; }
        
        public OwnedData? OwnData { get; set; }
        
        public string Name { get; set; } = string.Empty;
    }

    public class SharedState
    {
        public string ConfigValue { get; set; } = string.Empty;
        public int Version { get; set; }
    }

    public class OwnedData
    {
        public string Value { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>
    /// Class with shallow-cloned field instead of property.
    /// </summary>
    public class ClassWithShallowField
    {
        [FastClonerShallow]
        public SharedState? SharedField;
        
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Class with shallow-cloned collection (reference preserved, not cloned).
    /// </summary>
    public class ClassWithShallowCollection
    {
        [FastClonerShallow]
        public List<int>? SharedList { get; set; }
        
        public List<int>? OwnList { get; set; }
    }

    #endregion

    #region Test Classes for Source Generator (AOT) Cloning

    /// <summary>
    /// AOT parent class referenced by children.
    /// </summary>
    [FastClonerClonable]
    public partial class AotParentObject
    {
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
    }

    /// <summary>
    /// AOT child class with shallow-cloned parent reference.
    /// </summary>
    [FastClonerClonable]
    public partial class AotChildWithShallowParent
    {
        [FastClonerShallow]
        public AotParentObject? Parent { get; set; }
        
        public string ChildName { get; set; } = string.Empty;
        public int ChildId { get; set; }
    }

    /// <summary>
    /// AOT class with mix of shallow and deep cloned members.
    /// </summary>
    [FastClonerClonable]
    public partial class AotMixedCloneClass
    {
        [FastClonerShallow]
        public AotSharedState? SharedData { get; set; }
        
        public AotOwnedData? OwnData { get; set; }
        
        public string Name { get; set; } = string.Empty;
    }

    [FastClonerClonable]
    public partial class AotSharedState
    {
        public string ConfigValue { get; set; } = string.Empty;
        public int Version { get; set; }
    }

    [FastClonerClonable]
    public partial class AotOwnedData
    {
        public string Value { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>
    /// AOT class with shallow-cloned field.
    /// </summary>
    [FastClonerClonable]
    public partial class AotClassWithShallowField
    {
        [FastClonerShallow]
        public AotSharedState? SharedField;
        
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// AOT class with shallow-cloned collection.
    /// </summary>
    [FastClonerClonable]
    public partial class AotClassWithShallowCollection
    {
        [FastClonerShallow]
        public List<int>? SharedList { get; set; }
        
        public List<int>? OwnList { get; set; }
    }

    #endregion

    #region Reflection-based Cloning Tests

    [Test]
    public void Reflection_ShallowAttribute_ParentReference_ShouldBeShallowCloned()
    {
        // Arrange
        var parent = new ParentObject
        {
            Name = "Parent1",
            Id = 1
        };
        
        var child = new ChildWithShallowParent
        {
            Parent = parent,
            ChildName = "Child1",
            ChildId = 100
        };

        // Act
        var clone = FastCloner.DeepClone(child);

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.ChildName, Is.EqualTo("Child1"));
        Assert.That(clone.ChildId, Is.EqualTo(100));
        
        // Parent should be the SAME reference (shallow cloned)
        Assert.That(clone.Parent, Is.SameAs(parent));
    }

    [Test]
    public void Reflection_MixedClone_ShallowAndDeep_ShouldWorkCorrectly()
    {
        // Arrange
        var sharedState = new SharedState
        {
            ConfigValue = "Config1",
            Version = 1
        };
        
        var ownedData = new OwnedData
        {
            Value = "Owned1",
            Count = 42
        };
        
        var original = new MixedCloneClass
        {
            SharedData = sharedState,
            OwnData = ownedData,
            Name = "Mixed"
        };

        // Act
        var clone = FastCloner.DeepClone(original);

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.EqualTo("Mixed"));
        
        // SharedData should be the SAME reference (shallow cloned)
        Assert.That(clone.SharedData, Is.SameAs(sharedState));
        
        // OwnData should be a DIFFERENT reference (deep cloned)
        Assert.That(clone.OwnData, Is.Not.SameAs(ownedData));
        Assert.That(clone.OwnData, Is.Not.Null);
        Assert.That(clone.OwnData!.Value, Is.EqualTo("Owned1"));
        Assert.That(clone.OwnData.Count, Is.EqualTo(42));
    }

    [Test]
    public void Reflection_ShallowField_ShouldBeShallowCloned()
    {
        // Arrange
        var sharedState = new SharedState
        {
            ConfigValue = "FieldConfig",
            Version = 5
        };
        
        var original = new ClassWithShallowField
        {
            SharedField = sharedState,
            Name = "FieldTest"
        };

        // Act
        var clone = FastCloner.DeepClone(original);

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.EqualTo("FieldTest"));
        
        // SharedField should be the SAME reference (shallow cloned)
        Assert.That(clone.SharedField, Is.SameAs(sharedState));
    }

    [Test]
    public void Reflection_ShallowCollection_ShouldPreserveReference()
    {
        // Arrange
        var sharedList = new List<int> { 1, 2, 3, 4, 5 };
        var ownList = new List<int> { 10, 20, 30 };
        
        var original = new ClassWithShallowCollection
        {
            SharedList = sharedList,
            OwnList = ownList
        };

        // Act
        var clone = FastCloner.DeepClone(original);

        // Assert
        Assert.That(clone, Is.Not.Null);
        
        // SharedList should be the SAME reference (shallow cloned)
        Assert.That(clone!.SharedList, Is.SameAs(sharedList));
        
        // OwnList should be a DIFFERENT reference (deep cloned)
        Assert.That(clone.OwnList, Is.Not.SameAs(ownList));
        Assert.That(clone.OwnList, Is.Not.Null);
        Assert.That(clone.OwnList, Is.EqualTo(ownList));
    }

    [Test]
    public void Reflection_ShallowAttribute_NullValue_ShouldBeHandledCorrectly()
    {
        // Arrange
        var original = new ChildWithShallowParent
        {
            Parent = null,
            ChildName = "OrphanChild",
            ChildId = 999
        };

        // Act
        var clone = FastCloner.DeepClone(original);

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Parent, Is.Null);
        Assert.That(clone.ChildName, Is.EqualTo("OrphanChild"));
        Assert.That(clone.ChildId, Is.EqualTo(999));
    }

    [Test]
    public void Reflection_ModifyingShallowClonedReference_AffectsOriginal()
    {
        // Arrange
        var parent = new ParentObject
        {
            Name = "OriginalParent",
            Id = 1
        };
        
        var child = new ChildWithShallowParent
        {
            Parent = parent,
            ChildName = "Child1",
            ChildId = 100
        };

        // Act
        var clone = FastCloner.DeepClone(child);
        
        // Modify the shallow-cloned parent through the clone
        clone!.Parent!.Name = "ModifiedParent";

        // Assert - modifying through clone should affect original
        Assert.That(parent.Name, Is.EqualTo("ModifiedParent"));
    }

    [Test]
    public void Reflection_ShallowOnValueType_ShouldCopyValue()
    {
        // Arrange
        var original = new ClassWithShallowValueType
        {
            ShallowInt = 42,
            NormalInt = 100
        };

        // Act
        var clone = FastCloner.DeepClone(original);

        // Assert - value types are always copied by value
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.ShallowInt, Is.EqualTo(42));
        Assert.That(clone.NormalInt, Is.EqualTo(100));
    }

    [Test]
    public void Reflection_ShallowOnReadonlyField_ShouldCopyReference()
    {
        // Arrange
        var sharedState = new SharedState { ConfigValue = "ReadonlyTest", Version = 10 };
        var original = new ClassWithReadonlyShallowField(sharedState, "TestName");

        // Act
        var clone = FastCloner.DeepClone(original);

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.EqualTo("TestName"));
        // Readonly shallow field should be the SAME reference
        Assert.That(clone.SharedField, Is.SameAs(sharedState));
    }

    [Test]
    public void Reflection_ShallowOnNestedObject_ShouldPreserveDeepNesting()
    {
        // Arrange
        var deepNested = new DeepNestedObject
        {
            Level1 = new Level1Object
            {
                Value = "L1",
                Level2 = new Level2Object
                {
                    Value = "L2"
                }
            }
        };

        var original = new ClassWithShallowNestedObject
        {
            ShallowNested = deepNested,
            Name = "NestedTest"
        };

        // Act
        var clone = FastCloner.DeepClone(original);

        // Assert
        Assert.That(clone, Is.Not.Null);
        // The entire nested structure should be the SAME reference
        Assert.That(clone!.ShallowNested, Is.SameAs(deepNested));
        Assert.That(clone.ShallowNested!.Level1, Is.SameAs(deepNested.Level1));
        Assert.That(clone.ShallowNested.Level1!.Level2, Is.SameAs(deepNested.Level1.Level2));
    }

    [Test]
    public void Reflection_ShallowInInheritedClass_ShouldRespectAttribute()
    {
        // Arrange
        var sharedState = new SharedState { ConfigValue = "Inherited", Version = 5 };
        var original = new DerivedClassWithShallowMember
        {
            SharedData = sharedState,
            DerivedValue = "Derived",
            BaseValue = "Base"
        };

        // Act
        var clone = FastCloner.DeepClone(original);

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.DerivedValue, Is.EqualTo("Derived"));
        Assert.That(clone.BaseValue, Is.EqualTo("Base"));
        // Shallow member in base class should be the SAME reference
        Assert.That(clone.SharedData, Is.SameAs(sharedState));
    }

    [Test]
    public void Reflection_ShallowWithCircularReference_ShouldNotCauseInfiniteLoop()
    {
        // Arrange - create a circular reference scenario
        var node1 = new CircularNodeWithShallowParent { Name = "Node1" };
        var node2 = new CircularNodeWithShallowParent { Name = "Node2", Parent = node1 };
        node1.Children.Add(node2);

        // Act
        var clone = FastCloner.DeepClone(node1);

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.EqualTo("Node1"));
        Assert.That(clone.Children, Has.Count.EqualTo(1));
        
        var clonedChild = clone.Children[0];
        Assert.That(clonedChild.Name, Is.EqualTo("Node2"));
        // Parent is shallow-cloned, so it references the ORIGINAL node1, not the clone
        Assert.That(clonedChild.Parent, Is.SameAs(node1));
    }

    [Test]
    public void Reflection_MultipleShallowMembers_ShouldAllBeShallowCloned()
    {
        // Arrange
        var shared1 = new SharedState { ConfigValue = "Shared1", Version = 1 };
        var shared2 = new SharedState { ConfigValue = "Shared2", Version = 2 };
        var owned = new OwnedData { Value = "Owned", Count = 42 };

        var original = new ClassWithMultipleShallowMembers
        {
            SharedState1 = shared1,
            SharedState2 = shared2,
            OwnedData = owned
        };

        // Act
        var clone = FastCloner.DeepClone(original);

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.SharedState1, Is.SameAs(shared1));
        Assert.That(clone.SharedState2, Is.SameAs(shared2));
        Assert.That(clone.OwnedData, Is.Not.SameAs(owned));
        Assert.That(clone.OwnedData!.Value, Is.EqualTo("Owned"));
    }

    #endregion

    #region Additional Test Classes for Reflection

    public class ClassWithShallowValueType
    {
        [FastClonerShallow]
        public int ShallowInt { get; set; }
        
        public int NormalInt { get; set; }
    }

    public class ClassWithReadonlyShallowField
    {
        [FastClonerShallow]
        public readonly SharedState? SharedField;
        
        public string Name { get; set; } = string.Empty;

        public ClassWithReadonlyShallowField(SharedState? sharedField, string name)
        {
            SharedField = sharedField;
            Name = name;
        }
    }

    public class DeepNestedObject
    {
        public Level1Object? Level1 { get; set; }
    }

    public class Level1Object
    {
        public string Value { get; set; } = string.Empty;
        public Level2Object? Level2 { get; set; }
    }

    public class Level2Object
    {
        public string Value { get; set; } = string.Empty;
    }

    public class ClassWithShallowNestedObject
    {
        [FastClonerShallow]
        public DeepNestedObject? ShallowNested { get; set; }
        
        public string Name { get; set; } = string.Empty;
    }

    public class BaseClassWithShallowMember
    {
        [FastClonerShallow]
        public SharedState? SharedData { get; set; }
        
        public string BaseValue { get; set; } = string.Empty;
    }

    public class DerivedClassWithShallowMember : BaseClassWithShallowMember
    {
        public string DerivedValue { get; set; } = string.Empty;
    }

    public class CircularNodeWithShallowParent
    {
        public string Name { get; set; } = string.Empty;
        
        [FastClonerShallow]
        public CircularNodeWithShallowParent? Parent { get; set; }
        
        public List<CircularNodeWithShallowParent> Children { get; set; } = [];
    }

    public class ClassWithMultipleShallowMembers
    {
        [FastClonerShallow]
        public SharedState? SharedState1 { get; set; }
        
        [FastClonerShallow]
        public SharedState? SharedState2 { get; set; }
        
        public OwnedData? OwnedData { get; set; }
    }

    #endregion

    #region Source Generator (AOT) Cloning Tests

    [Test]
    [SourceGeneratorCompatible]
    public void Aot_ShallowAttribute_ParentReference_ShouldBeShallowCloned()
    {
        // Arrange
        var parent = new AotParentObject
        {
            Name = "AotParent1",
            Id = 1
        };
        
        var child = new AotChildWithShallowParent
        {
            Parent = parent,
            ChildName = "AotChild1",
            ChildId = 100
        };

        // Act
        var clone = child.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.ChildName, Is.EqualTo("AotChild1"));
        Assert.That(clone.ChildId, Is.EqualTo(100));
        
        // Parent should be the SAME reference (shallow cloned)
        Assert.That(clone.Parent, Is.SameAs(parent));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Aot_MixedClone_ShallowAndDeep_ShouldWorkCorrectly()
    {
        // Arrange
        var sharedState = new AotSharedState
        {
            ConfigValue = "AotConfig1",
            Version = 1
        };
        
        var ownedData = new AotOwnedData
        {
            Value = "AotOwned1",
            Count = 42
        };
        
        var original = new AotMixedCloneClass
        {
            SharedData = sharedState,
            OwnData = ownedData,
            Name = "AotMixed"
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.EqualTo("AotMixed"));
        
        // SharedData should be the SAME reference (shallow cloned)
        Assert.That(clone.SharedData, Is.SameAs(sharedState));
        
        // OwnData should be a DIFFERENT reference (deep cloned)
        Assert.That(clone.OwnData, Is.Not.SameAs(ownedData));
        Assert.That(clone.OwnData, Is.Not.Null);
        Assert.That(clone.OwnData!.Value, Is.EqualTo("AotOwned1"));
        Assert.That(clone.OwnData.Count, Is.EqualTo(42));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Aot_ShallowField_ShouldBeShallowCloned()
    {
        // Arrange
        var sharedState = new AotSharedState
        {
            ConfigValue = "AotFieldConfig",
            Version = 5
        };
        
        var original = new AotClassWithShallowField
        {
            SharedField = sharedState,
            Name = "AotFieldTest"
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.EqualTo("AotFieldTest"));
        
        // SharedField should be the SAME reference (shallow cloned)
        Assert.That(clone.SharedField, Is.SameAs(sharedState));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Aot_ShallowCollection_ShouldPreserveReference()
    {
        // Arrange
        var sharedList = new List<int> { 1, 2, 3, 4, 5 };
        var ownList = new List<int> { 10, 20, 30 };
        
        var original = new AotClassWithShallowCollection
        {
            SharedList = sharedList,
            OwnList = ownList
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        
        // SharedList should be the SAME reference (shallow cloned)
        Assert.That(clone!.SharedList, Is.SameAs(sharedList));
        
        // OwnList should be a DIFFERENT reference (deep cloned)
        Assert.That(clone.OwnList, Is.Not.SameAs(ownList));
        Assert.That(clone.OwnList, Is.Not.Null);
        Assert.That(clone.OwnList, Is.EqualTo(ownList));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Aot_ShallowAttribute_NullValue_ShouldBeHandledCorrectly()
    {
        // Arrange
        var original = new AotChildWithShallowParent
        {
            Parent = null,
            ChildName = "AotOrphanChild",
            ChildId = 999
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Parent, Is.Null);
        Assert.That(clone.ChildName, Is.EqualTo("AotOrphanChild"));
        Assert.That(clone.ChildId, Is.EqualTo(999));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Aot_ModifyingShallowClonedReference_AffectsOriginal()
    {
        // Arrange
        var parent = new AotParentObject
        {
            Name = "AotOriginalParent",
            Id = 1
        };
        
        var child = new AotChildWithShallowParent
        {
            Parent = parent,
            ChildName = "AotChild1",
            ChildId = 100
        };

        // Act
        var clone = child.FastDeepClone();
        
        // Modify the shallow-cloned parent through the clone
        clone!.Parent!.Name = "AotModifiedParent";

        // Assert - modifying through clone should affect original
        Assert.That(parent.Name, Is.EqualTo("AotModifiedParent"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Aot_ShallowOnNestedObject_ShouldPreserveDeepNesting()
    {
        // Arrange
        var deepNested = new AotDeepNestedObject
        {
            Level1 = new AotLevel1Object
            {
                Value = "L1",
                Level2 = new AotLevel2Object
                {
                    Value = "L2"
                }
            }
        };

        var original = new AotClassWithShallowNestedObject
        {
            ShallowNested = deepNested,
            Name = "AotNestedTest"
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        // The entire nested structure should be the SAME reference
        Assert.That(clone!.ShallowNested, Is.SameAs(deepNested));
        Assert.That(clone.ShallowNested!.Level1, Is.SameAs(deepNested.Level1));
        Assert.That(clone.ShallowNested.Level1!.Level2, Is.SameAs(deepNested.Level1.Level2));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Aot_ShallowInInheritedClass_ShouldRespectAttribute()
    {
        // Arrange
        var sharedState = new AotSharedState { ConfigValue = "AotInherited", Version = 5 };
        var original = new AotDerivedClassWithShallowMember
        {
            SharedData = sharedState,
            DerivedValue = "AotDerived",
            BaseValue = "AotBase"
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.DerivedValue, Is.EqualTo("AotDerived"));
        Assert.That(clone.BaseValue, Is.EqualTo("AotBase"));
        // Shallow member in base class should be the SAME reference
        Assert.That(clone.SharedData, Is.SameAs(sharedState));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Aot_MultipleShallowMembers_ShouldAllBeShallowCloned()
    {
        // Arrange
        var shared1 = new AotSharedState { ConfigValue = "AotShared1", Version = 1 };
        var shared2 = new AotSharedState { ConfigValue = "AotShared2", Version = 2 };
        var owned = new AotOwnedData { Value = "AotOwned", Count = 42 };

        var original = new AotClassWithMultipleShallowMembers
        {
            SharedState1 = shared1,
            SharedState2 = shared2,
            OwnedData = owned
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.SharedState1, Is.SameAs(shared1));
        Assert.That(clone.SharedState2, Is.SameAs(shared2));
        Assert.That(clone.OwnedData, Is.Not.SameAs(owned));
        Assert.That(clone.OwnedData!.Value, Is.EqualTo("AotOwned"));
    }

    #endregion

    #region Additional AOT Test Classes

    [FastClonerClonable]
    public partial class AotDeepNestedObject
    {
        public AotLevel1Object? Level1 { get; set; }
    }

    [FastClonerClonable]
    public partial class AotLevel1Object
    {
        public string Value { get; set; } = string.Empty;
        public AotLevel2Object? Level2 { get; set; }
    }

    [FastClonerClonable]
    public partial class AotLevel2Object
    {
        public string Value { get; set; } = string.Empty;
    }

    [FastClonerClonable]
    public partial class AotClassWithShallowNestedObject
    {
        [FastClonerShallow]
        public AotDeepNestedObject? ShallowNested { get; set; }
        
        public string Name { get; set; } = string.Empty;
    }

    [FastClonerClonable]
    public partial class AotBaseClassWithShallowMember
    {
        [FastClonerShallow]
        public AotSharedState? SharedData { get; set; }
        
        public string BaseValue { get; set; } = string.Empty;
    }

    [FastClonerClonable]
    public partial class AotDerivedClassWithShallowMember : AotBaseClassWithShallowMember
    {
        public string DerivedValue { get; set; } = string.Empty;
    }

    [FastClonerClonable]
    public partial class AotClassWithMultipleShallowMembers
    {
        [FastClonerShallow]
        public AotSharedState? SharedState1 { get; set; }
        
        [FastClonerShallow]
        public AotSharedState? SharedState2 { get; set; }
        
        public AotOwnedData? OwnedData { get; set; }
    }

    #endregion
}
