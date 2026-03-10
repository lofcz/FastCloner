using FastCloner.SourceGenerator.Shared;
using System.Threading.Tasks;

namespace FastCloner.Tests;

[FastClonerClonable]
public class ComplexModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public UserInfo? Owner { get; set; }
    public List<UserInfo>? Contributors { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public List<DataItem>? Items { get; set; }
    public Settings? Settings { get; set; }
}

[FastClonerClonable]
public class UserInfo
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public ContactInfo? Contact { get; set; }
}

[FastClonerClonable]
public class ContactInfo
{
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

[FastClonerClonable]
public class DataItem
{
    public string ItemId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Value { get; set; }
    public List<string>? Tags { get; set; }
    public List<SubItem>? SubItems { get; set; }
    public Dictionary<string, string>? Properties { get; set; }
}

[FastClonerClonable]
public class SubItem
{
    public string SubId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

[FastClonerClonable]
public class Settings
{
    public bool IsEnabled { get; set; }
    public int MaxItems { get; set; }
    public TimeSpan Timeout { get; set; }
    public List<string>? AllowedDomains { get; set; }
    public Dictionary<string, int>? Limits { get; set; }
    public AdvancedSettings? Advanced { get; set; }
}

[FastClonerClonable]
public class AdvancedSettings
{
    public int CacheSize { get; set; }
    public bool UseCompression { get; set; }
    public string CompressionLevel { get; set; } = string.Empty;
    public List<string>? Features { get; set; }
}

public enum UserRole
{
    Guest = 0,
    User = 1,
    Admin = 2,
    Owner = 3,
}
public class BenchmarkModelTests
{
    [Test]
    public async Task ComplexModel_DeepClone_ShouldCloneAllProperties()
    {
        // Arrange
        ComplexModel original = CreateComplexModel();

        // Act
        ComplexModel clone = original.FastDeepClone();

        // Assert - verify it's a different instance
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone).IsNotSameReferenceAs(original);

        // Verify top-level properties
        await Assert.That(clone.Id).IsEqualTo(original.Id);
        await Assert.That(clone.Name).IsEqualTo(original.Name);
        await Assert.That(clone.Version).IsEqualTo(original.Version);
        await Assert.That(clone.CreatedAt).IsEqualTo(original.CreatedAt);
        await Assert.That(clone.UpdatedAt).IsEqualTo(original.UpdatedAt);

        // Verify Owner is deep cloned
        await Assert.That(clone.Owner).IsNotNull();
        await Assert.That(clone.Owner).IsNotSameReferenceAs(original.Owner);
        await Assert.That(clone.Owner!.UserId).IsEqualTo(original.Owner!.UserId);
        await Assert.That(clone.Owner.Contact).IsNotSameReferenceAs(original.Owner.Contact);

        // Verify Contributors is deep cloned
        await Assert.That(clone.Contributors).IsNotNull();
        await Assert.That(clone.Contributors).IsNotSameReferenceAs(original.Contributors);
        await Assert.That(clone.Contributors!.Count).IsEqualTo(original.Contributors!.Count);
        await Assert.That(clone.Contributors[0]).IsNotSameReferenceAs(original.Contributors[0]);

        // Verify Metadata is deep cloned
        await Assert.That(clone.Metadata).IsNotNull();
        await Assert.That(clone.Metadata).IsNotSameReferenceAs(original.Metadata);

        // Verify Items is deep cloned
        await Assert.That(clone.Items).IsNotNull();
        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items![0]).IsNotSameReferenceAs(original.Items![0]);
        await Assert.That(clone.Items[0].SubItems).IsNotSameReferenceAs(original.Items[0].SubItems);

        // Verify Settings is deep cloned
        await Assert.That(clone.Settings).IsNotNull();
        await Assert.That(clone.Settings).IsNotSameReferenceAs(original.Settings);
        await Assert.That(clone.Settings!.Advanced).IsNotSameReferenceAs(original.Settings!.Advanced);
    }

