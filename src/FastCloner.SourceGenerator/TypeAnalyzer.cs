using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace FastCloner.SourceGenerator;

/// <summary>
/// Utility class for analyzing types during code generation.
/// </summary>
internal static class TypeAnalyzer
{
    /// <summary>
    /// Determines if a type is safe to copy directly without cloning.
    /// </summary>
    public static bool IsSafeType(ITypeSymbol type, Compilation compilation)
    {
        return IsSafeTypeInternal(type, compilation, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
    }

    /// <summary>
    /// Determines if a type should NOT be deep cloned (shallow copy only).
    /// These are types like delegates, Lazy, Task, WeakReference where deep cloning
    /// would be semantically incorrect.
    /// </summary>
    public static bool IsDoNotCloneType(ITypeSymbol type)
    {
        if (type == null) return false;
        
        string fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "");
        
        // Check non-generic types
        if (SafeTypeCatalog.DoNotCloneTypes.Contains(fullName))
            return true;
        
        // Check if it's a delegate type
        if (type.TypeKind == TypeKind.Delegate)
            return true;
        
        // Check generic type definitions
        if (type is INamedTypeSymbol { IsGenericType: true } namedType)
        {
            string metadataName = GetFullMetadataName(namedType.OriginalDefinition);
            if (SafeTypeCatalog.DoNotCloneGenericTypes.Contains(metadataName))
                return true;
        }
        
        return false;
    }

    /// <summary>
    /// Determines if a type is a ref struct (ref-like type) that cannot be boxed.
    /// These types cannot be used with state tracking dictionary (which boxes values).
    /// Examples: Span&lt;T&gt;, ReadOnlySpan&lt;T&gt;, custom ref structs.
    /// </summary>
    public static bool IsRefStructType(ITypeSymbol type)
    {
        if (type == null) return false;
        
        // Check the IsRefLikeType property which covers all ref struct types
        return type.IsRefLikeType;
    }

