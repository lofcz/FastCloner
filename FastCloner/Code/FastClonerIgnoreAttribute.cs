namespace FastCloner.Code;

/// <summary>
/// Marks given field/property/event as ignored, effectively assigning a default value when cloning such entity.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Event)]
public class FastClonerIgnoreAttribute(bool ignored = true) : Attribute
{
    /// <summary>
    /// Gets whether the member should be ignored during cloning.
    /// </summary>
    public bool Ignored { get; } = ignored;
}