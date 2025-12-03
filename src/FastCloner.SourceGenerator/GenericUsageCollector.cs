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

        // Nullability context at usage site
        var nullability = context.SemanticModel.GetNullableContext(node.SpanStart).HasFlag(NullableContext.Enabled);

        foreach (var typeArg in symbol.TypeArguments)
        {
            var usage = GenericTypeAnalyzer.Analyze(symbol, typeArg, compilation, nullability);
            if (usage.HasValue)
            {
                usages.Add(usage.Value);
            }
        }

        return new EquatableArray<GenericUsage>(usages.ToArray());
    }
}
