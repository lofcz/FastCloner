using System;

namespace FastCloner.SourceGenerator.Shared;

/// <summary>
/// When applied to an abstract class with [FastClonerClonable], disables automatic discovery
/// of derived types in the compilation. Only types explicitly registered via [FastClonerInclude]
/// will be used for the type dispatcher.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
internal class FastClonerDisableAutoDiscoveryAttribute : Attribute
{
}

