using System;

namespace FastCloner.Tests;

/// <summary>
/// Marks a test as compatible with the source generator implementation.
/// Tests marked with this attribute can be run against both the reflection-based
/// FastCloner and the source generator-based FastCloner.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class SourceGeneratorCompatibleAttribute : Attribute
{
}