    [Test]
    public async Task ComplexModel_ModifyClone_ShouldNotAffectOriginal()
    {
        // Arrange
        ComplexModel original = CreateComplexModel();
        ComplexModel clone = original.FastDeepClone();

        // Act - modify the clone
        clone!.Name = "Modified";
        clone.Owner!.UserName = "Modified User";
        clone.Contributors![0].Email = "modified@example.com";
        clone.Items![0].Title = "Modified Item";
        clone.Settings!.Advanced!.CacheSize = 9999;

        // Assert - original should be unchanged
        await Assert.That(original.Name).IsEqualTo("Test Model");
        await Assert.That(original.Owner!.UserName).IsEqualTo("John Doe");
        await Assert.That(original.Contributors![0].Email).IsEqualTo("contributor1@example.com");
        await Assert.That(original.Items![0].Title).IsEqualTo("Item 1");
        await Assert.That(original.Settings!.Advanced!.CacheSize).IsEqualTo(1024);
    }

    private static ComplexModel CreateComplexModel()
    {
        return new ComplexModel
        {
            Id = "model-001",
            Name = "Test Model",
            Version = 1,
            CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 6, 15, 8, 30, 0, DateTimeKind.Utc),
            Owner = new UserInfo
            {
                UserId = "user-001",
                UserName = "John Doe",
                Email = "john@example.com",
                Role = UserRole.Owner,
                Contact = new ContactInfo
                {
                    Phone = "+1-555-0100",
                    Address = "123 Main St",
                    City = "New York",
                    Country = "USA"
                }
            },
            Contributors =
            [
                new UserInfo
                {
                    UserId = "user-002",
                    UserName = "Jane Smith",
                    Email = "contributor1@example.com",
                    Role = UserRole.Admin,
                    Contact = new ContactInfo
                    {
                        Phone = "+1-555-0101",
                        Address = "456 Oak Ave",
                        City = "Los Angeles",
                        Country = "USA"
                    }
                },
                new UserInfo
                {
                    UserId = "user-003",
                    UserName = "Bob Wilson",
                    Email = "contributor2@example.com",
                    Role = UserRole.User
                }
            ],
            Metadata = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" },
                { "key3", "value3" }
            },
            Items =
            [
                new DataItem
                {
                    ItemId = "item-001",
                    Title = "Item 1",
                    Description = "First item description",
                    Value = 99.99,
                    Tags = ["tag1", "tag2", "tag3"],
                    SubItems =
                    [
                        new SubItem { SubId = "sub-001", Label = "Sub 1", Quantity = 10, Price = 5.99m },
                        new SubItem { SubId = "sub-002", Label = "Sub 2", Quantity = 20, Price = 3.49m }
                    ],
                    Properties = new Dictionary<string, string>
                    {
                        { "prop1", "val1" },
                        { "prop2", "val2" }
                    }
                },
                new DataItem
                {
                    ItemId = "item-002",
                    Title = "Item 2",
                    Description = "Second item description",
                    Value = 149.99,
                    Tags = ["tag4", "tag5"],
                    SubItems =
                    [
                        new SubItem { SubId = "sub-003", Label = "Sub 3", Quantity = 5, Price = 12.99m }
                    ]
                }
            ],
            Settings = new Settings
            {
                IsEnabled = true,
                MaxItems = 100,
                Timeout = TimeSpan.FromMinutes(5),
                AllowedDomains = ["example.com", "test.com"],
                Limits = new Dictionary<string, int>
                {
                    { "daily", 1000 },
                    { "monthly", 10000 }
                },
                Advanced = new AdvancedSettings
                {
                    CacheSize = 1024,
                    UseCompression = true,
                    CompressionLevel = "high",
                    Features = ["feature1", "feature2", "feature3"]
                }
            }
        };
    }
}