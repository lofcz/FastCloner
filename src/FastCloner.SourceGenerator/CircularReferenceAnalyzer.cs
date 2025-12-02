using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace FastCloner.SourceGenerator;

internal static class CircularReferenceAnalyzer
{
    public static bool Analyze(INamedTypeSymbol rootType, Compilation compilation, List<string> log)
    {
        log.Add($"=== Analyzing circular references for {rootType.ToDisplayString()} ===");
        
        // Structs can't have circular references (value types)
        if (rootType.IsValueType)
        {
            log.Add("  -> Type is a struct (value type), cannot have circular references");
            return false;
        }
        
        // Build a set of all reference types that can be reached from this type's MEMBERS
        var reachableTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        
        log.Add($"  -> Collecting reachable reference types from {rootType.Name} members...");
        
        // First check if rootType has a direct self-reference
        bool hasDirectSelfReference = HasDirectSelfReference(rootType, rootType, compilation, log);
        
        // Then collect types reachable from members
        CollectReachableReferenceTypesFromMembers(rootType, reachableTypes, visited, compilation, log, rootType);
        
        log.Add($"  -> Found {reachableTypes.Count} reachable reference types:");
        foreach (var type in reachableTypes)
        {
            log.Add($"     - {type.ToDisplayString()}");
        }
        
        // Check if any of the reachable types can reference back to rootType
        bool canReferenceBack = reachableTypes.Any(t => CanReferenceType(t, rootType, compilation, log));
        
        log.Add($"  -> Direct self-reference check: {hasDirectSelfReference}");
        log.Add($"  -> Can reference back check: {canReferenceBack}");
        
        bool result = hasDirectSelfReference || canReferenceBack;
        log.Add($"  -> Final result: {(result ? "CAN have circular references" : "CANNOT have circular references")}");
        
        return result;
    }
    
    private static bool HasDirectSelfReference(INamedTypeSymbol type, INamedTypeSymbol rootType, Compilation compilation, List<string> log)
    {
        foreach (var member in type.GetMembers())
        {
            if (member.IsStatic || member.IsImplicitlyDeclared)
                continue;
            
            ITypeSymbol? memberType = null;
            if (member is IPropertySymbol property && property.GetMethod != null && property.SetMethod != null)
            {
                memberType = property.Type;
            }
            else if (member is IFieldSymbol field && !field.IsConst && !field.IsStatic)
            {
                memberType = field.Type;
            }
            
            if (memberType != null)
            {
                var underlyingMemberType = memberType.WithNullableAnnotation(NullableAnnotation.None);
                if (SymbolEqualityComparer.Default.Equals(underlyingMemberType, rootType))
                {
                    log.Add($"  -> Found direct self-reference: {member.Name} of type {rootType.Name}");
                    return true;
                }
            }
        }
        
        return false;
    }
    
    private static void CollectReachableReferenceTypesFromMembers(
        INamedTypeSymbol type,
        HashSet<ITypeSymbol> reachableTypes,
        HashSet<ITypeSymbol> visited,
        Compilation compilation,
        List<string> log,
        INamedTypeSymbol rootType)
    {
        foreach (var member in type.GetMembers())
        {
            if (member.IsStatic || member.IsImplicitlyDeclared)
                continue;
            
            ITypeSymbol? memberType = null;
            if (member is IPropertySymbol property && property.GetMethod != null && property.SetMethod != null)
            {
                memberType = property.Type;
                log.Add($"     Analyzing property {member.Name}: {memberType.ToDisplayString()}");
            }
            else if (member is IFieldSymbol field && !field.IsConst && !field.IsStatic)
            {
                memberType = field.Type;
                log.Add($"     Analyzing field {member.Name}: {memberType.ToDisplayString()}");
            }
            
            if (memberType != null)
            {
                CollectReachableReferenceTypes(memberType, reachableTypes, visited, compilation, log, rootType);
            }
        }
    }
    
