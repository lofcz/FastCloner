using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace FastCloner.SourceGenerator;

internal static class StateRequirementAnalyzer
{
    public readonly record struct AnalysisResult(
        bool HasCircularRefs,
        bool NeedsStateTracking
    );
    
    public static AnalysisResult Analyze(INamedTypeSymbol rootType, Compilation compilation, List<string> log, bool? preserveIdentity = null)
    {
        log.Add($"=== Analyzing state requirements for {rootType.ToDisplayString()} ===");
        log.Add($"  -> PreserveIdentity attribute: {preserveIdentity?.ToString() ?? "not set"}");
        
        if (rootType.IsValueType)
        {
            log.Add("  -> Type is a struct (value type), no state needed");
            return new AnalysisResult(false, false);
        }
        
        Dictionary<ITypeSymbol, int> typeOccurrences = new(SymbolEqualityComparer.Default);
        HashSet<ITypeSymbol> visited = new(SymbolEqualityComparer.Default);
        HashSet<ITypeSymbol> typesInCollections = new(SymbolEqualityComparer.Default);
        
        log.Add($"  -> Collecting reachable reference types from {rootType.Name} members...");
        
        bool hasDirectSelfReference = HasDirectSelfReference(rootType, rootType, log);
        
        CollectReachableTypes(rootType, typeOccurrences, visited, typesInCollections, compilation, log, rootType);
        
        log.Add($"  -> Found {typeOccurrences.Count} reachable reference types:");
        foreach (KeyValuePair<ITypeSymbol, int> kvp in typeOccurrences)
        {
            log.Add($"     - {kvp.Key.ToDisplayString()} (occurrences: {kvp.Value})");
        }
        
        bool canReferenceBack = typeOccurrences.Keys.Any(t => CanReferenceType(t, rootType, compilation, log));
        bool hasCircularRefs = hasDirectSelfReference || canReferenceBack;
        
        log.Add($"  -> Direct self-reference: {hasDirectSelfReference}");
        log.Add($"  -> Can reference back: {canReferenceBack}");
        log.Add($"  -> Has circular refs: {hasCircularRefs}");
        
        bool needsIdentityPreservation = preserveIdentity == true;
        
        log.Add($"  -> Identity preservation requested: {needsIdentityPreservation}");
        
        bool needsStateTracking = hasCircularRefs || needsIdentityPreservation;
        log.Add($"  -> Final result: NeedsStateTracking = {needsStateTracking}");
        
        return new AnalysisResult(hasCircularRefs, needsStateTracking);
    }
    
    private static bool HasDirectSelfReference(INamedTypeSymbol type, INamedTypeSymbol rootType, List<string> log)
    {
        foreach (ISymbol? member in type.GetMembers())
        {
            if (member.IsStatic || member.IsImplicitlyDeclared)
                continue;
            
            ITypeSymbol? memberType = null;
            if (member is IPropertySymbol { GetMethod: not null, SetMethod: not null } property)
            {
                memberType = property.Type;
            }
            else if (member is IFieldSymbol { IsConst: false, IsStatic: false } field)
            {
                memberType = field.Type;
            }
            
            if (memberType != null)
            {
                ITypeSymbol underlyingMemberType = memberType.WithNullableAnnotation(NullableAnnotation.None);
                if (SymbolEqualityComparer.Default.Equals(underlyingMemberType, rootType))
                {
                    log.Add($"  -> Found direct self-reference: {member.Name} of type {rootType.Name}");
                    return true;
                }
            }
        }
        
        return false;
    }
    
    private static void CollectReachableTypes(
        INamedTypeSymbol type,
        Dictionary<ITypeSymbol, int> typeOccurrences,
        HashSet<ITypeSymbol> visited,
        HashSet<ITypeSymbol> typesInCollections,
        Compilation compilation,
        List<string> log,
        INamedTypeSymbol rootType)
    {
        foreach (ISymbol? member in type.GetMembers())
        {
            if (member.IsStatic || member.IsImplicitlyDeclared)
                continue;
            
            ITypeSymbol? memberType = null;
            if (member is IPropertySymbol { GetMethod: not null, SetMethod: not null } property)
            {
                memberType = property.Type;
                log.Add($"     Analyzing property {member.Name}: {memberType.ToDisplayString()}");
            }
            else if (member is IFieldSymbol { IsConst: false, IsStatic: false } field)
            {
                memberType = field.Type;
                log.Add($"     Analyzing field {member.Name}: {memberType.ToDisplayString()}");
            }
            
            if (memberType != null)
            {
                CollectType(memberType, typeOccurrences, visited, typesInCollections, compilation, log, rootType, isInCollection: false);
            }
        }
    }
    
