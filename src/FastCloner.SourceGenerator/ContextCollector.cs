using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace FastCloner.SourceGenerator;

internal static class ContextCollector
{
    public static Result<ContextModel> Collect(GeneratorAttributeSyntaxContext context, System.Threading.CancellationToken cancellationToken)
    {
        var symbol = context.TargetSymbol as INamedTypeSymbol;
        if (symbol == null)
        {
            return Result<ContextModel>.Error(Diagnostic.Create(
                new DiagnosticDescriptor("FCG005", "Invalid Symbol", "Symbol is not a named type", "FastCloner", DiagnosticSeverity.Error, true),
                context.TargetNode.GetLocation()));
        }

        // Verify inheritance from FastClonerContext
        var baseType = symbol.BaseType;
        bool inheritsFromContext = false;
        while (baseType != null)
        {
            if (baseType.Name == "FastClonerContext" && 
                baseType.ContainingNamespace.ToDisplayString() == "FastCloner.SourceGenerator.Shared")
            {
                inheritsFromContext = true;
                break;
            }
            baseType = baseType.BaseType;
        }

        if (!inheritsFromContext)
        {
             return Result<ContextModel>.Error(Diagnostic.Create(
                new DiagnosticDescriptor("FCG006", "Invalid Base Class", "Class must inherit from FastClonerContext", "FastCloner", DiagnosticSeverity.Error, true),
                symbol.Locations.FirstOrDefault() ?? Location.None));
        }

        var registeredTypes = new List<TypeModel>();
        var compilation = context.SemanticModel.Compilation;
        var nullabilityEnabled = context.SemanticModel.GetNullableContext(context.TargetNode.SpanStart)
                        .HasFlag(NullableContext.Enabled);
        
        // Cache to prevent re-analyzing same types in recursion (shared across this context analysis)
        var implicitCache = new Dictionary<ITypeSymbol, TypeModel?>(SymbolEqualityComparer.Default);
        var processingStack = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == "FastCloner.SourceGenerator.Shared.FastClonerRegisterAttribute")
            {
                if (attr.ConstructorArguments.Length > 0)
                {
                    var typesArg = attr.ConstructorArguments[0];
                    var typesToProcess = new List<ITypeSymbol>();

                    if (typesArg.Kind == TypedConstantKind.Array)
                    {
                        foreach (var typedConst in typesArg.Values)
                        {
                            if (typedConst.Value is ITypeSymbol type)
                            {
                                typesToProcess.Add(type);
                            }
                        }
                    }
                    else if (typesArg.Value is ITypeSymbol type)
                    {
                        typesToProcess.Add(type);
                    }

                    foreach (var typeToRegister in typesToProcess)
                    {
                        if (ImplicitTypeAnalyzer.TryAnalyze(typeToRegister, compilation, nullabilityEnabled, implicitCache, processingStack, out var model))
                        {
                            if (model != null)
                            {
                                registeredTypes.Add(model);
                                // Also add related types!
                                foreach (var rel in model.RelatedTypes)
                                {
                                    registeredTypes.Add(rel);
                                }
                            }
                        }
                    }
                }
            }
        }

        // Deduplicate
        var uniqueTypes = registeredTypes.GroupBy(t => t.FullyQualifiedName).Select(g => g.First()).ToArray();

        // Check if FastCloner library is available (same check as TypeModelFactory)
        var isFastClonerAvailable = compilation.GetTypeByMetadataName("FastCloner.FastCloner") != null;
        
        // Check if type has SimulateNoRuntime attribute (for testing)
        var hasSimulateNoRuntime = symbol.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == "FastCloner.SourceGenerator.Shared.FastClonerSimulateNoRuntimeAttribute");
        
        if (hasSimulateNoRuntime)
        {
            isFastClonerAvailable = false;
        }

        // Check if NotNullIfNotNullAttribute exists in System.Diagnostics.CodeAnalysis
        // Generate attributes only if the built-in attribute is available
        var hasNotNullIfNotNullAttribute = compilation.GetTypeByMetadataName("System.Diagnostics.CodeAnalysis.NotNullIfNotNullAttribute") != null;

        return Result<ContextModel>.Success(new ContextModel(
            symbol.Name,
            TypeAnalyzer.GetNamespace(symbol),
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            new EquatableArray<TypeModel>(uniqueTypes),
            isFastClonerAvailable,
            hasNotNullIfNotNullAttribute
        ));
    }
}