    private static void CollectReachableReferenceTypes(
        ITypeSymbol type, 
        HashSet<ITypeSymbol> reachableTypes, 
        HashSet<ITypeSymbol> visited,
        Compilation compilation,
        List<string> log,
        INamedTypeSymbol rootType)
    {
        var typeDisplayName = type.ToDisplayString();
        if (!visited.Add(type))
        {
            log.Add($"     [SKIP] {typeDisplayName} (already visited)");
            return;
        }
        
        if (type.IsValueType)
        {
            log.Add($"     [SKIP] {typeDisplayName} (value type)");
            return;
        }
        
        if (TypeAnalyzer.IsSafeType(type, compilation))
        {
            log.Add($"     [SKIP] {typeDisplayName} (safe type - primitive/string)");
            return;
        }
        
        var underlyingType = type.WithNullableAnnotation(NullableAnnotation.None);
        
        if (underlyingType is INamedTypeSymbol namedType)
        {
            if (!SymbolEqualityComparer.Default.Equals(namedType, rootType))
            {
                log.Add($"     [ADD] {typeDisplayName}");
                reachableTypes.Add(namedType);
            }
            else
            {
                log.Add($"     [SKIP] {typeDisplayName} (is root type)");
            }
            
            if (TypeAnalyzer.HasClonableAttribute(namedType))
            {
                log.Add($"        -> Analyzing members of {typeDisplayName}...");
                CollectReachableReferenceTypesFromMembers(namedType, reachableTypes, visited, compilation, log, rootType);
            }
        }
        else if (TypeAnalyzer.IsCollectionType(type))
        {
            var elementType = TypeAnalyzer.GetCollectionElementType(type, compilation);
            if (elementType != null)
            {
                log.Add($"        -> Collection element type: {elementType.ToDisplayString()}");
                CollectReachableReferenceTypes(elementType, reachableTypes, visited, compilation, log, rootType);
            }
        }
        else if (TypeAnalyzer.IsDictionaryType(type))
        {
            var dictTypes = TypeAnalyzer.GetDictionaryTypes(type, compilation);
            if (dictTypes.HasValue)
            {
                log.Add($"        -> Dictionary key type: {dictTypes.Value.KeyType.ToDisplayString()}");
                CollectReachableReferenceTypes(dictTypes.Value.KeyType, reachableTypes, visited, compilation, log, rootType);
                log.Add($"        -> Dictionary value type: {dictTypes.Value.ValueType.ToDisplayString()}");
                CollectReachableReferenceTypes(dictTypes.Value.ValueType, reachableTypes, visited, compilation, log, rootType);
            }
        }
        else if (type is IArrayTypeSymbol arrayType)
        {
            var elementType = arrayType.ElementType;
            log.Add($"        -> Array element type: {elementType.ToDisplayString()}");
            CollectReachableReferenceTypes(elementType, reachableTypes, visited, compilation, log, rootType);
        }
    }
    
    private static bool CanReferenceType(ITypeSymbol type, INamedTypeSymbol target, Compilation compilation, List<string> log)
    {
        log.Add($"     [CHECK] Checking if {type.ToDisplayString()} can reference {target.ToDisplayString()}...");
        
        if (type is not INamedTypeSymbol namedType || !TypeAnalyzer.HasClonableAttribute(namedType))
        {
            return false;
        }
        
        foreach (var member in namedType.GetMembers())
        {
            if (member.IsStatic || member.IsImplicitlyDeclared)
                continue;
            
            ITypeSymbol? memberType = null;
            if (member is IPropertySymbol property && property.GetMethod != null && property.SetMethod != null)
            {
                memberType = property.Type;
            }
            else if (member is IFieldSymbol field && !field.IsConst && !field.IsStatic)
            {
                memberType = field.Type;
            }
            
            if (memberType != null)
            {
                var underlyingType = memberType.WithNullableAnnotation(NullableAnnotation.None);
                if (SymbolEqualityComparer.Default.Equals(underlyingType, target))
                {
                    log.Add($"        [MATCH] {member.Name} references {target.ToDisplayString()}");
                    return true;
                }
                
                if (TypeAnalyzer.IsCollectionType(memberType))
                {
                    var elementType = TypeAnalyzer.GetCollectionElementType(memberType, compilation);
                    if (elementType != null)
                    {
                        var underlyingElementType = elementType.WithNullableAnnotation(NullableAnnotation.None);
                        if (SymbolEqualityComparer.Default.Equals(underlyingElementType, target))
                        {
                            log.Add($"        [MATCH] Collection {member.Name} contains {target.ToDisplayString()}");
                            return true;
                        }
                    }
                }
            }
        }
        
        log.Add($"        [NO MATCH] No members reference {target.ToDisplayString()}");
        return false;
    }
}
