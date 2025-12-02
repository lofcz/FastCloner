// Analyzer removed - partial requirement no longer needed
using FastCloner.SourceGenerator.Shared;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace FastCloner.Tests;

[TestFixture]
[SourceGeneratorCompatible]
public class SourceGeneratorTests
{
    [FastClonerClonable]
    public partial class Person
    {
        public string? Name { get; set; }
        public int Age { get; set; }
        public List<string>? Hobbies { get; set; }
    }
    
    [Test]
    [SourceGeneratorCompatible]
    public void SimpleObject_Should_Be_Cloned()
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
        Assert.That(copy, Is.Not.Null);
        Assert.That(copy!.Age, Is.EqualTo(person.Age));
        Assert.That(copy.Name, Is.EqualTo(person.Name));
        Assert.That(copy.Hobbies, Is.Not.Null);
        Assert.That(copy.Hobbies!.Count, Is.EqualTo(person.Hobbies!.Count));
        Assert.That(copy.Hobbies, Is.Not.SameAs(person.Hobbies)); // Should be a different list instance
        
        // Verify deep clone independence - modify original list
        person.Hobbies![0] = "Swimming";
        person.Hobbies.Add("Gaming");
        
        // Original should see the updates
        Assert.That(person.Hobbies[0], Is.EqualTo("Swimming"));
        Assert.That(person.Hobbies.Count, Is.EqualTo(3));
        Assert.That(person.Hobbies[2], Is.EqualTo("Gaming"));
        
