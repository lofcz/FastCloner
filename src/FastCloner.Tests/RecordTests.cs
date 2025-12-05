using FastCloner.SourceGenerator.Shared;
using NUnit.Framework;

namespace FastCloner.Tests;

[TestFixture]
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
    public void SimpleRecord_Should_Clone()
    {
        // Arrange
        var record = new SimpleRecord("Alice", 30);
        
        // Act
        var clone = record.FastDeepClone();
        
        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.Not.SameAs(record));
        Assert.That(clone!.Name, Is.EqualTo("Alice"));
        Assert.That(clone.Age, Is.EqualTo(30));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void SimpleRecord_Clone_Should_Be_Independent()
    {
        // Arrange
        var record = new SimpleRecord("Bob", 25);
        
        // Act
        var clone = record.FastDeepClone();
        var modified = record with { Name = "Charlie" };
        
        // Assert - clone should be unaffected by modifications to original
        Assert.That(clone!.Name, Is.EqualTo("Bob"));
        Assert.That(modified.Name, Is.EqualTo("Charlie"));
    }

    #endregion

    #region Tests - Record with Collections
    
    [Test]
    [SourceGeneratorCompatible]
    public void RecordWithCollection_Should_DeepClone()
    {
        // Arrange
        var record = new RecordWithCollection
        {
            Name = "Test",
            Tags = ["tag1", "tag2", "tag3"],
            Count = 5
        };
        
        // Act
        var clone = record.FastDeepClone();
        
        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.EqualTo("Test"));
        Assert.That(clone.Count, Is.EqualTo(5));
        Assert.That(clone.Tags, Is.Not.Null);
        Assert.That(clone.Tags, Is.Not.SameAs(record.Tags)); // Deep cloned
        Assert.That(clone.Tags!.Count, Is.EqualTo(3));
        Assert.That(clone.Tags, Is.EquivalentTo(record.Tags));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void RecordWithCollection_Clone_Should_Be_Independent()
    {
        // Arrange
        var record = new RecordWithCollection
        {
            Name = "Test",
            Tags = ["a", "b"],
            Count = 2
        };
        
        // Act
        var clone = record.FastDeepClone();
        record.Tags!.Add("c");
        
        // Assert - clone's list should be unchanged
        Assert.That(record.Tags.Count, Is.EqualTo(3));
        Assert.That(clone!.Tags!.Count, Is.EqualTo(2));
        Assert.That(clone.Tags, Does.Not.Contain("c"));
    }

    #endregion

    #region Tests - Record with Nested Types
    
    [Test]
    [SourceGeneratorCompatible]
    public void PersonRecord_Should_DeepClone_Nested()
    {
        // Arrange
        var person = new PersonRecord
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
        var clone = person.FastDeepClone();
        
        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.FirstName, Is.EqualTo("John"));
        Assert.That(clone.LastName, Is.EqualTo("Doe"));
        
        // Nested record should be deep cloned
        Assert.That(clone.Address, Is.Not.Null);
        Assert.That(clone.Address, Is.Not.SameAs(person.Address));
        Assert.That(clone.Address!.Street, Is.EqualTo("123 Main St"));
        Assert.That(clone.Address.City, Is.EqualTo("Seattle"));
        
        // List should be deep cloned
        Assert.That(clone.PhoneNumbers, Is.Not.SameAs(person.PhoneNumbers));
        Assert.That(clone.PhoneNumbers!.Count, Is.EqualTo(2));
    }

    #endregion
    
    [Test]
    [SourceGeneratorCompatible]
    public void PointRecord_Struct_Should_Clone()
    {
        // Arrange
        var point = new PointRecord(10.5, 20.5);
        
        // Act
        var clone = point.FastDeepClone();
        
        // Assert
        Assert.That(clone.X, Is.EqualTo(10.5));
        Assert.That(clone.Y, Is.EqualTo(20.5));
    }
    
    [Test]
    [SourceGeneratorCompatible]
    public void RecordStructWithCollection_Should_DeepClone()
    {
        // Arrange
        var record = new RecordStructWithCollection
        {
            Label = "Data",
            Values = [1, 2, 3, 4, 5]
        };
        
        // Act
        var clone = record.FastDeepClone();
        record.Values!.Add(6);
        
        // Assert - clone's list should be independent
        Assert.That(clone.Label, Is.EqualTo("Data"));
        Assert.That(clone.Values!.Count, Is.EqualTo(5));
        Assert.That(record.Values.Count, Is.EqualTo(6));
    }
    
    // #endregion

    #region Tests - Record with Dictionary
    
    [Test]
    [SourceGeneratorCompatible]
    public void RecordWithDictionary_Should_DeepClone()
    {
        // Arrange
        var record = new RecordWithDictionary
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
        var clone = record.FastDeepClone();
        record.Scores!["Alice"] = 50;
        record.Scores["Diana"] = 92;
        
        // Assert - clone's dictionary should be independent
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.EqualTo("Scores"));
        Assert.That(clone.Scores, Is.Not.SameAs(record.Scores));
        Assert.That(clone.Scores!.Count, Is.EqualTo(3));
        Assert.That(clone.Scores["Alice"], Is.EqualTo(100)); // Original value
        Assert.That(clone.Scores.ContainsKey("Diana"), Is.False);
    }

    #endregion

    #region Tests - Null Handling
    
    [Test]
    [SourceGeneratorCompatible]
    public void Null_Record_Should_Return_Null()
    {
        // Arrange
        SimpleRecord? record = null;
        
        // Act
        var clone = record.FastDeepClone();
        
        // Assert
        Assert.That(clone, Is.Null);
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Record_With_Null_Properties_Should_Clone()
    {
        // Arrange
        var record = new RecordWithCollection
        {
            Name = null,
            Tags = null,
            Count = 0
        };
        
        // Act
        var clone = record.FastDeepClone();
        
        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.Null);
        Assert.That(clone.Tags, Is.Null);
        Assert.That(clone.Count, Is.EqualTo(0));
    }

    #endregion
}

