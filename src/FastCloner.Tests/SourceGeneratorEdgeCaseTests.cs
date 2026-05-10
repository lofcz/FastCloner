using System.Collections.ObjectModel;
using FastCloner.Code;
using FastCloner.SourceGenerator.Shared;
using System.Threading.Tasks;

namespace FastCloner.Tests;
[SourceGeneratorCompatible]
public class SourceGeneratorEdgeCaseTests
{
    #region Issue 1: Multi-dimensional Arrays

    [FastClonerClonable]
    public class ClassWith2dArray
    {
        public int[,]? Matrix { get; set; }
        public string? Name { get; set; }
    }

    [FastClonerClonable]
    public class ClassWith3dArray
    {
        public double[,,]? Cube { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task MultiDimensionalArray_2D_Should_Be_Cloned()
    {
        // Arrange
        ClassWith2dArray original = new ClassWith2dArray
        {
            Name = "Test",
            Matrix = new int[2, 3]
            {
                { 1, 2, 3 },
                { 4, 5, 6 }
            }
        };

        // Act - requires FastCloner runtime for multi-dimensional arrays
        ClassWith2dArray clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Name).IsEqualTo("Test");
        await Assert.That(clone.Matrix).IsNotNull();
        await Assert.That(clone.Matrix).IsNotSameReferenceAs(original.Matrix);
        await Assert.That(clone.Matrix![0, 0]).IsEqualTo(1);
        await Assert.That(clone.Matrix[1, 2]).IsEqualTo(6);

        // Verify independence
        original.Matrix![0, 0] = 999;
        await Assert.That(clone.Matrix[0, 0]).IsEqualTo(1);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task MultiDimensionalArray_3D_Should_Be_Cloned()
    {
        // Arrange
        ClassWith3dArray original = new ClassWith3dArray
        {
            Cube = new double[2, 2, 2]
        };
        original.Cube[0, 0, 0] = 1.1;
        original.Cube[1, 1, 1] = 2.2;

        // Act - requires FastCloner runtime for multi-dimensional arrays
        ClassWith3dArray clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Cube).IsNotNull();
        await Assert.That(clone.Cube).IsNotSameReferenceAs(original.Cube);
        await Assert.That(clone.Cube![0, 0, 0]).IsEqualTo(1.1);
        await Assert.That(clone.Cube[1, 1, 1]).IsEqualTo(2.2);
    }

    #endregion

    #region Issue 2: Fields in Object Initializers

    [FastClonerClonable]
    public class ClassWithFields
    {
        public int IntField;
        public string? StringField;
        public List<int>? ListField;
    }

    [FastClonerClonable]
    public class ClassWithMixedMembers
    {
        public int PropertyValue { get; set; }
        public int FieldValue;
        public string? PropertyString { get; set; }
        public string? FieldString;
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Fields_Should_Be_Cloned_In_Object_Initializers()
    {
        // Arrange
        ClassWithFields original = new ClassWithFields
        {
            IntField = 42,
            StringField = "Test",
            ListField = [1, 2, 3]
        };

        // Act
        ClassWithFields clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.IntField).IsEqualTo(42);
        await Assert.That(clone.StringField).IsEqualTo("Test");
        await Assert.That(clone.ListField).IsNotNull();
        await Assert.That(clone.ListField).IsNotSameReferenceAs(original.ListField);
        await Assert.That(clone.ListField).IsEquivalentTo([1, 2, 3]);

        // Verify independence
        original.IntField = 999;
        original.ListField!.Add(99);
        await Assert.That(clone.IntField).IsEqualTo(42);
        await Assert.That(clone.ListField!.Count).IsEqualTo(3);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Mixed_Properties_And_Fields_Should_Be_Cloned()
    {
        // Arrange
        ClassWithMixedMembers original = new ClassWithMixedMembers
        {
            PropertyValue = 10,
            FieldValue = 20,
            PropertyString = "PropStr",
            FieldString = "FieldStr"
        };

        // Act
        ClassWithMixedMembers clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.PropertyValue).IsEqualTo(10);
        await Assert.That(clone.FieldValue).IsEqualTo(20);
        await Assert.That(clone.PropertyString).IsEqualTo("PropStr");
        await Assert.That(clone.FieldString).IsEqualTo("FieldStr");
    }

    #endregion

    #region Issue 3: Init-Only Properties

    [FastClonerClonable]
    public class ClassWithInitOnlyProps
    {
        public int Id { get; init; }
        public string? Name { get; init; }
        public string? MutableValue { get; set; }
    }

    [FastClonerClonable]
    public record RecordWithInitProps
    {
        public int Id { get; init; }
        public string? Name { get; init; }
        public List<string>? Tags { get; init; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task InitOnly_Properties_Should_Be_Cloned()
    {
        // Arrange
        ClassWithInitOnlyProps original = new ClassWithInitOnlyProps
        {
            Id = 123,
            Name = "Test",
            MutableValue = "Mutable"
        };

        // Act
        ClassWithInitOnlyProps clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Id).IsEqualTo(123);
        await Assert.That(clone.Name).IsEqualTo("Test");
        await Assert.That(clone.MutableValue).IsEqualTo("Mutable");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Record_With_Init_Properties_Should_Be_Cloned()
    {
        // Arrange
        RecordWithInitProps original = new RecordWithInitProps
        {
            Id = 456,
            Name = "RecordTest",
            Tags = ["tag1", "tag2"]
        };

        // Act
        RecordWithInitProps clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Id).IsEqualTo(456);
        await Assert.That(clone.Name).IsEqualTo("RecordTest");
        await Assert.That(clone.Tags).IsNotNull();
        await Assert.That(clone.Tags).IsNotSameReferenceAs(original.Tags);
        await Assert.That(clone.Tags).IsEquivalentTo(["tag1", "tag2"]);
    }

    #endregion

    #region Issue 4: Private / Protected / Internal member cloning fidelity

    [FastClonerClonable]
    public class ClassWithPrivateSetter
    {
        public int PublicProperty { get; set; }
        public int PrivateSetterProperty { get; private set; }

