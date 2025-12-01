using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace FastCloner.SourceGenerator;

internal record struct MemberAnalysis(MemberModel Model, ITypeSymbol Type);

internal static class MemberCollector
{
    public static List<MemberAnalysis> GetMembers(
        INamedTypeSymbol symbol,
        Compilation compilation,
        bool nullabilityEnabled)
    {
        var members = new List<MemberAnalysis>();
        var seenNames = new HashSet<string>();

        // Get all members from base types too (walking up the inheritance chain)
        var currentType = symbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in currentType.GetMembers())
            {
                if (member.IsStatic || member.IsImplicitlyDeclared)
                    continue;

                // Skip if we've already seen this member name (base class members override derived)
                if (!seenNames.Add(member.Name))
                    continue;

                if (member is IPropertySymbol property)
                {
                    if (property.GetMethod != null && property.SetMethod != null && !property.IsIndexer)
                    {
                        if (!HasIgnoreAttribute(property, compilation))
                        {
                            members.Add(new MemberAnalysis(MemberModel.Create(property, nullabilityEnabled, compilation), property.Type));
                        }
                    }
                }
                else if (member is IFieldSymbol field)
                {
                    if (field.IsConst) continue; // Skip const fields
                    
                    if (!HasIgnoreAttribute(field, compilation))
                    {
                        members.Add(new MemberAnalysis(MemberModel.Create(field, nullabilityEnabled, compilation), field.Type));
                    }
                }
            }

            currentType = currentType.BaseType;
        }

        return members;
    }

    private static bool HasIgnoreAttribute(ISymbol member, Compilation compilation)
    {
        var ignoreAttribute = compilation.GetTypeByMetadataName("FastCloner.Code.FastClonerIgnoreAttribute");
        var nonSerializedAttribute = compilation.GetTypeByMetadataName("System.NonSerializedAttribute");

        foreach (var attr in member.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, ignoreAttribute))
            {
                // Check if Ignored property is true (default is true)
                if (attr.ConstructorArguments.Length == 0)
                    return true;
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is bool ignored)
                    return ignored;
                return true;
            }

            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, nonSerializedAttribute))
            {
                return true;
            }
        }

        return false;
    }
}
