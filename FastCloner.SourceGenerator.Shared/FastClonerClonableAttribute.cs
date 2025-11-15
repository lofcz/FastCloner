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