        public void SetPrivate(int value) => PrivateSetterProperty = value;
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task PrivateSetter_Property_Is_Cloned_Via_Backing_Field_Accessor()
    {
        ClassWithPrivateSetter original = new ClassWithPrivateSetter
        {
            PublicProperty = 100
        };
        original.SetPrivate(200);

        ClassWithPrivateSetter clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.PublicProperty).IsEqualTo(100);
        await Assert.That(clone.PrivateSetterProperty).IsEqualTo(200);
    }

    [FastClonerClonable]
    public class ClassWithPrivateField
    {
        public int PublicProperty { get; set; }
        private int privateField;

        public int PrivateFieldValue => privateField;

        public void SetPrivateField(int value) => privateField = value;
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Private_Field_Is_Cloned_Via_UnsafeAccessor()
    {
        ClassWithPrivateField original = new ClassWithPrivateField
        {
            PublicProperty = 100
        };
        original.SetPrivateField(200);

        ClassWithPrivateField clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.PublicProperty).IsEqualTo(100);
        await Assert.That(clone.PrivateFieldValue).IsEqualTo(200);
    }

    [FastClonerClonable]
    public class ClassWithProtectedField
    {
        public int PublicProperty { get; set; }
        protected int protectedField;

        public int ProtectedFieldValue => protectedField;

        public void SetProtectedField(int value) => protectedField = value;
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Protected_Field_Is_Cloned_Via_UnsafeAccessor()
    {
        ClassWithProtectedField original = new ClassWithProtectedField
        {
            PublicProperty = 100
        };
        original.SetProtectedField(300);

        ClassWithProtectedField clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.PublicProperty).IsEqualTo(100);
        await Assert.That(clone.ProtectedFieldValue).IsEqualTo(300);
    }

    [FastClonerClonable]
    public class ClassWithInternalField
    {
        public int PublicProperty { get; set; }
        internal int internalField;

        public int InternalFieldValue => internalField;

        public void SetInternalField(int value) => internalField = value;
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Internal_Field_Is_Cloned()
    {
        ClassWithInternalField original = new ClassWithInternalField
        {
            PublicProperty = 100
        };
        original.SetInternalField(400);

        ClassWithInternalField clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.PublicProperty).IsEqualTo(100);
        await Assert.That(clone.InternalFieldValue).IsEqualTo(400);
    }

    [FastClonerClonable]
    public class ClassWithReferenceTypePrivateField
    {
        public string? PublicTag { get; set; }
        private List<int>? privateList;

        public IReadOnlyList<int>? PrivateListSnapshot => privateList;

        public void SetPrivateList(List<int>? list) => privateList = list;
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Private_Reference_Field_Is_Deep_Cloned()
    {
        List<int> originalList = [1, 2, 3];
        ClassWithReferenceTypePrivateField original = new ClassWithReferenceTypePrivateField { PublicTag = "tag" };
        original.SetPrivateList(originalList);

        ClassWithReferenceTypePrivateField clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.PublicTag).IsEqualTo("tag");
        await Assert.That(clone.PrivateListSnapshot).IsNotNull();
        // Deep clone, so values match but the instance is independent of the original.
        await Assert.That(clone.PrivateListSnapshot!).IsEquivalentTo(originalList);
        await Assert.That(ReferenceEquals(clone.PrivateListSnapshot, originalList)).IsFalse();
    }

    public class BaseWithPrivateField
    {
        private int inheritedPrivate;
        public int InheritedPrivateValue => inheritedPrivate;
        public void SetInheritedPrivate(int v) => inheritedPrivate = v;
    }

    [FastClonerClonable]
    public class DerivedWithBasePrivateField : BaseWithPrivateField
    {
        public int Own { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Inherited_Private_Field_Is_Cloned()
    {
        DerivedWithBasePrivateField original = new DerivedWithBasePrivateField { Own = 7 };
        original.SetInheritedPrivate(42);

        DerivedWithBasePrivateField clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Own).IsEqualTo(7);
        await Assert.That(clone.InheritedPrivateValue).IsEqualTo(42);
    }

    [FastClonerClonable]
    public class ClassWithNonAutoPrivateGetterAndSetter
    {
        private int _backing;

        public string? PublicTag { get; set; }
        
        private int HiddenValue
        {
            get => _backing;
            set => _backing = value;
        }

        public int InspectHiddenValue() => HiddenValue;
        public void AssignHiddenValue(int v) => HiddenValue = v;
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task NonAuto_Property_With_NonPublic_Getter_And_Setter_Is_Cloned()
    {
        ClassWithNonAutoPrivateGetterAndSetter original = new ClassWithNonAutoPrivateGetterAndSetter
        {
            PublicTag = "hidden"
        };
        original.AssignHiddenValue(123);

        ClassWithNonAutoPrivateGetterAndSetter clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.PublicTag).IsEqualTo("hidden");
        await Assert.That(clone.InspectHiddenValue()).IsEqualTo(123);
    }

    [FastClonerClonable]
    public class GenericWithPrivateField<T>
    {
        public string? Tag { get; set; }
        private T? value;

        public T? Value => value;
        public void SetValue(T? v) => value = v;
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Generic_Private_Field_Is_Cloned_Via_Generic_Accessor_Shell()
    {
        GenericWithPrivateField<int> original = new GenericWithPrivateField<int> { Tag = "g" };
        original.SetValue(99);

        GenericWithPrivateField<int> clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Tag).IsEqualTo("g");
        await Assert.That(clone.Value).IsEqualTo(99);
    }

    [FastClonerClonable]
    public struct StructWithPrivateField
    {
        public int Public;
        private int hidden;

        public int Hidden => hidden;
        public void SetHidden(int v) => hidden = v;
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Struct_Private_Field_Is_Cloned()
    {
        StructWithPrivateField original = new StructWithPrivateField { Public = 5 };
        original.SetHidden(11);

        StructWithPrivateField clone = original.FastDeepClone();

        await Assert.That(clone.Public).IsEqualTo(5);
        await Assert.That(clone.Hidden).IsEqualTo(11);
    }

    #endregion

    #region Type-level [FastClonerVisibility] policy

    [FastClonerClonable]
    [FastClonerVisibility(FastClonerMemberVisibility.Public)]
    public class PublicOnlyDto
    {
        public int Pub { get; set; }
        private int priv;
        public int PrivValue => priv;
        public void SetPriv(int v) => priv = v;
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Visibility_Public_Excludes_Private_Fields()
    {
        PublicOnlyDto original = new PublicOnlyDto { Pub = 10 };
        original.SetPriv(99);

        PublicOnlyDto clone = original.FastDeepClone();

        await Assert.That(clone!.Pub).IsEqualTo(10);
        await Assert.That(clone.PrivValue).IsEqualTo(0); // explicitly opted out via type policy
    }

