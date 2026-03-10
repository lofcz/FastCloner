// Analyzer removed - partial requirement no longer needed
using FastCloner.SourceGenerator.Shared;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;

namespace FastCloner.Tests;
[SourceGeneratorCompatible]
public class SourceGeneratorTests
{
    [FastClonerClonable]
    public class Person
    {
        public string? Name { get; set; }
        public int Age { get; set; }
        public List<string>? Hobbies { get; set; }
    }
    
    [Test]
    [SourceGeneratorCompatible]
    public async Task SimpleObject_Should_Be_Cloned()
    {
        // Arrange
        Person person = new Person
        {
            Name = "John",
            Age = 30,
            Hobbies = ["Reading", "Coding"]
        };
        
        // Act
        Person? copy = person.FastDeepClone();
        
        // Assert - initial state
        await Assert.That(copy).IsNotNull();
        await Assert.That(copy!.Age).IsEqualTo(person.Age);
        await Assert.That(copy.Name).IsEqualTo(person.Name);
        await Assert.That(copy.Hobbies).IsNotNull();
        await Assert.That(copy.Hobbies!.Count).IsEqualTo(person.Hobbies!.Count);
        await Assert.That(copy.Hobbies).IsNotSameReferenceAs(person.Hobbies); // Should be a different list instance

        // Verify deep clone independence - modify original list
        person.Hobbies![0] = "Swimming";
        person.Hobbies.Add("Gaming");
        
        // Original should see the updates
        await Assert.That(person.Hobbies[0]).IsEqualTo("Swimming");
        await Assert.That(person.Hobbies.Count).IsEqualTo(3);
        await Assert.That(person.Hobbies[2]).IsEqualTo("Gaming");

        // Clone should NOT see the updates (proves deep clone worked)
        await Assert.That(copy.Hobbies![0]).IsEqualTo("Reading"); // Original value preserved
        await Assert.That(copy.Hobbies.Count).IsEqualTo(2); // Original count preserved
        await Assert.That(copy.Hobbies).DoesNotContain("Swimming");
        await Assert.That(copy.Hobbies).DoesNotContain("Gaming");
    }

    [Test]
    public async Task Analyzer1()
    {
        //  disabled as there is no longer a requirement to mark classes as partial!
        ;

        // [|text|]: indicates that a diagnostic is reported for text. By default, this form may only be used for testing analyzers with exactly one DiagnosticDescriptor provided by DiagnosticAnalyzer.SupportedDiagnostics.
        // {|ExpectedDiagnosticId:text|}: indicates that a diagnostic with Id ExpectedDiagnosticId is reported for text.

        /*var sharedAssembly = typeof(FastClonerClonableAttribute).Assembly;
        
        CSharpAnalyzerTest<FastClonerClonableAnalyzer, DefaultVerifier> context = new CSharpAnalyzerTest<FastClonerClonableAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestState =
            {
                AdditionalReferences = { sharedAssembly }
            },
            
            TestCode = """
                       using System;
                       using FastCloner.SourceGenerator.Shared;

                       namespace MyTestCode
                       {
                           [FastClonerClonable]
                           public partial class Person
                           {
                           }
                       }
                       """
        };

        await context.RunAsync();*/
    }
    
    // Test classes mirroring BenchDolly scenario
    [FastClonerClonable]
    public class SimpleClass3
    {
        public int Int { get; set; }
        public uint UInt { get; set; }
        public long Long { get; set; }
        public ulong ULong { get; set; }
        public double Double { get; set; }
        public float Float { get; set; }
        public string String { get; set; } = "";
    }

    [FastClonerClonable]
    public class ComplexClass
    {
        public SimpleClass3? SimpleClass2 { get; set; }
        public SimpleClass3[]? Array { get; set; }
        public List<SimpleClass3>? List { get; set; }
    }

    [FastClonerClonable]
    public class DictionaryContainer
    {
        public Dictionary<string, SimpleClass3>? Dict { get; set; }
    }

