using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace FastCloner.SourceGenerator;

/// <summary>
/// Represents the member-level clone behavior as determined by attributes.
/// Mirrors FastCloner.Code.CloneBehavior enum values.
/// </summary>
internal enum MemberCloneBehavior
{
    Clone = 0,      // Default: deep clone
    Reference = 1,  // Copy reference directly
    Shallow = 2,    // MemberwiseClone (treated same as Reference for members)
    Ignore = 3      // Skip, set to default
}

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
                            MemberCloneBehavior behavior = GetMemberBehavior(property, compilation);
                            if (behavior != MemberCloneBehavior.Ignore)
                            {
                                members.Add(new MemberAnalysis(MemberModel.Create(property, nullabilityEnabled, compilation, behavior), property.Type));
                            }
                        }
                    }
                }
                else if (member is IFieldSymbol field)
                {
                    if (field.IsConst) continue; // Skip const fields
                    
                    MemberCloneBehavior behavior = GetMemberBehavior(field, compilation);
                    if (behavior != MemberCloneBehavior.Ignore)
                    {
                        members.Add(new MemberAnalysis(MemberModel.Create(field, nullabilityEnabled, compilation, behavior), field.Type));
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

    /// <summary>
    /// Gets the clone behavior for a type by checking for FastClonerBehaviorAttribute on the type definition.
    /// </summary>
    private static MemberCloneBehavior? GetTypeBehavior(ITypeSymbol type, Compilation compilation)
    {
        INamedTypeSymbol? behaviorAttribute = compilation.GetTypeByMetadataName("FastCloner.Code.FastClonerBehaviorAttribute");
        INamedTypeSymbol? ignoreAttribute = compilation.GetTypeByMetadataName("FastCloner.Code.FastClonerIgnoreAttribute");
        INamedTypeSymbol? shallowAttribute = compilation.GetTypeByMetadataName("FastCloner.Code.FastClonerShallowAttribute");
        INamedTypeSymbol? referenceAttribute = compilation.GetTypeByMetadataName("FastCloner.Code.FastClonerReferenceAttribute");

        foreach (AttributeData attr in type.GetAttributes())
        {
            INamedTypeSymbol? attrClass = attr.AttributeClass;
            if (attrClass == null)
                continue;

            if (ignoreAttribute != null && SymbolEqualityComparer.Default.Equals(attrClass, ignoreAttribute))
            {
                return attr.ConstructorArguments.Length switch
                {
                    0 => MemberCloneBehavior.Ignore,
                    > 0 when attr.ConstructorArguments[0].Value is bool ignored => ignored ? MemberCloneBehavior.Ignore : null,
                    _ => MemberCloneBehavior.Ignore
                };
            }

            if (shallowAttribute != null && SymbolEqualityComparer.Default.Equals(attrClass, shallowAttribute))
            {
                return MemberCloneBehavior.Shallow;
            }

            if (referenceAttribute != null && SymbolEqualityComparer.Default.Equals(attrClass, referenceAttribute))
            {
                return MemberCloneBehavior.Reference;
            }

            if (behaviorAttribute != null && SymbolEqualityComparer.Default.Equals(attrClass, behaviorAttribute))
            {
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int behaviorInt)
                {
                    return (MemberCloneBehavior)behaviorInt;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the clone behavior for a member by checking:
    /// 1. Member-level FastClonerBehaviorAttribute (highest priority)
    /// 2. [NonSerialized] attribute (treat as Ignore)
    /// 3. Type-level FastClonerBehaviorAttribute on the member's type (lowest priority)
    /// </summary>
    private static MemberCloneBehavior GetMemberBehavior(ISymbol member, ITypeSymbol memberType, Compilation compilation)
    {
        // Get all relevant attribute types
        INamedTypeSymbol? behaviorAttribute = compilation.GetTypeByMetadataName("FastCloner.Code.FastClonerBehaviorAttribute");
        INamedTypeSymbol? ignoreAttribute = compilation.GetTypeByMetadataName("FastCloner.Code.FastClonerIgnoreAttribute");
        INamedTypeSymbol? shallowAttribute = compilation.GetTypeByMetadataName("FastCloner.Code.FastClonerShallowAttribute");
        INamedTypeSymbol? referenceAttribute = compilation.GetTypeByMetadataName("FastCloner.Code.FastClonerReferenceAttribute");
        INamedTypeSymbol? nonSerializedAttribute = compilation.GetTypeByMetadataName("System.NonSerializedAttribute");

        // 1. Check for member-level attributes first (highest priority)
        foreach (AttributeData attr in member.GetAttributes())
        {
            INamedTypeSymbol? attrClass = attr.AttributeClass;
            if (attrClass == null)
                continue;

            // Check for specific derived attributes first (shorthand attributes)
            if (ignoreAttribute != null && SymbolEqualityComparer.Default.Equals(attrClass, ignoreAttribute))
            {
                return attr.ConstructorArguments.Length switch
                {
                    // Check if Ignored property is true (default is true)
                    0 => MemberCloneBehavior.Ignore,
                    > 0 when attr.ConstructorArguments[0].Value is bool ignored => ignored ? MemberCloneBehavior.Ignore : MemberCloneBehavior.Clone,
                    _ => MemberCloneBehavior.Ignore
                };
            }

            if (shallowAttribute != null && SymbolEqualityComparer.Default.Equals(attrClass, shallowAttribute))
            {
                return MemberCloneBehavior.Shallow;
            }

            if (referenceAttribute != null && SymbolEqualityComparer.Default.Equals(attrClass, referenceAttribute))
            {
                return MemberCloneBehavior.Reference;
            }

            // Check for base FastClonerBehaviorAttribute with explicit behavior parameter
            if (behaviorAttribute != null && SymbolEqualityComparer.Default.Equals(attrClass, behaviorAttribute))
            {
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int behaviorInt)
                {
                    return (MemberCloneBehavior)behaviorInt;
                }
            }

            // Check for [NonSerialized] - treat as Ignore
            if (nonSerializedAttribute != null && SymbolEqualityComparer.Default.Equals(attrClass, nonSerializedAttribute))
            {
                return MemberCloneBehavior.Ignore;
            }
        }

        // 2. Check for type-level attribute on the member's type
        MemberCloneBehavior? typeBehavior = GetTypeBehavior(memberType, compilation);
        
        return typeBehavior ?? MemberCloneBehavior.Clone;
    }

    /// <summary>
    /// Gets the clone behavior for a member (overload for backward compatibility).
    /// </summary>
    private static MemberCloneBehavior GetMemberBehavior(ISymbol member, Compilation compilation)
    {
        ITypeSymbol? memberType = member switch
        {
            IFieldSymbol f => f.Type,
            IPropertySymbol p => p.Type,
            IEventSymbol e => e.Type,
            _ => null
        };

        return memberType != null 
            ? GetMemberBehavior(member, memberType, compilation) 
            : MemberCloneBehavior.Clone;
    }
}

