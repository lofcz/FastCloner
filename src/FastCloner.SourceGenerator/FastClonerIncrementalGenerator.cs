using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace FastCloner.SourceGenerator;

[Generator(LanguageNames.CSharp)]
public class FastClonerIncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // CRITICAL FOR IDE PERFORMANCE:
        // 1. Use ForAttributeWithMetadataName() - 99% more efficient than CreateSyntaxProvider
        // 2. Return a data model (TypeModel record), NEVER return ISymbol or *Syntax instances
        // 3. Do NOT combine with CompilationProvider - it breaks caching
        // 4. Use EquatableArray for collections, not ImmutableArray
        
        var pipeline = context.SyntaxProvider.ForAttributeWithMetadataName<Result<TypeModel>>(
            fullyQualifiedMetadataName: "FastCloner.SourceGenerator.Shared.FastClonerClonableAttribute",
            predicate: static (node, cancellationToken) => 
                node is ClassDeclarationSyntax || node is StructDeclarationSyntax || node is RecordDeclarationSyntax,
            transform: static (ctx, cancellationToken) =>
            {
                var symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.TargetNode);
                if (symbol is INamedTypeSymbol namedTypeSymbol)
                {
                    var nullabilityEnabled = ctx.SemanticModel.GetNullableContext(ctx.TargetNode.SpanStart)
                        .HasFlag(NullableContext.Enabled);
                    
                    var compilation = ctx.SemanticModel.Compilation;
                    
                    return TypeModelFactory.TryCreate(namedTypeSymbol, nullabilityEnabled, compilation, out var model, out var error) ? 
                        Result<TypeModel>.Success(model!) : 
                        Result<TypeModel>.Error(error!);
                }
                
                return Result<TypeModel>.Error(
                    Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "FCG003",
                            "Failed to get type symbol",
                            "Could not get type symbol for node",
                            "FastCloner",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true),
                        ctx.TargetNode.GetLocation()));
            });

        // Secondary pipeline: Collect usages of generic types to optimize dispatch
        var explicitUsages = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: GenericUsageCollector.IsCandidate,
            transform: GenericUsageCollector.Collect)
            .Where(x => x.Count > 0);

        var includedUsages = context.SyntaxProvider.ForAttributeWithMetadataName<EquatableArray<GenericUsage>>(
            fullyQualifiedMetadataName: "FastCloner.SourceGenerator.Shared.FastClonerIncludeAttribute",
            predicate: static (node, _) => node is ClassDeclarationSyntax || node is StructDeclarationSyntax,
            transform: IncludeAttributeCollector.Collect)
            .Where(x => x.Count > 0);

        var usagePipeline = explicitUsages.Collect().Combine(includedUsages.Collect())
            .Select(static (pair, _) =>
            {
                var (explicitList, includedList) = pair;
                var list = new System.Collections.Generic.List<GenericUsage>();
                
                foreach (var array in explicitList) 
                    list.AddRange(array);
                foreach (var array in includedList) 
                    list.AddRange(array);

                return new EquatableArray<GenericUsage>(list.Distinct().ToArray());
            });

        // Combine type models with collected usages
        var combinedPipeline = pipeline.Combine(usagePipeline);

        // OPTIMAL PERFORMANCE: No Compilation combine!
        // All type analysis is pre-computed in TypeModel during the transform step.
        // This ensures the generator only re-runs when decorated types actually change,
        // not on every keypress.
        context.RegisterSourceOutput(combinedPipeline, static (ctx, source) =>
        {
            var (result, usages) = source;
            
            result.Handle(
                model =>
                {
                    try
                    {
                        // Check for silent failure cases where FastCloner runtime is missing
                        // We only warn here because generating broken code is sometimes better than nothing (e.g. partial clone),
                        // but ideally the user should install FastCloner or fix the type.
                        if (!model.IsFastClonerAvailable)
                        {
                            bool hasInitOnlyWithCycles = model.CanHaveCircularReferences && model.Members.Any(m => m.IsInitOnly);
                            bool structWithReadonlyRefs = model.IsStruct && model.Members.Any(m => !m.IsValueType && m.IsReadOnly);
                            
                            if (hasInitOnlyWithCycles)
                            {
                                ctx.ReportDiagnostic(Diagnostic.Create(
                                    new DiagnosticDescriptor(
                                        "FCG005",
                                        "Init-only properties skipped",
                                        "Type '{0}' has init-only properties and requires circular reference tracking, but FastCloner runtime is not available. Init-only properties will not be cloned.",
                                        "FastCloner",
                                        DiagnosticSeverity.Warning,
                                        isEnabledByDefault: true),
                                    Location.None,
                                    model.Name));
                            }
                            
                            if (structWithReadonlyRefs)
                            {
                                ctx.ReportDiagnostic(Diagnostic.Create(
                                    new DiagnosticDescriptor(
                                        "FCG006",
                                        "Readonly reference fields in struct skipped",
                                        "Struct '{0}' has readonly reference fields, but FastCloner runtime is not available. These fields will be shallow-copied.",
                                        "FastCloner",
                                        DiagnosticSeverity.Warning,
                                        isEnabledByDefault: true),
                                    Location.None,
                                    model.Name));
                            }
                        }

                        var generator = new CloneCodeGenerator(model, usages);
                        var generatedSource = generator.Generate();
                        
                        // Use FullyQualifiedName to avoid collisions when same class name exists in different namespaces
                        var safeName = model.FullyQualifiedName
                            .Replace("global::", "")
                            .Replace(".", "_")
                            .Replace("<", "_")
                            .Replace(">", "_")
                            .Replace(" ", "")
                            .Replace(",", "_")
                            .Replace(":", "_");

                        ctx.AddSource($"{safeName}_FastDeepClone.g.cs", SourceText.From(generatedSource, Encoding.UTF8));
                    }
                    catch (System.Exception ex)
                    {
                        var location = Location.None;
                        ctx.ReportDiagnostic(
                            Diagnostic.Create(
                                new DiagnosticDescriptor(
                                    "FCG001",
                                    "Generator Error",
                                    "Error generating clone code: {0}",
                                    "FastCloner",
                                    DiagnosticSeverity.Error,
                                    isEnabledByDefault: true),
                                location,
                                ex.ToString()));
                    }
                },
                error =>
                {
                    ctx.ReportDiagnostic(error);
                });
        });

        // FastClonerContext Pipeline
        var contextPipeline = context.SyntaxProvider.ForAttributeWithMetadataName<Result<ContextModel>>(
            fullyQualifiedMetadataName: "FastCloner.SourceGenerator.Shared.FastClonerRegisterAttribute",
            predicate: static (node, _) => node is ClassDeclarationSyntax,
            transform: ContextCollector.Collect);

        // Deduplicate pipeline results
        var dedupedContextPipeline = contextPipeline.Collect().SelectMany((results, _) => 
        {
             // Group successes by FQN to remove duplicates (caused by partial classes with attributes)
             var uniqueSuccesses = results
                 .Where(r => r.IsSuccess && r.Value != null)
                 .GroupBy(r => r.Value!.FullyQualifiedName)
                 .Select(g => g.First());

             var errors = results.Where(r => !r.IsSuccess);
             
             return uniqueSuccesses.Concat(errors);
        });

        context.RegisterSourceOutput(dedupedContextPipeline, static (ctx, result) =>
        {
            result.Handle(
                model =>
                {
                    try
                    {
                        var generator = new ContextCodeGenerator(model);
                        var source = generator.Generate();
                        
                        var safeName = model.FullyQualifiedName
                            .Replace("global::", "")
                            .Replace(".", "_")
                            .Replace("<", "_")
                            .Replace(">", "_")
                            .Replace(" ", "")
                            .Replace(",", "_")
                            .Replace(":", "_");

                        ctx.AddSource($"{safeName}_FastClonerContext.g.cs", SourceText.From(source, Encoding.UTF8));
                    }
                    catch (System.Exception ex)
                    {
                        var location = Location.None;
                        ctx.ReportDiagnostic(
                            Diagnostic.Create(
                                new DiagnosticDescriptor(
                                    "FCG001",
                                    "Generator Error",
                                    "Error generating clone code: {0}",
                                    "FastCloner",
                                    DiagnosticSeverity.Error,
                                    isEnabledByDefault: true),
                                location,
                                ex.ToString()));
                    }
                },
                error => ctx.ReportDiagnostic(error));
        });
    }
}
