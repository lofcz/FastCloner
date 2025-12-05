using System;

namespace FastCloner.SourceGenerator;

/// <summary>
/// Represents a type model for code generation.
/// CRITICAL: This is a record for proper equality comparison to enable incremental caching.
/// It stores NO Roslyn symbols (ISymbol, Syntax nodes) as they break caching.
/// </summary>
internal sealed record TypeModel(
    string Namespace,
    string Name,
    string FullyQualifiedName,
    bool IsStruct,
    bool IsSealed,
    bool HasClonableBaseClass,
    bool CanHaveCircularReferences,
    bool IsFastClonerAvailable,
    EquatableArray<MemberModel> Members,
    EquatableArray<string> TypeParameters,
    EquatableArray<string> TypeConstraints,
    EquatableArray<TypeModel> RelatedTypes, // Implicitly clonable types that we generate helpers for
    EquatableArray<MemberModel> NestedTypes, // Nested collection types that need helpers
    bool NullabilityEnabled,
    bool HasParameterlessConstructor = true, // Whether the type has a public parameterless constructor (defaults to true for safety)
    EquatableArray<string> CircularAnalysisLog = default) : IEquatable<TypeModel>;
