using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FastCloner.Tests;

[TestFixture(Low)]
[TestFixture(High)]
public class IgnoredTypesTests(int maxRecursionDepth) : BaseTestFixture(maxRecursionDepth)
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

    public class ClassWithNullableValueTypeProperty
    {
        public int Id { get; set; }
        public MyValueType? MyNullableStruct { get; set; }
    }

    public class ClassWithPrimitiveProperties
    {
        public int IntProp { get; set; }
        public bool BoolProp { get; set; }
        public string? StringProp { get; set; }
    }

    public class BaseClassForIgnore
    {
        public int BaseValue { get; set; }
    }

    public class DerivedClassA : BaseClassForIgnore
    {
        public int DerivedAValue { get; set; }
    }

    public class DerivedClassB : BaseClassForIgnore
    {
        public int DerivedBValue { get; set; }
    }

    public class ContainerWithBaseClassProperties
    {
        public BaseClassForIgnore? PropA { get; set; }
        public BaseClassForIgnore? PropB { get; set; }
    }

    public class ContainerWithMixedHierarchy
    {
        public BaseClassForIgnore? BaseProp { get; set; }
        public DerivedClassA? DerivedAProp { get; set; }
        public BaseClassForIgnore? DerivedBPropAsBase { get; set; }
    }
    
    [TearDown]
    public void TearDown()
    {
        FastCloner.ClearIgnoredTypes();
    }

    [Test]
    public void IgnoreType_PropertyOfIgnoredReferenceType_IsNullAfterCloning()
    {
        // Arrange
        ClassWithProperties original = new ClassWithProperties
        {
            Id = 1,
            SimpleProp = new SimpleClass { IntValue = 10, StringValue = "Test" },
            AnotherSimpleProp = new AnotherSimpleClass { DoubleValue = 1.23 }
        };
        FastCloner.IgnoreType(typeof(SimpleClass));

        // Act
        ClassWithProperties cloned = original.DeepClone();
        FastCloner.ClearIgnoredTypes();
        ClassWithProperties cloned2 = original.DeepClone();

        // Assert
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned, Is.Not.SameAs(original));
        Assert.That(cloned.Id, Is.EqualTo(original.Id));
        Assert.That(cloned.SimpleProp, Is.Null, "SimpleProp should be null as its type is ignored.");
        Assert.That(cloned.AnotherSimpleProp, Is.Not.Null, "AnotherSimpleProp should be cloned.");
        Assert.That(cloned.AnotherSimpleProp, Is.Not.SameAs(original.AnotherSimpleProp));
        Assert.That(cloned.AnotherSimpleProp!.DoubleValue, Is.EqualTo(original.AnotherSimpleProp!.DoubleValue));
        
        Assert.That(cloned2.SimpleProp, Is.Not.Null, "SimpleProp should not null after resetting the ignored types.");
    }
    
    [Test]
    public void IgnoreType_PropertyOfIgnoredValueType_IsDefaultAfterCloning()
    {
        // Arrange
        ClassWithValueTypeProperty original = new ClassWithValueTypeProperty
        {
            Id = 1,
            MyStruct = new MyValueType { Value = 42 }
        };
        FastCloner.IgnoreType(typeof(MyValueType));

        // Act
        ClassWithValueTypeProperty cloned = original.DeepClone();

        // Assert
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned.Id, Is.EqualTo(original.Id));
        Assert.That(cloned.MyStruct, Is.EqualTo(default(MyValueType)), "Ignored value type property should be default.");
        Assert.That(cloned.MyStruct.Value, Is.EqualTo(0)); // Default for int
    }
    
    [Test]
    public void IgnoreType_RootObjectOfIgnoredType_ReturnsNull()
    {
        // Arrange
        ClassToBeIgnored original = new ClassToBeIgnored { Data = "Important Data" };
        FastCloner.IgnoreType(typeof(ClassToBeIgnored));

        // Act
        ClassToBeIgnored cloned = original.DeepClone();

        // Assert
        Assert.That(cloned, Is.Null, "Cloning an object of an globally ignored type should return null.");
    }
    
    [Test]
    public void IgnoreType_AddsTypeToGetIgnoredTypesList()
    {
        // Arrange
        FastCloner.ClearIgnoredTypes();

        // Act
        FastCloner.IgnoreType(typeof(SimpleClass));
        HashSet<Type> ignoredTypes = FastCloner.GetIgnoredTypes();

        // Assert
        Assert.That(ignoredTypes, Is.Not.Null);
        Assert.That(ignoredTypes.Count, Is.EqualTo(1));
        Assert.That(ignoredTypes, Contains.Item(typeof(SimpleClass)));
    }
    
    [Test]
    public void IgnoreType_CalledMultipleTimesWithSameType_ResultsInOneEntry()
    {
        // Arrange
        FastCloner.ClearIgnoredTypes();

        // Act
        FastCloner.IgnoreType(typeof(SimpleClass));
        FastCloner.IgnoreType(typeof(SimpleClass)); // Call again
        HashSet<Type> ignoredTypes = FastCloner.GetIgnoredTypes();

        // Assert
        Assert.That(ignoredTypes.Count, Is.EqualTo(1));
        Assert.That(ignoredTypes, Contains.Item(typeof(SimpleClass)));
    }
    
    [Test]
    public void IgnoreType_ForPrimitiveIntProperty_SetsToDefault()
    {
        // Arrange
        ClassWithPrimitiveProperties original = new ClassWithPrimitiveProperties { IntProp = 123, BoolProp = true, StringProp = "Hello" };
        FastCloner.IgnoreType(typeof(int));

        // Act
        ClassWithPrimitiveProperties cloned = original.DeepClone();

        // Assert
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned.IntProp, Is.EqualTo(0), "Ignored int property should be default.");
        Assert.That(cloned.BoolProp, Is.EqualTo(original.BoolProp), "BoolProp should not be affected.");
        Assert.That(cloned.StringProp, Is.EqualTo(original.StringProp), "StringProp should not be affected.");
    }
    
    [Test]
    public void IgnoreTypes_MultiplePropertiesOfIgnoredTypes_AreNullOrDefaultForValueTypes()
    {
        // Arrange
        ClassWithProperties original = new ClassWithProperties
        {
            Id = 1,
            SimpleProp = new SimpleClass { IntValue = 10, StringValue = "Test" },
            AnotherSimpleProp = new AnotherSimpleClass { DoubleValue = 1.23 }
        };
        List<Type> typesToIgnore = new List<Type> { typeof(SimpleClass), typeof(AnotherSimpleClass) };
        FastCloner.IgnoreTypes(typesToIgnore);

        // Act
        ClassWithProperties cloned = original.DeepClone();

        // Assert
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned.Id, Is.EqualTo(original.Id));
        Assert.That(cloned.SimpleProp, Is.Null, "SimpleProp should be null.");
        Assert.That(cloned.AnotherSimpleProp, Is.Null, "AnotherSimpleProp should be null.");
    }
    
    [Test]
    public void IgnoreTypes_ItemsInCollectionOfIgnoredType_BecomeNullInClonedCollection()
    {
        // Arrange
        FastCloner.ClearIgnoredTypes();
        
        ClassWithProperties original = new ClassWithProperties
        {
            Id = 1,
            ListOfSimpleProp =
            [
                new SimpleClass { IntValue = 1, StringValue = "A" },
                new SimpleClass { IntValue = 2, StringValue = "B" }
            ]
        };
        FastCloner.IgnoreType(typeof(SimpleClass));

        // Act
        ClassWithProperties cloned = original.DeepClone();

        // Assert
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned.Id, Is.EqualTo(original.Id));
        Assert.That(cloned.ListOfSimpleProp, Is.Not.Null, "Collection itself should be cloned.");
        Assert.That(cloned.ListOfSimpleProp, Is.Not.SameAs(original.ListOfSimpleProp));
        Assert.That(cloned.ListOfSimpleProp!.Count, Is.EqualTo(original.ListOfSimpleProp!.Count));
        Assert.That(cloned.ListOfSimpleProp[0], Is.Null, "First item of ignored type should be null in cloned list.");
        Assert.That(cloned.ListOfSimpleProp[1], Is.Null, "Second item of ignored type should be null in cloned list.");
    }
    
    public class ClassWithSetProperties
    {
        public int Id { get; set; }
        public HashSet<SimpleClass>? SetOfSimpleClass { get; set; }
        public HashSet<MyValueType>? SetOfMyValueType { get; set; }
        public HashSet<string>? SetOfString { get; set; }
    }
    
    [Test]
    public void IgnoreType_StringsInSet_OriginalStringsUsedIfStringIgnored()
    {
        // Arrange
        FastCloner.ClearIgnoredTypes();
        ClassWithSetProperties original = new ClassWithSetProperties
        {
            Id = 1,
            SetOfString = ["Hello", "World"]
        };
        FastCloner.IgnoreType(typeof(string));

        // Act
        ClassWithSetProperties cloned = original.DeepClone();

        // Assert
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned.SetOfString, Is.Not.Null);
        Assert.That(cloned.SetOfString!.Count, Is.EqualTo(2));
        Assert.That(cloned.SetOfString, Contains.Item("Hello"));
        Assert.That(cloned.SetOfString, Contains.Item("World"));
        Assert.That(cloned.SetOfString.Contains(null), Is.False);
    }
    
    [Test]
    public void IgnoreType_ItemsInSetOfIgnoredValueType_OriginalItemsUsedIfElementIgnored()
    {
        // Arrange
        FastCloner.ClearIgnoredTypes();
        MyValueType item1 = new MyValueType { Value = 10 };
        MyValueType item2 = new MyValueType { Value = 20 };
        ClassWithSetProperties original = new ClassWithSetProperties
        {
            Id = 1,
            SetOfMyValueType = [item1, item2]
        };
        FastCloner.IgnoreType(typeof(MyValueType));

        // Act
        ClassWithSetProperties cloned = original.DeepClone();

        // Assert
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned.SetOfMyValueType, Is.Not.Null);
        Assert.That(cloned.SetOfMyValueType!.Count, Is.EqualTo(original.SetOfMyValueType!.Count));
        Assert.That(cloned.SetOfMyValueType, Contains.Item(item1)); // Original item1
        Assert.That(cloned.SetOfMyValueType, Contains.Item(item2)); // Original item2
        Assert.That(cloned.SetOfMyValueType.Contains(default), Is.False);
    }
    
    [Test]
    public void IgnoreType_ItemsInSetOfIgnoredReferenceType_OriginalItemsUsedIfElementIgnored()
    {
        // Arrange
        FastCloner.ClearIgnoredTypes();
        SimpleClass item1 = new SimpleClass { IntValue = 1, StringValue = "A" };
        SimpleClass item2 = new SimpleClass { IntValue = 2, StringValue = "B" };
        ClassWithSetProperties original = new ClassWithSetProperties
        {
            Id = 1,
            SetOfSimpleClass = [item1, item2]
        };
        FastCloner.IgnoreType(typeof(SimpleClass));

        // Act
        ClassWithSetProperties cloned = original.DeepClone();

        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned.SetOfSimpleClass, Is.Not.Null);
        Assert.That(cloned.SetOfSimpleClass, Is.Not.SameAs(original.SetOfSimpleClass));
        Assert.That(cloned.SetOfSimpleClass!.Count, Is.EqualTo(original.SetOfSimpleClass!.Count));
        Assert.That(cloned.SetOfSimpleClass, Contains.Item(item1), "Set should contain original item1 instance.");
        Assert.That(cloned.SetOfSimpleClass, Contains.Item(item2), "Set should contain original item2 instance.");
    }
    
    public class ClassWithArrayProperties
    {
        public int Id { get; set; }
        public SimpleClass[]? ArrayOfSimpleClass { get; set; }
        public MyValueType[]? ArrayOfMyValueType { get; set; }
        public SimpleClass[,]? TwoDArrayOfSimpleClass { get; set; }
        public MyValueType[,]? TwoDArrayOfMyValueType { get; set; }
        public int[]? ArrayOfInt { get; set; } // For ignoring primitive types in arrays
        public string[]? ArrayOfString { get; set; } // For ignoring string types in arrays
    }
    
    [Test]
    public void IgnoreType_ItemsIn1DArrayOfIgnoredReferenceType_BecomeNull()
    {
        // Arrange
        FastCloner.ClearIgnoredTypes();
        ClassWithArrayProperties original = new ClassWithArrayProperties
        {
            Id = 1,
            ArrayOfSimpleClass =
            [
                new SimpleClass { IntValue = 1, StringValue = "A" },
                new SimpleClass { IntValue = 2, StringValue = "B" }
            ]
        };
        FastCloner.IgnoreType(typeof(SimpleClass));

        // Act
        ClassWithArrayProperties cloned = original.DeepClone();

        // Assert
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned.Id, Is.EqualTo(original.Id));
        Assert.That(cloned.ArrayOfSimpleClass, Is.Not.Null);
        Assert.That(cloned.ArrayOfSimpleClass, Is.Not.SameAs(original.ArrayOfSimpleClass));
        Assert.That(cloned.ArrayOfSimpleClass!.Length, Is.EqualTo(original.ArrayOfSimpleClass!.Length));
        Assert.That(cloned.ArrayOfSimpleClass[0], Is.Null, "First item of ignored reference type should be null.");
        Assert.That(cloned.ArrayOfSimpleClass[1], Is.Null, "Second item of ignored reference type should be null.");
    }
    
    [Test]
    public void IgnoreType_ItemsIn1DArrayOfIgnoredValueType_BecomeDefault()
    {
        // Arrange
        FastCloner.ClearIgnoredTypes();
        ClassWithArrayProperties original = new ClassWithArrayProperties
        {
            Id = 1,
            ArrayOfMyValueType =
            [
                new MyValueType { Value = 10 },
                new MyValueType { Value = 20 }
            ]
        };
        FastCloner.IgnoreType(typeof(MyValueType));

        // Act
        ClassWithArrayProperties cloned = original.DeepClone();

        // Assert
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned.Id, Is.EqualTo(original.Id));
        Assert.That(cloned.ArrayOfMyValueType, Is.Not.Null);
        Assert.That(cloned.ArrayOfMyValueType, Is.Not.SameAs(original.ArrayOfMyValueType));
        Assert.That(cloned.ArrayOfMyValueType!.Length, Is.EqualTo(original.ArrayOfMyValueType!.Length));
        Assert.That(cloned.ArrayOfMyValueType[0], Is.EqualTo(default(MyValueType)), "First item of ignored value type should be default.");
        Assert.That(cloned.ArrayOfMyValueType[1], Is.EqualTo(default(MyValueType)), "Second item of ignored value type should be default.");
        Assert.That(cloned.ArrayOfMyValueType[0].Value, Is.EqualTo(0));
    }
    
    [Test]
    public void IgnoreType_ItemsIn2DArrayOfIgnoredReferenceType_BecomeNull()
    {
        // Arrange
        FastCloner.ClearIgnoredTypes();
        ClassWithArrayProperties original = new ClassWithArrayProperties
        {
            Id = 1,
            TwoDArrayOfSimpleClass = new[,]
            {
                { new SimpleClass { IntValue = 1, StringValue = "A" } },
                { new SimpleClass { IntValue = 2, StringValue = "B" } }
            }
        };
        FastCloner.IgnoreType(typeof(SimpleClass));

        // Act
        ClassWithArrayProperties cloned = original.DeepClone();

        // Assert
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned.Id, Is.EqualTo(original.Id));
        Assert.That(cloned.TwoDArrayOfSimpleClass, Is.Not.Null);
        Assert.That(cloned.TwoDArrayOfSimpleClass, Is.Not.SameAs(original.TwoDArrayOfSimpleClass));
        Assert.That(cloned.TwoDArrayOfSimpleClass!.GetLength(0), Is.EqualTo(original.TwoDArrayOfSimpleClass!.GetLength(0)));
        Assert.That(cloned.TwoDArrayOfSimpleClass!.GetLength(1), Is.EqualTo(original.TwoDArrayOfSimpleClass!.GetLength(1)));
        Assert.That(cloned.TwoDArrayOfSimpleClass[0, 0], Is.Null);
        Assert.That(cloned.TwoDArrayOfSimpleClass[1, 0], Is.Null);
    }
    
    [Test]
    public void IgnoreType_PrimitiveIntInArray_BecomesDefault()
    {
        // Arrange
        FastCloner.ClearIgnoredTypes();
        ClassWithArrayProperties original = new ClassWithArrayProperties
        {
            Id = 1,
            ArrayOfInt = [10, 20, 30]
        };
        FastCloner.IgnoreType(typeof(int));

        // Act
        ClassWithArrayProperties cloned = original.DeepClone();

        // Assert
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned.ArrayOfInt, Is.Not.Null);
        Assert.That(cloned.ArrayOfInt!.Length, Is.EqualTo(3));
        Assert.That(cloned.ArrayOfInt[0], Is.EqualTo(0));
        Assert.That(cloned.ArrayOfInt[1], Is.EqualTo(0));
        Assert.That(cloned.ArrayOfInt[2], Is.EqualTo(0));
    }
    
    [Test]
    public void IgnoreType_StringInArray_BecomesNull()
    {
        // Arrange
        FastCloner.ClearIgnoredTypes();
        ClassWithArrayProperties original = new ClassWithArrayProperties
        {
            Id = 1,
            ArrayOfString = ["Hello", "World"]
        };
        FastCloner.IgnoreType(typeof(string));

        // Act
        ClassWithArrayProperties cloned = original.DeepClone();

        // Assert
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned.ArrayOfString, Is.Not.Null);
        Assert.That(cloned.ArrayOfString!.Length, Is.EqualTo(2));
        Assert.That(cloned.ArrayOfString[0], Is.Null);
        Assert.That(cloned.ArrayOfString[1], Is.Null);
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

    [Test]
    public void IgnoreType_StringValuesInDictionary_BecomeNull()
    {
        // Arrange
        FastCloner.ClearIgnoredTypes();
        ClassWithDictionaryProperties original = new ClassWithDictionaryProperties
        {
            Id = 1,
            DictIntString = new Dictionary<int, string>
            {
                [1] = "Hello",
                [2] = "World"
            }
        };
        FastCloner.IgnoreType(typeof(string));

        // Act
        ClassWithDictionaryProperties cloned = original.DeepClone();

        // Assert
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned.DictIntString, Is.Not.Null);
        Assert.That(cloned.DictIntString!.Count, Is.EqualTo(2));
        Assert.That(cloned.DictIntString[1], Is.Null);
        Assert.That(cloned.DictIntString[2], Is.Null);
    }

    [Test]
    public void IgnoreType_BothKeyAndValueIgnored_ReferenceTypes_UsesOriginalKeysAndNullValues()
    {
        // Arrange
        FastCloner.ClearIgnoredTypes();
        SimpleClass key1 = new SimpleClass { IntValue = 101, StringValue = "Key1" };
        AnotherSimpleClass value1 = new AnotherSimpleClass { DoubleValue = 1.1 };
        Dictionary<SimpleClass, AnotherSimpleClass> original = new Dictionary<SimpleClass, AnotherSimpleClass>
        {
            [key1] = value1
        };
        FastCloner.IgnoreType(typeof(SimpleClass));      // Ignore Key Type
        FastCloner.IgnoreType(typeof(AnotherSimpleClass)); // Ignore Value Type

        // Act
        Dictionary<SimpleClass, AnotherSimpleClass> cloned = original.DeepClone();

        // Assert
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned!.Count, Is.EqualTo(1));
        Assert.That(cloned.ContainsKey(key1), Is.True, "Should use original key instance as key type is ignored.");
        Assert.That(cloned[key1], Is.Null, "Value should be null as value type is ignored.");
    }

    [Test]
    public void IgnoreType_BothKeyAndValueIgnored_ValueTypes_UsesOriginalKeysAndDefaultValues()
    {
        // Arrange
        FastCloner.ClearIgnoredTypes();
        MyValueType key1 = new MyValueType { Value = 77 };
        MyValueType value1 = new MyValueType { Value = 88 }; // Using MyValueType for both for simplicity
        Dictionary<MyValueType, MyValueType> original = new Dictionary<MyValueType, MyValueType>
        {
            [key1] = value1
        };
        FastCloner.IgnoreType(typeof(MyValueType)); // Ignore the type used for both key and value

        // Act
        Dictionary<MyValueType, MyValueType> cloned = original.DeepClone();

        // Assert
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned!.Count, Is.EqualTo(1));
        Assert.That(cloned.ContainsKey(key1), Is.True, "Should use original key instance as key type is ignored.");
        Assert.That(cloned[key1], Is.EqualTo(default(MyValueType)), "Value should be default as value type is ignored.");
    }
    
    [Test]
    public void IgnoreType_PrimitiveKeyTypeInt_And_ReferenceValueTypeIgnored()
    {
        // Arrange
        FastCloner.ClearIgnoredTypes();
        Dictionary<int, SimpleClass> original = new Dictionary<int, SimpleClass>
        {
            [1] = new SimpleClass { IntValue = 10, StringValue = "Val1" },
            [2] = new SimpleClass { IntValue = 20, StringValue = "Val2" }
        };
        FastCloner.IgnoreType(typeof(SimpleClass)); // Ignore value type

        // Act
        Dictionary<int, SimpleClass> cloned = original.DeepClone();

        // Assert
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned!.Count, Is.EqualTo(2));
        Assert.That(cloned[1], Is.Null);
        Assert.That(cloned[2], Is.Null);
    }
    
    [Test]
    public void IgnoreType_ReferenceKeyType_And_PrimitiveValueTypeIntIgnored()
    {
        // Arrange
        FastCloner.ClearIgnoredTypes();
        SimpleClass key1 = new SimpleClass { IntValue = 1, StringValue = "Key1" };
        Dictionary<SimpleClass, int> original = new Dictionary<SimpleClass, int>
        {
            [key1] = 100
        };
        FastCloner.IgnoreType(typeof(int)); // Ignore value type

        // Act
        Dictionary<SimpleClass, int> cloned = original.DeepClone();

        // Assert
        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned!.Count, Is.EqualTo(1));
        Assert.That(cloned.FirstOrDefault().Value, Is.EqualTo(0), "Ignored int value should be default.");
    }

    partial class IteratorInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged = (_, _) =>
        {
            
        };
        
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }
        
        protected virtual void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
        {
            PropertyChanged?.Invoke(this, eventArgs);
        }

        public bool HasPropertyChanged => PropertyChanged is not null;
    }

    [Test]
    public void MinimalIssue8Ignored()
    {
        // Arrange
        FastCloner.ClearIgnoredTypes();
        FastCloner.IgnoreType(typeof(PropertyChangedEventHandler));
        
        IteratorInfo nfo = new IteratorInfo();

        // Act
        IteratorInfo copy = nfo.DeepClone();

        // Assert
        Assert.That(copy, Is.Not.Null);
        Assert.That(nfo.HasPropertyChanged, Is.True);
        Assert.That(copy.HasPropertyChanged, Is.False);
    }
    
    [Test]
    public void MinimalIssue8Kept()
    {
        // Arrange
        FastCloner.ClearIgnoredTypes();

        IteratorInfo nfo = new IteratorInfo();

        // Act
        IteratorInfo copy = nfo.DeepClone();

        // Assert
        Assert.That(copy, Is.Not.Null);
        Assert.That(nfo.HasPropertyChanged, Is.True);
        Assert.That(copy.HasPropertyChanged, Is.True);
    }
}