using System;

namespace FastCloner.SourceGenerator.Shared;

/// <summary>
/// Internal attribute for testing purposes only.
/// When applied to a type with [FastClonerClonable], the source generator will behave as if
/// the FastCloner runtime library is not available, allowing you to test diagnostic errors.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
internal sealed class FastClonerSimulateNoRuntimeAttribute : Attribute
{
}
