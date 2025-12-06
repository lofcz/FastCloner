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