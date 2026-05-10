namespace FastCloner.Code;

[Flags]
public enum FastClonerMemberVisibility
{
    /// <summary>
    /// No members are eligible.
    /// </summary>
    None = 0,

    /// <summary>
    /// Public members.
    /// </summary>
    Public = 1 << 0,

    /// <summary>
    /// Pure-internal members. Combines with <see cref="Protected"/> to also include
    /// <c>protected internal</c> and <c>private protected</c>.
    /// </summary>
    Internal = 1 << 1,

    /// <summary>
    /// Protected members. Combines with <see cref="Internal"/> to also include
    /// <c>protected internal</c> and <c>private protected</c>.
    /// </summary>
    Protected = 1 << 2,

    /// <summary>
    /// Private members.
    /// </summary>
    Private = 1 << 3,

    /// <summary>
    /// All non-public visibilities (<see cref="Internal"/> | <see cref="Protected"/> | <see cref="Private"/>).
    /// </summary>
    NonPublic = Internal | Protected | Private,

    /// <summary>
    /// Shorthand for <see cref="Public"/> | <see cref="Internal"/>.
    /// </summary>
    PublicOrInternal = Public | Internal,

    /// <summary>
    /// All visibilities. This is the implicit default when no <see cref="FastClonerVisibilityAttribute"/> is applied.
    /// </summary>
    All = Public | NonPublic
}

/// <summary>
/// Controls which member visibilities are eligible for cloning on the target type.
/// <br/>
/// When this attribute is not present, the policy is <see cref="FastClonerMemberVisibility.All"/>:
/// every member is cloned regardless of visibility.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class FastClonerVisibilityAttribute : Attribute
{
    /// <summary>
    /// The set of visibilities whose members participate in cloning.
    /// </summary>
    public FastClonerMemberVisibility Visibility { get; }

    /// <summary>
    /// Apply a visibility policy to the target type.
    /// </summary>
    /// <param name="visibility">The set of member visibilities to include during cloning.</param>
    public FastClonerVisibilityAttribute(FastClonerMemberVisibility visibility)
    {
        Visibility = visibility;
    }
}
