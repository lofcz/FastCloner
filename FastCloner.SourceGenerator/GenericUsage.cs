using System;

namespace FastCloner.SourceGenerator;

internal readonly record struct GenericUsage(
    string GenericTypeMetadataName,
    string ArgumentTypeMetadataName,
    string? ExtensionClassFQN,
    MemberModel? CollectionModel,
    EquatableArray<MemberModel> NestedHelpers,
    EquatableArray<TypeModel> ImplicitTypes,
    bool IsSafe,
    bool IsClonable
) : IEquatable<GenericUsage>;