        // Clone should NOT see the updates (proves deep clone worked)
        Assert.That(copy.Hobbies![0], Is.EqualTo("Reading")); // Original value preserved
        Assert.That(copy.Hobbies.Count, Is.EqualTo(2)); // Original count preserved
        Assert.That(copy.Hobbies, Does.Not.Contain("Swimming"));
        Assert.That(copy.Hobbies, Does.Not.Contain("Gaming"));
    }

    [Test]
    public async Task Analyzer1()
    {
        //  disabled as there is no longer a requirement to mark classes as partial!
        Assert.Pass();
        
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
    public void ComplexClass_DeepClone_Should_Create_Independent_Copy()
    {
        // Arrange - create a complex object with nested objects, arrays, and lists
        var original = new ComplexClass
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
        var clone = original.FastDeepClone();

        // Assert - verify the clone is not null
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.SimpleClass2, Is.Not.Null);
        Assert.That(clone.Array, Is.Not.Null);
        Assert.That(clone.List, Is.Not.Null);

        // Verify initial values are copied correctly
        Assert.That(clone.SimpleClass2!.Int, Is.EqualTo(10));
        Assert.That(clone.SimpleClass2.String, Is.EqualTo("Lorem ipsum ..."));
        Assert.That(clone.Array!.Length, Is.EqualTo(2));
        Assert.That(clone.Array[0].String, Is.EqualTo("Array Item 1"));
        Assert.That(clone.Array[1].String, Is.EqualTo("Array Item 2"));
        Assert.That(clone.List!.Count, Is.EqualTo(2));
        Assert.That(clone.List[0].String, Is.EqualTo("List Item 1"));
        Assert.That(clone.List[1].String, Is.EqualTo("List Item 2"));

        // CRITICAL: Verify deep clone - not same references
        Assert.That(clone.SimpleClass2, Is.Not.SameAs(original.SimpleClass2));
        Assert.That(clone.Array, Is.Not.SameAs(original.Array));
        Assert.That(clone.Array[0], Is.Not.SameAs(original.Array![0]));
        Assert.That(clone.Array[1], Is.Not.SameAs(original.Array[1]));
        Assert.That(clone.List, Is.Not.SameAs(original.List));
        Assert.That(clone.List[0], Is.Not.SameAs(original.List![0]));
        Assert.That(clone.List[1], Is.Not.SameAs(original.List[1]));

        // Modify the original - clone should NOT be affected
        original.SimpleClass2.Int = 999;
        original.SimpleClass2.String = "MODIFIED";
        original.Array[0].String = "ARRAY MODIFIED 1";
        original.Array[1].Int = 888;
        original.List[0].String = "LIST MODIFIED 1";
        original.List[1].Double = 999.999;
        original.List.Add(new SimpleClass3 { String = "NEW ITEM" });

        // Verify clone is unaffected
        Assert.That(clone.SimpleClass2.Int, Is.EqualTo(10), "SimpleClass2 should not be modified");
        Assert.That(clone.SimpleClass2.String, Is.EqualTo("Lorem ipsum ..."), "SimpleClass2.String should not be modified");
        Assert.That(clone.Array[0].String, Is.EqualTo("Array Item 1"), "Array[0] should not be modified");
        Assert.That(clone.Array[1].Int, Is.EqualTo(20), "Array[1] should not be modified");
        Assert.That(clone.List[0].String, Is.EqualTo("List Item 1"), "List[0] should not be modified");
        Assert.That(clone.List[1].Double, Is.EqualTo(4940.4943048).Within(0.0001), "List[1] should not be modified");
        Assert.That(clone.List.Count, Is.EqualTo(2), "List count should not change");
        Assert.That(clone.List, Does.Not.Contain(original.List[2]), "Clone should not have new item added to original");
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Dictionary_DeepClone_Should_Clone_Keys_And_Values()
    {
        // Arrange
        var original = new DictionaryContainer
        {
            Dict = new Dictionary<string, SimpleClass3>
            {
                { "Key1", new SimpleClass3 { String = "Value 1" } },
                { "Key2", new SimpleClass3 { String = "Value 2" } }
            }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Dict, Is.Not.Null);
        Assert.That(clone.Dict!.Count, Is.EqualTo(2));
        Assert.That(clone.Dict["Key1"].String, Is.EqualTo("Value 1"));
        Assert.That(clone.Dict["Key2"].String, Is.EqualTo("Value 2"));

        // Verify deep clone - values should be new instances
        Assert.That(clone.Dict["Key1"], Is.Not.SameAs(original.Dict!["Key1"]));
        Assert.That(clone.Dict["Key2"], Is.Not.SameAs(original.Dict["Key2"]));
        
        // Modify original
        original.Dict["Key1"].String = "MODIFIED";
        
        // Verify clone is unaffected
        Assert.That(clone.Dict["Key1"].String, Is.EqualTo("Value 1"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void RecursiveCollection_Should_Be_DeepCloned()
    {
        // Arrange
        var original = new RecursiveCollectionContainer
        {
            NestedList = [ [1, 2], [3, 4] ],
            NestedDict = new Dictionary<string, Dictionary<string, string>>
            {
                { "Group1", new Dictionary<string, string> { { "A", "1" } } },
                { "Group2", new Dictionary<string, string> { { "B", "2" } } }
            }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        
        // Check nested list
        Assert.That(clone!.NestedList, Is.Not.Null);
        Assert.That(clone.NestedList!.Count, Is.EqualTo(2));
        Assert.That(clone.NestedList[0], Is.Not.SameAs(original.NestedList![0]));
        Assert.That(clone.NestedList[0], Is.EqualTo(new[] { 1, 2 }));
        
        // Check nested dict
        Assert.That(clone.NestedDict, Is.Not.Null);
        Assert.That(clone.NestedDict!.Count, Is.EqualTo(2));
        Assert.That(clone.NestedDict["Group1"], Is.Not.SameAs(original.NestedDict!["Group1"]));
        Assert.That(clone.NestedDict["Group1"]["A"], Is.EqualTo("1"));
        
        // Verify independence
        original.NestedList[0].Add(99);
        original.NestedDict["Group1"]["A"] = "MODIFIED";
        
        Assert.That(clone.NestedList[0].Count, Is.EqualTo(2));
        Assert.That(clone.NestedDict["Group1"]["A"], Is.EqualTo("1"));
    }

    [FastClonerClonable]
    public partial class EnumerableSamples
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
    public void EnumerableSamples_Should_Be_DeepCloned()
    {
        // Arrange
        var original = new EnumerableSamples
        {
            ListInts = new List<int> { 1, 2, 3 },
            ArrayInts = new int[] { 4, 5, 6 },
            IListInts = new List<int> { 7, 8, 9 },
            ICollectionInts = new List<int> { 10, 11, 12 },
            IEnumerableInts = new List<int> { 13, 14, 15 },
            IReadOnlyListInts = new List<int> { 16, 17, 18 },
            IReadOnlyCollectionInts = new List<int> { 19, 20, 21 },
            HashSetInts = new HashSet<int> { 22, 23, 24 },
            QueueInts = new Queue<int>(new[] { 25, 26, 27 }),
            StackInts = new Stack<int>(new[] { 28, 29, 30 }),
            LinkedListInts = new LinkedList<int>(new[] { 31, 32, 33 }),
            DictionaryInts = new Dictionary<int, string> { { 1, "One" }, { 2, "Two" } },
            IReadOnlyDictionaryInts = new Dictionary<int, string> { { 3, "Three" }, { 4, "Four" } },
            SortedDictionaryInts = new SortedDictionary<int, string> { { 5, "Five" }, { 6, "Six" } },
            SortedListInts = new SortedList<int, string> { { 7, "Seven" }, { 8, "Eight" } },
            IReadOnlyDictionaryIntPeople = new Dictionary<int, MyPersonCls> { { 9, new MyPersonCls { MySuperName = "Nine" } } }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);

        // List
        Assert.That(clone!.ListInts, Is.Not.SameAs(original.ListInts));
        Assert.That(clone.ListInts, Is.EquivalentTo(original.ListInts));

        // Array
        Assert.That(clone.ArrayInts, Is.Not.SameAs(original.ArrayInts));
        Assert.That(clone.ArrayInts, Is.EquivalentTo(original.ArrayInts));

        // IList
        Assert.That(clone.IListInts, Is.Not.SameAs(original.IListInts));
        Assert.That(clone.IListInts, Is.EquivalentTo(original.IListInts));
        Assert.That(clone.IListInts, Is.InstanceOf<List<int>>()); // Typically clones to List for interfaces

        // ICollection
        Assert.That(clone.ICollectionInts, Is.Not.SameAs(original.ICollectionInts));
        Assert.That(clone.ICollectionInts, Is.EquivalentTo(original.ICollectionInts));

        // IEnumerable
        Assert.That(clone.IEnumerableInts, Is.Not.SameAs(original.IEnumerableInts));
        Assert.That(clone.IEnumerableInts, Is.EquivalentTo(original.IEnumerableInts));

        // IReadOnlyList
        Assert.That(clone.IReadOnlyListInts, Is.Not.SameAs(original.IReadOnlyListInts));
        Assert.That(clone.IReadOnlyListInts, Is.EquivalentTo(original.IReadOnlyListInts));

        // IReadOnlyCollection
        Assert.That(clone.IReadOnlyCollectionInts, Is.Not.SameAs(original.IReadOnlyCollectionInts));
        Assert.That(clone.IReadOnlyCollectionInts, Is.EquivalentTo(original.IReadOnlyCollectionInts));

        // HashSet
        Assert.That(clone.HashSetInts, Is.Not.SameAs(original.HashSetInts));
        Assert.That(clone.HashSetInts, Is.EquivalentTo(original.HashSetInts));

        // Queue
        Assert.That(clone.QueueInts, Is.Not.SameAs(original.QueueInts));
        Assert.That(clone.QueueInts, Is.EquivalentTo(original.QueueInts));

        // Stack
        Assert.That(clone.StackInts, Is.Not.SameAs(original.StackInts));
        Assert.That(clone.StackInts, Is.EquivalentTo(original.StackInts));

        // LinkedList
        Assert.That(clone.LinkedListInts, Is.Not.SameAs(original.LinkedListInts));
        Assert.That(clone.LinkedListInts, Is.EquivalentTo(original.LinkedListInts));

        // Dictionary
        Assert.That(clone.DictionaryInts, Is.Not.SameAs(original.DictionaryInts));
        Assert.That(clone.DictionaryInts, Is.EquivalentTo(original.DictionaryInts));

        // IReadOnlyDictionary
        Assert.That(clone.IReadOnlyDictionaryInts, Is.Not.SameAs(original.IReadOnlyDictionaryInts));
        Assert.That(clone.IReadOnlyDictionaryInts, Is.EquivalentTo(original.IReadOnlyDictionaryInts));

        // SortedDictionary
        Assert.That(clone.SortedDictionaryInts, Is.Not.SameAs(original.SortedDictionaryInts));
        Assert.That(clone.SortedDictionaryInts, Is.EquivalentTo(original.SortedDictionaryInts));

        // SortedList
        Assert.That(clone.SortedListInts, Is.Not.SameAs(original.SortedListInts));
        Assert.That(clone.SortedListInts, Is.EquivalentTo(original.SortedListInts));

        // Nested Implicit
        Assert.That(clone.IReadOnlyDictionaryIntPeople, Is.Not.SameAs(original.IReadOnlyDictionaryIntPeople));
        Assert.That(clone.IReadOnlyDictionaryIntPeople![9], Is.Not.SameAs(original.IReadOnlyDictionaryIntPeople![9]));
        Assert.That(clone.IReadOnlyDictionaryIntPeople[9].MySuperName, Is.EqualTo("Nine"));
    }
}
