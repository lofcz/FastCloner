using FastCloner.SourceGenerator.Shared;
using IDeepCloneable;
using Riok.Mapperly.Abstractions;

namespace FastCloner.Benchmark;

/// <summary>
/// Complex model for benchmarking deep cloning operations.
/// </summary>
[DeepCloneable]
[FastClonerClonable]
[FastClonerTrustNullability]
public partial class ComplexModel
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

public class UserInfo
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public ContactInfo? Contact { get; set; }
}

public class ContactInfo
{
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

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

public class SubItem
{
    public string SubId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class Settings
{
    public bool IsEnabled { get; set; }
    public int MaxItems { get; set; }
    public TimeSpan Timeout { get; set; }
    public List<string>? AllowedDomains { get; set; }
    public Dictionary<string, int>? Limits { get; set; }
    public AdvancedSettings? Advanced { get; set; }
}

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

/// <summary>
/// Mapperly mapper for ComplexModel (compile-time code generation).
/// </summary>
[Mapper(UseDeepCloning = true)]
public static partial class MapperlyCloner
{
    public static partial ComplexModel Clone(ComplexModel source);
}