    [FastClonerClonable]
    [FastClonerVisibility(FastClonerMemberVisibility.Public | FastClonerMemberVisibility.Internal)]
    public class PublicAndInternalOnly
    {
        public int Pub;
        internal int Inter;
        protected int Prot;
        private int Priv;

        public int ProtValue => Prot;
        public int PrivValue => Priv;
        public void SetAll(int prot, int priv)
        {
            Prot = prot;
            Priv = priv;
        }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Visibility_Public_And_Internal_Excludes_Protected_And_Private()
    {
        PublicAndInternalOnly original = new PublicAndInternalOnly { Pub = 1, Inter = 2 };
        original.SetAll(3, 4);

        PublicAndInternalOnly clone = original.FastDeepClone();

        await Assert.That(clone!.Pub).IsEqualTo(1);
        await Assert.That(clone.Inter).IsEqualTo(2);
        await Assert.That(clone.ProtValue).IsEqualTo(0);
        await Assert.That(clone.PrivValue).IsEqualTo(0);
    }

    [FastClonerClonable]
    [FastClonerVisibility(FastClonerMemberVisibility.Public)]
    public class MemberLevelOverridesPolicy
    {
        public int Pub;

        // Type policy excludes private, but the explicit member-level FastClonerBehavior
        // overrides it - this private field IS cloned.
        [global::FastCloner.Code.FastClonerBehavior(global::FastCloner.Code.CloneBehavior.Clone)]
        private int includedDespitePolicy;

        // Type policy excludes private, no override -> stays excluded.
        private int excludedByPolicy;

        public int IncludedValue => includedDespitePolicy;
        public int ExcludedValue => excludedByPolicy;
        public void SetBoth(int included, int excluded)
        {
            includedDespitePolicy = included;
            excludedByPolicy = excluded;
        }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Member_Level_FastClonerBehavior_Wins_Over_Type_Visibility_Policy()
    {
        MemberLevelOverridesPolicy original = new MemberLevelOverridesPolicy { Pub = 1 };
        original.SetBoth(included: 42, excluded: 99);

        MemberLevelOverridesPolicy clone = original.FastDeepClone();

        await Assert.That(clone!.Pub).IsEqualTo(1);
        await Assert.That(clone.IncludedValue).IsEqualTo(42);
        await Assert.That(clone.ExcludedValue).IsEqualTo(0);
    }

    [FastClonerClonable]
    [FastClonerVisibility(FastClonerMemberVisibility.All)]
    public class IgnoreWinsOverPolicy
    {
        public int Pub;
        [global::FastCloner.Code.FastClonerIgnore] private int alwaysSkipped;

        public int SkippedValue => alwaysSkipped;
        public void SetSkipped(int v) => alwaysSkipped = v;
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Member_Level_Ignore_Wins_Over_Permissive_Type_Policy()
    {
        IgnoreWinsOverPolicy original = new IgnoreWinsOverPolicy { Pub = 1 };
        original.SetSkipped(123);

        IgnoreWinsOverPolicy clone = original.FastDeepClone();

        await Assert.That(clone!.Pub).IsEqualTo(1);
        await Assert.That(clone.SkippedValue).IsEqualTo(0);
    }

    [FastClonerVisibility(FastClonerMemberVisibility.Public)]
    public class SgBaseWithPolicy
    {
        public int BasePublic;
        private int _basePrivate;
        public void SetBasePrivate(int v) => _basePrivate = v;
        public int GetBasePrivate() => _basePrivate;
    }

    [FastClonerClonable]
    public class SgDerivedInheritsPolicy : SgBaseWithPolicy
    {
        public int DerivedPublic { get; set; }
        private int _derivedPrivate;
        public void SetDerivedPrivate(int v) => _derivedPrivate = v;
        public int GetDerivedPrivate() => _derivedPrivate;
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task SG_Visibility_Policy_Is_Inherited_From_Base_Type()
    {
        SgDerivedInheritsPolicy original = new SgDerivedInheritsPolicy { BasePublic = 1, DerivedPublic = 3 };
        original.SetBasePrivate(2);
        original.SetDerivedPrivate(4);

        SgDerivedInheritsPolicy clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.BasePublic).IsEqualTo(1);
        await Assert.That(clone.GetBasePrivate()).IsEqualTo(0);
        await Assert.That(clone.DerivedPublic).IsEqualTo(3);
        await Assert.That(clone.GetDerivedPrivate()).IsEqualTo(0);
    }

    [FastClonerClonable]
    [FastClonerVisibility(FastClonerMemberVisibility.Public)]
    public class SgMixedAccessibilityPropertyDto
    {
        // Property is publicly declared even though the setter is private. Under
        // [FastClonerVisibility(Public)] it must be cloned (mask = most-permissive of get/set).
        public int PublicGetPrivateSet { get; private set; }
        public void Set(int v) => PublicGetPrivateSet = v;
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task SG_PublicOnly_Policy_Includes_Property_With_Public_Get_And_Private_Set()
    {
        SgMixedAccessibilityPropertyDto original = new SgMixedAccessibilityPropertyDto();
        original.Set(42);

        SgMixedAccessibilityPropertyDto clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.PublicGetPrivateSet).IsEqualTo(42);
    }

    [FastClonerClonable]
    [FastClonerVisibility(FastClonerMemberVisibility.Public)]
    public class SgIgnoreFalseOverridesPolicy
    {
        public int Pub;

        // Type-level Public-only policy excludes private; explicit "don't ignore me"
        // member-level attribute should put it back in (parity with runtime).
        [global::FastCloner.Code.FastClonerIgnore(false)]
        private int forciblyIncluded;

        public int IncludedValue => forciblyIncluded;
        public void SetIncluded(int v) => forciblyIncluded = v;
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task SG_FastClonerIgnore_False_Overrides_Visibility_Policy_Exclusion()
    {
        SgIgnoreFalseOverridesPolicy original = new SgIgnoreFalseOverridesPolicy { Pub = 1 };
        original.SetIncluded(7);

        SgIgnoreFalseOverridesPolicy clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Pub).IsEqualTo(1);
        await Assert.That(clone.IncludedValue).IsEqualTo(7);
    }

