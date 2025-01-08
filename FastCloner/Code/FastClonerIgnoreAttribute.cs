namespace FastCloner.Code;

/// <summary>
/// Marks given field / property as ignored, effectively assigning a default value when cloning such entity.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class FastClonerIgnoreAttribute(bool ignored = true) : Attribute
{
    /// <summary>
    /// Gets whether the member should be ignored during cloning.
    /// </summary>
    public bool Ignored { get; } = ignored;
}