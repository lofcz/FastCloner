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
        while (true)
        {
            // Handle nullable types
            if (type is INamedTypeSymbol namedType && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                type = namedType.TypeArguments[0];
                continue;
            }

            // Primitives and enums
            if (type.IsValueType || type.TypeKind == TypeKind.Enum)
            {
                var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    .Replace("global::", ""); // SafeTypeCatalog doesn't use global:: prefix

                if (SafeTypeCatalog.SafeTypeNames.Contains(fullName)) return true;

                // Check if it's a simple value type (all fields are safe)
                if (type.IsValueType && !type.IsReferenceType)
                {
                    return IsSimpleValueType(type, compilation);
                }
            }

            // String is safe (immutable)
            if (type.SpecialType == SpecialType.System_String) return true;

            return false;
        }
    }

    private static bool IsSimpleValueType(ITypeSymbol type, Compilation compilation)
    {
        // Check all fields recursively
        foreach (var member in type.GetMembers())
        {
            if (member is IFieldSymbol field && !field.IsStatic && !field.IsConst)
            {
                if (!IsSafeType(field.Type, compilation))
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
        // Check if type itself is IEnumerable<T>
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
        {
            if (type is INamedTypeSymbol namedType) return namedType.TypeArguments[0];
        }

        // Generic IEnumerable<T> (most generic collections implement this)
        var genericEnumerable = type.AllInterfaces.FirstOrDefault(i =>
            i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);

        if (genericEnumerable != null)
        {
            return genericEnumerable.TypeArguments[0];
        }

        // Non-generic ICollection
        var nonGenericCollection = type.AllInterfaces.FirstOrDefault(i =>
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
        var dictInterface = type.AllInterfaces.FirstOrDefault(i =>
            i.MetadataName == "IDictionary`2" &&
            i.ContainingNamespace.ToDisplayString() == "System.Collections.Generic");

        if (dictInterface != null)
        {
            return (dictInterface.TypeArguments[0], dictInterface.TypeArguments[1]);
        }

        // IReadOnlyDictionary<TKey, TValue>
        var roDictInterface = type.AllInterfaces.FirstOrDefault(i =>
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
             if (type is INamedTypeSymbol namedType && namedType.TypeArguments.Length == 2)
             {
                 return (namedType.TypeArguments[0], namedType.TypeArguments[1]);
             }
        }

        // Non-generic IDictionary
        var nonGenericDict = type.AllInterfaces.FirstOrDefault(i =>
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
            .Any(a => a.AttributeClass?.ToDisplayString() == "FastCloner.SourceGenerator.Shared.FastClonerClonableAttribute");
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
        var nonNullableType = type.WithNullableAnnotation(NullableAnnotation.None);
        
        // Use a format with global:: prefix to avoid namespace conflicts
        var format = new SymbolDisplayFormat(
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
        if (!namedType.IsReferenceType && !namedType.IsValueType)
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
        var constraints = new List<string>();
        foreach (var tp in symbol.TypeParameters)
        {
            var constraintParts = new List<string>();
            
            if (tp.HasReferenceTypeConstraint)
                constraintParts.Add("class");
            if (tp.HasValueTypeConstraint)
                constraintParts.Add("struct");
            if (tp.HasUnmanagedTypeConstraint)
                constraintParts.Add("unmanaged");
            if (tp.HasNotNullConstraint)
                constraintParts.Add("notnull");
            
            foreach (var constraintType in tp.ConstraintTypes)
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
            var baseType = symbol.BaseType;
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
}
