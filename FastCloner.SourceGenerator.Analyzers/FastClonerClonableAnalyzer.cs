using System;
using System.Collections.Immutable;
using System.Linq;
using FastCloner.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FastCloner.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FastClonerClonableAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "FC0001";
    private const string Title = "FastClonerClonable class must be partial";
    private const string MessageFormat = "The class '{0}' is marked with [FastClonerClonable] but is not partial";
    private const string Description = "Classes marked with [FastClonerClonable] must be partial to allow the source generator to add the cloning implementation.";
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [ rule ];
    
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
    }
    
    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        ClassDeclarationSyntax classDeclaration = (ClassDeclarationSyntax)context.Node;
        ISymbol? classSymbol = ModelExtensions.GetDeclaredSymbol(context.SemanticModel, classDeclaration);
        
        if (classSymbol is null)
        {
            return;
        }
   
        bool hasClonableAttribute = classSymbol.GetAttributes().Any(x => string.Equals(x.AttributeClass?.Name, nameof(FastClonerClonableAttribute), StringComparison.OrdinalIgnoreCase));

        if (!hasClonableAttribute)
        {
            return;
        }
        
        bool isPartial = classDeclaration.Modifiers.Any(x => x.IsKind(SyntaxKind.PartialKeyword));

        if (!isPartial)
        {
            Diagnostic diagnostic = Diagnostic.Create(rule, classDeclaration.Identifier.GetLocation(), classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }
}