    [FastClonerClonable]
    public class RecursiveCollectionContainer
    {
        public List<List<int>>? NestedList { get; set; }
        public Dictionary<string, Dictionary<string, string>>? NestedDict { get; set; }
    }
    
    [Test]
    [SourceGeneratorCompatible]
    public async Task ComplexClass_DeepClone_Should_Create_Independent_Copy()
    {
        // Arrange - create a complex object with nested objects, arrays, and lists
        ComplexClass original = new ComplexClass
        {
            SimpleClass2 = new SimpleClass3
            {
                Int = 10,
                UInt = 1231,
                Long = 1231234561L,
                ULong = 1516524352UL,
                Double = 1235.1235762,
                Float = 1.333F,
                String = "Lorem ipsum ...",
            },
            Array = [
                new SimpleClass3
                {
                    Int = 10,
                    UInt = 1231,
                    Long = 1231234561L,
                    ULong = 1516524352UL,
                    Double = 1235.1235762,
                    Float = 1.333F,
                    String = "Array Item 1",
                },
                new SimpleClass3
                {
                    Int = 20,
                    UInt = 2462,
                    Long = 2462469122L,
                    ULong = 3033048704UL,
                    Double = 2470.2471524,
                    Float = 2.666F,
                    String = "Array Item 2",
                },
            ],
            List = [
                new SimpleClass3
                {
                    Int = 30,
                    UInt = 3693,
                    Long = 3693703683L,
                    ULong = 4549573056UL,
                    Double = 3705.3707286,
                    Float = 3.999F,
                    String = "List Item 1",
                },
                new SimpleClass3
                {
                    Int = 40,
                    UInt = 4924,
                    Long = 4924938244L,
                    ULong = 6066097408UL,
                    Double = 4940.4943048,
                    Float = 5.332F,
                    String = "List Item 2",
                },
            ]
        };

        // Act - deep clone the object
        ComplexClass clone = original.FastDeepClone();

        // Assert - verify the clone is not null
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.SimpleClass2).IsNotNull();
        await Assert.That(clone.Array).IsNotNull();
        await Assert.That(clone.List).IsNotNull();

        // Verify initial values are copied correctly
        await Assert.That(clone.SimpleClass2!.Int).IsEqualTo(10);
        await Assert.That(clone.SimpleClass2.String).IsEqualTo("Lorem ipsum ...");
        await Assert.That(clone.Array!.Length).IsEqualTo(2);
        await Assert.That(clone.Array[0].String).IsEqualTo("Array Item 1");
        await Assert.That(clone.Array[1].String).IsEqualTo("Array Item 2");
        await Assert.That(clone.List!.Count).IsEqualTo(2);
        await Assert.That(clone.List[0].String).IsEqualTo("List Item 1");
        await Assert.That(clone.List[1].String).IsEqualTo("List Item 2");

        // CRITICAL: Verify deep clone - not same references
        await Assert.That(clone.SimpleClass2).IsNotSameReferenceAs(original.SimpleClass2);
        await Assert.That(clone.Array).IsNotSameReferenceAs(original.Array);
        await Assert.That(clone.Array[0]).IsNotSameReferenceAs(original.Array![0]);
        await Assert.That(clone.Array[1]).IsNotSameReferenceAs(original.Array[1]);
        await Assert.That(clone.List).IsNotSameReferenceAs(original.List);
        await Assert.That(clone.List[0]).IsNotSameReferenceAs(original.List![0]);
        await Assert.That(clone.List[1]).IsNotSameReferenceAs(original.List[1]);

        // Modify the original - clone should NOT be affected
        original.SimpleClass2.Int = 999;
        original.SimpleClass2.String = "MODIFIED";
        original.Array[0].String = "ARRAY MODIFIED 1";
        original.Array[1].Int = 888;
        original.List[0].String = "LIST MODIFIED 1";
        original.List[1].Double = 999.999;
        original.List.Add(new SimpleClass3 { String = "NEW ITEM" });

