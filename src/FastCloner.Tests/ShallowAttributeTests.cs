using FastCloner.Code;
using FastCloner.SourceGenerator.Shared;
using System.Threading.Tasks;

namespace FastCloner.Tests;
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
    public async Task Reflection_ShallowAttribute_ParentReference_ShouldBeShallowCloned()
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
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.ChildName).IsEqualTo("Child1");
        await Assert.That(clone.ChildId).IsEqualTo(100);

        // Parent should be the SAME reference (shallow cloned)
        await Assert.That(clone.Parent).IsSameReferenceAs(parent);
    }

    [Test]
    public async Task Reflection_MixedClone_ShallowAndDeep_ShouldWorkCorrectly()
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
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Name).IsEqualTo("Mixed");

        // SharedData should be the SAME reference (shallow cloned)
        await Assert.That(clone.SharedData).IsSameReferenceAs(sharedState);

        // OwnData should be a DIFFERENT reference (deep cloned)
        await Assert.That(clone.OwnData).IsNotSameReferenceAs(ownedData);
        await Assert.That(clone.OwnData).IsNotNull();
        await Assert.That(clone.OwnData!.Value).IsEqualTo("Owned1");
        await Assert.That(clone.OwnData.Count).IsEqualTo(42);
    }

    [Test]
    public async Task Reflection_ShallowField_ShouldBeShallowCloned()
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
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Name).IsEqualTo("FieldTest");

        // SharedField should be the SAME reference (shallow cloned)
        await Assert.That(clone.SharedField).IsSameReferenceAs(sharedState);
    }

    [Test]
    public async Task Reflection_ShallowCollection_ShouldPreserveReference()
    {
        // Arrange
        List<int> sharedList = [1, 2, 3, 4, 5];
        List<int> ownList = [10, 20, 30];
        
        ClassWithShallowCollection original = new ClassWithShallowCollection
        {
            SharedList = sharedList,
            OwnList = ownList
        };

        // Act
        ClassWithShallowCollection? clone = FastCloner.DeepClone(original);

        // Assert
        await Assert.That(clone).IsNotNull();

        // SharedList should be the SAME reference (shallow cloned)
        await Assert.That(clone!.SharedList).IsSameReferenceAs(sharedList);

        // OwnList should be a DIFFERENT reference (deep cloned)
        await Assert.That(clone.OwnList).IsNotSameReferenceAs(ownList);
        await Assert.That(clone.OwnList).IsNotNull();
        await Assert.That(clone.OwnList).IsEquivalentTo(ownList);
    }

    [Test]
    public async Task Reflection_ShallowAttribute_NullValue_ShouldBeHandledCorrectly()
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
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Parent).IsNull();
        await Assert.That(clone.ChildName).IsEqualTo("OrphanChild");
        await Assert.That(clone.ChildId).IsEqualTo(999);
    }

    [Test]
    public async Task Reflection_ModifyingShallowClonedReference_AffectsOriginal()
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
        await Assert.That(parent.Name).IsEqualTo("ModifiedParent");
    }

    [Test]
    public async Task Reflection_ShallowOnValueType_ShouldCopyValue()
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
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.ShallowInt).IsEqualTo(42);
        await Assert.That(clone.NormalInt).IsEqualTo(100);
    }

    [Test]
    public async Task Reflection_ShallowOnReadonlyField_ShouldCopyReference()
    {
        // Arrange
        SharedState sharedState = new SharedState { ConfigValue = "ReadonlyTest", Version = 10 };
        ClassWithReadonlyShallowField original = new ClassWithReadonlyShallowField(sharedState, "TestName");

        // Act
        ClassWithReadonlyShallowField? clone = FastCloner.DeepClone(original);

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Name).IsEqualTo("TestName");
        // Readonly shallow field should be the SAME reference
        await Assert.That(clone.SharedField).IsSameReferenceAs(sharedState);
    }

    [Test]
    public async Task Reflection_ShallowOnNestedObject_ShouldPreserveDeepNesting()
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
        await Assert.That(clone).IsNotNull();
        // The entire nested structure should be the SAME reference
        await Assert.That(clone!.ShallowNested).IsSameReferenceAs(deepNested);
        await Assert.That(clone.ShallowNested!.Level1).IsSameReferenceAs(deepNested.Level1);
        await Assert.That(clone.ShallowNested.Level1!.Level2).IsSameReferenceAs(deepNested.Level1.Level2);
    }

    [Test]
    public async Task Reflection_ShallowInInheritedClass_ShouldRespectAttribute()
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
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.DerivedValue).IsEqualTo("Derived");
        await Assert.That(clone.BaseValue).IsEqualTo("Base");
        // Shallow member in base class should be the SAME reference
        await Assert.That(clone.SharedData).IsSameReferenceAs(sharedState);
    }

    [Test]
    public async Task Reflection_ShallowWithCircularReference_ShouldNotCauseInfiniteLoop()
    {
        // Arrange - create a circular reference scenario
        CircularNodeWithShallowParent node1 = new CircularNodeWithShallowParent { Name = "Node1" };
        CircularNodeWithShallowParent node2 = new CircularNodeWithShallowParent { Name = "Node2", Parent = node1 };
        node1.Children.Add(node2);

        // Act
        CircularNodeWithShallowParent? clone = FastCloner.DeepClone(node1);

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Name).IsEqualTo("Node1");
        await Assert.That(clone.Children).Count().IsEqualTo(1);

        CircularNodeWithShallowParent clonedChild = clone.Children[0];
        await Assert.That(clonedChild.Name).IsEqualTo("Node2");
        // Parent is shallow-cloned, so it references the ORIGINAL node1, not the clone
        await Assert.That(clonedChild.Parent).IsSameReferenceAs(node1);
    }

    [Test]
    public async Task Reflection_MultipleShallowMembers_ShouldAllBeShallowCloned()
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
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.SharedState1).IsSameReferenceAs(shared1);
        await Assert.That(clone.SharedState2).IsSameReferenceAs(shared2);
        await Assert.That(clone.OwnedData).IsNotSameReferenceAs(owned);
        await Assert.That(clone.OwnedData!.Value).IsEqualTo("Owned");
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
    public async Task Reflection_FieldKeyword_ShallowAttribute_ShouldBeRespected()
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
        await Assert.That(clone).IsNotNull();

        // Property with [FastClonerShallow] and field keyword should preserve reference
        await Assert.That(clone!.ShallowWithValidation).IsSameReferenceAs(sharedState);

        // Normal property should be copied
        await Assert.That(clone.NormalProperty).IsEqualTo("trimmed");

        // Deep cloned data should be a different reference
        await Assert.That(clone.DeepClonedData).IsNotSameReferenceAs(ownedData);
        await Assert.That(clone.DeepClonedData!.Value).IsEqualTo("Owned");
    }

    [Test]
    public async Task Reflection_FieldKeyword_WithValidation_ShallowAttribute_ShouldWork()
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
        await Assert.That(clone).IsNotNull();

        // Config with [FastClonerShallow] should preserve reference
        await Assert.That(clone!.Config).IsSameReferenceAs(config);

        // Counter should be cloned (value was already clamped to 0)
        await Assert.That(clone.Counter).IsEqualTo(0);
    }

    [Test]
    public async Task Reflection_FieldKeyword_ModifyingShallowClonedReference_AffectsOriginal()
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
        await Assert.That(sharedState.ConfigValue).IsEqualTo("Modified");
    }

    #endregion

    #region Source Generator (AOT) Cloning Tests

    [Test]
    [SourceGeneratorCompatible]
    public async Task Aot_ShallowAttribute_ParentReference_ShouldBeShallowCloned()
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
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.ChildName).IsEqualTo("AotChild1");
        await Assert.That(clone.ChildId).IsEqualTo(100);

        // Parent should be the SAME reference (shallow cloned)
        await Assert.That(clone.Parent).IsSameReferenceAs(parent);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Aot_MixedClone_ShallowAndDeep_ShouldWorkCorrectly()
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
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Name).IsEqualTo("AotMixed");

        // SharedData should be the SAME reference (shallow cloned)
        await Assert.That(clone.SharedData).IsSameReferenceAs(sharedState);

        // OwnData should be a DIFFERENT reference (deep cloned)
        await Assert.That(clone.OwnData).IsNotSameReferenceAs(ownedData);
        await Assert.That(clone.OwnData).IsNotNull();
        await Assert.That(clone.OwnData!.Value).IsEqualTo("AotOwned1");
        await Assert.That(clone.OwnData.Count).IsEqualTo(42);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Aot_ShallowField_ShouldBeShallowCloned()
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
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Name).IsEqualTo("AotFieldTest");

        // SharedField should be the SAME reference (shallow cloned)
        await Assert.That(clone.SharedField).IsSameReferenceAs(sharedState);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Aot_ShallowCollection_ShouldPreserveReference()
    {
        // Arrange
        List<int> sharedList = [1, 2, 3, 4, 5];
        List<int> ownList = [10, 20, 30];
        
        AotClassWithShallowCollection original = new AotClassWithShallowCollection
        {
            SharedList = sharedList,
            OwnList = ownList
        };

        // Act
        AotClassWithShallowCollection clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();

        // SharedList should be the SAME reference (shallow cloned)
        await Assert.That(clone!.SharedList).IsSameReferenceAs(sharedList);

        // OwnList should be a DIFFERENT reference (deep cloned)
        await Assert.That(clone.OwnList).IsNotSameReferenceAs(ownList);
        await Assert.That(clone.OwnList).IsNotNull();
        await Assert.That(clone.OwnList).IsEquivalentTo(ownList);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Aot_ShallowAttribute_NullValue_ShouldBeHandledCorrectly()
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
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Parent).IsNull();
        await Assert.That(clone.ChildName).IsEqualTo("AotOrphanChild");
        await Assert.That(clone.ChildId).IsEqualTo(999);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Aot_ModifyingShallowClonedReference_AffectsOriginal()
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
        await Assert.That(parent.Name).IsEqualTo("AotModifiedParent");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Aot_ShallowOnNestedObject_ShouldPreserveDeepNesting()
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
        await Assert.That(clone).IsNotNull();
        // The entire nested structure should be the SAME reference
        await Assert.That(clone!.ShallowNested).IsSameReferenceAs(deepNested);
        await Assert.That(clone.ShallowNested!.Level1).IsSameReferenceAs(deepNested.Level1);
        await Assert.That(clone.ShallowNested.Level1!.Level2).IsSameReferenceAs(deepNested.Level1.Level2);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Aot_ShallowInInheritedClass_ShouldRespectAttribute()
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
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.DerivedValue).IsEqualTo("AotDerived");
        await Assert.That(clone.BaseValue).IsEqualTo("AotBase");
        // Shallow member in base class should be the SAME reference
        await Assert.That(clone.SharedData).IsSameReferenceAs(sharedState);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Aot_MultipleShallowMembers_ShouldAllBeShallowCloned()
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
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.SharedState1).IsSameReferenceAs(shared1);
        await Assert.That(clone.SharedState2).IsSameReferenceAs(shared2);
        await Assert.That(clone.OwnedData).IsNotSameReferenceAs(owned);
        await Assert.That(clone.OwnedData!.Value).IsEqualTo("AotOwned");
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
    public async Task Aot_ShallowGetterOnlyCollection_ShouldShallowCloneItems()
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
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Name).IsEqualTo("ShallowGetterOnly");
        await Assert.That(clone.Items).Count().IsEqualTo(2);

        // Items should be the SAME references (shallow cloned) - Issue #22 fix
        await Assert.That(clone.Items[0]).IsSameReferenceAs(item1).Because("Item 0 should be the same reference (shallow clone)");
        await Assert.That(clone.Items[1]).IsSameReferenceAs(item2).Because("Item 1 should be the same reference (shallow clone)");

        // The collection itself is a different instance (getter-only creates new collection)
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Aot_DeepGetterOnlyCollection_ShouldDeepCloneItems()
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
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Name).IsEqualTo("DeepGetterOnly");
        await Assert.That(clone.Items).Count().IsEqualTo(2);

        // Items should be DIFFERENT references (deep cloned) - the default behavior
        await Assert.That(clone.Items[0]).IsNotSameReferenceAs(item1).Because("Item 0 should be a different reference (deep clone)");
        await Assert.That(clone.Items[1]).IsNotSameReferenceAs(item2).Because("Item 1 should be a different reference (deep clone)");

        // But values should be equal
        await Assert.That(clone.Items[0].Value).IsEqualTo("Item1");
        await Assert.That(clone.Items[0].Count).IsEqualTo(1);
        await Assert.That(clone.Items[1].Value).IsEqualTo("Item2");
        await Assert.That(clone.Items[1].Count).IsEqualTo(2);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Aot_ShallowGetterOnlyCollection_ModifyingClonedItemsAffectsOriginal()
    {
        // Arrange
        AotOwnedData item1 = new AotOwnedData { Value = "Original", Count = 1 };
        
        AotClassWithShallowGetterOnlyCollection original = new AotClassWithShallowGetterOnlyCollection { Name = "Test" };
        original.Items.Add(item1);

        // Act
        AotClassWithShallowGetterOnlyCollection clone = original.FastDeepClone();
        clone!.Items[0].Value = "Modified";

        // Assert - modifying through clone should affect original (shallow clone)
        await Assert.That(item1.Value).IsEqualTo("Modified");
        await Assert.That(original.Items[0].Value).IsEqualTo("Modified");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Aot_DeepGetterOnlyCollection_ModifyingClonedItemsDoesNotAffectOriginal()
    {
        // Arrange
        AotOwnedData item1 = new AotOwnedData { Value = "Original", Count = 1 };
        
        AotClassWithDeepGetterOnlyCollection original = new AotClassWithDeepGetterOnlyCollection { Name = "Test" };
        original.Items.Add(item1);

        // Act
        AotClassWithDeepGetterOnlyCollection clone = original.FastDeepClone();
        clone!.Items[0].Value = "Modified";

        // Assert - modifying through clone should NOT affect original (deep clone)
        await Assert.That(item1.Value).IsEqualTo("Original");
        await Assert.That(original.Items[0].Value).IsEqualTo("Original");
    }

    #endregion
}