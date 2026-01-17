namespace FastCloner.Benchmark;

/// <summary>
/// Helper class to create sample complex models for benchmarking.
/// </summary>
public static class TestDataGenerator
{
    public static ComplexModel CreateSampleModel()
    {
        return new ComplexModel
        {
            Id = "model-12345",
            Name = "Sample Complex Model",
            Version = 1,
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Owner = new UserInfo
            {
                UserId = "user-001",
                UserName = "john.doe",
                Email = "john.doe@example.com",
                Role = UserRole.Owner,
                Contact = new ContactInfo
                {
                    Phone = "+1-555-1234",
                    Address = "123 Main St",
                    City = "San Francisco",
                    Country = "USA",
                },
            },
            Contributors =
            [
                new()
                {
                    UserId = "user-002",
                    UserName = "jane.smith",
                    Email = "jane.smith@example.com",
                    Role = UserRole.Admin,
                    Contact = new ContactInfo
                    {
                        Phone = "+1-555-5678",
                        Address = "456 Oak Ave",
                        City = "New York",
                        Country = "USA",
                    },
                },
                new()
                {
                    UserId = "user-003",
                    UserName = "bob.wilson",
                    Email = "bob.wilson@example.com",
                    Role = UserRole.User,
                    Contact = new ContactInfo
                    {
                        Phone = "+1-555-9012",
                        Address = "789 Pine Rd",
                        City = "Seattle",
                        Country = "USA",
                    },
                },
                new()
                {
                    UserId = "user-004",
                    UserName = "alice.johnson",
                    Email = "alice.johnson@example.com",
                    Role = UserRole.User,
                    Contact = new ContactInfo
                    {
                        Phone = "+1-555-3456",
                        Address = "321 Elm St",
                        City = "Boston",
                        Country = "USA",
                    },
                },
                new()
                {
                    UserId = "user-005",
                    UserName = "charlie.brown",
                    Email = "charlie.brown@example.com",
                    Role = UserRole.User,
                    Contact = new ContactInfo
                    {
                        Phone = "+1-555-7890",
                        Address = "654 Maple Dr",
                        City = "Chicago",
                        Country = "USA",
                    },
                },
            ],
            Metadata = new Dictionary<string, string>
            {
                { "category", "test" },
                { "priority", "high" },
                { "status", "active" },
                { "environment", "production" },
                { "version", "2.1.0" },
                { "region", "us-west-2" },
                { "tier", "premium" },
            },
            Items =
            [
                new()
                {
                    ItemId = "item-001",
                    Title = "First Item",
                    Description = "This is the first item with a detailed description",
                    Value = 99.99,
                    Tags = ["tag1", "tag2", "tag3", "important", "featured"],
                    SubItems =
                    [
                        new() { SubId = "sub-001", Label = "Sub Item 1", Quantity = 10, Price = 19.99m },
                        new() { SubId = "sub-002", Label = "Sub Item 2", Quantity = 5, Price = 29.99m },
                        new() { SubId = "sub-003", Label = "Sub Item 3", Quantity = 8, Price = 24.99m },
                    ],
                    Properties = new Dictionary<string, string>
                    {
                        { "color", "blue" },
                        { "size", "large" },
                        { "weight", "1.5" },
                        { "material", "steel" },
                        { "manufacturer", "ACME Corp" },
                    },
                },
                new()
                {
                    ItemId = "item-002",
                    Title = "Second Item",
                    Description = "This is the second item with another detailed description",
                    Value = 149.99,
                    Tags = ["tag4", "tag5", "premium"],
                    SubItems =
                    [
                        new() { SubId = "sub-004", Label = "Sub Item 4", Quantity = 15, Price = 39.99m },
                        new() { SubId = "sub-005", Label = "Sub Item 5", Quantity = 12, Price = 44.99m },
                    ],
                    Properties = new Dictionary<string, string>
                    {
                        { "color", "red" },
                        { "size", "medium" },
                        { "warranty", "2 years" },
                    },
                },
                new()
                {
                    ItemId = "item-003",
                    Title = "Third Item",
                    Description = "This is the third item for additional testing data",
                    Value = 79.99,
                    Tags = ["tag6", "tag7", "tag8"],
                    SubItems =
                    [
                        new() { SubId = "sub-006", Label = "Sub Item 6", Quantity = 20, Price = 9.99m },
                    ],
                    Properties = new Dictionary<string, string>
                    {
                        { "color", "green" },
                        { "size", "small" },
                    },
                },
                new()
                {
                    ItemId = "item-004",
                    Title = "Fourth Item",
                    Description = "This is the fourth item with extensive details and properties",
                    Value = 199.99,
                    Tags = ["tag9", "tag10", "exclusive", "limited"],
                    SubItems =
                    [
                        new() { SubId = "sub-007", Label = "Sub Item 7", Quantity = 3, Price = 59.99m },
                        new() { SubId = "sub-008", Label = "Sub Item 8", Quantity = 7, Price = 34.99m },
                        new() { SubId = "sub-009", Label = "Sub Item 9", Quantity = 11, Price = 29.99m },
                    ],
                    Properties = new Dictionary<string, string>
                    {
                        { "color", "black" },
                        { "size", "extra-large" },
                        { "weight", "3.2" },
                        { "material", "aluminum" },
                    },
                },
                new()
                {
                    ItemId = "item-005",
                    Title = "Fifth Item",
                    Description = "This is the fifth item to increase data volume",
                    Value = 59.99,
                    Tags = ["tag11", "tag12"],
                    SubItems =
                    [
                        new() { SubId = "sub-010", Label = "Sub Item 10", Quantity = 25, Price = 14.99m },
                        new() { SubId = "sub-011", Label = "Sub Item 11", Quantity = 18, Price = 19.99m },
                    ],
                    Properties = new Dictionary<string, string>
                    {
                        { "color", "white" },
                        { "size", "medium" },
                        { "finish", "matte" },
                    },
                },
            ],
            Settings = new Settings
            {
                IsEnabled = true,
                MaxItems = 100,
                Timeout = TimeSpan.FromSeconds(30),
                AllowedDomains =
                [
                    "example.com",
                    "test.com",
                    "sample.org",
                    "demo.net",
                    "staging.dev",
                    "production.io",
                ],
                Limits = new Dictionary<string, int>
                {
                    { "maxUsers", 1000 },
                    { "maxStorage", 10240 },
                    { "maxConnections", 500 },
                    { "maxRequests", 10000 },
                    { "maxBandwidth", 1000000 },
                },
                Advanced = new AdvancedSettings
                {
                    CacheSize = 256,
                    UseCompression = true,
                    CompressionLevel = "high",
                    Features =
                    [
                        "feature1",
                        "feature2",
                        "feature3",
                        "feature4",
                        "feature5",
                        "authentication",
                        "logging",
                        "monitoring",
                    ],
                },
            },
        };
    }
}