        // Verify clone is unaffected
        await Assert.That(clone.SimpleClass2.Int).IsEqualTo(10).Because("SimpleClass2 should not be modified");
        await Assert.That(clone.SimpleClass2.String).IsEqualTo("Lorem ipsum ...").Because("SimpleClass2.String should not be modified");
        await Assert.That(clone.Array[0].String).IsEqualTo("Array Item 1").Because("Array[0] should not be modified");
        await Assert.That(clone.Array[1].Int).IsEqualTo(20).Because("Array[1] should not be modified");
        await Assert.That(clone.List[0].String).IsEqualTo("List Item 1").Because("List[0] should not be modified");
        await Assert.That(clone.List[1].Double).IsEqualTo(4940.4943048).Within(0.0001).Because("List[1] should not be modified");
        await Assert.That(clone.List.Count).IsEqualTo(2).Because("List count should not change");
        await Assert.That(clone.List).DoesNotContain(original.List[2]).Because("Clone should not have new item added to original");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Dictionary_DeepClone_Should_Clone_Keys_And_Values()
    {
        // Arrange
        DictionaryContainer original = new DictionaryContainer
        {
            Dict = new Dictionary<string, SimpleClass3>
            {
                { "Key1", new SimpleClass3 { String = "Value 1" } },
                { "Key2", new SimpleClass3 { String = "Value 2" } }
            }
        };

        // Act
        DictionaryContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Dict).IsNotNull();
        await Assert.That(clone.Dict!.Count).IsEqualTo(2);
        await Assert.That(clone.Dict["Key1"].String).IsEqualTo("Value 1");
        await Assert.That(clone.Dict["Key2"].String).IsEqualTo("Value 2");

        // Verify deep clone - values should be new instances
        await Assert.That(clone.Dict["Key1"]).IsNotSameReferenceAs(original.Dict!["Key1"]);
        await Assert.That(clone.Dict["Key2"]).IsNotSameReferenceAs(original.Dict["Key2"]);

        // Modify original
        original.Dict["Key1"].String = "MODIFIED";
        
        // Verify clone is unaffected
        await Assert.That(clone.Dict["Key1"].String).IsEqualTo("Value 1");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task RecursiveCollection_Should_Be_DeepCloned()
    {
        // Arrange
        RecursiveCollectionContainer original = new RecursiveCollectionContainer
        {
            NestedList = [ [1, 2], [3, 4] ],
            NestedDict = new Dictionary<string, Dictionary<string, string>>
            {
                { "Group1", new Dictionary<string, string> { { "A", "1" } } },
                { "Group2", new Dictionary<string, string> { { "B", "2" } } }
            }
        };

        // Act
        RecursiveCollectionContainer clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();

        // Check nested list
        await Assert.That(clone!.NestedList).IsNotNull();
        await Assert.That(clone.NestedList!.Count).IsEqualTo(2);
        await Assert.That(clone.NestedList[0]).IsNotSameReferenceAs(original.NestedList![0]);
        await Assert.That(clone.NestedList[0]).IsEquivalentTo([1, 2]);

        // Check nested dict
        await Assert.That(clone.NestedDict).IsNotNull();
        await Assert.That(clone.NestedDict!.Count).IsEqualTo(2);
        await Assert.That(clone.NestedDict["Group1"]).IsNotSameReferenceAs(original.NestedDict!["Group1"]);
        await Assert.That(clone.NestedDict["Group1"]["A"]).IsEqualTo("1");

        // Verify independence
        original.NestedList[0].Add(99);
        original.NestedDict["Group1"]["A"] = "MODIFIED";
        
        await Assert.That(clone.NestedList[0].Count).IsEqualTo(2);
        await Assert.That(clone.NestedDict["Group1"]["A"]).IsEqualTo("1");
    }

