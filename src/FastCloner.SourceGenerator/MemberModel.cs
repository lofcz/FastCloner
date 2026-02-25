using System;
using System.Collections.Generic;
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
    int ArrayRank,                   // For multi-dimensional arrays: the rank (2 for T[,], 3 for T[,,], etc.)
    bool IsNullable,                 // Whether the member is explicitly nullable (annotated with ?)
    // Property accessor capabilities
    bool HasGetter,                  // Whether the property has a getter
    bool HasSetter,                  // Whether the property has a setter (regular, not init-only)
    bool SetterIsAccessible,         // Whether the setter is publicly accessible (not private/protected)
    MemberCloneBehavior MemberBehavior,  // The clone behavior for this member (Clone, Reference, Shallow, Ignore)
    bool? PreserveIdentity = null,   // null=inherit from type, true=preserve identity for this member's subgraph, false=disabled
    bool CollectionHasCount = true,  // Whether the source collection type has Count property
    bool CollectionHasIndexer = true // Whether the source collection type supports [i] indexing
) : IEquatable<MemberModel>
{
    /// <summary>
    /// Returns true if the member should have its reference copied directly without deep cloning.
    /// This applies to both Shallow and Reference behaviors.
    /// </summary>
    private bool ShouldCopyReference => MemberBehavior is MemberCloneBehavior.Shallow or MemberCloneBehavior.Reference;
    
    // Legacy property for backward compatibility
    public bool IsShallowClone => ShouldCopyReference;

    public static MemberModel Create(IPropertySymbol property, bool nullabilityEnabled, Compilation compilation, MemberCloneBehavior memberBehavior = MemberCloneBehavior.Clone)
    {
        (MemberTypeKind typeKind, string? elementName, string? keyName, string? valueName, bool elementSafe, bool elementClonable, bool keySafe, bool keyClonable, bool valSafe, bool valClonable, bool requiresFastCloner, CollectionKind collectionKind, string? concreteType, int arrayRank, bool collHasCount, bool collHasIndexer) 
            = AnalyzeType(property.Type, compilation);
        
        // Check if the property has an init-only setter (C# 9+)
        bool isInitOnly = property.SetMethod?.IsInitOnly ?? false;
        
        // Property accessor capabilities
        bool hasGetter = property.GetMethod != null;
        bool hasSetter = property.SetMethod != null && !isInitOnly;
        bool setterIsAccessible = property.SetMethod != null &&
                                  (property.SetMethod.DeclaredAccessibility == Accessibility.Public ||
                                   property.SetMethod.DeclaredAccessibility == Accessibility.Internal ||
                                   property.SetMethod.DeclaredAccessibility == Accessibility.ProtectedOrInternal);
        
        // Check nullability
        bool isNullable = property.NullableAnnotation == NullableAnnotation.Annotated;
        
        // Check for PreserveIdentity attribute
        bool? preserveIdentity = GetPreserveIdentityFromAttributes(property.GetAttributes());
        
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
            arrayRank,
            isNullable,
            hasGetter,
            hasSetter,
            setterIsAccessible,
            memberBehavior,
            preserveIdentity,
            collHasCount,
            collHasIndexer);
    }

    public static MemberModel Create(IFieldSymbol field, bool nullabilityEnabled, Compilation compilation, MemberCloneBehavior memberBehavior = MemberCloneBehavior.Clone)
    {
        (MemberTypeKind typeKind, string? elementName, string? keyName, string? valueName, bool elementSafe, bool elementClonable, bool keySafe, bool keyClonable, bool valSafe, bool valClonable, bool requiresFastCloner, CollectionKind collectionKind, string? concreteType, int arrayRank, bool collHasCount, bool collHasIndexer) 
            = AnalyzeType(field.Type, compilation);
        
        // Check nullability
        bool isNullable = field.NullableAnnotation == NullableAnnotation.Annotated;
        
        // Field accessor capabilities - fields always have getters, setters depend on readonly
        bool hasGetter = true;
        bool hasSetter = !field.IsReadOnly;
        bool setterIsAccessible = !field.IsReadOnly; // If not readonly, it's accessible (we only collect public fields)
        
        // Check for PreserveIdentity attribute
        bool? preserveIdentity = GetPreserveIdentityFromAttributes(field.GetAttributes());
        
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
            arrayRank,
            isNullable,
            hasGetter,
            hasSetter,
            setterIsAccessible,
            memberBehavior,
            preserveIdentity,
            collHasCount,
            collHasIndexer);
    }
    
    /// <summary>
    /// Extracts PreserveIdentity value from attributes if present.
    /// </summary>
    private static bool? GetPreserveIdentityFromAttributes(System.Collections.Immutable.ImmutableArray<AttributeData> attributes)
    {
        foreach (AttributeData attr in attributes)
        {
            if (attr.AttributeClass?.ToDisplayString() == "FastCloner.SourceGenerator.Shared.FastClonerPreserveIdentityAttribute")
            {
                // Check named argument first (Enabled = true/false)
                foreach (KeyValuePair<string, TypedConstant> namedArg in attr.NamedArguments)
                {
                    if (namedArg is { Key: "Enabled", Value.Value: bool namedEnabled })
                        return namedEnabled;
                }
                
                // Check constructor argument [FastClonerPreserveIdentity(true/false)]
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is bool enabled)
                    return enabled;
                
                // Default value is true when attribute is present with no arguments
                return true;
            }
        }
        return null; // No attribute found
    }
    
    private static (MemberTypeKind kind, string? elem, string? key, string? val, bool elemSafe, bool elemClon, bool keySafe, bool keyClon, bool valSafe, bool valClon, bool requiresFastCloner, CollectionKind collKind, string? concreteType, int arrayRank, bool collHasCount, bool collHasIndexer) 
        AnalyzeType(ITypeSymbol type, Compilation compilation)
    {
        // Check if safe type (primitives, strings, etc.)
        if (TypeAnalyzer.IsSafeType(type, compilation))
            return (MemberTypeKind.Safe, null, null, null, false, false, false, false, false, false, false, CollectionKind.None, null, 0, true, true);
        
        // Check if this is a "do not clone" type (delegates, Lazy, Task, etc.)
        // These are treated as Safe to prevent deep cloning (shallow copy semantics)
        if (TypeAnalyzer.IsDoNotCloneType(type))
            return (MemberTypeKind.Safe, null, null, null, false, false, false, false, false, false, false, CollectionKind.None, null, 0, true, true);
        
        // Check if this is a ref struct type (Span<T>, ReadOnlySpan<T>, etc.)
        // Ref structs cannot be boxed and cannot be used with state tracking dictionary.
        // Treat them as Safe to use shallow copy (which is the correct semantics for ref structs anyway).
        if (TypeAnalyzer.IsRefStructType(type))
            return (MemberTypeKind.Safe, null, null, null, false, false, false, false, false, false, false, CollectionKind.None, null, 0, true, true);
        
        // Check if has clonable attribute
        if (TypeAnalyzer.HasClonableAttribute(type))
            return (MemberTypeKind.Clonable, null, null, null, false, false, false, false, false, false, false, CollectionKind.None, null, 0, true, true);
        
        // Check if System.Object or Type Parameter (generic T)
        // For generics, we don't know at compile time if it's clonable.
        // We generate a smart fallback that handles safe types at runtime.
        if (type.SpecialType == SpecialType.System_Object || type.TypeKind == Microsoft.CodeAnalysis.TypeKind.TypeParameter)
            return (MemberTypeKind.Object, null, null, null, false, false, false, false, false, false, false, CollectionKind.None, null, 0, true, true);
        
        // IMPORTANT: Check array BEFORE collection (arrays implement ICollection<T>)
        if (type is IArrayTypeSymbol arrayType)
        {
            string elemName = arrayType.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            bool elemSafe = TypeAnalyzer.IsSafeType(arrayType.ElementType, compilation);
            bool elemClon = TypeAnalyzer.HasClonableAttribute(arrayType.ElementType);
            int rank = arrayType.Rank;
            
            // Multi-dimensional arrays (int[,], int[,,], etc.)
            if (rank > 1)
            {
                // Multi-dimensional arrays: if element is not safe and not clonable, we need FastCloner to deep clone elements
                bool requiresFastCloner = !elemSafe && !elemClon;
                return (MemberTypeKind.MultiDimArray, elemName, null, null, elemSafe, elemClon, false, false, false, false, requiresFastCloner, CollectionKind.None, null, rank, true, true);
            }
            
            // Single-dimensional arrays: if element is not safe and not clonable, we need FastCloner to deep clone it
            bool requiresFastClonerSingle = !elemSafe && !elemClon;
            return (MemberTypeKind.Array, elemName, null, null, elemSafe, elemClon, false, false, false, false, requiresFastClonerSingle, CollectionKind.None, null, 1, true, true);
        }
        
        // Check dictionary BEFORE collection (dictionaries implement ICollection<KeyValuePair<K,V>>)
        if (TypeAnalyzer.IsDictionaryType(type))
        {
            (ITypeSymbol KeyType, ITypeSymbol ValueType)? dictTypes = TypeAnalyzer.GetDictionaryTypes(type, compilation);
            if (dictTypes.HasValue)
            {
                string keyName = dictTypes.Value.KeyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                string valName = dictTypes.Value.ValueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                
                bool keySafe = TypeAnalyzer.IsSafeType(dictTypes.Value.KeyType, compilation);
                bool keyClon = TypeAnalyzer.HasClonableAttribute(dictTypes.Value.KeyType);
                bool valSafe = TypeAnalyzer.IsSafeType(dictTypes.Value.ValueType, compilation);
                bool valClon = TypeAnalyzer.HasClonableAttribute(dictTypes.Value.ValueType);
                
                // If key or value is not safe/clonable, we might need FastCloner
                bool requiresFastCloner = (!keySafe && !keyClon) || (!valSafe && !valClon);
                
                CollectionKind collKind = TypeAnalyzer.GetCollectionKind(type);
                string concreteType = TypeAnalyzer.GetConcreteTypeForCollection(type, collKind, $"{keyName}, {valName}");

                return (MemberTypeKind.Dictionary, null, keyName, valName, false, false, keySafe, keyClon, valSafe, valClon, requiresFastCloner, collKind, concreteType, 0, true, true);
            }
        }
        
        // Check collection (must be after array and dictionary)
        if (TypeAnalyzer.IsCollectionType(type))
        {
            ITypeSymbol? elemType = TypeAnalyzer.GetCollectionElementType(type, compilation);
            string? elemName = elemType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            bool elemSafe = elemType != null && TypeAnalyzer.IsSafeType(elemType, compilation);
            bool elemClon = elemType != null && TypeAnalyzer.HasClonableAttribute(elemType);
            // If element is not safe and not clonable, we need FastCloner
            bool requiresFastCloner = !elemSafe && !elemClon;
            
            CollectionKind collKind = TypeAnalyzer.GetCollectionKind(type);
            string concreteType = TypeAnalyzer.GetConcreteTypeForCollection(type, collKind, elemName!);
            
            bool collHasCount = TypeAnalyzer.CollectionHasCountProperty(type);
            bool collHasIndexer = TypeAnalyzer.CollectionHasIndexer(type);
            
            return (MemberTypeKind.Collection, elemName, null, null, elemSafe, elemClon, false, false, false, false, requiresFastCloner, collKind, concreteType, 0, collHasCount, collHasIndexer);
        }

        // Check for implicit candidate (must be after collection)
        if (TypeAnalyzer.IsImplicitCandidate(type))
        {
            // It's a candidate for implicit cloning (generated recursively)
            return (MemberTypeKind.Implicit, null, null, null, false, false, false, false, false, false, false, CollectionKind.None, null, 0, true, true);
        }
        
        // Everything else - shallow copy fallback
        // If it's "Other", it's an unknown type. We definitely need FastCloner to deep clone it.
        return (MemberTypeKind.Other, null, null, null, false, false, false, false, false, false, true, CollectionKind.None, null, 0, true, true);
    }
}
