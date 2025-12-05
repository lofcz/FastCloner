using System;
using System.Diagnostics.CodeAnalysis;

namespace FastCloner.SourceGenerator.Shared;

/// <summary>
/// Base class for FastCloner contexts.
/// Inherit from this class and annotate with [FastClonerRegister] to generate a cloning context.
/// </summary>
public abstract class FastClonerContext
{
    /// <summary>
    /// Clones the object using the generated dispatch logic or falls back to reflection.
    /// </summary>
    /// <param name="input">The object to clone.</param>
    /// <returns>The cloned object.</returns>
    public abstract object Clone(object input);

    /// <summary>
    /// Checks if the type is handled by this context (i.e. has a source generated clone method).
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is handled, false otherwise.</returns>
    public abstract bool IsHandled(Type type);

    /// <summary>
    /// Tries to clone the object using the source generated clone method.
    /// Returns false if the type is not handled by this context.
    /// </summary>
    /// <param name="input">The object to clone.</param>
    /// <param name="clone">The cloned object if successful, null otherwise.</param>
    /// <returns>True if the object was cloned, false otherwise.</returns>
    public abstract bool TryClone(object input, [NotNullWhen(true)] out object? clone);
}
