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
            (ITypeSymbol KeyType, ITypeSymbol ValueType)? dictTypes = TypeAnalyzer.GetDictionaryTypes(type, compilation);
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
            ITypeSymbol? elemType = TypeAnalyzer.GetCollectionElementType(type, compilation);
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
             (ITypeSymbol KeyType, ITypeSymbol ValueType)? dictTypes = TypeAnalyzer.GetDictionaryTypes(type, compilation);
             if (dictTypes.HasValue)
             {
                 bool keySafe = TypeAnalyzer.IsSafeType(dictTypes.Value.KeyType, compilation);
                 bool keyClon = TypeAnalyzer.HasClonableAttribute(dictTypes.Value.KeyType);
                 bool valSafe = TypeAnalyzer.IsSafeType(dictTypes.Value.ValueType, compilation);
                 bool valClon = TypeAnalyzer.HasClonableAttribute(dictTypes.Value.ValueType);

                 bool requiresFastCloner = (!keySafe && !keyClon) || (!valSafe && !valClon);

                 CollectionKind collKind = TypeAnalyzer.GetCollectionKind(type);
                 string keyTypeName = dictTypes.Value.KeyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                 string valTypeName = dictTypes.Value.ValueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                 string concreteType = TypeAnalyzer.GetConcreteTypeForCollection(type, collKind, $"{keyTypeName}, {valTypeName}");

                 MemberModel model = new MemberModel(
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
                    0,     // ArrayRank - not applicable for dictionaries
                    false, // IsNullable
                    true,  // HasGetter - helper methods always have access
                    true,  // HasSetter - helper methods always have access
                    true   // SetterIsAccessible - helper methods always have access
                 );
                 
                 if (!nestedTypes.ContainsKey(model.TypeFullName))
                    nestedTypes[model.TypeFullName] = model;
             }
        }
        else if (TypeAnalyzer.IsCollectionType(type))
        {
            ITypeSymbol? elemType = TypeAnalyzer.GetCollectionElementType(type, compilation);
            if (elemType != null)
            {
                 bool elemSafe = TypeAnalyzer.IsSafeType(elemType, compilation);
                 bool elemClon = TypeAnalyzer.HasClonableAttribute(elemType);
                 bool requiresFastCloner = !elemSafe && !elemClon;

                 MemberTypeKind kind = type is IArrayTypeSymbol ? MemberTypeKind.Array : MemberTypeKind.Collection;
                 
                 CollectionKind collectionKind = CollectionKind.None;
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

                 MemberModel model = new MemberModel(
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
                    arrayRank,
                    false, // IsNullable
                    true,  // HasGetter - helper methods always have access
                    true,  // HasSetter - helper methods always have access
                    true   // SetterIsAccessible - helper methods always have access
                 );

                 if (!nestedTypes.ContainsKey(model.TypeFullName))
                    nestedTypes[model.TypeFullName] = model;
            }
        }
    }
}
