using System.Collections.Generic;
using System.Collections.Immutable;
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
        
        IncrementalValueProvider<TargetFramework> targetFrameworkProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => TargetFrameworkDetector.Detect(provider));
        IncrementalValueProvider<BridgeContract> bridgeContractProvider = context.CompilationProvider
            .Select(static (compilation, _) => BridgeContractCollector.Collect(compilation));
        IncrementalValueProvider<(TargetFramework Tfm, BridgeContract Contract)> bridgeProxyConditions =
            targetFrameworkProvider.Combine(bridgeContractProvider);
        
        context.RegisterSourceOutput(bridgeProxyConditions, static (ctx, args) =>
        {
            (TargetFramework tfm, BridgeContract contract) = args;
            if (BridgeProxyEmitter.ShouldEmit(tfm, contract))
            {
                ctx.AddSource(BridgeProxyEmitter.HintName, SourceText.From(BridgeProxyEmitter.Emit(contract), Encoding.UTF8));
            }
        });
        
        IncrementalValuesProvider<(GeneratorAttributeSyntaxContext Ctx, TargetFramework Tfm)> attributeProvider = 
            context.SyntaxProvider.ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: "FastCloner.SourceGenerator.Shared.FastClonerClonableAttribute",
                predicate: static (node, cancellationToken) => 
                    node is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax,
                transform: static (ctx, cancellationToken) => ctx)
            .Combine(targetFrameworkProvider)
            .Select(static (pair, _) => (pair.Left, pair.Right));
        
        IncrementalValuesProvider<Result<TypeModel>> pipeline = attributeProvider
            .Select(static (pair, cancellationToken) =>
            {
                (GeneratorAttributeSyntaxContext ctx, TargetFramework tfm) = pair;
                
                ISymbol? symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.TargetNode);
                if (symbol is INamedTypeSymbol namedTypeSymbol)
                {
                    bool nullabilityEnabled = ctx.SemanticModel.GetNullableContext(ctx.TargetNode.SpanStart)
                        .HasFlag(NullableContext.Enabled);
                    
                    Compilation compilation = ctx.SemanticModel.Compilation;
                    
                    return TypeModelFactory.TryCreate(namedTypeSymbol, nullabilityEnabled, compilation, tfm, out TypeModel? model, out Diagnostic? error) ? 
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
        IncrementalValuesProvider<EquatableArray<GenericUsage>> explicitUsages = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: GenericUsageCollector.IsCandidate,
            transform: static (ctx, _) => ctx)
            .Combine(targetFrameworkProvider)
            .Select(static (pair, cancellationToken) => GenericUsageCollector.Collect(pair.Left, pair.Right, cancellationToken))
            .Where(x => x.Count > 0);

        IncrementalValuesProvider<EquatableArray<GenericUsage>> includedUsages = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "FastCloner.SourceGenerator.Shared.FastClonerIncludeAttribute",
            predicate: static (node, _) => node is ClassDeclarationSyntax || node is StructDeclarationSyntax,
            transform: static (ctx, _) => ctx)
            .Combine(targetFrameworkProvider)
            .Select(static (pair, cancellationToken) => IncludeAttributeCollector.Collect(pair.Left, pair.Right, cancellationToken))
            .Where(x => x.Count > 0);

        IncrementalValueProvider<EquatableArray<GenericUsage>> usagePipeline = explicitUsages.Collect().Combine(includedUsages.Collect())
            .Select(static (pair, _) =>
            {
                (ImmutableArray<EquatableArray<GenericUsage>> explicitList, ImmutableArray<EquatableArray<GenericUsage>> includedList) = pair;
                List<GenericUsage> list = [];
                
                foreach (EquatableArray<GenericUsage> array in explicitList) 
                    list.AddRange(array);
                foreach (EquatableArray<GenericUsage> array in includedList) 
                    list.AddRange(array);

                return new EquatableArray<GenericUsage>(list.Distinct().ToArray());
            });
        
        IncrementalValuesProvider<((Result<TypeModel> Left, EquatableArray<GenericUsage> Right) Data, BridgeContract Contract)> combinedPipeline =
            pipeline.Combine(usagePipeline).Combine(bridgeContractProvider);

        // OPTIMAL PERFORMANCE: No Compilation combine!
        // All type analysis is pre-computed in TypeModel during the transform step.
        // This ensures the generator only re-runs when decorated types actually change,
        // not on every keypress.
        context.RegisterSourceOutput(combinedPipeline, static (ctx, source) =>
        {
            ((Result<TypeModel>? result, EquatableArray<GenericUsage> usages), BridgeContract contract) = source;
            
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
                            bool hasInitOnlyWithCycles = model.NeedsStateTracking && model.Members.Any(m => m.IsInitOnly);
                            bool structWithReadonlyRefs = model.IsStruct && model.Members.Any(m => m is { IsValueType: false, IsReadOnly: true });
                            
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

                        CloneCodeGenerator generator = new CloneCodeGenerator(model, usages, contract);
                        string generatedSource = generator.Generate();

                        if (generator.SkippedNonPublicMembers.Count > 0)
                        {
                            string skippedList = string.Join(", ", generator.SkippedNonPublicMembers);
                            ctx.ReportDiagnostic(Diagnostic.Create(
                                new DiagnosticDescriptor(
                                    "FCG010",
                                    "Non-public members skipped by source generator",
                                    "Type '{0}' has non-public members ({1}) that the source generator cannot clone on this target framework. " +
                                    "Either upgrade the consumer to .NET 8+, or install the FastCloner runtime package " +
                                    ", or apply [FastClonerVisibility] / [FastClonerIgnore] to opt out explicitly.",
                                    "FastCloner",
                                    DiagnosticSeverity.Warning,
                                    isEnabledByDefault: true),
                                Location.None,
                                model.Name,
                                skippedList));
                        }

                        // Use FullyQualifiedName to avoid collisions when same class name exists in different namespaces
                        string safeName = model.FullyQualifiedName
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
                        Location location = Location.None;
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
        IncrementalValuesProvider<(GeneratorAttributeSyntaxContext Ctx, TargetFramework Tfm)> contextAttributeProvider = 
            context.SyntaxProvider.ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: "FastCloner.SourceGenerator.Shared.FastClonerRegisterAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => ctx)
            .Combine(targetFrameworkProvider)
            .Select(static (pair, _) => (pair.Left, pair.Right));
        
        IncrementalValuesProvider<Result<ContextModel>> contextPipeline = contextAttributeProvider
            .Select(static (pair, cancellationToken) => ContextCollector.Collect(pair.Ctx, pair.Tfm, cancellationToken));

        // Deduplicate pipeline results
        IncrementalValuesProvider<Result<ContextModel>> dedupedContextPipeline = contextPipeline.Collect().SelectMany((results, _) => 
        {
             // Group successes by FQN to remove duplicates (caused by partial classes with attributes)
             IEnumerable<Result<ContextModel>> uniqueSuccesses = results
                 .Where(r => r.IsSuccess && r.Value != null)
                 .GroupBy(r => r.Value!.FullyQualifiedName)
                 .Select(g => g.First());

             IEnumerable<Result<ContextModel>> errors = results.Where(r => !r.IsSuccess);
             
             return uniqueSuccesses.Concat(errors);
        });

        context.RegisterSourceOutput(dedupedContextPipeline, static (ctx, result) =>
        {
            result.Handle(
                model =>
                {
                    try
                    {
                        ContextCodeGenerator generator = new ContextCodeGenerator(model);
                        string source = generator.Generate();
                        
                        string safeName = model.FullyQualifiedName
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
                        Location location = Location.None;
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
