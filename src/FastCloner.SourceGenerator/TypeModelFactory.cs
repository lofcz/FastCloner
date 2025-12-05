using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace FastCloner.SourceGenerator;

internal static class TypeModelFactory
{
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

        // Check if FastCloner library is available
        var isFastClonerAvailable = compilation.GetTypeByMetadataName("FastCloner.FastCloner") != null;
        
        // Check if type has SimulateNoRuntime attribute (for testing)
        var hasSimulateNoRuntime = symbol.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == "FastCloner.SourceGenerator.Shared.FastClonerSimulateNoRuntimeAttribute");
        
        if (hasSimulateNoRuntime)
        {
            isFastClonerAvailable = false;
        }

        var memberAnalyses = MemberCollector.GetMembers(symbol, compilation, nullabilityEnabled);
        
        // Analyze implicit types recursively
        var relatedTypes = new Dictionary<string, TypeModel>(); // Use FQN as key to avoid dupes
        var implicitCache = new Dictionary<ITypeSymbol, TypeModel?>(SymbolEqualityComparer.Default);
        var processingStack = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        
        // Refine members based on implicit analysis
        var finalMembers = new List<MemberModel>();
        var nestedTypes = new Dictionary<string, MemberModel>();
        
        foreach (var analysis in memberAnalyses)
        {
            var memberModel = analysis.Model;
            var memberType = analysis.Type;
            
            // If member is 'Other' or 'Implicit' candidate, try to analyze if it's implicitly clonable
            if (memberModel.TypeKind == MemberTypeKind.Other || memberModel.TypeKind == MemberTypeKind.Implicit)
            {
                if (ImplicitTypeAnalyzer.TryAnalyze(memberType, compilation, nullabilityEnabled, implicitCache, processingStack, out var implicitModel))
                {
                    // It is implicit! Update member kind (ensure it is set to Implicit)
                    memberModel = memberModel with 
                    { 
                        TypeKind = MemberTypeKind.Implicit, 
                        RequiresFastCloner = false 
                    };
                    
                    if (implicitModel != null && !relatedTypes.ContainsKey(implicitModel.FullyQualifiedName))
                    {
                        relatedTypes[implicitModel.FullyQualifiedName] = implicitModel;
                        // Add recursive related types
                        foreach (var rel in implicitModel.RelatedTypes)
                        {
                            if (!relatedTypes.ContainsKey(rel.FullyQualifiedName))
                                relatedTypes[rel.FullyQualifiedName] = rel;
                        }
                        // Add recursive nested types
                        foreach (var nested in implicitModel.NestedTypes)
                        {
                            if (!nestedTypes.ContainsKey(nested.TypeFullName))
                                nestedTypes[nested.TypeFullName] = nested;
                        }
                    }
                }
                else
                {
                    // If implicit analysis failed, revert to Other (fallback to runtime cloning)
                    if (memberModel.TypeKind == MemberTypeKind.Implicit)
                    {
                        memberModel = memberModel with 
                        { 
                            TypeKind = MemberTypeKind.Other, 
                            RequiresFastCloner = true 
                        };
                    }
                }
            }
            
            if (memberModel.TypeKind != MemberTypeKind.Implicit)
            {
                // Analyze nested types for collections/dictionaries (only if not handled as implicit)
                NestedTypeCollector.Collect(memberType, compilation, nullabilityEnabled, nestedTypes);

                // Also check if elements of collections/dictionaries are implicit types
                if (memberModel.TypeKind == MemberTypeKind.Collection || 
                    memberModel.TypeKind == MemberTypeKind.Array)
                {
                    var elemType = (memberModel.TypeKind == MemberTypeKind.Array) 
                        ? ((IArrayTypeSymbol)memberType).ElementType 
                        : TypeAnalyzer.GetCollectionElementType(memberType, compilation);

                    if (elemType != null && !memberModel.ElementIsSafe && !memberModel.ElementHasClonableAttr)
                    {
                        if (ImplicitTypeAnalyzer.TryAnalyze(elemType, compilation, nullabilityEnabled, implicitCache, processingStack, out var implicitModel))
                        {
                            if (implicitModel != null)
                            {
                                if (!relatedTypes.ContainsKey(implicitModel.FullyQualifiedName))
                                    relatedTypes[implicitModel.FullyQualifiedName] = implicitModel;
                                
                                foreach (var rel in implicitModel.RelatedTypes)
                                    if (!relatedTypes.ContainsKey(rel.FullyQualifiedName))
                                        relatedTypes[rel.FullyQualifiedName] = rel;

                                foreach (var nested in implicitModel.NestedTypes)
                                    if (!nestedTypes.ContainsKey(nested.TypeFullName))
                                        nestedTypes[nested.TypeFullName] = nested;
                            }
                        }
                    }
                }
                else if (memberModel.TypeKind == MemberTypeKind.Dictionary)
                {
                    var dictTypes = TypeAnalyzer.GetDictionaryTypes(memberType, compilation);
                    if (dictTypes.HasValue)
                    {
                        // Check Key
                        if (!memberModel.KeyIsSafe && !memberModel.KeyIsClonable)
                        {
                            if (ImplicitTypeAnalyzer.TryAnalyze(dictTypes.Value.KeyType, compilation, nullabilityEnabled, implicitCache, processingStack, out var implicitKey))
                            {
                                if (implicitKey != null)
                                {
                                    if (!relatedTypes.ContainsKey(implicitKey.FullyQualifiedName))
                                        relatedTypes[implicitKey.FullyQualifiedName] = implicitKey;
                                    
                                    foreach (var rel in implicitKey.RelatedTypes)
                                        if (!relatedTypes.ContainsKey(rel.FullyQualifiedName))
                                            relatedTypes[rel.FullyQualifiedName] = rel;

                                    foreach (var nested in implicitKey.NestedTypes)
                                        if (!nestedTypes.ContainsKey(nested.TypeFullName))
                                            nestedTypes[nested.TypeFullName] = nested;
                                }
                            }
                        }

                        // Check Value
                        if (!memberModel.ValueIsSafe && !memberModel.ValueIsClonable)
                        {
                            if (ImplicitTypeAnalyzer.TryAnalyze(dictTypes.Value.ValueType, compilation, nullabilityEnabled, implicitCache, processingStack, out var implicitVal))
                            {
                                if (implicitVal != null)
                                {
                                    if (!relatedTypes.ContainsKey(implicitVal.FullyQualifiedName))
                                        relatedTypes[implicitVal.FullyQualifiedName] = implicitVal;
                                    
                                    foreach (var rel in implicitVal.RelatedTypes)
                                        if (!relatedTypes.ContainsKey(rel.FullyQualifiedName))
                                            relatedTypes[rel.FullyQualifiedName] = rel;

                                    foreach (var nested in implicitVal.NestedTypes)
                                        if (!nestedTypes.ContainsKey(nested.TypeFullName))
                                            nestedTypes[nested.TypeFullName] = nested;
                                }
                            }
                        }
                    }
                }
            }
            
            finalMembers.Add(memberModel);
        }
        
        var typeParameters = TypeAnalyzer.GetTypeParameters(symbol);
        var typeConstraints = TypeAnalyzer.GetTypeConstraints(symbol);
        var flags = TypeAnalyzer.GetStructureFlags(symbol);
        
        // Check for members that require FastCloner when it's not available
        if (!isFastClonerAvailable)
        {
            var unclonableMembers = finalMembers
                .Where(m => m.RequiresFastCloner)
                .Select(m => m.Name)
                .ToList();
            
            if (unclonableMembers.Count > 0)
            {
                // Use warning severity for FastCloner test assemblies, error for production code
                var assemblyName = compilation.AssemblyName ?? string.Empty;
                var isFastClonerAssembly = assemblyName.StartsWith("FastCloner", StringComparison.OrdinalIgnoreCase);
                var severity = isFastClonerAssembly ? DiagnosticSeverity.Error : DiagnosticSeverity.Error;
                
                // Format member list with line breaks for readability
                var membersList = string.Join("\n  - ", unclonableMembers);
                
                error = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "FCG004",
                        "Missing FastCloner dependency for deep cloning",
                        "Type '{0}' cannot be deep cloned without the FastCloner runtime library.\n\n" +
                        "Members requiring deep cloning:\n  - {1}\n\n" +
                        "Solutions:\n" +
                        "  1. Install the FastCloner NuGet package, OR\n" +
                        "  2. Mark the member types with [FastClonerClonable] to enable source generation",
                        "FastCloner",
                        severity,
                        isEnabledByDefault: true),
                    symbol.Locations.FirstOrDefault() ?? Location.None,
                    symbol.Name,
                    membersList);
                    
                // Only prevent code generation if it's an error (production code)
                return isFastClonerAssembly;
            }
        }

        // Analyze circular references at compile-time (we have the symbol here)
        var circRefLog = new List<string>();
        var canHaveCircularRefs = CircularReferenceAnalyzer.Analyze(symbol, compilation, circRefLog);
        
        // Check if type has a parameterless constructor
        var hasParameterlessConstructor = TypeAnalyzer.HasParameterlessConstructor(symbol);
        
        model = new TypeModel(
            TypeAnalyzer.GetNamespace(symbol),
            symbol.Name,
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            flags.IsStruct,
            flags.IsSealed,
            flags.HasClonableBaseClass,
            canHaveCircularRefs,
            isFastClonerAvailable,
            new EquatableArray<MemberModel>(finalMembers.ToArray()),
            new EquatableArray<string>(typeParameters.ToArray()),
            new EquatableArray<string>(typeConstraints.ToArray()),
            new EquatableArray<TypeModel>(relatedTypes.Values.ToArray()),
            new EquatableArray<MemberModel>(nestedTypes.Values.ToArray()),
            nullabilityEnabled,
            hasParameterlessConstructor,
            new EquatableArray<string>(circRefLog.ToArray()));

        return true;
    }
}
