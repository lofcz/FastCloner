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
        List<MemberAnalysis> members = [];
        HashSet<string> seenNames = [];

        // Get all members from base types too (walking up the inheritance chain)
        INamedTypeSymbol? currentType = symbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (ISymbol? member in currentType.GetMembers())
            {
                if (member.IsStatic || member.IsImplicitlyDeclared)
                    continue;

                // Skip if we've already seen this member name (base class members override derived)
                if (!seenNames.Add(member.Name))
                    continue;

                if (member is IPropertySymbol property)
                {
                    if (property is { GetMethod: not null, IsIndexer: false })
                    {
                        // Check if property has an accessible setter
                        bool hasAccessibleSetter = property.SetMethod != null &&
                            (property.SetMethod.DeclaredAccessibility == Accessibility.Public ||
                             property.SetMethod.DeclaredAccessibility == Accessibility.Internal ||
                             property.SetMethod.DeclaredAccessibility == Accessibility.ProtectedOrInternal);
                        
                        // For getter-only properties, we can still clone if:
                        // 1. It's a collection type that supports population (Add/Clear)
                        // 2. The property returns a non-null collection instance
                        bool isPopulatableCollection = IsPopulatableCollectionType(property.Type);
                        
                        // Include property if it has an accessible setter OR it's a getter-only populatable collection
                        if (hasAccessibleSetter || isPopulatableCollection)
                        {
                            if (!HasIgnoreAttribute(property, compilation))
                            {
                                members.Add(new MemberAnalysis(MemberModel.Create(property, nullabilityEnabled, compilation), property.Type));
                            }
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

    /// <summary>
    /// Checks if a type is a collection that can be populated via Add/Clear methods.
    /// This allows getter-only collection properties to be cloned by clearing and repopulating.
    /// </summary>
    private static bool IsPopulatableCollectionType(ITypeSymbol type)
    {
        // Arrays cannot be populated (fixed size)
        if (type is IArrayTypeSymbol)
            return false;
        
        // Check for common populatable collection types
        string typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        
        // Check if it's a generic type
        if (type is INamedTypeSymbol { IsGenericType: true } namedType)
        {
            string originalDef = namedType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            
            // List of populatable collection types (can use Clear + Add pattern)
            string[] populatableTypes =
            [
                "global::System.Collections.Generic.List<T>",
                "global::System.Collections.Generic.HashSet<T>",
                "global::System.Collections.Generic.LinkedList<T>",
                "global::System.Collections.Generic.Queue<T>",
                "global::System.Collections.Generic.Stack<T>",
                "global::System.Collections.Generic.SortedSet<T>",
                "global::System.Collections.ObjectModel.Collection<T>",
                "global::System.Collections.ObjectModel.ObservableCollection<T>",
                "global::System.Collections.Concurrent.ConcurrentBag<T>",
                "global::System.Collections.Concurrent.ConcurrentQueue<T>",
                "global::System.Collections.Concurrent.ConcurrentStack<T>",
                // Dictionaries
                "global::System.Collections.Generic.Dictionary<TKey, TValue>",
                "global::System.Collections.Generic.SortedDictionary<TKey, TValue>",
                "global::System.Collections.Generic.SortedList<TKey, TValue>",
                "global::System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue>"
            ];
            
            foreach (string populatable in populatableTypes)
            {
                if (originalDef == populatable)
                    return true;
            }
        }
        
        // Also check if the type implements ICollection<T> and has Add method
        // This covers custom collection types
        foreach (INamedTypeSymbol? iface in type.AllInterfaces)
        {
            if (iface.IsGenericType)
            {
                string ifaceDef = iface.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (ifaceDef == "global::System.Collections.Generic.ICollection<T>")
                {
                    // Has ICollection<T>, check if it's not read-only
                    // ICollection<T>.IsReadOnly would need runtime check, so we just allow it
                    // The generated code will handle runtime failures gracefully
                    return true;
                }
            }
        }
        
        return false;
    }

    private static bool HasIgnoreAttribute(ISymbol member, Compilation compilation)
    {
        INamedTypeSymbol? ignoreAttribute = compilation.GetTypeByMetadataName("FastCloner.Code.FastClonerIgnoreAttribute");
        INamedTypeSymbol? nonSerializedAttribute = compilation.GetTypeByMetadataName("System.NonSerializedAttribute");

        foreach (AttributeData? attr in member.GetAttributes())
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
