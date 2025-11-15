using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace FastCloner.SourceGenerator;

/// <summary>
/// Represents a type model for code generation.
/// CRITICAL: This is a record for proper equality comparison to enable incremental caching.
/// It stores NO Roslyn symbols (ISymbol, Syntax nodes) as they break caching.
/// </summary>
internal sealed record TypeModel(
    string Namespace,
    string Name,
    string FullyQualifiedName,
    bool IsStruct,
    bool IsSealed,
    bool HasClonableBaseClass,
    bool CanHaveCircularReferences,
    bool IsFastClonerAvailable,
    EquatableArray<MemberModel> Members,
    bool NullabilityEnabled) : IEquatable<TypeModel>
{
    // All data extraction from ISymbol happens in TryCreate below
    // The record stores only strings, primitives, and equatable collections for proper caching
    
    public static bool TryCreate(
        INamedTypeSymbol symbol,
        bool nullabilityEnabled,
        Compilation compilation,
        out TypeModel? model,
        out Diagnostic? error)
    {
        model = null;
        error = null;

        if (symbol.IsAbstract)
        {
            error = Diagnostic.Create(
                new DiagnosticDescriptor(
                    "FCG002",
                    "Abstract types cannot be cloned",
                    "Type '{0}' is abstract and cannot be marked with [FastClonerClonable]",
                    "FastCloner",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                symbol.Locations.FirstOrDefault() ?? Location.None,
                symbol.ToDisplayString());
            return false;
        }

        var members = GetMembers(symbol, compilation, nullabilityEnabled);
        var flags = GetFlags(symbol);
        
        // Analyze circular references at compile-time (we have the symbol here)
        var circRefLog = new List<string>();
        var canHaveCircularRefs = AnalyzeCircularReferences(symbol, compilation, circRefLog);
        
        // Check if FastCloner library is available
        var isFastClonerAvailable = compilation.GetTypeByMetadataName("FastCloner.FastCloner") != null;

        model = new TypeModel(
            GetNamespace(symbol),
            symbol.Name,
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            flags.IsStruct,
            flags.IsSealed,
            flags.HasClonableBaseClass,
            canHaveCircularRefs,
            isFastClonerAvailable,
            new EquatableArray<MemberModel>(members.ToArray()),
            nullabilityEnabled);

        return true;
    }

    private static List<MemberModel> GetMembers(
        INamedTypeSymbol symbol,
        Compilation compilation,
        bool nullabilityEnabled)
    {
        var members = new List<MemberModel>();
        var seenNames = new HashSet<string>();

        // Get all members from base types too (walking up the inheritance chain)
        var currentType = symbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in currentType.GetMembers())
            {
                if (member.IsStatic || member.IsImplicitlyDeclared)
                    continue;

                // Skip if we've already seen this member name (base class members override derived)
                if (!seenNames.Add(member.Name))
                    continue;

                if (member is IPropertySymbol property)
                {
                    if (property.GetMethod != null && property.SetMethod != null && !property.IsIndexer)
                    {
                        if (!HasIgnoreAttribute(property, compilation))
                        {
                            members.Add(MemberModel.Create(property, nullabilityEnabled, compilation));
                        }
                    }
                }
                else if (member is IFieldSymbol field)
                {
                    if (!field.IsConst && !HasIgnoreAttribute(field, compilation))
                    {
                        members.Add(MemberModel.Create(field, nullabilityEnabled, compilation));
                    }
                }
            }

            currentType = currentType.BaseType;
        }

        return members;
    }

    private static bool HasIgnoreAttribute(ISymbol member, Compilation compilation)
    {
        var ignoreAttribute = compilation.GetTypeByMetadataName("FastCloner.Code.FastClonerIgnoreAttribute");
        var nonSerializedAttribute = compilation.GetTypeByMetadataName("System.NonSerializedAttribute");

        foreach (var attr in member.GetAttributes())
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

    private static (bool IsStruct, bool IsSealed, bool HasClonableBaseClass) GetFlags(INamedTypeSymbol symbol)
    {
        bool isStruct = symbol.IsValueType;
        bool isSealed = symbol.IsSealed;
        bool hasClonableBaseClass = false;

        if (!isStruct)
        {
            var baseType = symbol.BaseType;
            while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
            {
                var clonableAttr = baseType.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "FastCloner.SourceGenerator.Shared.FastClonerClonableAttribute");
                
                if (clonableAttr != null)
                {
                    hasClonableBaseClass = true;
                    break;
                }

                baseType = baseType.BaseType;
            }
        }

        return (isStruct, isSealed, hasClonableBaseClass);
    }

    private static string GetNamespace(INamedTypeSymbol symbol)
    {
        return symbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : symbol.ContainingNamespace.ToDisplayString();
    }

    // NOTE: Circular reference analysis methods copied from CloneCodeGenerator
    // These run during TypeModel creation when we still have access to ISymbol
    
    private static bool AnalyzeCircularReferences(INamedTypeSymbol rootType, Compilation compilation, List<string> log)
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

