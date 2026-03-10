using FastCloner.SourceGenerator.Shared;
using System.Threading.Tasks;

namespace FastCloner.Tests;
[SourceGeneratorCompatible]
public class RecordTests
{
    #region Test Records - Simple Record
    
    [FastClonerClonable]
    public record SimpleRecord(string Name, int Age);

    #endregion

    #region Test Records - Record with Collections
    
    [FastClonerClonable]
    public record RecordWithCollection
    {
        public string? Name { get; init; }
        public List<string>? Tags { get; init; }
        public int Count { get; init; }
    }

    #endregion

    #region Test Records - Record with Nested Complex Types
    
    [FastClonerClonable]
    public record PersonRecord
    {
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public AddressRecord? Address { get; init; }
        public List<string>? PhoneNumbers { get; init; }
    }

    [FastClonerClonable]
    public record AddressRecord
    {
        public string? Street { get; init; }
        public string? City { get; init; }
        public string? ZipCode { get; init; }
    }

    #endregion

    #region Test Records - Record Struct
    
    [FastClonerClonable]
    public record struct PointRecord(double X, double Y);

    [FastClonerClonable]
    public record struct RecordStructWithCollection
    {
        public string? Label { get; init; }
        public List<int>? Values { get; init; }
    }

    #endregion

    #region Test Records - Record with Dictionary
    
    [FastClonerClonable]
    public record RecordWithDictionary
    {
        public string? Name { get; init; }
        public Dictionary<string, int>? Scores { get; init; }
    }

    #endregion

    #region Tests - Simple Record Cloning
    
    [Test]
    [SourceGeneratorCompatible]
    public async Task SimpleRecord_Should_Clone()
    {
        // Arrange
        SimpleRecord record = new SimpleRecord("Alice", 30);
        
        // Act
        SimpleRecord clone = record.FastDeepClone();
        
        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone).IsNotSameReferenceAs(record);
        await Assert.That(clone!.Name).IsEqualTo("Alice");
        await Assert.That(clone.Age).IsEqualTo(30);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task SimpleRecord_Clone_Should_Be_Independent()
    {
        // Arrange
        SimpleRecord record = new SimpleRecord("Bob", 25);
        
        // Act
        SimpleRecord clone = record.FastDeepClone();
        SimpleRecord modified = record with { Name = "Charlie" };
        
        // Assert - clone should be unaffected by modifications to original
        await Assert.That(clone!.Name).IsEqualTo("Bob");
        await Assert.That(modified.Name).IsEqualTo("Charlie");
    }

    #endregion

    #region Tests - Record with Collections
    
    [Test]
    [SourceGeneratorCompatible]
    public async Task RecordWithCollection_Should_DeepClone()
    {
        // Arrange
        RecordWithCollection record = new RecordWithCollection
        {
            Name = "Test",
            Tags = ["tag1", "tag2", "tag3"],
            Count = 5
        };
        
        // Act
        RecordWithCollection clone = record.FastDeepClone();
        
        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Name).IsEqualTo("Test");
        await Assert.That(clone.Count).IsEqualTo(5);
        await Assert.That(clone.Tags).IsNotNull();
        await Assert.That(clone.Tags).IsNotSameReferenceAs(record.Tags); // Deep cloned
        await Assert.That(clone.Tags!.Count).IsEqualTo(3);
        await Assert.That(clone.Tags).IsEquivalentTo(record.Tags);
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task RecordWithCollection_Clone_Should_Be_Independent()
    {
        // Arrange
        RecordWithCollection record = new RecordWithCollection
        {
            Name = "Test",
            Tags = ["a", "b"],
            Count = 2
        };
        
        // Act
        RecordWithCollection clone = record.FastDeepClone();
        record.Tags!.Add("c");
        
        // Assert - clone's list should be unchanged
        await Assert.That(record.Tags.Count).IsEqualTo(3);
        await Assert.That(clone!.Tags!.Count).IsEqualTo(2);
        await Assert.That(clone.Tags).DoesNotContain("c");
    }

    #endregion

    #region Tests - Record with Nested Types
    
