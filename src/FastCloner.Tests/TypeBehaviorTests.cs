using System.Collections.Concurrent;
using FastCloner.Code;
using System.Threading.Tasks;

namespace FastCloner.Tests;
[NotInParallel("FastClonerGlobalState")]
public class TypeBehaviorTests(int maxRecursionDepth) : BaseTestFixture(maxRecursionDepth)
{
    public class SimpleClass
    {
        public int IntValue { get; set; }
        public string? StringValue { get; set; }
    }

    public class AnotherSimpleClass
    {
        public double DoubleValue { get; set; }
    }

    public class ClassWithProperties
    {
        public int Id { get; set; }
        public SimpleClass? SimpleProp { get; set; }
        public AnotherSimpleClass? AnotherSimpleProp { get; set; }
        public List<SimpleClass>? ListOfSimpleProp { get; set; }
    }

    public class ClassToBeIgnored
    {
        public string Data { get; set; } = "InitialData";
    }

    public record struct MyValueType
    {
        public int Value { get; set; }
    }

    public class ClassWithValueTypeProperty
    {
        public int Id { get; set; }
        public MyValueType MyStruct { get; set; }
    }

    [After(Test)]
    public void TearDown()
    {
        FastCloner.SetDisableOptionalFeatures(false);
        FastCloner.ClearAllTypeBehaviors();
    }

    [Test]
    public async Task SetTypeBehavior_Ignore_PropertyOfIgnoredReferenceType_IsNullAfterCloning()
    {
        // Arrange
        ClassWithProperties original = new ClassWithProperties
        {
            Id = 1,
            SimpleProp = new SimpleClass { IntValue = 10, StringValue = "Test" },
            AnotherSimpleProp = new AnotherSimpleClass { DoubleValue = 1.23 }
        };
        FastCloner.SetTypeBehavior<SimpleClass>(CloneBehavior.Ignore);

        // Act
        ClassWithProperties cloned = original.DeepClone();
        FastCloner.ClearTypeBehavior<SimpleClass>();
        ClassWithProperties cloned2 = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned).IsNotSameReferenceAs(original);
        await Assert.That(cloned.Id).IsEqualTo(original.Id);
        await Assert.That(cloned.SimpleProp).IsNull().Because("SimpleProp should be null as its type is ignored.");
        await Assert.That(cloned.AnotherSimpleProp).IsNotNull().Because("AnotherSimpleProp should be cloned.");
        await Assert.That(cloned.AnotherSimpleProp).IsNotSameReferenceAs(original.AnotherSimpleProp);
        await Assert.That(cloned.AnotherSimpleProp!.DoubleValue).IsEqualTo(original.AnotherSimpleProp!.DoubleValue);

