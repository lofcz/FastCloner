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
        // Skip safe types (they don't need helpers)
        if (TypeAnalyzer.IsSafeType(type, compilation))
            return;

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

                 var collKind = TypeAnalyzer.GetCollectionKind(type);
                 var keyTypeName = dictTypes.Value.KeyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                 var valTypeName = dictTypes.Value.ValueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                 var concreteType = TypeAnalyzer.GetConcreteTypeForCollection(type, collKind, $"{keyTypeName}, {valTypeName}");

                 var model = new MemberModel(
                    "NestedHelper", // Dummy name
                    type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    false, true, false,
                    MemberTypeKind.Dictionary,
                    null,
                    keyTypeName,
                    valTypeName,
                    false, false,
                    keySafe, keyClon, valSafe, valClon,
                    requiresFastCloner,
                    collKind,
                    concreteType,
                    type.IsValueType,
                    false, // IsInitOnly - not applicable for helper methods
                    false, // IsRequired - not applicable for helper methods
                    false, // HasPrivateSetter - not applicable for helper methods
                    0,      // ArrayRank - not applicable for dictionaries
                    false   // IsNullable
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
                 
                 var collectionKind = CollectionKind.None;
                 string? concreteType = null;

                 if (kind == MemberTypeKind.Collection)
                 {
                     collectionKind = TypeAnalyzer.GetCollectionKind(type);
                     concreteType = TypeAnalyzer.GetConcreteTypeForCollection(type, collectionKind, elemType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                 }

                 // Determine the array rank if it's an array type
                 int arrayRank = 0;
                 if (type is IArrayTypeSymbol arrayType)
                 {
                     arrayRank = arrayType.Rank;
                     if (arrayRank > 1)
                     {
                         kind = MemberTypeKind.MultiDimArray;
                     }
                 }

                 var model = new MemberModel(
                    "NestedHelper",
                    type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    false, true, false,
                    kind,
                    elemType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    null, null,
                    elemSafe, elemClon,
                    false, false, false, false,
                    requiresFastCloner,
                    collectionKind,
                    concreteType,
                    type.IsValueType,
                    false, // IsInitOnly - not applicable for helper methods
                    false, // IsRequired - not applicable for helper methods
                    false, // HasPrivateSetter - not applicable for helper methods
                    arrayRank,
                    false   // IsNullable
                 );

                 if (!nestedTypes.ContainsKey(model.TypeFullName))
                    nestedTypes[model.TypeFullName] = model;
            }
        }
    }
}