    [Test]
    [SourceGeneratorCompatible]
    public async Task PersonRecord_Should_DeepClone_Nested()
    {
        // Arrange
        PersonRecord person = new PersonRecord
        {
            FirstName = "John",
            LastName = "Doe",
            Address = new AddressRecord
            {
                Street = "123 Main St",
                City = "Seattle",
                ZipCode = "98101"
            },
            PhoneNumbers = ["555-1234", "555-5678"]
        };
        
        // Act
        PersonRecord clone = person.FastDeepClone();
        
        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.FirstName).IsEqualTo("John");
        await Assert.That(clone.LastName).IsEqualTo("Doe");

        // Nested record should be deep cloned
        await Assert.That(clone.Address).IsNotNull();
        await Assert.That(clone.Address).IsNotSameReferenceAs(person.Address);
        await Assert.That(clone.Address!.Street).IsEqualTo("123 Main St");
        await Assert.That(clone.Address.City).IsEqualTo("Seattle");

        // List should be deep cloned
        await Assert.That(clone.PhoneNumbers).IsNotSameReferenceAs(person.PhoneNumbers);
        await Assert.That(clone.PhoneNumbers!.Count).IsEqualTo(2);
    }

    #endregion
    
    [Test]
    [SourceGeneratorCompatible]
    public async Task PointRecord_Struct_Should_Clone()
    {
        // Arrange
        PointRecord point = new PointRecord(10.5, 20.5);
        
        // Act
        PointRecord clone = point.FastDeepClone();
        
        // Assert
        await Assert.That(clone.X).IsEqualTo(10.5);
        await Assert.That(clone.Y).IsEqualTo(20.5);
    }
    
    [Test]
    [SourceGeneratorCompatible]
    public async Task RecordStructWithCollection_Should_DeepClone()
    {
        // Arrange
        RecordStructWithCollection record = new RecordStructWithCollection
        {
            Label = "Data",
            Values = [1, 2, 3, 4, 5]
        };
        
        // Act
        RecordStructWithCollection clone = record.FastDeepClone();
        record.Values!.Add(6);
        
        // Assert - clone's list should be independent
        await Assert.That(clone.Label).IsEqualTo("Data");
        await Assert.That(clone.Values!.Count).IsEqualTo(5);
        await Assert.That(record.Values.Count).IsEqualTo(6);
    }
    
    // #endregion

    #region Tests - Record with Dictionary
    
    [Test]
    [SourceGeneratorCompatible]
    public async Task RecordWithDictionary_Should_DeepClone()
    {
        // Arrange
        RecordWithDictionary record = new RecordWithDictionary
        {
            Name = "Scores",
            Scores = new Dictionary<string, int>
            {
                ["Alice"] = 100,
                ["Bob"] = 95,
                ["Charlie"] = 87
            }
        };
        
        // Act
        RecordWithDictionary clone = record.FastDeepClone();
        record.Scores!["Alice"] = 50;
        record.Scores["Diana"] = 92;
        
        // Assert - clone's dictionary should be independent
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Name).IsEqualTo("Scores");
        await Assert.That(clone.Scores).IsNotSameReferenceAs(record.Scores);
        await Assert.That(clone.Scores!.Count).IsEqualTo(3);
        await Assert.That(clone.Scores["Alice"]).IsEqualTo(100); // Original value
        await Assert.That(clone.Scores.ContainsKey("Diana")).IsFalse();
    }

    #endregion

    #region Tests - Null Handling
    
    [Test]
    [SourceGeneratorCompatible]
    public async Task Null_Record_Should_Return_Null()
    {
        // Arrange
        SimpleRecord? record = null;
        
        // Act
        SimpleRecord? clone = record.FastDeepClone();
        
        // Assert
        await Assert.That(clone).IsNull();
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task Record_With_Null_Properties_Should_Clone()
    {
        // Arrange
        RecordWithCollection record = new RecordWithCollection
        {
            Name = null,
            Tags = null,
            Count = 0
        };
        
        // Act
        RecordWithCollection clone = record.FastDeepClone();
        
        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Name).IsNull();
        await Assert.That(clone.Tags).IsNull();
        await Assert.That(clone.Count).IsEqualTo(0);
    }

    #endregion
}