    [FastClonerClonable]
    public class EnumerableSamples
    {
        public List<int>? ListInts { get; set; }
        public int[]? ArrayInts { get; set; }
        public IList<int>? IListInts { get; set; }
        public ICollection<int>? ICollectionInts { get; set; }
        public IEnumerable<int>? IEnumerableInts { get; set; }
        public IReadOnlyList<int>? IReadOnlyListInts { get; set; }
        public IReadOnlyCollection<int>? IReadOnlyCollectionInts { get; set; }
        public HashSet<int>? HashSetInts { get; set; }
        public Queue<int>? QueueInts { get; set; }
        public Stack<int>? StackInts { get; set; }
        public LinkedList<int>? LinkedListInts { get; set; }
        public Dictionary<int, string>? DictionaryInts { get; set; }
        public IReadOnlyDictionary<int, string>? IReadOnlyDictionaryInts { get; set; }
        public SortedDictionary<int, string>? SortedDictionaryInts { get; set; }
        public SortedList<int, string>? SortedListInts { get; set; }
        public IReadOnlyDictionary<int, MyPersonCls>? IReadOnlyDictionaryIntPeople { get; set; }
    }

    public class MyPersonCls
    {
        public string MySuperName { get; set; }
        public MyPersonNestedClass MyDict { get; set; }
    }

