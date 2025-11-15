using System;
using Microsoft.CodeAnalysis;

namespace FastCloner.SourceGenerator;

/// <summary>
/// Categorizes member types for optimized clone code generation.
/// </summary>
internal enum MemberTypeKind
{
    Safe,           // Primitives, strings - can be shallow copied
    Clonable,       // Has [FastClonerClonable] attribute
    Collection,     // List<T>, IEnumerable<T>, etc.
    Dictionary,     // Dictionary<K,V>
    Array,          // T[]
    Object,         // System.Object
    Other           // Everything else - shallow copy fallback
}

/// <summary>
/// Represents a member (property or field) model for code generation.
/// This is a record struct for proper equality comparison in incremental generation.
/// All type analysis is pre-computed during creation to avoid needing Compilation later.
/// </summary>
internal readonly record struct MemberModel(
    string Name,
    string TypeFullName,
    bool IsReadOnly,
    bool IsProperty,
    bool IsField,
    // Pre-analyzed type characteristics for code generation
    MemberTypeKind TypeKind,
    string? ElementTypeName,         // For collections/arrays
    string? KeyTypeName,             // For dictionaries
    string? ValueTypeName,           // For dictionaries
    bool ElementIsSafe,              // For optimization - can skip deep cloning
    bool ElementHasClonableAttr      // For optimization - call FastDeepClone
) : IEquatable<MemberModel>
{
    public static MemberModel Create(IPropertySymbol property, bool nullabilityEnabled, Compilation compilation)
    {
        var (typeKind, elementName, keyName, valueName, elementSafe, elementClonable) 
            = AnalyzeType(property.Type, compilation);
        
        return new MemberModel(
            property.Name,
            property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            property.SetMethod == null || property.IsReadOnly,
            true,
            false,
            typeKind,
            elementName,
            keyName,
            valueName,
            elementSafe,
            elementClonable);
    }

    public static MemberModel Create(IFieldSymbol field, bool nullabilityEnabled, Compilation compilation)
    {
        var (typeKind, elementName, keyName, valueName, elementSafe, elementClonable) 
            = AnalyzeType(field.Type, compilation);
        
        return new MemberModel(
            field.Name,
            field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            field.IsReadOnly,
            false,
            true,
            typeKind,
            elementName,
            keyName,
            valueName,
            elementSafe,
            elementClonable);
    }
    
    private static (MemberTypeKind kind, string? elem, string? key, string? val, bool elemSafe, bool elemClon) 
        AnalyzeType(ITypeSymbol type, Compilation compilation)
    {
        // Check if safe type (primitives, strings, etc.)
        if (TypeAnalyzer.IsSafeType(type, compilation))
            return (MemberTypeKind.Safe, null, null, null, false, false);
        
        // Check if has clonable attribute
        if (TypeAnalyzer.HasClonableAttribute(type))
            return (MemberTypeKind.Clonable, null, null, null, false, false);
        
        // Check if System.Object
        if (type.SpecialType == SpecialType.System_Object)
            return (MemberTypeKind.Object, null, null, null, false, false);
        
        // IMPORTANT: Check array BEFORE collection (arrays implement ICollection<T>)
        if (type is IArrayTypeSymbol arrayType)
        {
            var elemName = arrayType.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var elemSafe = TypeAnalyzer.IsSafeType(arrayType.ElementType, compilation);
            var elemClon = TypeAnalyzer.HasClonableAttribute(arrayType.ElementType);
            return (MemberTypeKind.Array, elemName, null, null, elemSafe, elemClon);
        }
        
        // Check dictionary BEFORE collection (dictionaries implement ICollection<KeyValuePair<K,V>>)
        if (TypeAnalyzer.IsDictionaryType(type))
        {
            var dictTypes = TypeAnalyzer.GetDictionaryTypes(type, compilation);
            if (dictTypes.HasValue)
            {
                var keyName = dictTypes.Value.KeyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var valName = dictTypes.Value.ValueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                return (MemberTypeKind.Dictionary, null, keyName, valName, false, false);
            }
        }
        
        // Check collection (must be after array and dictionary)
        if (TypeAnalyzer.IsCollectionType(type))
        {
            var elemType = TypeAnalyzer.GetCollectionElementType(type, compilation);
            var elemName = elemType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var elemSafe = elemType != null && TypeAnalyzer.IsSafeType(elemType, compilation);
            var elemClon = elemType != null && TypeAnalyzer.HasClonableAttribute(elemType);
            return (MemberTypeKind.Collection, elemName, null, null, elemSafe, elemClon);
        }
        
        // Everything else - shallow copy fallback
        return (MemberTypeKind.Other, null, null, null, false, false);
    }
}

