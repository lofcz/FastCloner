using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastCloner.SourceGenerator.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Document = Microsoft.CodeAnalysis.Document;

namespace FastCloner.SourceGenerator.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FastClonerClonableCodeFixProvider)), Shared]
public class FastClonerClonableCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [FastClonerClonableAnalyzer.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics.First();
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;
        ClassDeclarationSyntax? declaration = root?.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().First();

        if (declaration is not null)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Make class partial",
                    createChangedDocument: c => MakeClassPartialAsync(context.Document, declaration, c),
                    equivalenceKey: "Make class partial"),
                diagnostic);   
        }
    }

    private static async Task<Document> MakeClassPartialAsync(Document document, ClassDeclarationSyntax classDeclaration, CancellationToken cancellationToken)
    {
        SyntaxToken partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword);
        SyntaxTokenList newModifiers = classDeclaration.Modifiers.Add(partialToken);
        ClassDeclarationSyntax newClassDeclaration = classDeclaration.WithModifiers(newModifiers);
        SyntaxNode? oldRoot = await document.GetSyntaxRootAsync(cancellationToken);
        SyntaxNode? newRoot = oldRoot?.ReplaceNode(classDeclaration, newClassDeclaration);
        return newRoot is not null ? document.WithSyntaxRoot(newRoot) : document;
    }
}