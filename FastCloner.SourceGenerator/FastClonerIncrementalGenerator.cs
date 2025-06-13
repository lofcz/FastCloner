using System;
using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace FastCloner.SourceGenerator
{
    [Generator(LanguageNames.CSharp)]
    public class FastClonerIncrementalGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            return;
            
            IncrementalValuesProvider<(TypeDeclarationSyntax Syntax, SemanticModel SemanticModel)> typesToProcess = 
                context.SyntaxProvider
                    .CreateSyntaxProvider(
                        predicate: static (s, _) => s is TypeDeclarationSyntax,
                        transform: (ctx, _) => 
                        {
                            TypeDeclarationSyntax syntax = (TypeDeclarationSyntax)ctx.Node;
                            return (Syntax: syntax, SemanticModel: ctx.SemanticModel);
                        });
            
            context.RegisterSourceOutput(
                typesToProcess.Combine(context.CompilationProvider),
                static (spc, source) => Execute(
                    source.Left.Syntax, 
                    source.Left.SemanticModel, 
                    source.Right, 
                    spc));
        }

        private static void Execute(
            TypeDeclarationSyntax typeDecl, 
            SemanticModel semanticModel,
            Compilation compilation,
            SourceProductionContext context)
        {
            try
            {
                CloneCodeGenerator generator = new CloneCodeGenerator(compilation, typeDecl);
                string source = generator.Generate();
            
                context.AddSource($"{typeDecl.Identifier.Text}Clone.g.cs", 
                    SourceText.From(source, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "FCG001",
                            "Generator Error",
                            "Error generating clone code: {0}",
                            "FastCloner",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true),
                        Location.None,
                        ex.ToString()));
            }
        }
    }

}