    [FastClonerClonable]
    public class SgClassWithPrivateInitProperty
    {
        // Non-accessible init setter on an auto-property. The SG routes the assignment
        // through the auto-property's backing field via [UnsafeAccessor], bypassing the
        // init-restriction entirely.
        public int PrivateInit { get; private init; }

        public string? Tag { get; set; }

        public SgClassWithPrivateInitProperty() { }
        public SgClassWithPrivateInitProperty(int init) { PrivateInit = init; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task SG_PrivateInit_Property_Is_Cloned_Via_Backing_Field()
    {
        SgClassWithPrivateInitProperty original = new SgClassWithPrivateInitProperty(99) { Tag = "ok" };

        SgClassWithPrivateInitProperty clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Tag).IsEqualTo("ok");
        await Assert.That(clone.PrivateInit).IsEqualTo(99);
    }

    [FastClonerClonable]
    public class SgClassWithPrivateInitNonAutoProperty
    {
        private int _backing;

        // Non-auto property whose init setter is private. There is no auto-property backing
        // field, so the SG must emit an [UnsafeAccessor] for set_X. The IsExternalInit modreq
        // on the init setter is ignored by name-based UnsafeAccessor binding.
        public int CustomInit
        {
            get => _backing;
            private init => _backing = value;
        }

        public string? Tag { get; set; }

        public SgClassWithPrivateInitNonAutoProperty() { }
        public SgClassWithPrivateInitNonAutoProperty(int init) { CustomInit = init; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task SG_NonAuto_PrivateInit_Property_Is_Cloned_Via_Backing_Field()
    {
        // The non-auto property's setter is a trivial assignment (`_backing = value`) to a
        // field that is also collected, so the SG dedups the property's SetterMethod accessor
        // and clones via the field directly. The post-condition is the same: PrivateInit is
        // observed equal in the clone.
        SgClassWithPrivateInitNonAutoProperty original = new SgClassWithPrivateInitNonAutoProperty(123) { Tag = "ok2" };

        SgClassWithPrivateInitNonAutoProperty clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Tag).IsEqualTo("ok2");
        await Assert.That(clone.CustomInit).IsEqualTo(123);
    }

    [FastClonerClonable]
    public class SgClassWithComputedPrivateInitNonAuto
    {
        // Storage and observed value differ: the setter doubles the input. Dedup-via-field
        // would lose this transformation, so the SG must keep the SetterMethod UnsafeAccessor
        // here (i.e. NOT dedup). Cloning then observes the same `Computed` value because
        // we go through the property accessor which inverts the doubling on read.
        private int _half;

        public int Computed
        {
            get => _half * 2;
            private init => _half = value / 2;
        }

        public string? Tag { get; set; }

        public SgClassWithComputedPrivateInitNonAuto() { }
        public SgClassWithComputedPrivateInitNonAuto(int v) { Computed = v; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task SG_NonAuto_PrivateInit_With_Computed_Setter_Keeps_Setter_Accessor()
    {
        SgClassWithComputedPrivateInitNonAuto original = new SgClassWithComputedPrivateInitNonAuto(20) { Tag = "ok3" };

        SgClassWithComputedPrivateInitNonAuto clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Tag).IsEqualTo("ok3");
        await Assert.That(clone.Computed).IsEqualTo(20);
    }


    #endregion

    #region Issue 5: Delegate and Behavioral Types (Lazy, Func, Task)

    [FastClonerClonable]
    public class ClassWithDelegates
    {
        public Func<int>? IntFunc { get; set; }
        public Action? SimpleAction { get; set; }
        public string? Name { get; set; }
    }

    [FastClonerClonable]
    public class ClassWithLazy
    {
        public Lazy<string>? LazyValue { get; set; }
        public int RegularValue { get; set; }
    }

    [FastClonerClonable]
    public class ClassWithWeakReference
    {
        public WeakReference<object>? WeakRef { get; set; }
        public string? Name { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Delegates_Should_Be_Shallow_Copied()
    {
        // Arrange
        int counter = 0;
        ClassWithDelegates original = new ClassWithDelegates
        {
            IntFunc = () => ++counter,
            SimpleAction = () => counter++,
            Name = "Test"
        };

        // Act
        ClassWithDelegates clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Name).IsEqualTo("Test");
        // Delegates should be the same reference (shallow copied)
        await Assert.That(ReferenceEquals(clone.IntFunc, original.IntFunc)).IsTrue();
        await Assert.That(ReferenceEquals(clone.SimpleAction, original.SimpleAction)).IsTrue();

        // Both should reference the same counter
        clone.IntFunc!();
        await Assert.That(counter).IsEqualTo(1);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Lazy_Should_Be_Shallow_Copied()
    {
        // Arrange
        int initCount = 0;
        ClassWithLazy original = new ClassWithLazy
        {
            LazyValue = new Lazy<string>(() =>
            {
                initCount++;
                return "Initialized";
            }),
            RegularValue = 42
        };

        // Act
        ClassWithLazy clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.RegularValue).IsEqualTo(42);
        // Lazy should be same reference (shallow copied)
        await Assert.That(clone.LazyValue).IsSameReferenceAs(original.LazyValue);

        // Accessing value should only initialize once
        string _ = clone.LazyValue!.Value;
        string __ = original.LazyValue!.Value;
        await Assert.That(initCount).IsEqualTo(1); // Should be 1 because same Lazy instance
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task WeakReference_Should_Be_Shallow_Copied()
    {
        // Arrange
        object target = new object();
        ClassWithWeakReference original = new ClassWithWeakReference
        {
            WeakRef = new WeakReference<object>(target),
            Name = "Test"
        };

        // Act
        ClassWithWeakReference clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Name).IsEqualTo("Test");
        // WeakReference should be same instance (shallow copied)
        await Assert.That(clone.WeakRef).IsSameReferenceAs(original.WeakRef);

        // Both should reference the same target
        original.WeakRef!.TryGetTarget(out object? origTarget);
        clone.WeakRef!.TryGetTarget(out object? cloneTarget);
        await Assert.That(cloneTarget).IsSameReferenceAs(origTarget);
    }

