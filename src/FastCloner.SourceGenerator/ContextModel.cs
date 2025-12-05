namespace FastCloner.SourceGenerator;

internal record ContextModel(
    string Name,
    string Namespace,
    string FullyQualifiedName,
    EquatableArray<TypeModel> RegisteredTypes,
    bool IsFastClonerAvailable,
    bool HasNotNullIfNotNullAttribute
);
