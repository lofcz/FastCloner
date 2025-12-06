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
    Array,          // T[] (single-dimensional)
    MultiDimArray,  // T[,], T[,,], etc. (multi-dimensional arrays)
    Object,         // System.Object
    Implicit,       // Implicitly clonable (struct/class with all clonable members)
    Other           // Everything else - shallow copy fallback
}

internal enum CollectionKind
{
    None,
    List,
    HashSet,
    Queue,
    Stack,
    LinkedList,
    SortedSet,
    ConcurrentQueue,
    ConcurrentStack,
    ConcurrentBag,
    Dictionary,
    SortedDictionary,
    SortedList,
    ConcurrentDictionary,
    ObservableCollection,
    ReadOnlyCollection,
    ReadOnlyDictionary,
    // Immutable collections
    ImmutableList,
    ImmutableArray,
    ImmutableHashSet,
    ImmutableSortedSet,
    ImmutableQueue,
    ImmutableStack,
    ImmutableDictionary,
    ImmutableSortedDictionary
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
    bool ElementHasClonableAttr,     // For optimization - call FastDeepClone
    bool KeyIsSafe,                  // For dictionary key optimization
    bool KeyIsClonable,              // For dictionary key optimization
    bool ValueIsSafe,                // For dictionary value optimization
    bool ValueIsClonable,            // For dictionary value optimization
    bool RequiresFastCloner,         // If true, this member requires FastCloner library to be cloned correctly
    CollectionKind CollectionKind,   // Specific kind of collection (List, Set, Queue, etc.)
    string? ConcreteTypeFullName,    // Concrete type to instantiate (if interface/abstract)
    bool IsValueType,                // Whether the member type is a value type (struct)
    bool IsInitOnly,                 // Whether the property has init accessor (requires object initializer)
    bool IsRequired,                 // Whether the member is required (must be in object initializer)
    bool HasPrivateSetter,           // Whether the setter is private/protected (may not be accessible from extension class)
    int ArrayRank,                   // For multi-dimensional arrays: the rank (2 for T[,], 3 for T[,,], etc.)
    bool IsNullable                  // Whether the member is explicitly nullable (annotated with ?)
) : IEquatable<MemberModel>
{
    public static MemberModel Create(IPropertySymbol property, bool nullabilityEnabled, Compilation compilation)
    {
        var (typeKind, elementName, keyName, valueName, elementSafe, elementClonable, keySafe, keyClonable, valSafe, valClonable, requiresFastCloner, collectionKind, concreteType, arrayRank) 
            = AnalyzeType(property.Type, compilation);
        
        // Check if the property has an init-only setter (C# 9+)
        var isInitOnly = property.SetMethod?.IsInitOnly ?? false;
        
        // Check if the setter is not publicly accessible
        var hasPrivateSetter = property.SetMethod != null && 
                               property.SetMethod.DeclaredAccessibility != Accessibility.Public;
        
        // Check nullability
        var isNullable = property.NullableAnnotation == NullableAnnotation.Annotated;
        
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
            elementClonable,
            keySafe,
            keyClonable,
            valSafe,
            valClonable,
            requiresFastCloner,
            collectionKind,
            concreteType,
            property.Type.IsValueType,
            isInitOnly,
            property.IsRequired,
            hasPrivateSetter,
            arrayRank,
            isNullable);
    }

    public static MemberModel Create(IFieldSymbol field, bool nullabilityEnabled, Compilation compilation)
    {
        var (typeKind, elementName, keyName, valueName, elementSafe, elementClonable, keySafe, keyClonable, valSafe, valClonable, requiresFastCloner, collectionKind, concreteType, arrayRank) 
            = AnalyzeType(field.Type, compilation);
        
        // Check nullability
        var isNullable = field.NullableAnnotation == NullableAnnotation.Annotated;
        
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
            elementClonable,
            keySafe,
            keyClonable,
            valSafe,
            valClonable,
            requiresFastCloner,
            collectionKind,
            concreteType,
            field.Type.IsValueType,
            false,  // Fields don't have init-only semantics
            field.IsRequired,
            false,  // Fields accessibility is checked elsewhere (we only collect accessible fields)
            arrayRank,
            isNullable);
    }
    
    private static (MemberTypeKind kind, string? elem, string? key, string? val, bool elemSafe, bool elemClon, bool keySafe, bool keyClon, bool valSafe, bool valClon, bool requiresFastCloner, CollectionKind collKind, string? concreteType, int arrayRank) 
        AnalyzeType(ITypeSymbol type, Compilation compilation)
    {
        // Check if safe type (primitives, strings, etc.)
        if (TypeAnalyzer.IsSafeType(type, compilation))
            return (MemberTypeKind.Safe, null, null, null, false, false, false, false, false, false, false, CollectionKind.None, null, 0);
        
        // Check if this is a "do not clone" type (delegates, Lazy, Task, etc.)
        // These are treated as Safe to prevent deep cloning (shallow copy semantics)
        if (TypeAnalyzer.IsDoNotCloneType(type))
            return (MemberTypeKind.Safe, null, null, null, false, false, false, false, false, false, false, CollectionKind.None, null, 0);
        
        // Check if this is a ref struct type (Span<T>, ReadOnlySpan<T>, etc.)
        // Ref structs cannot be boxed and cannot be used with state tracking dictionary.
        // Treat them as Safe to use shallow copy (which is the correct semantics for ref structs anyway).
        if (TypeAnalyzer.IsRefStructType(type))
            return (MemberTypeKind.Safe, null, null, null, false, false, false, false, false, false, false, CollectionKind.None, null, 0);
        
        // Check if has clonable attribute
        if (TypeAnalyzer.HasClonableAttribute(type))
            return (MemberTypeKind.Clonable, null, null, null, false, false, false, false, false, false, false, CollectionKind.None, null, 0);
        
        // Check if System.Object or Type Parameter (generic T)
        // For generics, we don't know at compile time if it's clonable.
        // We generate a smart fallback that handles safe types at runtime.
        if (type.SpecialType == SpecialType.System_Object || type.TypeKind == Microsoft.CodeAnalysis.TypeKind.TypeParameter)
            return (MemberTypeKind.Object, null, null, null, false, false, false, false, false, false, false, CollectionKind.None, null, 0);
        
        // IMPORTANT: Check array BEFORE collection (arrays implement ICollection<T>)
        if (type is IArrayTypeSymbol arrayType)
        {
            var elemName = arrayType.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var elemSafe = TypeAnalyzer.IsSafeType(arrayType.ElementType, compilation);
            var elemClon = TypeAnalyzer.HasClonableAttribute(arrayType.ElementType);
            var rank = arrayType.Rank;
            
            // Multi-dimensional arrays (int[,], int[,,], etc.)
            if (rank > 1)
            {
                // Multi-dimensional arrays: if element is not safe and not clonable, we need FastCloner to deep clone elements
                var requiresFastCloner = !elemSafe && !elemClon;
                return (MemberTypeKind.MultiDimArray, elemName, null, null, elemSafe, elemClon, false, false, false, false, requiresFastCloner, CollectionKind.None, null, rank);
            }
            
            // Single-dimensional arrays: if element is not safe and not clonable, we need FastCloner to deep clone it
            var requiresFastClonerSingle = !elemSafe && !elemClon;
            return (MemberTypeKind.Array, elemName, null, null, elemSafe, elemClon, false, false, false, false, requiresFastClonerSingle, CollectionKind.None, null, 1);
        }
        
        // Check dictionary BEFORE collection (dictionaries implement ICollection<KeyValuePair<K,V>>)
        if (TypeAnalyzer.IsDictionaryType(type))
        {
            var dictTypes = TypeAnalyzer.GetDictionaryTypes(type, compilation);
            if (dictTypes.HasValue)
            {
                var keyName = dictTypes.Value.KeyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var valName = dictTypes.Value.ValueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                
                var keySafe = TypeAnalyzer.IsSafeType(dictTypes.Value.KeyType, compilation);
                var keyClon = TypeAnalyzer.HasClonableAttribute(dictTypes.Value.KeyType);
                var valSafe = TypeAnalyzer.IsSafeType(dictTypes.Value.ValueType, compilation);
                var valClon = TypeAnalyzer.HasClonableAttribute(dictTypes.Value.ValueType);
                
                // If key or value is not safe/clonable, we might need FastCloner
                var requiresFastCloner = (!keySafe && !keyClon) || (!valSafe && !valClon);
                
                var collKind = TypeAnalyzer.GetCollectionKind(type);
                var concreteType = TypeAnalyzer.GetConcreteTypeForCollection(type, collKind, $"{keyName}, {valName}");

                return (MemberTypeKind.Dictionary, null, keyName, valName, false, false, keySafe, keyClon, valSafe, valClon, requiresFastCloner, collKind, concreteType, 0);
            }
        }
        
        // Check collection (must be after array and dictionary)
        if (TypeAnalyzer.IsCollectionType(type))
        {
            var elemType = TypeAnalyzer.GetCollectionElementType(type, compilation);
            var elemName = elemType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var elemSafe = elemType != null && TypeAnalyzer.IsSafeType(elemType, compilation);
            var elemClon = elemType != null && TypeAnalyzer.HasClonableAttribute(elemType);
            // If element is not safe and not clonable, we need FastCloner
            var requiresFastCloner = !elemSafe && !elemClon;
            
            var collKind = TypeAnalyzer.GetCollectionKind(type);
            var concreteType = TypeAnalyzer.GetConcreteTypeForCollection(type, collKind, elemName!);
            
            return (MemberTypeKind.Collection, elemName, null, null, elemSafe, elemClon, false, false, false, false, requiresFastCloner, collKind, concreteType, 0);
        }

        // Check for implicit candidate (must be after collection)
        if (TypeAnalyzer.IsImplicitCandidate(type))
        {
            // It's a candidate for implicit cloning (generated recursively)
            return (MemberTypeKind.Implicit, null, null, null, false, false, false, false, false, false, false, CollectionKind.None, null, 0);
        }
        
        // Everything else - shallow copy fallback
        // If it's "Other", it's an unknown type. We definitely need FastCloner to deep clone it.
        return (MemberTypeKind.Other, null, null, null, false, false, false, false, false, false, true, CollectionKind.None, null, 0);
    }
}