    #endregion
    
    // Test combining multiple edge cases

    [FastClonerClonable]
    public class ComplexEdgeCase
    {
        public int[,]? Matrix { get; set; }
        public List<int>? ListField;
        public string? Name { get; init; }
        public Func<bool>? Predicate { get; set; }
        public int RegularProp { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Complex_EdgeCase_Combining_Multiple_Issues()
    {
        // Arrange
        ComplexEdgeCase original = new ComplexEdgeCase
        {
            Matrix = new int[2, 2] { { 1, 2 }, { 3, 4 } },
            ListField = [10, 20, 30],
            Name = "Complex",
            Predicate = () => true,
            RegularProp = 999
        };

        // Act
        ComplexEdgeCase clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();

        // Multi-dim array should be cloned
        await Assert.That(clone!.Matrix).IsNotNull();
        await Assert.That(clone.Matrix).IsNotSameReferenceAs(original.Matrix);

        // Field should be cloned
        await Assert.That(clone.ListField).IsNotNull();
        await Assert.That(clone.ListField).IsNotSameReferenceAs(original.ListField);
        await Assert.That(clone.ListField!.Count).IsEqualTo(3);

        // Init property should be cloned
        await Assert.That(clone.Name).IsEqualTo("Complex");

        // Delegate should be shallow copied
        await Assert.That(ReferenceEquals(clone.Predicate, original.Predicate)).IsTrue();

        // Regular property should be cloned
        await Assert.That(clone.RegularProp).IsEqualTo(999);

        // Verify independence
        original.ListField!.Add(100);
        original.Matrix![0, 0] = 999;
        await Assert.That(clone.ListField.Count).IsEqualTo(3);
        await Assert.That(clone.Matrix![0, 0]).IsEqualTo(1);
    }

    #region Issue 6b: Multi-dimensional Arrays with Special Types

    [FastClonerClonable]
    public class ClassWithHttpClientMatrix
    {
        public HttpClient[,]? ClientMatrix { get; set; }
        public string? Name { get; set; }
    }

    [FastClonerClonable]
    public class ClassWithHttpClient3dArray
    {
        public HttpClient[,,]? ClientCube { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task MultiDimArray_HttpClient_Should_Be_DeepCloned()
    {
        // Arrange
        HttpClient client1 = new HttpClient();
        HttpClient client2 = new HttpClient();
        HttpClient client3 = new HttpClient();
        HttpClient client4 = new HttpClient();

        ClassWithHttpClientMatrix original = new ClassWithHttpClientMatrix
        {
            Name = "HttpClientTest",
            ClientMatrix = new HttpClient[2, 2]
            {
                { client1, client2 },
                { client3, client4 }
            }
        };

        // Act
        ClassWithHttpClientMatrix clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Name).IsEqualTo("HttpClientTest");
        await Assert.That(clone.ClientMatrix).IsNotNull();

        // The matrix itself should be a different reference (new array)
        await Assert.That(clone.ClientMatrix).IsNotSameReferenceAs(original.ClientMatrix);

        // HttpClient instances should be deep cloned (different references)
        await Assert.That(clone.ClientMatrix![0, 0]).IsNotSameReferenceAs(client1);
        await Assert.That(clone.ClientMatrix[0, 1]).IsNotSameReferenceAs(client2);
        await Assert.That(clone.ClientMatrix[1, 0]).IsNotSameReferenceAs(client3);
        await Assert.That(clone.ClientMatrix[1, 1]).IsNotSameReferenceAs(client4);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task MultiDimArray_3D_HttpClient_Should_Be_DeepCloned()
    {
        // Arrange
        HttpClient[,,] clients = new HttpClient[2, 2, 2];
        for (int i = 0; i < 2; i++)
            for (int j = 0; j < 2; j++)
                for (int k = 0; k < 2; k++)
                    clients[i, j, k] = new HttpClient();

        ClassWithHttpClient3dArray original = new ClassWithHttpClient3dArray
        {
            ClientCube = clients
        };

        // Act
        ClassWithHttpClient3dArray clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.ClientCube).IsNotNull();
        await Assert.That(clone.ClientCube).IsNotSameReferenceAs(original.ClientCube);

        // All HttpClient instances should be deep cloned (different references)
        for (int i = 0; i < 2; i++)
            for (int j = 0; j < 2; j++)
                for (int k = 0; k < 2; k++)
                    await Assert.That(clone.ClientCube![i, j, k]).IsNotSameReferenceAs(original.ClientCube![i, j, k]);
    }

    #endregion

    #region Issue 7: Jagged Arrays

    [FastClonerClonable]
    public class ClassWithJaggedArray
    {
        public int[][]? JaggedInts { get; set; }
        public string? Name { get; set; }
    }

    [FastClonerClonable]
    public class ClassWith3LevelJaggedArray
    {
        public int[][][]? DeepJagged { get; set; }
    }

    [FastClonerClonable]
    public class ClassWithJaggedArrayOfObjects
    {
        public SimpleItem[][]? Items { get; set; }
    }

    public class SimpleItem
    {
        public string? Name { get; set; }
        public int Value { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task JaggedArray_2D_Should_Be_Cloned()
    {
        // Arrange
        ClassWithJaggedArray original = new ClassWithJaggedArray
        {
            Name = "Test",
            JaggedInts = new int[][]
            {
                [1, 2, 3],
                [4, 5],
                [6, 7, 8, 9]
            }
        };

        // Act
        ClassWithJaggedArray clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Name).IsEqualTo("Test");
        await Assert.That(clone.JaggedInts).IsNotNull();
        await Assert.That(clone.JaggedInts).IsNotSameReferenceAs(original.JaggedInts);
        await Assert.That(clone.JaggedInts!.Length).IsEqualTo(3);

        // Each inner array should be a different reference
        await Assert.That(clone.JaggedInts[0]).IsNotSameReferenceAs(original.JaggedInts![0]);
        await Assert.That(clone.JaggedInts[1]).IsNotSameReferenceAs(original.JaggedInts[1]);
        await Assert.That(clone.JaggedInts[2]).IsNotSameReferenceAs(original.JaggedInts[2]);

        // Values should be the same
        await Assert.That(clone.JaggedInts[0]).IsEquivalentTo([1, 2, 3]);
        await Assert.That(clone.JaggedInts[1]).IsEquivalentTo([4, 5]);
        await Assert.That(clone.JaggedInts[2]).IsEquivalentTo([6, 7, 8, 9]);

        // Verify independence
        original.JaggedInts[0][0] = 999;
        await Assert.That(clone.JaggedInts[0][0]).IsEqualTo(1);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task JaggedArray_3D_Should_Be_Cloned()
    {
        // Arrange
        ClassWith3LevelJaggedArray original = new ClassWith3LevelJaggedArray
        {
            DeepJagged = new int[][][]
            {
                new int[][]
                {
                    [1, 2],
                    [3, 4, 5]
                },
                new int[][]
                {
                    [6]
                }
            }
        };

        // Act
        ClassWith3LevelJaggedArray clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.DeepJagged).IsNotNull();
        await Assert.That(clone.DeepJagged).IsNotSameReferenceAs(original.DeepJagged);
        await Assert.That(clone.DeepJagged!.Length).IsEqualTo(2);

        // All levels should be different references
        await Assert.That(clone.DeepJagged[0]).IsNotSameReferenceAs(original.DeepJagged![0]);
        await Assert.That(clone.DeepJagged[0][0]).IsNotSameReferenceAs(original.DeepJagged[0][0]);
        await Assert.That(clone.DeepJagged[1][0]).IsNotSameReferenceAs(original.DeepJagged[1][0]);

        // Values should be preserved
        await Assert.That(clone.DeepJagged[0][0]).IsEquivalentTo([1, 2]);
        await Assert.That(clone.DeepJagged[0][1]).IsEquivalentTo([3, 4, 5]);
        await Assert.That(clone.DeepJagged[1][0]).IsEquivalentTo([6]);

        // Verify independence
        original.DeepJagged[0][0][0] = 999;
        await Assert.That(clone.DeepJagged[0][0][0]).IsEqualTo(1);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task JaggedArray_WithObjects_Should_Be_Cloned()
    {
        // Arrange
        ClassWithJaggedArrayOfObjects original = new ClassWithJaggedArrayOfObjects
        {
            Items = new SimpleItem[][]
            {
                [
                    new SimpleItem { Name = "A1", Value = 1 },
                    new SimpleItem { Name = "A2", Value = 2 }
                ],
                [
                    new SimpleItem { Name = "B1", Value = 10 }
                ]
            }
        };

        // Act
        ClassWithJaggedArrayOfObjects clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Items).IsNotNull();
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items!.Length).IsEqualTo(2);

        // Inner arrays should be different references
        await Assert.That(clone.Items[0]).IsNotSameReferenceAs(original.Items![0]);
        await Assert.That(clone.Items[1]).IsNotSameReferenceAs(original.Items[1]);

        // Objects should be different references but same values
        await Assert.That(clone.Items[0][0]).IsNotSameReferenceAs(original.Items[0][0]);
        await Assert.That(clone.Items[0][0].Name).IsEqualTo("A1");
        await Assert.That(clone.Items[0][0].Value).IsEqualTo(1);

        await Assert.That(clone.Items[1][0]).IsNotSameReferenceAs(original.Items[1][0]);
        await Assert.That(clone.Items[1][0].Name).IsEqualTo("B1");
        await Assert.That(clone.Items[1][0].Value).IsEqualTo(10);

        // Verify independence
        original.Items[0][0].Name = "Changed";
        await Assert.That(clone.Items[0][0].Name).IsEqualTo("A1");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task JaggedArray_WithNullElements_Should_Be_Handled()
    {
        // Arrange
        ClassWithJaggedArray original = new ClassWithJaggedArray
        {
            Name = "WithNulls",
            JaggedInts = new int[][]
            {
                [1, 2],
                null!,  // null element in the outer array
                [3]
            }
        };

        // Act
        ClassWithJaggedArray clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.JaggedInts).IsNotNull();
        await Assert.That(clone.JaggedInts!.Length).IsEqualTo(3);
        await Assert.That(clone.JaggedInts[0]).IsEquivalentTo([1, 2]);
        await Assert.That(clone.JaggedInts[1]).IsNull();
        await Assert.That(clone.JaggedInts[2]).IsEquivalentTo([3]);
    }

    #endregion

    #region Additional Tests for Struct Fields

    [FastClonerClonable]
    public struct StructWithFields
    {
        public int IntField;
        public string? StringProp { get; set; }
        public List<int>? ListField;
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Struct_With_Fields_Should_Be_Cloned()
    {
        // Arrange
        StructWithFields original = new StructWithFields
        {
            IntField = 42,
            StringProp = "Test",
            ListField = [1, 2, 3]
        };

        // Act
        StructWithFields clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone.IntField).IsEqualTo(42);
        await Assert.That(clone.StringProp).IsEqualTo("Test");
        await Assert.That(clone.ListField).IsNotNull();
        // Note: For value types, the list is a new reference due to the struct copy
        await Assert.That(clone.ListField).IsEquivalentTo([1, 2, 3]);
    }

    #endregion

    #region Issue 8: Required Members (Runtime Fallback)

    [FastClonerClonable]
    public class ClassWithRequiredMembers
    {
        public required string RequiredName { get; set; }
        public required int RequiredId { get; set; }
        public string? OptionalDescription { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Class_With_Required_Members_Should_Be_Cloned()
    {
        // Arrange
        ClassWithRequiredMembers original = new ClassWithRequiredMembers 
        { 
            RequiredName = "Required", 
            RequiredId = 123,
            OptionalDescription = "Optional" 
        };

        // Act
        ClassWithRequiredMembers clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone!.RequiredName).IsEqualTo("Required");
        await Assert.That(clone.RequiredId).IsEqualTo(123);
        await Assert.That(clone.OptionalDescription).IsEqualTo("Optional");
    }

    #endregion

    #region Issue 9: Init-Only Properties with Circular References (Runtime Fallback)

    [FastClonerClonable]
    public class ClassWithInitAndCycle
    {
        public string? Name { get; init; }
        public ClassWithInitAndCycle? Self { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Class_With_Init_Properties_And_Cycles_Should_Be_Deep_Cloned()
    {
        // Arrange
        ClassWithInitAndCycle original = new ClassWithInitAndCycle
        {
            Name = "CyclicInit"
        };
        original.Self = original;

        // Act
        ClassWithInitAndCycle clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone!.Name).IsEqualTo("CyclicInit"); // This would be null without the fix
        await Assert.That(clone.Self).IsSameReferenceAs(clone);
    }

    #endregion

    #region Issue 10: Structs with Readonly Reference Fields (Runtime Fallback)

    [FastClonerClonable]
    public struct StructWithReadonlyRefs
    {
        public readonly List<int> ReadonlyList;
        public int NormalField;

        public StructWithReadonlyRefs(List<int> list, int val)
        {
            ReadonlyList = list;
            NormalField = val;
        }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Struct_With_Readonly_Reference_Fields_Should_Be_Deep_Cloned()
    {
        // Arrange
        List<int> list = [1, 2, 3];
        StructWithReadonlyRefs original = new StructWithReadonlyRefs(list, 42);

        // Act
        StructWithReadonlyRefs clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone.NormalField).IsEqualTo(42);
        await Assert.That(clone.ReadonlyList).IsNotNull();
        await Assert.That(clone.ReadonlyList).IsNotSameReferenceAs(original.ReadonlyList); // This would fail (be SameAs) without the fix
        await Assert.That(clone.ReadonlyList).IsEquivalentTo([1, 2, 3]);

        // Verify independence
        list.Add(4);
        await Assert.That(clone.ReadonlyList.Count).IsEqualTo(3);
    }

    #endregion

    #region Issue 11: ObservableCollection Properties Without Setters (GitHub Issue #19)

    // Test class for ObservableCollection with getter only - currently NOT supported
    // This represents the bug reported in GitHub Issue #19
    [FastClonerClonable]
    public class ClassWithObservableCollectionGetterOnly
    {
        public ObservableCollection<string> Items { get; } = [];
        public string? Name { get; set; }
    }

    // Test class for ObservableCollection with getter and setter - should work
    [FastClonerClonable]
    public class ClassWithObservableCollectionGetterSetter
    {
        public ObservableCollection<string>? Items { get; set; }
        public string? Name { get; set; }
    }

    // Test class for ObservableCollection with init - workaround from the issue
    [FastClonerClonable]
    public class ClassWithObservableCollectionInit
    {
        public ObservableCollection<string> Items { get; init; } = [];
        public string? Name { get; set; }
    }

    // Test class with nested object in ObservableCollection
    public class ObservableItem
    {
        public string? Value { get; set; }
        public int Id { get; set; }
    }

    [FastClonerClonable]
    public class ClassWithObservableCollectionOfObjects
    {
        public ObservableCollection<ObservableItem> Items { get; } = [];
        public string? Description { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ObservableCollection_WithGetterOnly_Should_Be_Cloned()
    {
        // Arrange - This test documents the expected behavior for Issue #19
        // Currently this scenario is NOT supported by the source generator
        ClassWithObservableCollectionGetterOnly original = new ClassWithObservableCollectionGetterOnly
        {
            Name = "Test"
        };
        original.Items.Add("Item1");
        original.Items.Add("Item2");
        original.Items.Add("Item3");

        // Act
        ClassWithObservableCollectionGetterOnly clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Name).IsEqualTo("Test");
        await Assert.That(clone.Items).IsNotNull();
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items.Count).IsEqualTo(3);
        await Assert.That(clone.Items[0]).IsEqualTo("Item1");
        await Assert.That(clone.Items[1]).IsEqualTo("Item2");
        await Assert.That(clone.Items[2]).IsEqualTo("Item3");

        // Verify independence
        original.Items.Add("NewItem");
        original.Items[0] = "Modified";
        await Assert.That(clone.Items.Count).IsEqualTo(3);
        await Assert.That(clone.Items[0]).IsEqualTo("Item1");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ObservableCollection_WithGetterSetter_Should_Be_Cloned()
    {
        // Arrange - This scenario is already supported
        ClassWithObservableCollectionGetterSetter original = new ClassWithObservableCollectionGetterSetter
        {
            Name = "Test",
            Items = ["Item1", "Item2", "Item3"]
        };

        // Act
        ClassWithObservableCollectionGetterSetter clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Name).IsEqualTo("Test");
        await Assert.That(clone.Items).IsNotNull();
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items!.Count).IsEqualTo(3);
        await Assert.That(clone.Items[0]).IsEqualTo("Item1");
        await Assert.That(clone.Items[1]).IsEqualTo("Item2");
        await Assert.That(clone.Items[2]).IsEqualTo("Item3");

        // Verify independence
        original.Items!.Add("NewItem");
        original.Items[0] = "Modified";
        await Assert.That(clone.Items.Count).IsEqualTo(3);
        await Assert.That(clone.Items[0]).IsEqualTo("Item1");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ObservableCollection_WithInit_Should_Be_Cloned()
    {
        // Arrange - This is the workaround mentioned in Issue #19
        ClassWithObservableCollectionInit original = new ClassWithObservableCollectionInit
        {
            Name = "Test",
            Items = ["Item1", "Item2", "Item3"]
        };

        // Act
        ClassWithObservableCollectionInit clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Name).IsEqualTo("Test");
        await Assert.That(clone.Items).IsNotNull();
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items.Count).IsEqualTo(3);
        await Assert.That(clone.Items[0]).IsEqualTo("Item1");
        await Assert.That(clone.Items[1]).IsEqualTo("Item2");
        await Assert.That(clone.Items[2]).IsEqualTo("Item3");

        // Verify independence
        original.Items.Add("NewItem");
        original.Items[0] = "Modified";
        await Assert.That(clone.Items.Count).IsEqualTo(3);
        await Assert.That(clone.Items[0]).IsEqualTo("Item1");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ObservableCollection_WithObjects_GetterOnly_Should_Be_Deep_Cloned()
    {
        // Arrange - Test with nested objects to verify deep cloning
        ClassWithObservableCollectionOfObjects original = new ClassWithObservableCollectionOfObjects
        {
            Description = "Container"
        };
        original.Items.Add(new ObservableItem { Value = "First", Id = 1 });
        original.Items.Add(new ObservableItem { Value = "Second", Id = 2 });

        // Act
        ClassWithObservableCollectionOfObjects clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Description).IsEqualTo("Container");
        await Assert.That(clone.Items).IsNotNull();
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items.Count).IsEqualTo(2);

        // Verify objects are also deep cloned (different references)
        await Assert.That(clone.Items[0]).IsNotSameReferenceAs(original.Items[0]);
        await Assert.That(clone.Items[1]).IsNotSameReferenceAs(original.Items[1]);
        await Assert.That(clone.Items[0].Value).IsEqualTo("First");
        await Assert.That(clone.Items[0].Id).IsEqualTo(1);
        await Assert.That(clone.Items[1].Value).IsEqualTo("Second");
        await Assert.That(clone.Items[1].Id).IsEqualTo(2);

        // Verify independence
        original.Items[0].Value = "Modified";
        original.Items.Add(new ObservableItem { Value = "Third", Id = 3 });
        await Assert.That(clone.Items[0].Value).IsEqualTo("First");
        await Assert.That(clone.Items.Count).IsEqualTo(2);
    }

    #endregion

    #region Issue 12: Nullable Warnings in Generated Collection Clone Code (GitHub Issue #30)

    /// <summary>
    /// Non-clonable type (no parameterless ctor, no [FastClonerClonable]).
    /// The source generator falls back to FastCloner.DeepClone() for this type,
    /// which previously generated a nullable cast (Type?) causing CS8604 warnings.
    /// </summary>
    public class ExternalNonClonableItem
    {
        public ExternalNonClonableItem(int id) { Id = id; }
        public int Id { get; set; }
        public string? Label { get; set; }
    }

    [FastClonerClonable]
    public class ContainerWithNonClonableList
    {
        public List<ExternalNonClonableItem> Items { get; set; } = [];
    }

    [FastClonerClonable]
    public class ContainerWithNonClonableArray
    {
        public ExternalNonClonableItem[] Items { get; set; } = [];
    }

    [FastClonerClonable]
    public class ContainerWithNonClonableDictionary
    {
        public Dictionary<string, ExternalNonClonableItem> Items { get; set; } = new();
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task List_WithNonClonableElements_ShouldDeepCloneViaFastCloner()
    {
        ContainerWithNonClonableList original = new()
        {
            Items =
            [
                new ExternalNonClonableItem(1) { Label = "First" },
                new ExternalNonClonableItem(2) { Label = "Second" },
                new ExternalNonClonableItem(3) { Label = "Third" }
            ]
        };

        ContainerWithNonClonableList clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items.Count).IsEqualTo(3);

        for (int i = 0; i < original.Items.Count; i++)
        {
            await Assert.That(clone.Items[i]).IsNotSameReferenceAs(original.Items[i]);
            await Assert.That(clone.Items[i].Id).IsEqualTo(original.Items[i].Id);
            await Assert.That(clone.Items[i].Label).IsEqualTo(original.Items[i].Label);
        }

        original.Items[0].Label = "Modified";
        original.Items.Add(new ExternalNonClonableItem(4) { Label = "Fourth" });
        await Assert.That(clone.Items[0].Label).IsEqualTo("First");
        await Assert.That(clone.Items.Count).IsEqualTo(3);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Array_WithNonClonableElements_ShouldDeepCloneViaFastCloner()
    {
        ContainerWithNonClonableArray original = new()
        {
            Items =
            [
                new ExternalNonClonableItem(1) { Label = "A" },
                new ExternalNonClonableItem(2) { Label = "B" }
            ]
        };

        ContainerWithNonClonableArray clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items.Length).IsEqualTo(2);
        await Assert.That(clone.Items[0]).IsNotSameReferenceAs(original.Items[0]);
        await Assert.That(clone.Items[0].Id).IsEqualTo(1);
        await Assert.That(clone.Items[0].Label).IsEqualTo("A");
        await Assert.That(clone.Items[1].Id).IsEqualTo(2);

        original.Items[0].Label = "Modified";
        await Assert.That(clone.Items[0].Label).IsEqualTo("A");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Dictionary_WithNonClonableValues_ShouldDeepCloneViaFastCloner()
    {
        ContainerWithNonClonableDictionary original = new()
        {
            Items = new Dictionary<string, ExternalNonClonableItem>
            {
                ["x"] = new ExternalNonClonableItem(1) { Label = "X" },
                ["y"] = new ExternalNonClonableItem(2) { Label = "Y" }
            }
        };

        ContainerWithNonClonableDictionary clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items.Count).IsEqualTo(2);
        await Assert.That(clone.Items["x"]).IsNotSameReferenceAs(original.Items["x"]);
        await Assert.That(clone.Items["x"].Id).IsEqualTo(1);
        await Assert.That(clone.Items["x"].Label).IsEqualTo("X");
        await Assert.That(clone.Items["y"].Id).IsEqualTo(2);

        original.Items["x"].Label = "Modified";
        await Assert.That(clone.Items["x"].Label).IsEqualTo("X");
    }

    [FastClonerClonable]
    public class ContainerWithObservableNonClonable
    {
        public ObservableCollection<ExternalNonClonableItem> Items { get; set; } = [];
        public string? Name { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task ObservableCollection_WithNonClonableElements_ShouldDeepCloneViaFastCloner()
    {
        ContainerWithObservableNonClonable original = new()
        {
            Name = "Issue30",
            Items =
            [
                new ExternalNonClonableItem(1) { Label = "Alpha" },
                new ExternalNonClonableItem(2) { Label = "Beta" },
                new ExternalNonClonableItem(3) { Label = "Gamma" }
            ]
        };

        ContainerWithObservableNonClonable clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone.Name).IsEqualTo("Issue30");
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items.Count).IsEqualTo(3);

        for (int i = 0; i < original.Items.Count; i++)
        {
            await Assert.That(clone.Items[i]).IsNotSameReferenceAs(original.Items[i]);
            await Assert.That(clone.Items[i].Id).IsEqualTo(original.Items[i].Id);
            await Assert.That(clone.Items[i].Label).IsEqualTo(original.Items[i].Label);
        }

        original.Items[0].Label = "Modified";
        original.Items.Add(new ExternalNonClonableItem(4) { Label = "Delta" });
        await Assert.That(clone.Items[0].Label).IsEqualTo("Alpha");
        await Assert.That(clone.Items.Count).IsEqualTo(3);
    }

    #endregion
}