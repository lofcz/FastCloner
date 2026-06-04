using System;

namespace FastCloner.SourceGenerator.Shared;

/// <summary>
/// Marks a class or struct for source generator-based cloning.
/// When applied, the source generator will generate FastDeepClone() extension methods for this type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class FastClonerClonableAttribute : Attribute
{
    /// <summary>
    /// Gets or sets whether subtype dispatch should be generated for this type.
    /// When true, generated clone code dispatches by runtime type and clones known subtypes similarly to abstract roots.
    /// This is ignored for abstract types, which always have subtype dispatch.
    /// This is ignored for structs, which cannot have subtypes.
    /// This is ignored for sealed classes, which cannot be subclassed.
    /// </summary>
    /// <example>
    /// <code>
    /// [FastClonerClonable(IncludeSubtypes = true)]
    /// public class BaseType { }
    /// </code>
    /// </example>
    public bool IncludeSubtypes { get; set; }

    /// <summary>
    /// Initializes a new instance of the FastClonerClonableAttribute.
    /// </summary>
    public FastClonerClonableAttribute()
    {
    }
}

/// <summary>
/// Instructs FastCloner to trust the nullability annotations of reference types.
/// If a reference type member is not annotated as nullable (e.g. string instead of string?),
/// FastCloner will NOT generate a null check for it, assuming it will never be null.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class FastClonerTrustNullabilityAttribute : Attribute
{
}