    public class MyPersonNestedClass
    {
        public Dictionary<string, string> Dict { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task EnumerableSamples_Should_Be_DeepCloned()
    {
        // Arrange
        EnumerableSamples original = new EnumerableSamples
        {
            ListInts = [1, 2, 3],
            ArrayInts = [4, 5, 6],
            IListInts = new List<int> { 7, 8, 9 },
            ICollectionInts = new List<int> { 10, 11, 12 },
            IEnumerableInts = new List<int> { 13, 14, 15 },
            IReadOnlyListInts = new List<int> { 16, 17, 18 },
            IReadOnlyCollectionInts = new List<int> { 19, 20, 21 },
            HashSetInts = [22, 23, 24],
            QueueInts = new Queue<int>([25, 26, 27]),
            StackInts = new Stack<int>([28, 29, 30]),
            LinkedListInts = new LinkedList<int>([31, 32, 33]),
            DictionaryInts = new Dictionary<int, string> { { 1, "One" }, { 2, "Two" } },
            IReadOnlyDictionaryInts = new Dictionary<int, string> { { 3, "Three" }, { 4, "Four" } },
            SortedDictionaryInts = new SortedDictionary<int, string> { { 5, "Five" }, { 6, "Six" } },
            SortedListInts = new SortedList<int, string> { { 7, "Seven" }, { 8, "Eight" } },
            IReadOnlyDictionaryIntPeople = new Dictionary<int, MyPersonCls> { { 9, new MyPersonCls { MySuperName = "Nine" } } }
        };

        // Act
        EnumerableSamples clone = original.FastDeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();

        // List
        await Assert.That(clone!.ListInts).IsNotSameReferenceAs(original.ListInts);
        await Assert.That(clone.ListInts).IsEquivalentTo(original.ListInts);

        // Array
        await Assert.That(clone.ArrayInts).IsNotSameReferenceAs(original.ArrayInts);
        await Assert.That(clone.ArrayInts).IsEquivalentTo(original.ArrayInts);

        // IList
        await Assert.That(clone.IListInts).IsNotSameReferenceAs(original.IListInts);
        await Assert.That(clone.IListInts).IsEquivalentTo(original.IListInts);
        await Assert.That(clone.IListInts).IsAssignableTo<List<int>>(); // Typically clones to List for interfaces

        // ICollection
        await Assert.That(clone.ICollectionInts).IsNotSameReferenceAs(original.ICollectionInts);
        await Assert.That(clone.ICollectionInts).IsEquivalentTo(original.ICollectionInts);

        // IEnumerable
        await Assert.That(clone.IEnumerableInts).IsNotSameReferenceAs(original.IEnumerableInts);
        await Assert.That(clone.IEnumerableInts).IsEquivalentTo(original.IEnumerableInts);

        // IReadOnlyList
        await Assert.That(clone.IReadOnlyListInts).IsNotSameReferenceAs(original.IReadOnlyListInts);
        await Assert.That(clone.IReadOnlyListInts).IsEquivalentTo(original.IReadOnlyListInts);

        // IReadOnlyCollection
        await Assert.That(clone.IReadOnlyCollectionInts).IsNotSameReferenceAs(original.IReadOnlyCollectionInts);
        await Assert.That(clone.IReadOnlyCollectionInts).IsEquivalentTo(original.IReadOnlyCollectionInts);

        // HashSet
        await Assert.That(clone.HashSetInts).IsNotSameReferenceAs(original.HashSetInts);
        await Assert.That(clone.HashSetInts).IsEquivalentTo(original.HashSetInts);

        // Queue
        await Assert.That(clone.QueueInts).IsNotSameReferenceAs(original.QueueInts);
        await Assert.That(clone.QueueInts).IsEquivalentTo(original.QueueInts);

        // Stack
        await Assert.That(clone.StackInts).IsNotSameReferenceAs(original.StackInts);
        await Assert.That(clone.StackInts).IsEquivalentTo(original.StackInts);

        // LinkedList
        await Assert.That(clone.LinkedListInts).IsNotSameReferenceAs(original.LinkedListInts);
        await Assert.That(clone.LinkedListInts).IsEquivalentTo(original.LinkedListInts);

        // Dictionary
        await Assert.That(clone.DictionaryInts).IsNotSameReferenceAs(original.DictionaryInts);
        await Assert.That(clone.DictionaryInts).IsEquivalentTo(original.DictionaryInts);

        // IReadOnlyDictionary
        await Assert.That(clone.IReadOnlyDictionaryInts).IsNotSameReferenceAs(original.IReadOnlyDictionaryInts);
        await Assert.That(clone.IReadOnlyDictionaryInts).IsEquivalentTo(original.IReadOnlyDictionaryInts);

        // SortedDictionary
        await Assert.That(clone.SortedDictionaryInts).IsNotSameReferenceAs(original.SortedDictionaryInts);
        await Assert.That(clone.SortedDictionaryInts).IsEquivalentTo(original.SortedDictionaryInts);

        // SortedList
        await Assert.That(clone.SortedListInts).IsNotSameReferenceAs(original.SortedListInts);
        await Assert.That(clone.SortedListInts).IsEquivalentTo(original.SortedListInts);

        // Nested Implicit
        await Assert.That(clone.IReadOnlyDictionaryIntPeople).IsNotSameReferenceAs(original.IReadOnlyDictionaryIntPeople);
        await Assert.That(clone.IReadOnlyDictionaryIntPeople![9]).IsNotSameReferenceAs(original.IReadOnlyDictionaryIntPeople![9]);
        await Assert.That(clone.IReadOnlyDictionaryIntPeople[9].MySuperName).IsEqualTo("Nine");
    }

    // Test classes without public parameterless constructors
    [FastClonerClonable]
    public class ClassWithoutParameterlessCtor
    {
        // Read-only properties set in constructor - these won't be cloned
        // since FormatterServices.GetUninitializedObject() doesn't call the constructor
        public string? Name { get; }
        public int Value { get; }
        
        // Writable properties - these will be cloned
        public string Description { get; set; } = string.Empty;
        public string? AdditionalData { get; set; }

        // Only constructor requires parameters
        public ClassWithoutParameterlessCtor(string name, int value)
        {
            Name = name;
            Value = value;
        }

        // Factory method for convenience
        public static ClassWithoutParameterlessCtor Create(string name, int value)
        {
            return new ClassWithoutParameterlessCtor(name, value);
        }
    }

    [FastClonerClonable]
    public class ClassWithCircularRefNoCtor
    {
        // Read-only property set in constructor - won't be cloned
        public string? Name { get; }
        // Writable property - will be cloned
        public ClassWithCircularRefNoCtor? Self { get; set; }

        public ClassWithCircularRefNoCtor(string name)
        {
            Name = name;
        }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Class_Without_Public_Parameterless_Constructor_Should_Be_Cloned()
    {
        // Arrange - ClassWithoutParameterlessCtor requires a parameter in its constructor
        // Note: Read-only properties set in constructor (Name, Value) won't be cloned since
        // FormatterServices.GetUninitializedObject() doesn't call the constructor.
        // Only writable properties set after construction will be cloned.
        ClassWithoutParameterlessCtor original = ClassWithoutParameterlessCtor.Create("TestName", 42);
        original.Description = "Test Description";
        original.AdditionalData = "Extra Data";
        
        // Act - Should use FormatterServices.GetUninitializedObject internally
        ClassWithoutParameterlessCtor clone = original.FastDeepClone();
        
        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone).IsNotSameReferenceAs(original);
        // Read-only properties from constructor will be default values (not cloned)
        await Assert.That(clone.Name).IsNull(); // Default value since constructor wasn't called
        await Assert.That(clone.Value).IsEqualTo(0); // Default value since constructor wasn't called
        // Writable properties set after construction will be cloned
        await Assert.That(clone.Description).IsEqualTo("Test Description");
        await Assert.That(clone.AdditionalData).IsEqualTo("Extra Data");
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Class_Without_Parameterless_Constructor_With_Circular_References_Should_Be_Cloned()
    {
        // Arrange - Test that circular reference tracking works with FormatterServices
        ClassWithCircularRefNoCtor original = new ClassWithCircularRefNoCtor("Initial");
        original.Self = original; // Create circular reference
        
        // Act
        ClassWithCircularRefNoCtor clone = original.FastDeepClone();
        
        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Name).IsNull(); // Read-only property from constructor won't be cloned
        await Assert.That(clone.Self).IsSameReferenceAs(clone); // Circular reference should be preserved
    }

    // Test classes for complex circular dependency testing
    [FastClonerClonable]
    public class CircularNodeA
    {
        public CircularNodeB? B { get; set; }
    }

    [FastClonerClonable]
    public class CircularNodeB
    {
        public CircularNodeA? A { get; set; }
    }

    [FastClonerClonable]
    public class CircularNodeC
    {
        public CircularNodeC? Self { get; set; }
    }

    [FastClonerClonable]
    public class CircularNodeD
    {
        public CircularNodeE? E { get; set; }
    }

    [FastClonerClonable]
    public class CircularNodeE
    {
        public CircularNodeF? F { get; set; }
    }

    [FastClonerClonable]
    public class CircularNodeF
    {
        public CircularNodeE? E { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task FastDeepClone_Should_Handle_Complex_Circular_Dependencies()
    {
        // 1. Direct Cycle A <-> B
        CircularNodeA a = new CircularNodeA();
        CircularNodeB b = new CircularNodeB();
        a.B = b;
        b.A = a;
        
        CircularNodeA cloneA = a.FastDeepClone();
        await Assert.That(cloneA).IsNotNull();
        await Assert.That(cloneA).IsNotSameReferenceAs(a);
        await Assert.That(cloneA!.B).IsNotSameReferenceAs(b);
        await Assert.That(cloneA.B!.A).IsSameReferenceAs(cloneA); // Cycle preserved

        // 2. Self Cycle C -> C
        CircularNodeC c = new CircularNodeC();
        c.Self = c;
        
        CircularNodeC cloneC = c.FastDeepClone();
        await Assert.That(cloneC).IsNotNull();
        await Assert.That(cloneC).IsNotSameReferenceAs(c);
        await Assert.That(cloneC!.Self).IsSameReferenceAs(cloneC); // Cycle preserved

        // 3. Lollipop Graph D -> E <-> F
        CircularNodeD d = new CircularNodeD();
        CircularNodeE e = new CircularNodeE();
        CircularNodeF f = new CircularNodeF();
        d.E = e;
        e.F = f;
        f.E = e;
        
        CircularNodeD cloneD = d.FastDeepClone();
        await Assert.That(cloneD).IsNotNull();
        await Assert.That(cloneD).IsNotSameReferenceAs(d);
        await Assert.That(cloneD!.E).IsNotSameReferenceAs(e);
        await Assert.That(cloneD.E!.F).IsNotSameReferenceAs(f);
        await Assert.That(cloneD.E!.F!.E).IsSameReferenceAs(cloneD.E); // Cycle preserved
    }
}