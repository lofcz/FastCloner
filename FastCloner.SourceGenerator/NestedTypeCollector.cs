using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace FastCloner.SourceGenerator;

internal static class NestedTypeCollector
{
    public static void Collect(
        ITypeSymbol type,
        Compilation compilation,
        bool nullabilityEnabled,
        Dictionary<string, MemberModel> nestedTypes)
    {
        // Recursively find nested Collection/Dictionary types that need cloning
        
        if (TypeAnalyzer.IsDictionaryType(type))
        {
            var dictTypes = TypeAnalyzer.GetDictionaryTypes(type, compilation);
            if (dictTypes.HasValue)
            {
                // Check Key
                Collect(dictTypes.Value.KeyType, compilation, nullabilityEnabled, nestedTypes);
                // Check Value
                Collect(dictTypes.Value.ValueType, compilation, nullabilityEnabled, nestedTypes);
            }
        }
        else if (TypeAnalyzer.IsCollectionType(type)) // Includes Array
        {
            var elemType = TypeAnalyzer.GetCollectionElementType(type, compilation);
            if (elemType != null)
            {
                Collect(elemType, compilation, nullabilityEnabled, nestedTypes);
            }
        }
        
        // If this type itself is a collection/dictionary that needs a helper (and isn't the root member type we started with)
        // Check if we need a helper for this type
        // Use a dummy property name since it's a type helper
        
        if (TypeAnalyzer.IsDictionaryType(type))
        {
             // We can reuse MemberModel.Create logic but we need an ISymbol.
             // But we don't have a property symbol here.
             // We need to manually construct MemberModel.
             
             // Analyze manually
             var dictTypes = TypeAnalyzer.GetDictionaryTypes(type, compilation);
             if (dictTypes.HasValue)
             {
                 var keySafe = TypeAnalyzer.IsSafeType(dictTypes.Value.KeyType, compilation);
                 var keyClon = TypeAnalyzer.HasClonableAttribute(dictTypes.Value.KeyType);
                 var valSafe = TypeAnalyzer.IsSafeType(dictTypes.Value.ValueType, compilation);
                 var valClon = TypeAnalyzer.HasClonableAttribute(dictTypes.Value.ValueType);

                 var requiresFastCloner = (!keySafe && !keyClon) || (!valSafe && !valClon);

                 var model = new MemberModel(
                    "NestedHelper", // Dummy name
                    type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    false, true, false,
                    MemberTypeKind.Dictionary,
                    null,
                    dictTypes.Value.KeyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    dictTypes.Value.ValueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    false, false,
                    keySafe, keyClon, valSafe, valClon,
                    requiresFastCloner
                 );
                 
                 if (!nestedTypes.ContainsKey(model.TypeFullName))
                    nestedTypes[model.TypeFullName] = model;
             }
        }
        else if (TypeAnalyzer.IsCollectionType(type))
        {
            var elemType = TypeAnalyzer.GetCollectionElementType(type, compilation);
            if (elemType != null)
            {
                 var elemSafe = TypeAnalyzer.IsSafeType(elemType, compilation);
                 var elemClon = TypeAnalyzer.HasClonableAttribute(elemType);
                 var requiresFastCloner = !elemSafe && !elemClon;

                 var kind = type is IArrayTypeSymbol ? MemberTypeKind.Array : MemberTypeKind.Collection;

                 var model = new MemberModel(
                    "NestedHelper",
                    type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    false, true, false,
                    kind,
                    elemType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    null, null,
                    elemSafe, elemClon,
                    false, false, false, false,
                    requiresFastCloner
                 );

                 if (!nestedTypes.ContainsKey(model.TypeFullName))
                    nestedTypes[model.TypeFullName] = model;
            }
        }
    }
}