    private static void CollectType(
        ITypeSymbol type, 
        Dictionary<ITypeSymbol, int> typeOccurrences, 
        HashSet<ITypeSymbol> visited,
        HashSet<ITypeSymbol> typesInCollections,
        Compilation compilation,
        List<string> log,
        INamedTypeSymbol rootType,
        bool isInCollection)
    {
        string typeDisplayName = type.ToDisplayString();
        
        if (type.IsValueType && !TypeAnalyzer.IsCollectionType(type) && !TypeAnalyzer.IsDictionaryType(type))
        {
            log.Add($"     [SKIP] {typeDisplayName} (value type)");
            return;
        }
        
        if (TypeAnalyzer.IsSafeType(type, compilation))
        {
            log.Add($"     [SKIP] {typeDisplayName} (safe type - primitive/string)");
            return;
        }
        
        ITypeSymbol underlyingType = type.WithNullableAnnotation(NullableAnnotation.None);

        if (TypeAnalyzer.IsCollectionType(type))
        {
            ITypeSymbol? elementType = TypeAnalyzer.GetCollectionElementType(type, compilation);
            if (elementType != null)
            {
                log.Add($"        -> Collection element type: {elementType.ToDisplayString()}");
                typesInCollections.Add(elementType.WithNullableAnnotation(NullableAnnotation.None));
                CollectType(elementType, typeOccurrences, visited, typesInCollections, compilation, log, rootType, isInCollection: true);
            }
            return;
        }
        
        if (TypeAnalyzer.IsDictionaryType(type))
        {
            (ITypeSymbol KeyType, ITypeSymbol ValueType)? dictTypes = TypeAnalyzer.GetDictionaryTypes(type, compilation);
            if (dictTypes.HasValue)
            {
                log.Add($"        -> Dictionary key type: {dictTypes.Value.KeyType.ToDisplayString()}");
                typesInCollections.Add(dictTypes.Value.KeyType.WithNullableAnnotation(NullableAnnotation.None));
                CollectType(dictTypes.Value.KeyType, typeOccurrences, visited, typesInCollections, compilation, log, rootType, isInCollection: true);
                
                log.Add($"        -> Dictionary value type: {dictTypes.Value.ValueType.ToDisplayString()}");
                typesInCollections.Add(dictTypes.Value.ValueType.WithNullableAnnotation(NullableAnnotation.None));
                CollectType(dictTypes.Value.ValueType, typeOccurrences, visited, typesInCollections, compilation, log, rootType, isInCollection: true);
            }
            return;
        }
        
        if (type is IArrayTypeSymbol arrayType)
        {
            ITypeSymbol elementType = arrayType.ElementType;
            log.Add($"        -> Array element type: {elementType.ToDisplayString()}");
            typesInCollections.Add(elementType.WithNullableAnnotation(NullableAnnotation.None));
            CollectType(elementType, typeOccurrences, visited, typesInCollections, compilation, log, rootType, isInCollection: true);
            return;
        }
        
        if (underlyingType is INamedTypeSymbol namedType)
        {
            if (typeOccurrences.TryGetValue(namedType, out int count))
            {
                typeOccurrences[namedType] = count + 1;
                log.Add($"     [COUNT] {typeDisplayName} now has {count + 1} occurrences");
            }
            else
            {
                typeOccurrences[namedType] = 1;
                log.Add($"     [ADD] {typeDisplayName}");
            }
            
            if (isInCollection)
            {
                typesInCollections.Add(namedType);
            }

            if (visited.Add(namedType) && TypeAnalyzer.HasClonableAttribute(namedType))
            {
                log.Add($"        -> Analyzing members of {typeDisplayName}...");
                CollectReachableTypes(namedType, typeOccurrences, visited, typesInCollections, compilation, log, rootType);
            }
        }
    }
    
    private static bool CanReferenceType(ITypeSymbol type, INamedTypeSymbol target, Compilation compilation, List<string> log)
    {
        log.Add($"     [CHECK] Checking if {type.ToDisplayString()} can reference {target.ToDisplayString()}...");
        
        if (type is not INamedTypeSymbol namedType || !TypeAnalyzer.HasClonableAttribute(namedType))
        {
            return false;
        }
        
        foreach (ISymbol? member in namedType.GetMembers())
        {
            if (member.IsStatic || member.IsImplicitlyDeclared)
                continue;
            
            ITypeSymbol? memberType = null;
            if (member is IPropertySymbol { GetMethod: not null, SetMethod: not null } property)
            {
                memberType = property.Type;
            }
            else if (member is IFieldSymbol { IsConst: false, IsStatic: false } field)
            {
                memberType = field.Type;
            }
            
            if (memberType != null)
            {
                ITypeSymbol underlyingType = memberType.WithNullableAnnotation(NullableAnnotation.None);
                if (SymbolEqualityComparer.Default.Equals(underlyingType, target))
                {
                    log.Add($"        [MATCH] {member.Name} references {target.ToDisplayString()}");
                    return true;
                }
                
                if (TypeAnalyzer.IsCollectionType(memberType))
                {
                    ITypeSymbol? elementType = TypeAnalyzer.GetCollectionElementType(memberType, compilation);
                    if (elementType != null)
                    {
                        ITypeSymbol underlyingElementType = elementType.WithNullableAnnotation(NullableAnnotation.None);
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
