using System;

namespace FastCloner.SourceGenerator.Shared;

/// <summary>
/// Controls identity preservation behavior during cloning.
/// <br/><br/>
/// Identity preservation ensures that if the same object instance appears multiple times
/// in the source graph (e.g., two properties pointing to the same object, or the same
/// object appearing twice in a collection), the cloned graph maintains this relationship
/// (both cloned references point to the same cloned instance).
/// <br/><br/>
/// By default, identity preservation is disabled for performance. Circular reference
/// detection is always enabled.
/// <br/><br/>
/// This attribute can be applied to:<br/>
/// - Classes/structs: Controls identity preservation for the entire type's subgraph<br/>
/// - Properties/fields: Controls identity preservation for that specific member's subgraph
/// <br/><br/>
/// Member-level attributes override type-level attributes.
/// </summary>
/// <example>
/// <code>
/// // Enable identity preservation (default when attribute is present)
/// [FastClonerPreserveIdentity]
/// public class MyClass { }
/// 
/// // Explicitly enable
/// [FastClonerPreserveIdentity(true)]
/// public class MyClass { }
/// 
/// // Disable identity preservation for a member
/// [FastClonerPreserveIdentity(false)]
/// public List&lt;Item&gt; Items { get; set; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class FastClonerPreserveIdentityAttribute : Attribute
{
    /// <summary>
    /// Gets or sets whether identity preservation is enabled.
    /// Default is true when the attribute is applied.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Enables identity preservation for this type or member's subgraph.
    /// </summary>
    public FastClonerPreserveIdentityAttribute()
    {
    }

    /// <summary>
    /// Controls identity preservation for this type or member's subgraph.
    /// </summary>
    /// <param name="enabled">True to enable identity preservation, false to disable.</param>
    public FastClonerPreserveIdentityAttribute(bool enabled)
    {
        Enabled = enabled;
    }
}
