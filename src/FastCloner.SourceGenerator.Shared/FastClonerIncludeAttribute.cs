using System;

namespace FastCloner.SourceGenerator.Shared;

/// <summary>
/// Specifies types that should be supported for generic cloning even if they are not detected at compile time.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class FastClonerIncludeAttribute : Attribute
{
    public Type[] Types { get; }

    /// <summary>
    /// Register types to be included in the source generator analysis for this generic type.
    /// </summary>
    /// <param name="types">The types to include.</param>
    public FastClonerIncludeAttribute(params Type[] types)
    {
        Types = types;
    }
}
