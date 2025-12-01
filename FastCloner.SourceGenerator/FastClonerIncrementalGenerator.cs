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
                node is ClassDeclarationSyntax || node is StructDeclarationSyntax,
            transform: static (ctx, cancellationToken) =>
            {
                var symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.TargetNode);
                if (symbol is INamedTypeSymbol namedTypeSymbol)
                {
                    var nullabilityEnabled = ctx.SemanticModel.GetNullableContext(ctx.TargetNode.SpanStart)
                        .HasFlag(NullableContext.Enabled);
                    
                    var compilation = ctx.SemanticModel.Compilation;
                    
                    if (TypeModelFactory.TryCreate(namedTypeSymbol, nullabilityEnabled, compilation, out var model, out var error))
                    {
                        return Result<TypeModel>.Success(model!);
                    }
                    else
                    {
                        return Result<TypeModel>.Error(error!);
                    }
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

        // OPTIMAL PERFORMANCE: No Compilation combine!
        // All type analysis is pre-computed in TypeModel during the transform step.
        // This ensures the generator only re-runs when decorated types actually change,
        // not on every keypress.
        context.RegisterSourceOutput(pipeline, static (ctx, result) =>
        {
            result.Handle(
                model =>
                {
                    try
                    {
                        var generator = new CloneCodeGenerator(model);
                        var generatedSource = generator.Generate();
                        
                        ctx.AddSource($"{model.Name}FastDeepClone.g.cs", SourceText.From(generatedSource, Encoding.UTF8));
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
    }
}
