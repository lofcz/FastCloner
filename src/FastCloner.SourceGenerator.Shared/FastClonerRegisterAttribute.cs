using System;

namespace FastCloner.SourceGenerator.Shared;

/// <summary>
/// Registers a type to be included in the source generated FastClonerContext.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class FastClonerRegisterAttribute : Attribute
{
    public Type[] TypesToRegister { get; }

    public FastClonerRegisterAttribute(params Type[] typesToRegister)
    {
        TypesToRegister = typesToRegister;
    }
}
