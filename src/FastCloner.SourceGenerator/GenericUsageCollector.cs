using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastCloner.SourceGenerator;

internal static class GenericUsageCollector
{
    public static bool IsCandidate(SyntaxNode node, CancellationToken cancellationToken)
    {
        return node is GenericNameSyntax;
    }

    public static EquatableArray<GenericUsage> Collect(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var node = (GenericNameSyntax)context.Node;
        var symbol = context.SemanticModel.GetSymbolInfo(node, cancellationToken).Symbol as INamedTypeSymbol;

        if (symbol == null || !symbol.IsGenericType)
            return EquatableArray<GenericUsage>.Empty;

        // Check if the generic type definition has the FastClonerClonable attribute
        // We check the OriginalDefinition because 'symbol' here is the concrete type (e.g. MyGeneric<int>)
        if (!TypeAnalyzer.HasClonableAttribute(symbol.OriginalDefinition))
            return EquatableArray<GenericUsage>.Empty;

        var usages = new List<GenericUsage>();
        var compilation = context.SemanticModel.Compilation;

        foreach (var typeArg in symbol.TypeArguments)
        {
            // We need to capture the concrete type used as argument.
            // If typeArg is still a type parameter (e.g. inside a generic method), we can't optimize it.

            if (typeArg.TypeKind == TypeKind.TypeParameter)
                continue;

            var isSafe = TypeAnalyzer.IsSafeType(typeArg, compilation);
            var isClonable = TypeAnalyzer.HasClonableAttribute(typeArg);
            
            // Analyze for collection types (List, Dictionary, Array, etc.)
            var nestedTypes = new Dictionary<string, MemberModel>();
            var nullability = context.SemanticModel.GetNullableContext(node.SpanStart).HasFlag(NullableContext.Enabled);
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

                usages.Add(new GenericUsage(
                    symbol.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    typeArgFQN,
                    extensionClassFQN,
                    collectionModel,
                    new EquatableArray<MemberModel>(nestedTypes.Values.ToArray()),
                    new EquatableArray<TypeModel>(implicitTypes.Values.ToArray()),
                    isSafe,
                    isClonable
                ));
            }
        }

        return new EquatableArray<GenericUsage>(usages.ToArray());
    }
}
