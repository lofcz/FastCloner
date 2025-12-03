using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace FastCloner.SourceGenerator;

internal static class GenericTypeAnalyzer
{
    public static GenericUsage? Analyze(
        INamedTypeSymbol targetSymbol,
        ITypeSymbol typeArg,
        Compilation compilation,
        bool nullability)
    {
        if (typeArg.TypeKind == TypeKind.TypeParameter)
            return null;

        var isSafe = TypeAnalyzer.IsSafeType(typeArg, compilation);
        var isClonable = TypeAnalyzer.HasClonableAttribute(typeArg);
        
        // Analyze for collection types (List, Dictionary, Array, etc.)
        var nestedTypes = new Dictionary<string, MemberModel>();
        NestedTypeCollector.Collect(typeArg, compilation, nullability, nestedTypes);
        
        MemberModel? collectionModel = null;
        var typeArgFQN = typeArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (nestedTypes.TryGetValue(typeArgFQN, out var model))
        {
            collectionModel = model;
        }

        // Analyze for implicit types (POCOs without attribute)
        var implicitTypes = new Dictionary<string, TypeModel>();
        var implicitCache = new Dictionary<ITypeSymbol, TypeModel?>(SymbolEqualityComparer.Default);
        var processingStack = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        void CollectImplicitRecursively(ITypeSymbol t)
        {
            // Skip safe types, they don't need implicit helpers
            if (TypeAnalyzer.IsSafeType(t, compilation))
                return;

            // Check if this type is a candidate for implicit cloning
            if (ImplicitTypeAnalyzer.TryAnalyze(t, compilation, nullability, implicitCache, processingStack, out var implicitModel))
            {
                if (implicitModel != null && !implicitTypes.ContainsKey(implicitModel.FullyQualifiedName))
                {
                    implicitTypes[implicitModel.FullyQualifiedName] = implicitModel;
                    // Dependencies are already collected in 'implicitModel.RelatedTypes', but we need to flatten them into our list
                    foreach (var rel in implicitModel.RelatedTypes)
                    {
                        if (!implicitTypes.ContainsKey(rel.FullyQualifiedName))
                            implicitTypes[rel.FullyQualifiedName] = rel;
                    }
                }
            }

            // Recurse into generics/arrays to find other candidates
            if (t is INamedTypeSymbol named && named.IsGenericType)
            {
                foreach (var arg in named.TypeArguments)
                    CollectImplicitRecursively(arg);
            }
            else if (t is IArrayTypeSymbol array)
            {
                CollectImplicitRecursively(array.ElementType);
            }
        }

        CollectImplicitRecursively(typeArg);

        // We capture if it's Safe, Clonable, a supported Collection/Dictionary, or has Implicit types
        if (isSafe || isClonable || collectionModel != null || implicitTypes.Count > 0)
        {
            string? extensionClassFQN = null;
            if (isClonable && typeArg is INamedTypeSymbol namedArg)
            {
                var ns = TypeAnalyzer.GetNamespace(namedArg);
                var name = namedArg.Name;
                var extName = $"{name}FastDeepCloneExtensions";
                extensionClassFQN = string.IsNullOrEmpty(ns) ? $"global::{extName}" : $"global::{ns}.{extName}";
            }

            return new GenericUsage(
                targetSymbol.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                typeArgFQN,
                extensionClassFQN,
                collectionModel,
                new EquatableArray<MemberModel>(nestedTypes.Values.ToArray()),
                new EquatableArray<TypeModel>(implicitTypes.Values.ToArray()),
                isSafe,
                isClonable
            );
        }

        return null;
    }
}
