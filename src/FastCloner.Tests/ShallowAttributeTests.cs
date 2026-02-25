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
    public class AotParentObject
    {
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
    }

    /// <summary>
    /// AOT child class with shallow-cloned parent reference.
    /// </summary>
    [FastClonerClonable]
    public class AotChildWithShallowParent
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
    public class AotMixedCloneClass
    {
        [FastClonerShallow]
        public AotSharedState? SharedData { get; set; }
        
        public AotOwnedData? OwnData { get; set; }
        
        public string Name { get; set; } = string.Empty;
    }

    [FastClonerClonable]
    public class AotSharedState
    {
        public string ConfigValue { get; set; } = string.Empty;
        public int Version { get; set; }
    }

    [FastClonerClonable]
    public class AotOwnedData
    {
        public string Value { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>
    /// AOT class with shallow-cloned field.
    /// </summary>
    [FastClonerClonable]
    public class AotClassWithShallowField
    {
        [FastClonerShallow]
        public AotSharedState? SharedField;
        
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// AOT class with shallow-cloned collection.
    /// </summary>
    [FastClonerClonable]
    public class AotClassWithShallowCollection
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
        ParentObject parent = new ParentObject
        {
            Name = "Parent1",
            Id = 1
        };
        
        ChildWithShallowParent child = new ChildWithShallowParent
        {
            Parent = parent,
            ChildName = "Child1",
            ChildId = 100
        };

        // Act
        ChildWithShallowParent? clone = FastCloner.DeepClone(child);

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
        SharedState sharedState = new SharedState
        {
            ConfigValue = "Config1",
            Version = 1
        };
        
        OwnedData ownedData = new OwnedData
        {
            Value = "Owned1",
            Count = 42
        };
        
        MixedCloneClass original = new MixedCloneClass
        {
            SharedData = sharedState,
            OwnData = ownedData,
            Name = "Mixed"
        };

        // Act
        MixedCloneClass? clone = FastCloner.DeepClone(original);

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
        SharedState sharedState = new SharedState
        {
            ConfigValue = "FieldConfig",
            Version = 5
        };
        
        ClassWithShallowField original = new ClassWithShallowField
        {
            SharedField = sharedState,
            Name = "FieldTest"
        };

        // Act
        ClassWithShallowField? clone = FastCloner.DeepClone(original);

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
        List<int> sharedList = new List<int> { 1, 2, 3, 4, 5 };
        List<int> ownList = new List<int> { 10, 20, 30 };
        
        ClassWithShallowCollection original = new ClassWithShallowCollection
        {
            SharedList = sharedList,
            OwnList = ownList
        };

        // Act
        ClassWithShallowCollection? clone = FastCloner.DeepClone(original);

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
        ChildWithShallowParent original = new ChildWithShallowParent
        {
            Parent = null,
            ChildName = "OrphanChild",
            ChildId = 999
        };

        // Act
        ChildWithShallowParent? clone = FastCloner.DeepClone(original);

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
        ParentObject parent = new ParentObject
        {
            Name = "OriginalParent",
            Id = 1
        };
        
        ChildWithShallowParent child = new ChildWithShallowParent
        {
            Parent = parent,
            ChildName = "Child1",
            ChildId = 100
        };

        // Act
        ChildWithShallowParent? clone = FastCloner.DeepClone(child);
        
        // Modify the shallow-cloned parent through the clone
        clone!.Parent!.Name = "ModifiedParent";

        // Assert - modifying through clone should affect original
        Assert.That(parent.Name, Is.EqualTo("ModifiedParent"));
    }

    [Test]
    public void Reflection_ShallowOnValueType_ShouldCopyValue()
    {
        // Arrange
        ClassWithShallowValueType original = new ClassWithShallowValueType
        {
            ShallowInt = 42,
            NormalInt = 100
        };

        // Act
        ClassWithShallowValueType? clone = FastCloner.DeepClone(original);

        // Assert - value types are always copied by value
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.ShallowInt, Is.EqualTo(42));
        Assert.That(clone.NormalInt, Is.EqualTo(100));
    }

    [Test]
    public void Reflection_ShallowOnReadonlyField_ShouldCopyReference()
    {
        // Arrange
        SharedState sharedState = new SharedState { ConfigValue = "ReadonlyTest", Version = 10 };
        ClassWithReadonlyShallowField original = new ClassWithReadonlyShallowField(sharedState, "TestName");

        // Act
        ClassWithReadonlyShallowField? clone = FastCloner.DeepClone(original);

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
        DeepNestedObject deepNested = new DeepNestedObject
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

        ClassWithShallowNestedObject original = new ClassWithShallowNestedObject
        {
            ShallowNested = deepNested,
            Name = "NestedTest"
        };

        // Act
        ClassWithShallowNestedObject? clone = FastCloner.DeepClone(original);

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
        SharedState sharedState = new SharedState { ConfigValue = "Inherited", Version = 5 };
        DerivedClassWithShallowMember original = new DerivedClassWithShallowMember
        {
            SharedData = sharedState,
            DerivedValue = "Derived",
            BaseValue = "Base"
        };

        // Act
        DerivedClassWithShallowMember? clone = FastCloner.DeepClone(original);

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
        CircularNodeWithShallowParent node1 = new CircularNodeWithShallowParent { Name = "Node1" };
        CircularNodeWithShallowParent node2 = new CircularNodeWithShallowParent { Name = "Node2", Parent = node1 };
        node1.Children.Add(node2);

        // Act
        CircularNodeWithShallowParent? clone = FastCloner.DeepClone(node1);

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.EqualTo("Node1"));
        Assert.That(clone.Children, Has.Count.EqualTo(1));
        
        CircularNodeWithShallowParent clonedChild = clone.Children[0];
        Assert.That(clonedChild.Name, Is.EqualTo("Node2"));
        // Parent is shallow-cloned, so it references the ORIGINAL node1, not the clone
        Assert.That(clonedChild.Parent, Is.SameAs(node1));
    }

    [Test]
    public void Reflection_MultipleShallowMembers_ShouldAllBeShallowCloned()
    {
        // Arrange
        SharedState shared1 = new SharedState { ConfigValue = "Shared1", Version = 1 };
        SharedState shared2 = new SharedState { ConfigValue = "Shared2", Version = 2 };
        OwnedData owned = new OwnedData { Value = "Owned", Count = 42 };

        ClassWithMultipleShallowMembers original = new ClassWithMultipleShallowMembers
        {
            SharedState1 = shared1,
            SharedState2 = shared2,
            OwnedData = owned
        };

        // Act
        ClassWithMultipleShallowMembers? clone = FastCloner.DeepClone(original);

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

    /// <summary>
    /// Test class using C# 14 field keyword with [FastClonerShallow] attribute.
    /// The field keyword generates the same &lt;PropertyName&gt;k__BackingField pattern.
    /// </summary>
    public class ClassWithFieldKeywordShallow
    {
        [FastClonerShallow]
        public SharedState? ShallowWithValidation
        {
            get => field;
            set => field = value; // Using field keyword
        }
        
        public string NormalProperty
        {
            get => field ?? string.Empty;
            set => field = value?.Trim();
        }
        
        public OwnedData? DeepClonedData { get; set; }
    }

    /// <summary>
    /// Test class using C# 14 field keyword with validation logic.
    /// </summary>
    public class ClassWithFieldKeywordValidation
    {
        [FastClonerShallow]
        public SharedState? Config
        {
            get => field;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                field = value;
            }
        }
        
        public int Counter
        {
            get => field;
            set => field = value < 0 ? 0 : value;
        }
    }

    #endregion

    #region C# 14 Field Keyword Tests

    [Test]
    public void Reflection_FieldKeyword_ShallowAttribute_ShouldBeRespected()
    {
        // Arrange
        SharedState sharedState = new SharedState
        {
            ConfigValue = "FieldKeywordTest",
            Version = 42
        };
        
        OwnedData ownedData = new OwnedData
        {
            Value = "Owned",
            Count = 100
        };
        
        ClassWithFieldKeywordShallow original = new ClassWithFieldKeywordShallow
        {
            ShallowWithValidation = sharedState,
            NormalProperty = "  trimmed  ",
            DeepClonedData = ownedData
        };

        // Act
        ClassWithFieldKeywordShallow? clone = FastCloner.DeepClone(original);

        // Assert
        Assert.That(clone, Is.Not.Null);
        
        // Property with [FastClonerShallow] and field keyword should preserve reference
        Assert.That(clone!.ShallowWithValidation, Is.SameAs(sharedState));
        
        // Normal property should be copied
        Assert.That(clone.NormalProperty, Is.EqualTo("trimmed"));
        
        // Deep cloned data should be a different reference
        Assert.That(clone.DeepClonedData, Is.Not.SameAs(ownedData));
        Assert.That(clone.DeepClonedData!.Value, Is.EqualTo("Owned"));
    }

    [Test]
    public void Reflection_FieldKeyword_WithValidation_ShallowAttribute_ShouldWork()
    {
        // Arrange
        SharedState config = new SharedState
        {
            ConfigValue = "ValidationTest",
            Version = 1
        };
        
        ClassWithFieldKeywordValidation original = new ClassWithFieldKeywordValidation
        {
            Config = config,
            Counter = -5 // Will be clamped to 0
        };

        // Act
        ClassWithFieldKeywordValidation? clone = FastCloner.DeepClone(original);

        // Assert
        Assert.That(clone, Is.Not.Null);
        
        // Config with [FastClonerShallow] should preserve reference
        Assert.That(clone!.Config, Is.SameAs(config));
        
        // Counter should be cloned (value was already clamped to 0)
        Assert.That(clone.Counter, Is.EqualTo(0));
    }

    [Test]
    public void Reflection_FieldKeyword_ModifyingShallowClonedReference_AffectsOriginal()
    {
        // Arrange
        SharedState sharedState = new SharedState
        {
            ConfigValue = "Original",
            Version = 1
        };
        
        ClassWithFieldKeywordShallow original = new ClassWithFieldKeywordShallow
        {
            ShallowWithValidation = sharedState
        };

        // Act
        ClassWithFieldKeywordShallow? clone = FastCloner.DeepClone(original);
        clone!.ShallowWithValidation!.ConfigValue = "Modified";

        // Assert - modifying through clone should affect original
        Assert.That(sharedState.ConfigValue, Is.EqualTo("Modified"));
    }

    #endregion

    #region Source Generator (AOT) Cloning Tests

    [Test]
    [SourceGeneratorCompatible]
    public void Aot_ShallowAttribute_ParentReference_ShouldBeShallowCloned()
    {
        // Arrange
        AotParentObject parent = new AotParentObject
        {
            Name = "AotParent1",
            Id = 1
        };
        
        AotChildWithShallowParent child = new AotChildWithShallowParent
        {
            Parent = parent,
            ChildName = "AotChild1",
            ChildId = 100
        };

        // Act
        AotChildWithShallowParent clone = child.FastDeepClone();

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
        AotSharedState sharedState = new AotSharedState
        {
            ConfigValue = "AotConfig1",
            Version = 1
        };
        
        AotOwnedData ownedData = new AotOwnedData
        {
            Value = "AotOwned1",
            Count = 42
        };
        
        AotMixedCloneClass original = new AotMixedCloneClass
        {
            SharedData = sharedState,
            OwnData = ownedData,
            Name = "AotMixed"
        };

        // Act
        AotMixedCloneClass clone = original.FastDeepClone();

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
        AotSharedState sharedState = new AotSharedState
        {
            ConfigValue = "AotFieldConfig",
            Version = 5
        };
        
        AotClassWithShallowField original = new AotClassWithShallowField
        {
            SharedField = sharedState,
            Name = "AotFieldTest"
        };

        // Act
        AotClassWithShallowField clone = original.FastDeepClone();

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
        List<int> sharedList = new List<int> { 1, 2, 3, 4, 5 };
        List<int> ownList = new List<int> { 10, 20, 30 };
        
        AotClassWithShallowCollection original = new AotClassWithShallowCollection
        {
            SharedList = sharedList,
            OwnList = ownList
        };

        // Act
        AotClassWithShallowCollection clone = original.FastDeepClone();

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
        AotChildWithShallowParent original = new AotChildWithShallowParent
        {
            Parent = null,
            ChildName = "AotOrphanChild",
            ChildId = 999
        };

        // Act
        AotChildWithShallowParent clone = original.FastDeepClone();

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
        AotParentObject parent = new AotParentObject
        {
            Name = "AotOriginalParent",
            Id = 1
        };
        
        AotChildWithShallowParent child = new AotChildWithShallowParent
        {
            Parent = parent,
            ChildName = "AotChild1",
            ChildId = 100
        };

        // Act
        AotChildWithShallowParent clone = child.FastDeepClone();
        
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
        AotDeepNestedObject deepNested = new AotDeepNestedObject
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

        AotClassWithShallowNestedObject original = new AotClassWithShallowNestedObject
        {
            ShallowNested = deepNested,
            Name = "AotNestedTest"
        };

        // Act
        AotClassWithShallowNestedObject clone = original.FastDeepClone();

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
        AotSharedState sharedState = new AotSharedState { ConfigValue = "AotInherited", Version = 5 };
        AotDerivedClassWithShallowMember original = new AotDerivedClassWithShallowMember
        {
            SharedData = sharedState,
            DerivedValue = "AotDerived",
            BaseValue = "AotBase"
        };

        // Act
        AotDerivedClassWithShallowMember clone = original.FastDeepClone();

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
        AotSharedState shared1 = new AotSharedState { ConfigValue = "AotShared1", Version = 1 };
        AotSharedState shared2 = new AotSharedState { ConfigValue = "AotShared2", Version = 2 };
        AotOwnedData owned = new AotOwnedData { Value = "AotOwned", Count = 42 };

        AotClassWithMultipleShallowMembers original = new AotClassWithMultipleShallowMembers
        {
            SharedState1 = shared1,
            SharedState2 = shared2,
            OwnedData = owned
        };

        // Act
        AotClassWithMultipleShallowMembers clone = original.FastDeepClone();

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
    public class AotDeepNestedObject
    {
        public AotLevel1Object? Level1 { get; set; }
    }

    [FastClonerClonable]
    public class AotLevel1Object
    {
        public string Value { get; set; } = string.Empty;
        public AotLevel2Object? Level2 { get; set; }
    }

    [FastClonerClonable]
    public class AotLevel2Object
    {
        public string Value { get; set; } = string.Empty;
    }

    [FastClonerClonable]
    public class AotClassWithShallowNestedObject
    {
        [FastClonerShallow]
        public AotDeepNestedObject? ShallowNested { get; set; }
        
        public string Name { get; set; } = string.Empty;
    }

    [FastClonerClonable]
    public class AotBaseClassWithShallowMember
    {
        [FastClonerShallow]
        public AotSharedState? SharedData { get; set; }
        
        public string BaseValue { get; set; } = string.Empty;
    }

    [FastClonerClonable]
    public class AotDerivedClassWithShallowMember : AotBaseClassWithShallowMember
    {
        public string DerivedValue { get; set; } = string.Empty;
    }

    [FastClonerClonable]
    public class AotClassWithMultipleShallowMembers
    {
        [FastClonerShallow]
        public AotSharedState? SharedState1 { get; set; }
        
        [FastClonerShallow]
        public AotSharedState? SharedState2 { get; set; }
        
        public AotOwnedData? OwnedData { get; set; }
    }

    /// <summary>
    /// Test class with getter-only collection (no setter) marked with [FastClonerShallow].
    /// The semantics should be: shallow clone the items (just copy references, don't deep clone).
    /// </summary>
    [FastClonerClonable]
    public class AotClassWithShallowGetterOnlyCollection
    {
        [FastClonerShallow]
        public System.Collections.ObjectModel.ObservableCollection<AotOwnedData> Items { get; } = [];
        
        public string Name { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Test class with getter-only collection WITHOUT [FastClonerShallow].
    /// The semantics should be: deep clone the items (the default behavior).
    /// </summary>
    [FastClonerClonable]
    public class AotClassWithDeepGetterOnlyCollection
    {
        public System.Collections.ObjectModel.ObservableCollection<AotOwnedData> Items { get; } = [];
        
        public string Name { get; set; } = string.Empty;
    }

    #endregion

    #region Getter-Only Collection Tests (Issue #22)

    [Test]
    [SourceGeneratorCompatible]
    public void Aot_ShallowGetterOnlyCollection_ShouldShallowCloneItems()
    {
        // Arrange - Issue #22: [FastClonerShallow] on getter-only collection should shallow clone items
        AotOwnedData item1 = new AotOwnedData { Value = "Item1", Count = 1 };
        AotOwnedData item2 = new AotOwnedData { Value = "Item2", Count = 2 };
        
        AotClassWithShallowGetterOnlyCollection original = new AotClassWithShallowGetterOnlyCollection { Name = "ShallowGetterOnly" };
        original.Items.Add(item1);
        original.Items.Add(item2);

        // Act
        AotClassWithShallowGetterOnlyCollection clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.EqualTo("ShallowGetterOnly"));
        Assert.That(clone.Items, Has.Count.EqualTo(2));
        
        // Items should be the SAME references (shallow cloned) - Issue #22 fix
        Assert.That(clone.Items[0], Is.SameAs(item1), "Item 0 should be the same reference (shallow clone)");
        Assert.That(clone.Items[1], Is.SameAs(item2), "Item 1 should be the same reference (shallow clone)");
        
        // The collection itself is a different instance (getter-only creates new collection)
        Assert.That(clone.Items, Is.Not.SameAs(original.Items));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Aot_DeepGetterOnlyCollection_ShouldDeepCloneItems()
    {
        // Arrange - Without [FastClonerShallow], getter-only collection should deep clone items
        AotOwnedData item1 = new AotOwnedData { Value = "Item1", Count = 1 };
        AotOwnedData item2 = new AotOwnedData { Value = "Item2", Count = 2 };
        
        AotClassWithDeepGetterOnlyCollection original = new AotClassWithDeepGetterOnlyCollection { Name = "DeepGetterOnly" };
        original.Items.Add(item1);
        original.Items.Add(item2);

        // Act
        AotClassWithDeepGetterOnlyCollection clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.EqualTo("DeepGetterOnly"));
        Assert.That(clone.Items, Has.Count.EqualTo(2));
        
        // Items should be DIFFERENT references (deep cloned) - the default behavior
        Assert.That(clone.Items[0], Is.Not.SameAs(item1), "Item 0 should be a different reference (deep clone)");
        Assert.That(clone.Items[1], Is.Not.SameAs(item2), "Item 1 should be a different reference (deep clone)");
        
        // But values should be equal
        Assert.That(clone.Items[0].Value, Is.EqualTo("Item1"));
        Assert.That(clone.Items[0].Count, Is.EqualTo(1));
        Assert.That(clone.Items[1].Value, Is.EqualTo("Item2"));
        Assert.That(clone.Items[1].Count, Is.EqualTo(2));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Aot_ShallowGetterOnlyCollection_ModifyingClonedItemsAffectsOriginal()
    {
        // Arrange
        AotOwnedData item1 = new AotOwnedData { Value = "Original", Count = 1 };
        
        AotClassWithShallowGetterOnlyCollection original = new AotClassWithShallowGetterOnlyCollection { Name = "Test" };
        original.Items.Add(item1);

        // Act
        AotClassWithShallowGetterOnlyCollection clone = original.FastDeepClone();
        clone!.Items[0].Value = "Modified";

        // Assert - modifying through clone should affect original (shallow clone)
        Assert.That(item1.Value, Is.EqualTo("Modified"));
        Assert.That(original.Items[0].Value, Is.EqualTo("Modified"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Aot_DeepGetterOnlyCollection_ModifyingClonedItemsDoesNotAffectOriginal()
    {
        // Arrange
        AotOwnedData item1 = new AotOwnedData { Value = "Original", Count = 1 };
        
        AotClassWithDeepGetterOnlyCollection original = new AotClassWithDeepGetterOnlyCollection { Name = "Test" };
        original.Items.Add(item1);

        // Act
        AotClassWithDeepGetterOnlyCollection clone = original.FastDeepClone();
        clone!.Items[0].Value = "Modified";

        // Assert - modifying through clone should NOT affect original (deep clone)
        Assert.That(item1.Value, Is.EqualTo("Original"));
        Assert.That(original.Items[0].Value, Is.EqualTo("Original"));
    }

    #endregion
}