        await Assert.That(cloned2.SimpleProp).IsNotNull().Because("SimpleProp should not null after resetting the ignored types.");
    }

    [Test]
    public async Task SetTypeBehavior_Reference_ReturnsSameInstance()
    {
        // Arrange
        SimpleClass original = new SimpleClass { IntValue = 42, StringValue = "KeepMe" };
        FastCloner.SetTypeBehavior<SimpleClass>(CloneBehavior.Reference);

        // Act
        SimpleClass cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsSameReferenceAs(original);
    }

    [Test]
    public async Task SetTypeBehavior_Reference_PropertyOfReferenceType_ReturnsSameInstance()
    {
        // Arrange
        ClassWithProperties original = new ClassWithProperties
        {
            Id = 1,
            SimpleProp = new SimpleClass { IntValue = 10, StringValue = "Test" },
        };
        FastCloner.SetTypeBehavior<SimpleClass>(CloneBehavior.Reference);

        // Act
        ClassWithProperties cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned).IsNotSameReferenceAs(original);
        await Assert.That(cloned.SimpleProp).IsSameReferenceAs(original.SimpleProp);
    }

    [Test]
    public async Task SetTypeBehavior_Ignore_PropertyOfIgnoredValueType_IsDefaultAfterCloning()
    {
        // Arrange
        ClassWithValueTypeProperty original = new ClassWithValueTypeProperty
        {
            Id = 1,
            MyStruct = new MyValueType { Value = 42 }
        };
        FastCloner.SetTypeBehavior<MyValueType>(CloneBehavior.Ignore);

        // Act
        ClassWithValueTypeProperty cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned.Id).IsEqualTo(original.Id);
        await Assert.That(cloned.MyStruct).IsEqualTo(default(MyValueType)).Because("Ignored value type property should be default.");
        await Assert.That(cloned.MyStruct.Value).IsEqualTo(0);
    }

    [Test]
    public async Task SetTypeBehavior_UpdatesBehaviorCorrectly()
    {
        // Arrange
        FastCloner.ClearAllTypeBehaviors();

        // Act
        FastCloner.SetTypeBehavior<SimpleClass>(CloneBehavior.Ignore);
        CloneBehavior? behaviorIgnore = FastCloner.GetTypeBehavior<SimpleClass>();
        
        FastCloner.SetTypeBehavior<SimpleClass>(CloneBehavior.Reference);
        CloneBehavior? behaviorReference = FastCloner.GetTypeBehavior<SimpleClass>();

        FastCloner.SetTypeBehavior<SimpleClass>(CloneBehavior.Clone);
        CloneBehavior? behaviorDeep = FastCloner.GetTypeBehavior<SimpleClass>();

        // Assert
        await Assert.That(behaviorIgnore).IsEqualTo(CloneBehavior.Ignore);
        await Assert.That(behaviorReference).IsEqualTo(CloneBehavior.Reference);
        await Assert.That(behaviorDeep).IsNull().Because("Start behavior (Clone) should remove the entry.");
    }

    public class ClassWithPrimitiveProperties
    {
        public int IntProp { get; set; }
        public bool BoolProp { get; set; }
        public string? StringProp { get; set; }
    }

    public class ClassWithSetProperties
    {
        public int Id { get; set; }
        public HashSet<SimpleClass>? SetOfSimpleClass { get; set; }
        public HashSet<MyValueType>? SetOfMyValueType { get; set; }
        public HashSet<string>? SetOfString { get; set; }
    }

    public class ClassWithArrayProperties
    {
        public int Id { get; set; }
        public SimpleClass[]? ArrayOfSimpleClass { get; set; }
        public MyValueType[]? ArrayOfMyValueType { get; set; }
        public SimpleClass[,]? TwoDArrayOfSimpleClass { get; set; }
        public MyValueType[,]? TwoDArrayOfMyValueType { get; set; }
        public int[]? ArrayOfInt { get; set; }
        public string[]? ArrayOfString { get; set; }
    }

    public class ClassWithDictionaryProperties
    {
        public int Id { get; set; }
        public Dictionary<string, SimpleClass>? DictStringSimple { get; set; }
        public Dictionary<SimpleClass, string>? DictSimpleString { get; set; }
        public Dictionary<string, MyValueType>? DictStringValueType { get; set; }
        public Dictionary<MyValueType, string>? DictValueTypeString { get; set; }
        public Dictionary<int, string>? DictIntString { get; set; }
    }

    class IteratorInfo : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged = (_, _) => { };
        public bool HasPropertyChanged => PropertyChanged is not null;
    }

    [Test]
    public async Task SetTypeBehavior_Ignore_RootObjectOfIgnoredType_ReturnsNull()
    {
        // Arrange
        ClassToBeIgnored original = new ClassToBeIgnored { Data = "Important Data" };
        FastCloner.SetTypeBehavior<ClassToBeIgnored>(CloneBehavior.Ignore);

        // Act
        ClassToBeIgnored cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNull().Because("Cloning an object of an globally ignored type should return null.");
    }
    
    [Test]
    public async Task SetTypeBehavior_Ignore_ForPrimitiveIntProperty_SetsToDefault()
    {
        // Arrange
        ClassWithPrimitiveProperties original = new ClassWithPrimitiveProperties { IntProp = 123, BoolProp = true, StringProp = "Hello" };
        FastCloner.SetTypeBehavior<int>(CloneBehavior.Ignore);

        // Act
        ClassWithPrimitiveProperties cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned.IntProp).IsEqualTo(0).Because("Ignored int property should be default.");
        await Assert.That(cloned.BoolProp).IsEqualTo(original.BoolProp).Because("BoolProp should not be affected.");
        await Assert.That(cloned.StringProp).IsEqualTo(original.StringProp).Because("StringProp should not be affected.");
    }

    [Test]
    public async Task SetTypeBehavior_IgnoreTypes_MultiplePropertiesOfIgnoredTypes_AreNullOrDefaultForValueTypes()
    {
        // Arrange
        ClassWithProperties original = new ClassWithProperties
        {
            Id = 1,
            SimpleProp = new SimpleClass { IntValue = 10, StringValue = "Test" },
            AnotherSimpleProp = new AnotherSimpleClass { DoubleValue = 1.23 }
        };
        FastCloner.SetTypeBehavior<SimpleClass>(CloneBehavior.Ignore);
        FastCloner.SetTypeBehavior<AnotherSimpleClass>(CloneBehavior.Ignore);

        // Act
        ClassWithProperties cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned.Id).IsEqualTo(original.Id);
        await Assert.That(cloned.SimpleProp).IsNull().Because("SimpleProp should be null.");
        await Assert.That(cloned.AnotherSimpleProp).IsNull().Because("AnotherSimpleProp should be null.");
    }

    [Test]
    public async Task SetTypeBehavior_IgnoreTypes_ItemsInCollectionOfIgnoredType_BecomeNullInClonedCollection()
    {
        // Arrange
        ClassWithProperties original = new ClassWithProperties
        {
            Id = 1,
            ListOfSimpleProp =
            [
                new SimpleClass { IntValue = 1, StringValue = "A" },
                new SimpleClass { IntValue = 2, StringValue = "B" }
            ]
        };
        FastCloner.SetTypeBehavior<SimpleClass>(CloneBehavior.Ignore);

        // Act
        ClassWithProperties cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned.Id).IsEqualTo(original.Id);
        await Assert.That(cloned.ListOfSimpleProp).IsNotNull().Because("Collection itself should be cloned.");
        await Assert.That(cloned.ListOfSimpleProp).IsNotSameReferenceAs(original.ListOfSimpleProp);
        await Assert.That(cloned.ListOfSimpleProp!.Count).IsEqualTo(original.ListOfSimpleProp!.Count);
        await Assert.That(cloned.ListOfSimpleProp[0]).IsNull().Because("First item of ignored type should be null in cloned list.");
        await Assert.That(cloned.ListOfSimpleProp[1]).IsNull().Because("Second item of ignored type should be null in cloned list.");
    }

    [Test]
    public async Task SetTypeBehavior_Ignore_StringsInSet_OriginalStringsUsedIfStringIgnored()
    {
        // Arrange
        ClassWithSetProperties original = new ClassWithSetProperties
        {
            Id = 1,
            SetOfString = ["Hello", "World"]
        };
        FastCloner.SetTypeBehavior<string>(CloneBehavior.Ignore);

        // Act
        ClassWithSetProperties cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned.SetOfString).IsNotNull();
        await Assert.That(cloned.SetOfString!.Count).IsEqualTo(2);
        await Assert.That(cloned.SetOfString).Contains("Hello");
        await Assert.That(cloned.SetOfString).Contains("World");
        await Assert.That(cloned.SetOfString.Contains(null)).IsFalse();
    }
    
    [Test]
    public async Task SetTypeBehavior_IgnoreType_ItemsInSetOfIgnoredValueType_OriginalItemsUsedIfElementIgnored()
    {
        // Arrange
        MyValueType item1 = new MyValueType { Value = 10 };
        MyValueType item2 = new MyValueType { Value = 20 };
        ClassWithSetProperties original = new ClassWithSetProperties
        {
            Id = 1,
            SetOfMyValueType = [item1, item2]
        };
        FastCloner.SetTypeBehavior<MyValueType>(CloneBehavior.Ignore);

        // Act
        ClassWithSetProperties cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned.SetOfMyValueType).IsNotNull();
        await Assert.That(cloned.SetOfMyValueType!.Count).IsEqualTo(original.SetOfMyValueType!.Count);
        await Assert.That(cloned.SetOfMyValueType).Contains(item1); // Original item1
        await Assert.That(cloned.SetOfMyValueType).Contains(item2); // Original item2
        await Assert.That(cloned.SetOfMyValueType.Contains(default)).IsFalse();
    }

    [Test]
    public async Task SetTypeBehavior_IgnoreType_ItemsInSetOfIgnoredReferenceType_OriginalItemsUsedIfElementIgnored()
    {
        // Arrange
        SimpleClass item1 = new SimpleClass { IntValue = 1, StringValue = "A" };
        SimpleClass item2 = new SimpleClass { IntValue = 2, StringValue = "B" };
        ClassWithSetProperties original = new ClassWithSetProperties
        {
            Id = 1,
            SetOfSimpleClass = [item1, item2]
        };
        FastCloner.SetTypeBehavior<SimpleClass>(CloneBehavior.Ignore);

        // Act
        ClassWithSetProperties cloned = original.DeepClone();

        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned.SetOfSimpleClass).IsNotNull();
        await Assert.That(cloned.SetOfSimpleClass).IsNotSameReferenceAs(original.SetOfSimpleClass);
        await Assert.That(cloned.SetOfSimpleClass!.Count).IsEqualTo(original.SetOfSimpleClass!.Count);
        await Assert.That(cloned.SetOfSimpleClass).Contains(item1).Because("Set should contain original item1 instance.");
        await Assert.That(cloned.SetOfSimpleClass).Contains(item2).Because("Set should contain original item2 instance.");
    }

    [Test]
    public async Task SetTypeBehavior_IgnoreType_ItemsIn1DArrayOfIgnoredReferenceType_BecomeNull()
    {
        // Arrange
        ClassWithArrayProperties original = new ClassWithArrayProperties
        {
            Id = 1,
            ArrayOfSimpleClass =
            [
                new SimpleClass { IntValue = 1, StringValue = "A" },
                new SimpleClass { IntValue = 2, StringValue = "B" }
            ]
        };
        FastCloner.SetTypeBehavior<SimpleClass>(CloneBehavior.Ignore);

        // Act
        ClassWithArrayProperties cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned.Id).IsEqualTo(original.Id);
        await Assert.That(cloned.ArrayOfSimpleClass).IsNotNull();
        await Assert.That(cloned.ArrayOfSimpleClass).IsNotSameReferenceAs(original.ArrayOfSimpleClass);
        await Assert.That(cloned.ArrayOfSimpleClass!.Length).IsEqualTo(original.ArrayOfSimpleClass!.Length);
        await Assert.That(cloned.ArrayOfSimpleClass[0]).IsNull().Because("First item of ignored reference type should be null.");
        await Assert.That(cloned.ArrayOfSimpleClass[1]).IsNull().Because("Second item of ignored reference type should be null.");
    }
    
    [Test]
    public async Task SetTypeBehavior_IgnoreType_ItemsIn1DArrayOfIgnoredValueType_BecomeDefault()
    {
        // Arrange
        ClassWithArrayProperties original = new ClassWithArrayProperties
        {
            Id = 1,
            ArrayOfMyValueType =
            [
                new MyValueType { Value = 10 },
                new MyValueType { Value = 20 }
            ]
        };
        FastCloner.SetTypeBehavior<MyValueType>(CloneBehavior.Ignore);

        // Act
        ClassWithArrayProperties cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned.Id).IsEqualTo(original.Id);
        await Assert.That(cloned.ArrayOfMyValueType).IsNotNull();
        await Assert.That(cloned.ArrayOfMyValueType).IsNotSameReferenceAs(original.ArrayOfMyValueType);
        await Assert.That(cloned.ArrayOfMyValueType!.Length).IsEqualTo(original.ArrayOfMyValueType!.Length);
        await Assert.That(cloned.ArrayOfMyValueType[0]).IsEqualTo(default(MyValueType)).Because("First item of ignored value type should be default.");
        await Assert.That(cloned.ArrayOfMyValueType[1]).IsEqualTo(default(MyValueType)).Because("Second item of ignored value type should be default.");
        await Assert.That(cloned.ArrayOfMyValueType[0].Value).IsEqualTo(0);
    }
    
    [Test]
    public async Task SetTypeBehavior_IgnoreType_ItemsIn2DArrayOfIgnoredReferenceType_BecomeNull()
    {
        // Arrange
        ClassWithArrayProperties original = new ClassWithArrayProperties
        {
            Id = 1,
            TwoDArrayOfSimpleClass = new[,]
            {
                { new SimpleClass { IntValue = 1, StringValue = "A" } },
                { new SimpleClass { IntValue = 2, StringValue = "B" } }
            }
        };
        FastCloner.SetTypeBehavior<SimpleClass>(CloneBehavior.Ignore);

        // Act
        ClassWithArrayProperties cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned.Id).IsEqualTo(original.Id);
        await Assert.That(cloned.TwoDArrayOfSimpleClass).IsNotNull();
        await Assert.That(cloned.TwoDArrayOfSimpleClass).IsNotSameReferenceAs(original.TwoDArrayOfSimpleClass);
        await Assert.That(cloned.TwoDArrayOfSimpleClass!.GetLength(0)).IsEqualTo(original.TwoDArrayOfSimpleClass!.GetLength(0));
        await Assert.That(cloned.TwoDArrayOfSimpleClass!.GetLength(1)).IsEqualTo(original.TwoDArrayOfSimpleClass!.GetLength(1));
        await Assert.That(cloned.TwoDArrayOfSimpleClass[0, 0]).IsNull();
        await Assert.That(cloned.TwoDArrayOfSimpleClass[1, 0]).IsNull();
    }
    
    [Test]
    public async Task SetTypeBehavior_IgnoreType_PrimitiveIntInArray_BecomesDefault()
    {
        // Arrange
        ClassWithArrayProperties original = new ClassWithArrayProperties
        {
            Id = 1,
            ArrayOfInt = [10, 20, 30]
        };
        FastCloner.SetTypeBehavior<int>(CloneBehavior.Ignore);

        // Act
        ClassWithArrayProperties cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned.ArrayOfInt).IsNotNull();
        await Assert.That(cloned.ArrayOfInt!.Length).IsEqualTo(3);
        await Assert.That(cloned.ArrayOfInt[0]).IsEqualTo(0);
        await Assert.That(cloned.ArrayOfInt[1]).IsEqualTo(0);
        await Assert.That(cloned.ArrayOfInt[2]).IsEqualTo(0);
    }
    
    [Test]
    public async Task SetTypeBehavior_IgnoreType_StringInArray_BecomesNull()
    {
        // Arrange
        ClassWithArrayProperties original = new ClassWithArrayProperties
        {
            Id = 1,
            ArrayOfString = ["Hello", "World"]
        };
        FastCloner.SetTypeBehavior<string>(CloneBehavior.Ignore);

        // Act
        ClassWithArrayProperties cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned.ArrayOfString).IsNotNull();
        await Assert.That(cloned.ArrayOfString!.Length).IsEqualTo(2);
        await Assert.That(cloned.ArrayOfString[0]).IsNull();
        await Assert.That(cloned.ArrayOfString[1]).IsNull();
    }
    
    [Test]
    public async Task SetTypeBehavior_IgnoreType_StringValuesInDictionary_BecomeNull()
    {
        // Arrange
        ClassWithDictionaryProperties original = new ClassWithDictionaryProperties
        {
            Id = 1,
            DictIntString = new Dictionary<int, string>
            {
                [1] = "Hello",
                [2] = "World"
            }
        };
        FastCloner.SetTypeBehavior<string>(CloneBehavior.Ignore);

        // Act
        ClassWithDictionaryProperties cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned.DictIntString).IsNotNull();
        await Assert.That(cloned.DictIntString!.Count).IsEqualTo(2);
        await Assert.That(cloned.DictIntString[1]).IsNull();
        await Assert.That(cloned.DictIntString[2]).IsNull();
    }

    [Test]
    public async Task SetTypeBehavior_IgnoreType_BothKeyAndValueIgnored_ReferenceTypes_UsesOriginalKeysAndNullValues()
    {
        // Arrange
        SimpleClass key1 = new SimpleClass { IntValue = 101, StringValue = "Key1" };
        AnotherSimpleClass value1 = new AnotherSimpleClass { DoubleValue = 1.1 };
        Dictionary<SimpleClass, AnotherSimpleClass> original = new Dictionary<SimpleClass, AnotherSimpleClass>
        {
            [key1] = value1
        };
        FastCloner.SetTypeBehavior<SimpleClass>(CloneBehavior.Ignore);
        FastCloner.SetTypeBehavior<AnotherSimpleClass>(CloneBehavior.Ignore);

        // Act
        Dictionary<SimpleClass, AnotherSimpleClass> cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned!.Count).IsEqualTo(1);
        await Assert.That(cloned.ContainsKey(key1)).IsTrue().Because("Should use original key instance as key type is ignored.");
        await Assert.That(cloned[key1]).IsNull().Because("Value should be null as value type is ignored.");
    }

    [Test]
    public async Task SetTypeBehavior_IgnoreType_BothKeyAndValueIgnored_ValueTypes_UsesOriginalKeysAndDefaultValues()
    {
        // Arrange
        MyValueType key1 = new MyValueType { Value = 77 };
        MyValueType value1 = new MyValueType { Value = 88 };
        Dictionary<MyValueType, MyValueType> original = new Dictionary<MyValueType, MyValueType>
        {
            [key1] = value1
        };
        FastCloner.SetTypeBehavior<MyValueType>(CloneBehavior.Ignore);

        // Act
        Dictionary<MyValueType, MyValueType> cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned!.Count).IsEqualTo(1);
        await Assert.That(cloned.ContainsKey(key1)).IsTrue().Because("Should use original key instance as key type is ignored.");
        await Assert.That(cloned[key1]).IsEqualTo(default(MyValueType)).Because("Value should be default as value type is ignored.");
    }
    
    [Test]
    public async Task SetTypeBehavior_IgnoreType_PrimitiveKeyTypeInt_And_ReferenceValueTypeIgnored()
    {
        // Arrange
        Dictionary<int, SimpleClass> original = new Dictionary<int, SimpleClass>
        {
            [1] = new SimpleClass { IntValue = 10, StringValue = "Val1" },
            [2] = new SimpleClass { IntValue = 20, StringValue = "Val2" }
        };
        FastCloner.SetTypeBehavior<SimpleClass>(CloneBehavior.Ignore);

        // Act
        Dictionary<int, SimpleClass> cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned!.Count).IsEqualTo(2);
        await Assert.That(cloned[1]).IsNull();
        await Assert.That(cloned[2]).IsNull();
    }
    
    [Test]
    public async Task SetTypeBehavior_IgnoreType_ReferenceKeyType_And_PrimitiveValueTypeIntIgnored()
    {
        // Arrange
        SimpleClass key1 = new SimpleClass { IntValue = 1, StringValue = "Key1" };
        Dictionary<SimpleClass, int> original = new Dictionary<SimpleClass, int>
        {
            [key1] = 100
        };
        FastCloner.SetTypeBehavior<int>(CloneBehavior.Ignore);

        // Act
        Dictionary<SimpleClass, int> cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned!.Count).IsEqualTo(1);
        await Assert.That(cloned.FirstOrDefault().Value).IsEqualTo(0).Because("Ignored int value should be default.");
    }

    [Test]
    public async Task SetTypeBehavior_MinimalIssue8Ignored()
    {
        // Arrange
        FastCloner.SetTypeBehavior<System.ComponentModel.PropertyChangedEventHandler>(CloneBehavior.Ignore);
        
        IteratorInfo nfo = new IteratorInfo();

        // Act
        IteratorInfo copy = nfo.DeepClone();

        // Assert
        await Assert.That(copy).IsNotNull();
        await Assert.That(nfo.HasPropertyChanged).IsTrue();
        await Assert.That(copy.HasPropertyChanged).IsFalse();
    }
    
    [Test]
    public async Task SetTypeBehavior_MinimalIssue8Kept()
    {
        // Arrange
        IteratorInfo nfo = new IteratorInfo();

        // Act
        IteratorInfo copy = nfo.DeepClone();

        // Assert
        await Assert.That(copy).IsNotNull();
        await Assert.That(nfo.HasPropertyChanged).IsTrue();
        await Assert.That(copy.HasPropertyChanged).IsTrue();
    }

    [Test]
    public async Task SetTypeBehavior_ExactTypeClone_Reflects_RuntimeMutations()
    {
        // Arrange
        SimpleClass original = new SimpleClass { IntValue = 42, StringValue = "Value" };

        // Act + Assert (default behavior)
        SimpleClass baseline = original.DeepClone();
        await Assert.That(baseline).IsNotNull();
        await Assert.That(baseline).IsNotSameReferenceAs(original);
        await Assert.That(baseline.IntValue).IsEqualTo(original.IntValue);
        await Assert.That(baseline.StringValue).IsEqualTo(original.StringValue);

        // Act + Assert (reference behavior)
        FastCloner.SetTypeBehavior<SimpleClass>(CloneBehavior.Reference);
        SimpleClass referenced = original.DeepClone();
        await Assert.That(referenced).IsSameReferenceAs(original);

        // Act + Assert (ignore behavior)
        FastCloner.SetTypeBehavior<SimpleClass>(CloneBehavior.Ignore);
        SimpleClass ignored = original.DeepClone();
        await Assert.That(ignored).IsNull();

        // Act + Assert (restored default behavior)
        FastCloner.ClearTypeBehavior<SimpleClass>();
        SimpleClass restored = original.DeepClone();
        await Assert.That(restored).IsNotNull();
        await Assert.That(restored).IsNotSameReferenceAs(original);
        await Assert.That(restored.IntValue).IsEqualTo(original.IntValue);
        await Assert.That(restored.StringValue).IsEqualTo(original.StringValue);
    }

    [Test]
    public async Task SetTypeBehavior_Shallow_PerformsShallowCopy()
    {
        // Arrange
        ClassWithProperties original = new ClassWithProperties
        {
            Id = 1,
            SimpleProp = new SimpleClass { IntValue = 10, StringValue = "Shared" }
        };
        FastCloner.SetTypeBehavior<ClassWithProperties>(CloneBehavior.Shallow);

        // Act
        ClassWithProperties clone = original.DeepClone();

        // Assert
        await Assert.That(clone).IsNotSameReferenceAs(original).Because("Shallow clone should be a new object");
        await Assert.That(clone.Id).IsEqualTo(1);
        await Assert.That(clone.SimpleProp).IsSameReferenceAs(original.SimpleProp).Because("Shallow clone should share references");

        // Value types should still be independent copies (because it's a new container)
        clone.Id = 2;
        await Assert.That(original.Id).IsEqualTo(1);
    }

    [Test]
    public async Task DisableOptionalFeatures_Toggle_RecomputesActiveTypeBehaviorChecks()
    {
        // Arrange
        FastCloner.ClearAllTypeBehaviors();
        FastCloner.SetDisableOptionalFeatures(false);

        // Act + Assert
        await Assert.That(FastClonerCache.HasActiveTypeBehaviorOverrides).IsFalse();

        FastCloner.SetTypeBehavior<SimpleClass>(CloneBehavior.Ignore);
        await Assert.That(FastClonerCache.HasActiveTypeBehaviorOverrides).IsTrue();

        FastCloner.SetDisableOptionalFeatures(true);
        await Assert.That(FastClonerCache.HasActiveTypeBehaviorOverrides).IsFalse();

        FastCloner.SetDisableOptionalFeatures(false);
        await Assert.That(FastClonerCache.HasActiveTypeBehaviorOverrides).IsTrue();

        FastCloner.ClearAllTypeBehaviors();
        await Assert.That(FastClonerCache.HasActiveTypeBehaviorOverrides).IsFalse();
    }

    public class ClassWithThreeStrings
    {
        public string? First { get; set; }
        public string? Middle { get; set; }
        public string? Last { get; set; }
        public int Id { get; set; }
    }

    public class ClassWithMiddleStringIgnored
    {
        public string? First { get; set; }

        [FastClonerIgnore]
        public string? Middle { get; set; }

        public string? Last { get; set; }
        public int Id { get; set; }
    }

    [Test]
    public async Task MemberIgnore_OnlyMiddleString_IsNull()
    {
        // Arrange
        ClassWithMiddleStringIgnored original = new ClassWithMiddleStringIgnored
        {
            First = "Alpha",
            Middle = "Beta",
            Last = "Gamma",
            Id = 99
        };

        // Act
        ClassWithMiddleStringIgnored cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned.Id).IsEqualTo(99);
        await Assert.That(cloned.First).IsEqualTo("Alpha");
        await Assert.That(cloned.Middle).IsNull();
        await Assert.That(cloned.Last).IsEqualTo("Gamma");
    }

    [Test]
    public async Task SetTypeBehavior_IgnoreString_ClassWithThreeStrings_AllStringsNull()
    {
        // Arrange
        ClassWithThreeStrings original = new ClassWithThreeStrings
        {
            First = "Alpha",
            Middle = "Beta",
            Last = "Gamma",
            Id = 99
        };
        FastCloner.SetTypeBehavior<string>(CloneBehavior.Ignore);

        // Act
        ClassWithThreeStrings cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned.Id).IsEqualTo(99);
        await Assert.That(cloned.First).IsNull();
        await Assert.That(cloned.Middle).IsNull();
        await Assert.That(cloned.Last).IsNull();
    }

    [Test]
    public async Task SetTypeBehavior_IgnoreString_MixedObjectArray_OnlyStringsNull()
    {
        // Arrange
        var original = new object?[] { "Hello", 42, "World", 3.14, "!" };
        FastCloner.SetTypeBehavior<string>(CloneBehavior.Ignore);

        // Act
        object?[] cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned.Length).IsEqualTo(5);
        await Assert.That(cloned[0]).IsNull();
        await Assert.That(cloned[1]).IsEqualTo(42);
        await Assert.That(cloned[2]).IsNull();
        await Assert.That(cloned[3]).IsEqualTo(3.14);
        await Assert.That(cloned[4]).IsNull();
    }

    [Test]
    public async Task DisableOptionalFeatures_WhenEnabled_IgnoresTypeBehaviorOverrides()
    {
        // Arrange
        SimpleClass original = new SimpleClass { IntValue = 42, StringValue = "Value" };
        FastCloner.SetTypeBehavior<SimpleClass>(CloneBehavior.Ignore);

        // Act + Assert (overrides active)
        SimpleClass ignored = original.DeepClone();
        await Assert.That(ignored).IsNull();

        FastCloner.SetDisableOptionalFeatures(true);
        SimpleClass clonedWithOptionalDisabled = original.DeepClone();
        await Assert.That(clonedWithOptionalDisabled).IsNotNull();
        await Assert.That(clonedWithOptionalDisabled).IsNotSameReferenceAs(original);

        FastCloner.SetDisableOptionalFeatures(false);
        SimpleClass ignoredAgain = original.DeepClone();
        await Assert.That(ignoredAgain).IsNull();
    }

    [Test]
    public async Task GlobalConfigPublish_BumpsCacheVersion_ForSubsequentReaders()
    {
        FastCloner.ClearCache();
        FastCloner.ClearAllTypeBehaviors();
        FastCloner.SetDisableOptionalFeatures(false);

        SimpleClass simple = new SimpleClass { IntValue = 7, StringValue = "Seven" };
        Dictionary<string, SimpleClass> dictionary = new Dictionary<string, SimpleClass>
        {
            ["one"] = new SimpleClass { IntValue = 1, StringValue = "One" }
        };

        long startingVersion = FastClonerCache.GetCacheVersion();

        FastCloner.MaxRecursionDepth = 999;
        _ = simple.DeepClone();
        _ = dictionary.DeepClone();

        long firstMutationVersion = FastClonerCache.GetCacheVersion();
        await Assert.That(firstMutationVersion).IsGreaterThan(startingVersion);

        FastCloner.MaxRecursionDepth = 998;
        _ = simple.DeepClone();
        _ = dictionary.DeepClone();

        await Assert.That(FastClonerCache.GetCacheVersion()).IsGreaterThan(firstMutationVersion);
    }

    [Test]
    public async Task RuntimeConfig_Create_DefaultValues_CanStaySingletonOrBecomeDistinctSnapshot()
    {
        FastClonerRuntimeConfig startupDefault = FastClonerRuntimeConfig.Create(
            1000,
            disableOptionalFeatures: false,
            typeBehaviors: null,
            cacheKey: 0,
            useStartupDefaultSingleton: true);

        FastClonerRuntimeConfig settledDefault = FastClonerRuntimeConfig.Create(
            1000,
            disableOptionalFeatures: false,
            typeBehaviors: null,
            cacheKey: 42,
            useStartupDefaultSingleton: false);

        await Assert.That(ReferenceEquals(startupDefault, FastClonerRuntimeConfig.Default)).IsTrue();
        await Assert.That(ReferenceEquals(settledDefault, FastClonerRuntimeConfig.Default)).IsFalse();
        await Assert.That(settledDefault.CacheKey).IsEqualTo(42);
        await Assert.That(settledDefault.MaxRecursionDepth).IsEqualTo(FastClonerRuntimeConfig.Default.MaxRecursionDepth);
        await Assert.That(settledDefault.DisableOptionalFeatures).IsEqualTo(FastClonerRuntimeConfig.Default.DisableOptionalFeatures);
        await Assert.That(settledDefault.TypeBehaviors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PublishedEngine_RestoringDefaults_ReturnsToStartupRail()
    {
        FastCloner.SetTypeBehavior<SimpleClass>(CloneBehavior.Reference);
        FastClonerPublishedEngine mutated = FastCloner.GetPublishedEngine();

        await Assert.That(mutated.UsesStartupDefaultRail).IsFalse();
        await Assert.That(mutated.RuntimeConfig.CacheKey).IsGreaterThan(0);

        FastCloner.ClearAllTypeBehaviors();
        FastCloner.SetDisableOptionalFeatures(false);
        FastCloner.MaxRecursionDepth = 1000;

        FastClonerPublishedEngine restored = FastCloner.GetPublishedEngine();
        await Assert.That(restored.UsesStartupDefaultRail).IsTrue();
        await Assert.That(ReferenceEquals(restored.RuntimeConfig, FastClonerRuntimeConfig.Default)).IsTrue();
        await Assert.That(restored.RuntimeConfig.CacheKey).IsEqualTo(0);
        await Assert.That(restored.RuntimeConfig.MaxRecursionDepth).IsEqualTo(FastClonerRuntimeConfig.Default.MaxRecursionDepth);
        await Assert.That(restored.RuntimeConfig.DisableOptionalFeatures).IsEqualTo(FastClonerRuntimeConfig.Default.DisableOptionalFeatures);
        await Assert.That(restored.RuntimeConfig.TypeBehaviors.Count).IsEqualTo(0);
    }
}