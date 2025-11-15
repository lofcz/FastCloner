using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace FastCloner.SourceGenerator;

/// <summary>
/// Utility class for analyzing types during code generation.
/// </summary>
internal static class TypeAnalyzer
{
    private static readonly HashSet<string> SafeTypeNames = new()
    {
        "System.Byte", "System.SByte", "System.Int16", "System.UInt16",
        "System.Int32", "System.UInt32", "System.Int64", "System.UInt64",
        "System.Single", "System.Double", "System.Decimal",
        "System.String", "System.Char", "System.Boolean",
        "System.Guid", "System.TimeSpan", "System.DateTime", "System.DateTimeOffset",
        "System.IntPtr", "System.UIntPtr", "System.DBNull"
    };

    /// <summary>
    /// Determines if a type is safe to copy directly without cloning.
    /// </summary>
    public static bool IsSafeType(ITypeSymbol type, Compilation compilation)
    {
        // Handle nullable types
        if (type is INamedTypeSymbol namedType && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return IsSafeType(namedType.TypeArguments[0], compilation);
        }

        // Primitives and enums
        if (type.IsValueType || type.TypeKind == TypeKind.Enum)
        {
            var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (SafeTypeNames.Contains(fullName))
                return true;

            // Check if it's a simple value type (all fields are safe)
            if (type.IsValueType && !type.IsReferenceType)
            {
                return IsSimpleValueType(type, compilation);
            }
        }

        // String is safe (immutable)
        if (type.SpecialType == SpecialType.System_String)
            return true;

        return false;
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
        return type.AllInterfaces.Any(i =>
            i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_ICollection_T ||
            (i.MetadataName == "ICollection" &&
             i.ContainingNamespace.ToDisplayString() == "System.Collections"));
    }

    /// <summary>
    /// Checks if a type is a dictionary type.
    /// </summary>
    public static bool IsDictionaryType(ITypeSymbol type)
    {
        return type.AllInterfaces.Any(i =>
            (i.MetadataName == "IDictionary`2" &&
             i.ContainingNamespace.ToDisplayString() == "System.Collections.Generic") ||
            (i.MetadataName == "IDictionary" &&
             i.ContainingNamespace.ToDisplayString() == "System.Collections"));
    }

    /// <summary>
    /// Gets the element type of a collection.
    /// </summary>
    public static ITypeSymbol? GetCollectionElementType(ITypeSymbol type, Compilation compilation)
    {
        // Generic ICollection<T>
        var genericCollection = type.AllInterfaces.FirstOrDefault(i =>
            i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_ICollection_T);

        if (genericCollection != null)
        {
            return genericCollection.TypeArguments[0];
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
        var dictInterface = type.AllInterfaces.FirstOrDefault(i =>
            i.MetadataName == "IDictionary`2" &&
            i.ContainingNamespace.ToDisplayString() == "System.Collections.Generic");

        if (dictInterface != null)
        {
            return (dictInterface.TypeArguments[0], dictInterface.TypeArguments[1]);
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
}