    private static bool IsSafeTypeInternal(ITypeSymbol type, Compilation compilation, HashSet<ITypeSymbol> visited)
    {
        if (type == null) return false;

        // Handle nullable types
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } namedType)
        {
            return IsSafeTypeInternal(namedType.TypeArguments[0], compilation, visited);
        }

        // Primitives and enums
        if (type.SpecialType == SpecialType.System_String) return true;
        if (type.TypeKind == TypeKind.Enum) return true;
        
        // Primitive value types (Int32, Boolean, etc.)
        if (type.IsValueType && type.SpecialType != SpecialType.None)
        {
             return true;
        }

        // Check SafeTypeCatalog for known safe types
        string fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", ""); // SafeTypeCatalog doesn't use global:: prefix

        if (SafeTypeCatalog.SafeTypeNames.Contains(fullName)) return true;
        
        // Additional safe types (if not in catalog)
        if (fullName == "System.Uri" || fullName == "System.Version") return true;

        // System.Type and Reflection types are effectively immutable/singletons
        if (IsOrInheritsFrom(type, "System.Type") || 
            IsOrInheritsFrom(type, "System.Reflection.MemberInfo") ||
            IsOrInheritsFrom(type, "System.Reflection.Assembly"))
        {
            return true;
        }

        // Recursively check Tuples and KeyValuePairs
        if (type is INamedTypeSymbol { IsGenericType: true } namedSym)
        {
            if (fullName.StartsWith("System.Tuple") || 
                fullName.StartsWith("System.ValueTuple") || 
                fullName.StartsWith("System.Collections.Generic.KeyValuePair"))
            {
                if (!visited.Add(type)) return true; // Cycle protection

                foreach (ITypeSymbol? arg in namedSym.TypeArguments)
                {
                    if (!IsSafeTypeInternal(arg, compilation, visited)) return false;
                }
                return true;
            }
        }

        // Check if it's a simple value type (struct with all safe fields)
        if (type is { IsValueType: true, IsReferenceType: false })
        {
             if (!visited.Add(type)) return true; // Cycle protection
             return IsSimpleValueType(type, compilation, visited);
        }

        return false;
    }

    private static bool IsSimpleValueType(ITypeSymbol type, Compilation compilation, HashSet<ITypeSymbol> visited)
    {
        // Check all fields recursively
        foreach (ISymbol? member in type.GetMembers())
        {
            if (member is IFieldSymbol { IsStatic: false, IsConst: false } field)
            {
                if (!IsSafeTypeInternal(field.Type, compilation, visited))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if a type is a collection type (List, IList, ICollection, etc.)
    /// </summary>
    public static bool IsCollectionType(ITypeSymbol type)
    {
        // Check generic interfaces (including read-only and base enumerable)
        if (type.AllInterfaces.Any(i =>
            i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_ICollection_T ||
            i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T ||
            i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IReadOnlyList_T ||
            i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IReadOnlyCollection_T ||
            (i.MetadataName == "ISet`1" && i.ContainingNamespace.ToDisplayString() == "System.Collections.Generic") ||
            (i.MetadataName == "IReadOnlySet`1" && i.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")))
        {
            return true;
        }

        // Check if type itself is one of these interfaces
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_ICollection_T ||
            type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T ||
            type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IReadOnlyList_T ||
            type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IReadOnlyCollection_T ||
            (type.MetadataName == "ISet`1" && type.ContainingNamespace.ToDisplayString() == "System.Collections.Generic") ||
            (type.MetadataName == "IReadOnlySet`1" && type.ContainingNamespace.ToDisplayString() == "System.Collections.Generic"))
        {
            return true;
        }

        // Check legacy ICollection
        return type.AllInterfaces.Any(i =>
            i.MetadataName == "ICollection" &&
            i.ContainingNamespace.ToDisplayString() == "System.Collections");
    }

    /// <summary>
    /// Checks if a type is a dictionary type.
    /// </summary>
    public static bool IsDictionaryType(ITypeSymbol type)
    {
        // Check generic dictionary interfaces
        if (type.AllInterfaces.Any(i =>
            (i.MetadataName == "IDictionary`2" && i.ContainingNamespace.ToDisplayString() == "System.Collections.Generic") ||
            (i.MetadataName == "IReadOnlyDictionary`2" && i.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")))
        {
            return true;
        }

        // Check if type itself is one of these interfaces
        if ((type.MetadataName == "IDictionary`2" && type.ContainingNamespace.ToDisplayString() == "System.Collections.Generic") ||
            (type.MetadataName == "IReadOnlyDictionary`2" && type.ContainingNamespace.ToDisplayString() == "System.Collections.Generic"))
        {
            return true;
        }

        // Check legacy IDictionary
        return type.AllInterfaces.Any(i =>
            i.MetadataName == "IDictionary" &&
            i.ContainingNamespace.ToDisplayString() == "System.Collections");
    }

    /// <summary>
    /// Gets the element type of a collection.
    /// </summary>
    public static ITypeSymbol? GetCollectionElementType(ITypeSymbol type, Compilation compilation)
    {
        // IMPORTANT: Check arrays first! Multi-dimensional arrays don't implement IEnumerable<T>
        // They only implement non-generic IEnumerable, ICollection, IList
        // So we must extract the element type directly from IArrayTypeSymbol
        if (type is IArrayTypeSymbol arrayType)
        {
            return arrayType.ElementType;
        }

        // Check if type itself is IEnumerable<T>
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
        {
            if (type is INamedTypeSymbol namedType) return namedType.TypeArguments[0];
        }

        // Generic IEnumerable<T> (most generic collections implement this)
        INamedTypeSymbol? genericEnumerable = type.AllInterfaces.FirstOrDefault(i =>
            i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);

        if (genericEnumerable != null)
        {
            return genericEnumerable.TypeArguments[0];
        }

        // Non-generic ICollection
        INamedTypeSymbol? nonGenericCollection = type.AllInterfaces.FirstOrDefault(i =>
            i.MetadataName == "ICollection" &&
            i.ContainingNamespace.ToDisplayString() == "System.Collections");

        if (nonGenericCollection != null)
        {
            return compilation.GetSpecialType(SpecialType.System_Object);
        }

        return null;
    }

    /// <summary>
    /// Gets the key and value types of a dictionary.
    /// </summary>
    public static (ITypeSymbol KeyType, ITypeSymbol ValueType)? GetDictionaryTypes(ITypeSymbol type, Compilation compilation)
    {
        // IDictionary<TKey, TValue>
        INamedTypeSymbol? dictInterface = type.AllInterfaces.FirstOrDefault(i =>
            i.MetadataName == "IDictionary`2" &&
            i.ContainingNamespace.ToDisplayString() == "System.Collections.Generic");

        if (dictInterface != null)
        {
            return (dictInterface.TypeArguments[0], dictInterface.TypeArguments[1]);
        }

        // IReadOnlyDictionary<TKey, TValue>
        INamedTypeSymbol? roDictInterface = type.AllInterfaces.FirstOrDefault(i =>
            i.MetadataName == "IReadOnlyDictionary`2" &&
            i.ContainingNamespace.ToDisplayString() == "System.Collections.Generic");

        if (roDictInterface != null)
        {
            return (roDictInterface.TypeArguments[0], roDictInterface.TypeArguments[1]);
        }

        // Check if type itself is IDictionary<TKey, TValue> or IReadOnlyDictionary<TKey, TValue>
        if ((type.MetadataName == "IDictionary`2" || type.MetadataName == "IReadOnlyDictionary`2") && 
            type.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")
        {
             if (type is INamedTypeSymbol { TypeArguments.Length: 2 } namedType)
             {
                 return (namedType.TypeArguments[0], namedType.TypeArguments[1]);
             }
        }

        // Non-generic IDictionary
        INamedTypeSymbol? nonGenericDict = type.AllInterfaces.FirstOrDefault(i =>
            i.MetadataName == "IDictionary" &&
            i.ContainingNamespace.ToDisplayString() == "System.Collections");

        if (nonGenericDict != null)
        {
            return (compilation.GetSpecialType(SpecialType.System_Object),
                compilation.GetSpecialType(SpecialType.System_Object));
        }

        return null;
    }

    /// <summary>
    /// Checks if a type has the FastClonerClonable attribute.
    /// </summary>
    public static bool HasClonableAttribute(ITypeSymbol type)
    {
        return type.GetAttributes()
            .Any(a => a.AttributeClass is not null && GetFullMetadataName(a.AttributeClass) == "FastCloner.SourceGenerator.Shared.FastClonerClonableAttribute");
    }

    /// <summary>
    /// Gets a clean type name for use in generated method names.
    /// </summary>
    public static string GetCleanTypeName(ITypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            .Replace('<', '_')
            .Replace('>', '_')
            .Replace(',', '_')
            .Replace(' ', '_')
            .Replace('.', '_')
            .Replace('[', '_')
            .Replace(']', '_')
            .Replace('?', '_');
    }

    /// <summary>
    /// Gets a type name for use in method signatures, without nullable annotations.
    /// Uses global:: prefix to avoid namespace conflicts.
    /// </summary>
    public static string GetTypeNameForSignature(ITypeSymbol type)
    {
        // Strip nullable annotation to get the underlying type
        ITypeSymbol nonNullableType = type.WithNullableAnnotation(NullableAnnotation.None);
        
        // Use a format with global:: prefix to avoid namespace conflicts
        SymbolDisplayFormat format = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            memberOptions: SymbolDisplayMemberOptions.IncludeType,
            parameterOptions: SymbolDisplayParameterOptions.IncludeType,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.None);
        
        return nonNullableType.ToDisplayString(format);
    }

    /// <summary>
    /// Checks if a type is a candidate for implicit cloning (class/struct with parameterless constructor).
    /// </summary>
    public static bool IsImplicitCandidate(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        // Must be class or struct
        if (namedType is { IsReferenceType: false, IsValueType: false })
            return false;
            
        // Ignore delegates, etc.
        if (namedType.TypeKind == TypeKind.Delegate || namedType.TypeKind == TypeKind.Interface)
            return false;

        // Ignore collections and dictionaries (should be handled by collection cloning logic)
        if (IsCollectionType(namedType) || IsDictionaryType(namedType))
            return false;

        // Must have public parameterless constructor (or be a struct which always has one effectively)
        // Note: Structs don't explicit ctor if default, but we can init them.
        // For classes, check constructors.
        if (namedType.IsReferenceType)
        {
            // Must have a public parameterless constructor
            if (!namedType.Constructors.Any(c => !c.IsStatic && c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public))
            {
                return false;
            }
        }
        
        return true;
    }

    public static string GetNamespace(INamedTypeSymbol symbol)
    {
        return symbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : symbol.ContainingNamespace.ToDisplayString();
    }

    public static List<string> GetTypeParameters(INamedTypeSymbol symbol)
    {
        return symbol.TypeParameters.Select(tp => tp.Name).ToList();
    }

    public static List<string> GetTypeConstraints(INamedTypeSymbol symbol)
    {
        List<string> constraints = [];
        foreach (ITypeParameterSymbol? tp in symbol.TypeParameters)
        {
            List<string> constraintParts = [];
            
            if (tp.HasReferenceTypeConstraint)
                constraintParts.Add("class");
            if (tp.HasValueTypeConstraint)
                constraintParts.Add("struct");
            if (tp.HasUnmanagedTypeConstraint)
                constraintParts.Add("unmanaged");
            if (tp.HasNotNullConstraint)
                constraintParts.Add("notnull");
            
            foreach (ITypeSymbol? constraintType in tp.ConstraintTypes)
            {
                constraintParts.Add(constraintType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }
            
            if (tp.HasConstructorConstraint)
                constraintParts.Add("new()");
                
            if (constraintParts.Count > 0)
            {
                constraints.Add($"where {tp.Name} : {string.Join(", ", constraintParts)}");
            }
        }
        return constraints;
    }

    public static (bool IsStruct, bool IsSealed, bool HasClonableBaseClass) GetStructureFlags(INamedTypeSymbol symbol)
    {
        bool isStruct = symbol.IsValueType;
        bool isSealed = symbol.IsSealed;
        bool hasClonableBaseClass = false;

        if (!isStruct)
        {
            INamedTypeSymbol? baseType = symbol.BaseType;
            while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
            {
                if (HasClonableAttribute(baseType))
                {
                    hasClonableBaseClass = true;
                    break;
                }

                baseType = baseType.BaseType;
            }
        }

        return (isStruct, isSealed, hasClonableBaseClass);
    }

    /// <summary>
    /// Checks if a type has a public parameterless constructor.
    /// Structs are considered to have a parameterless constructor (default constructor).
    /// </summary>
    public static bool HasParameterlessConstructor(INamedTypeSymbol symbol)
    {
        // Structs always have a default parameterless constructor (even if not explicitly defined)
        if (symbol.IsValueType)
        {
            return true;
        }

        // For classes, check if there's a public parameterless constructor
        return symbol.Constructors.Any(c => 
            !c.IsStatic && 
            c.Parameters.Length == 0 && 
            c.DeclaredAccessibility == Accessibility.Public);
    }

    /// <summary>
    /// Identifies the kind of collection (List, Set, Queue, etc.) for optimized code generation.
    /// </summary>
    public static CollectionKind GetCollectionKind(ITypeSymbol type)
    {
        // Check for specific types first (including derived)
        if (IsOrInheritsFrom(type, "System.Collections.Generic.Stack`1")) return CollectionKind.Stack;
        if (IsOrInheritsFrom(type, "System.Collections.Generic.Queue`1")) return CollectionKind.Queue;
        if (IsOrInheritsFrom(type, "System.Collections.Generic.LinkedList`1")) return CollectionKind.LinkedList;
        if (IsOrInheritsFrom(type, "System.Collections.Generic.SortedSet`1")) return CollectionKind.SortedSet;

        // Observable & ReadOnly wrappers
        if (IsOrInheritsFrom(type, "System.Collections.ObjectModel.ObservableCollection`1")) return CollectionKind.ObservableCollection;
        if (IsOrInheritsFrom(type, "System.Collections.ObjectModel.ReadOnlyCollection`1")) return CollectionKind.ReadOnlyCollection;
        if (IsOrInheritsFrom(type, "System.Collections.ObjectModel.ReadOnlyDictionary`2")) return CollectionKind.ReadOnlyDictionary;

        // Immutable Collections
        if (IsOrInheritsFrom(type, "System.Collections.Immutable.ImmutableArray`1")) return CollectionKind.ImmutableArray;
        
        if (IsOrInheritsFrom(type, "System.Collections.Immutable.ImmutableList`1") || 
            HasInterface(type, "System.Collections.Immutable.IImmutableList`1")) return CollectionKind.ImmutableList;

        if (IsOrInheritsFrom(type, "System.Collections.Immutable.ImmutableHashSet`1") || 
            HasInterface(type, "System.Collections.Immutable.IImmutableSet`1")) return CollectionKind.ImmutableHashSet;
            
        if (IsOrInheritsFrom(type, "System.Collections.Immutable.ImmutableSortedSet`1")) return CollectionKind.ImmutableSortedSet;

        if (IsOrInheritsFrom(type, "System.Collections.Immutable.ImmutableQueue`1") || 
            HasInterface(type, "System.Collections.Immutable.IImmutableQueue`1")) return CollectionKind.ImmutableQueue;

        if (IsOrInheritsFrom(type, "System.Collections.Immutable.ImmutableStack`1") || 
            HasInterface(type, "System.Collections.Immutable.IImmutableStack`1")) return CollectionKind.ImmutableStack;

        if (IsOrInheritsFrom(type, "System.Collections.Immutable.ImmutableDictionary`2") || 
            HasInterface(type, "System.Collections.Immutable.IImmutableDictionary`2")) return CollectionKind.ImmutableDictionary;

        if (IsOrInheritsFrom(type, "System.Collections.Immutable.ImmutableSortedDictionary`2")) return CollectionKind.ImmutableSortedDictionary;
        
        // Concurrent collections
        if (IsOrInheritsFrom(type, "System.Collections.Concurrent.ConcurrentQueue`1")) return CollectionKind.ConcurrentQueue;
        if (IsOrInheritsFrom(type, "System.Collections.Concurrent.ConcurrentStack`1")) return CollectionKind.ConcurrentStack;
        if (IsOrInheritsFrom(type, "System.Collections.Concurrent.ConcurrentBag`1")) return CollectionKind.ConcurrentBag;
        if (IsOrInheritsFrom(type, "System.Collections.Concurrent.ConcurrentDictionary`2")) return CollectionKind.ConcurrentDictionary;

        // Dictionaries
        if (IsOrInheritsFrom(type, "System.Collections.Generic.SortedDictionary`2")) return CollectionKind.SortedDictionary;
        if (IsOrInheritsFrom(type, "System.Collections.Generic.SortedList`2")) return CollectionKind.SortedList;
        if (IsOrInheritsFrom(type, "System.Collections.Generic.Dictionary`2") ||
            IsOrInheritsFrom(type, "System.Collections.Generic.IDictionary`2") ||
            IsOrInheritsFrom(type, "System.Collections.Generic.IReadOnlyDictionary`2") ||
            HasInterface(type, "System.Collections.Generic.IDictionary`2") ||
            HasInterface(type, "System.Collections.Generic.IReadOnlyDictionary`2"))
            return CollectionKind.Dictionary;

        // Sets
        if (IsOrInheritsFrom(type, "System.Collections.Generic.HashSet`1") || 
            IsOrInheritsFrom(type, "System.Collections.Generic.ISet`1") ||
            IsOrInheritsFrom(type, "System.Collections.Generic.IReadOnlySet`1") ||
            HasInterface(type, "System.Collections.Generic.ISet`1") ||
            HasInterface(type, "System.Collections.Generic.IReadOnlySet`1")) 
            return CollectionKind.HashSet;

        // Default to List (covers List<T>, arrays handled elsewhere, custom collections, IList, etc.)
        return CollectionKind.List;
    }

    /// <summary>
    /// Gets a concrete type name for interface/abstract collections.
    /// </summary>
    public static string GetConcreteTypeForCollection(ITypeSymbol type, CollectionKind kind, string elementTypeName)
    {
        // If it's a concrete type (class/struct) and not abstract, use it directly
        if ((type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct) && !type.IsAbstract)
        {
            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        // Map interfaces/abstract types to standard concrete implementations
        switch (kind)
        {
            case CollectionKind.HashSet:
                return $"global::System.Collections.Generic.HashSet<{elementTypeName}>";
            case CollectionKind.SortedSet:
                return $"global::System.Collections.Generic.SortedSet<{elementTypeName}>";
            case CollectionKind.Queue:
                return $"global::System.Collections.Generic.Queue<{elementTypeName}>";
            case CollectionKind.Stack:
                return $"global::System.Collections.Generic.Stack<{elementTypeName}>";
            case CollectionKind.LinkedList:
                return $"global::System.Collections.Generic.LinkedList<{elementTypeName}>";
            case CollectionKind.ConcurrentQueue:
                return $"global::System.Collections.Concurrent.ConcurrentQueue<{elementTypeName}>";
            case CollectionKind.ConcurrentStack:
                return $"global::System.Collections.Concurrent.ConcurrentStack<{elementTypeName}>";
            case CollectionKind.ConcurrentBag:
                return $"global::System.Collections.Concurrent.ConcurrentBag<{elementTypeName}>";

            case CollectionKind.ObservableCollection:
                return $"global::System.Collections.ObjectModel.ObservableCollection<{elementTypeName}>";
            case CollectionKind.ReadOnlyCollection:
                return $"global::System.Collections.ObjectModel.ReadOnlyCollection<{elementTypeName}>";
            case CollectionKind.ReadOnlyDictionary:
                return $"global::System.Collections.ObjectModel.ReadOnlyDictionary<{elementTypeName}>";

            case CollectionKind.ImmutableArray:
                return $"global::System.Collections.Immutable.ImmutableArray<{elementTypeName}>";
            case CollectionKind.ImmutableList:
                return $"global::System.Collections.Immutable.ImmutableList<{elementTypeName}>";
            case CollectionKind.ImmutableHashSet:
                return $"global::System.Collections.Immutable.ImmutableHashSet<{elementTypeName}>";
            case CollectionKind.ImmutableSortedSet:
                return $"global::System.Collections.Immutable.ImmutableSortedSet<{elementTypeName}>";
            case CollectionKind.ImmutableQueue:
                return $"global::System.Collections.Immutable.ImmutableQueue<{elementTypeName}>";
            case CollectionKind.ImmutableStack:
                return $"global::System.Collections.Immutable.ImmutableStack<{elementTypeName}>";
            case CollectionKind.ImmutableDictionary:
                return $"global::System.Collections.Immutable.ImmutableDictionary<{elementTypeName}>";
            case CollectionKind.ImmutableSortedDictionary:
                return $"global::System.Collections.Immutable.ImmutableSortedDictionary<{elementTypeName}>";
            
            // Dictionaries (elementTypeName here is actually Key,Value pair or we need overload? 
            // NOTE: elementTypeName here is passed as single string. For dictionaries it might be complicated.
            // But usually we just need to replace generic args.
            // If called for dictionary, elementTypeName should be "TKey, TValue".
            case CollectionKind.Dictionary:
                return $"global::System.Collections.Generic.Dictionary<{elementTypeName}>";
            case CollectionKind.SortedDictionary:
                return $"global::System.Collections.Generic.SortedDictionary<{elementTypeName}>";
            case CollectionKind.SortedList:
                return $"global::System.Collections.Generic.SortedList<{elementTypeName}>";
            case CollectionKind.ConcurrentDictionary:
                return $"global::System.Collections.Concurrent.ConcurrentDictionary<{elementTypeName}>";

            default:
                // Default to List for everything else (IList, ICollection, IEnumerable, etc.)
                return $"global::System.Collections.Generic.List<{elementTypeName}>";
        }
    }

    private static bool IsOrInheritsFrom(ITypeSymbol type, string fullMetadataName)
    {
        if (type == null) return false;
        
        // Check type itself
        if (GetFullMetadataName(type) == fullMetadataName) return true;
        
        // Check base types
        INamedTypeSymbol? baseType = type.BaseType;
        while (baseType != null)
        {
            if (GetFullMetadataName(baseType) == fullMetadataName) return true;
            baseType = baseType.BaseType;
        }
        
        return false;
    }

    private static bool HasInterface(ITypeSymbol type, string fullMetadataName)
    {
        foreach (INamedTypeSymbol? iface in type.AllInterfaces)
        {
            if (GetFullMetadataName(iface) == fullMetadataName) return true;
        }
        return false;
    }

    private static string GetFullMetadataName(ITypeSymbol symbol)
    {
        if (symbol is null) return string.Empty;
        if (IsRootNamespace(symbol)) return string.Empty;
        
        INamespaceSymbol? ns = symbol.ContainingNamespace;
        string nsName = ns?.IsGlobalNamespace == false ? ns.ToDisplayString() : string.Empty;
        
        return string.IsNullOrEmpty(nsName) ? symbol.MetadataName : $"{nsName}.{symbol.MetadataName}";
    }

    private static bool IsRootNamespace(ISymbol symbol) 
    {
       return symbol is INamespaceSymbol { IsGlobalNamespace: true };
    }
}
