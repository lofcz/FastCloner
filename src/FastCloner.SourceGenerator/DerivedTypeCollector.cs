using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace FastCloner.SourceGenerator;

/// <summary>
/// Collects concrete derived types for an abstract class.
/// Scans the compilation for types that inherit from the abstract class
/// and also collects explicitly registered types from [FastClonerInclude].
/// </summary>
internal static class DerivedTypeCollector
{
    /// <summary>
    /// Collects all concrete derived types for an abstract class.
    /// </summary>
    /// <param name="abstractType">The abstract type symbol to find derived types for.</param>
    /// <param name="compilation">The compilation to search within.</param>
    /// <param name="nullabilityEnabled">Whether nullability is enabled.</param>
    /// <returns>List of TypeModels for all concrete derived types.</returns>
    public static List<TypeModel> Collect(
        INamedTypeSymbol abstractType,
        Compilation compilation,
        bool nullabilityEnabled)
    {
        var derivedTypes = new List<TypeModel>();
        var processedTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // 1. Collect types from [FastClonerInclude] attribute on the abstract class
        CollectIncludedTypes(abstractType, compilation, nullabilityEnabled, derivedTypes, processedTypes);

        // 2. Auto-discover derived types within the compilation (unless disabled)
        var hasDisableAutoDiscovery = abstractType.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == "FastCloner.SourceGenerator.Shared.FastClonerDisableAutoDiscoveryAttribute");
        
        if (!hasDisableAutoDiscovery)
        {
            CollectDerivedTypesFromCompilation(abstractType, compilation, nullabilityEnabled, derivedTypes, processedTypes);
        }

        return derivedTypes;
    }

    private static void CollectIncludedTypes(
        INamedTypeSymbol abstractType,
        Compilation compilation,
        bool nullabilityEnabled,
        List<TypeModel> derivedTypes,
        HashSet<ITypeSymbol> processedTypes)
    {
        foreach (var attr in abstractType.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != "FastCloner.SourceGenerator.Shared.FastClonerIncludeAttribute")
                continue;

            if (attr.ConstructorArguments.Length == 0)
                continue;

            var arg = attr.ConstructorArguments[0];
            if (arg.Kind != TypedConstantKind.Array)
                continue;

            foreach (var typeConstant in arg.Values)
            {
                if (typeConstant.Value is not INamedTypeSymbol includedType)
                    continue;

                // Verify it's a derived type of the abstract class
                if (!IsDerivedFrom(includedType, abstractType))
                    continue;

                // Skip if already processed or if it's abstract itself
                if (!processedTypes.Add(includedType))
                    continue;

                if (includedType.IsAbstract)
                    continue;

                var model = CreateTypeModelForDerived(includedType, compilation, nullabilityEnabled);
                if (model != null)
                {
                    derivedTypes.Add(model);
                }
            }
        }
    }

    private static void CollectDerivedTypesFromCompilation(
        INamedTypeSymbol abstractType,
        Compilation compilation,
        bool nullabilityEnabled,
        List<TypeModel> derivedTypes,
        HashSet<ITypeSymbol> processedTypes)
    {
        // Scan all types in the compilation's assembly
        var visitor = new DerivedTypeVisitor(abstractType, compilation, nullabilityEnabled, derivedTypes, processedTypes);
        visitor.Visit(compilation.GlobalNamespace);
    }

    private static bool IsDerivedFrom(INamedTypeSymbol type, INamedTypeSymbol potentialBase)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, potentialBase))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static TypeModel? CreateTypeModelForDerived(
        INamedTypeSymbol derivedType,
        Compilation compilation,
        bool nullabilityEnabled)
    {
        // Check if the derived type already has [FastClonerClonable]
        // If so, it will generate its own clone method, we just need a reference
        if (TypeAnalyzer.HasClonableAttribute(derivedType))
        {
            // Return a minimal model - the type has its own generator
            return CreateMinimalTypeModel(derivedType, compilation, nullabilityEnabled, hasOwnClonable: true);
        }

        // Otherwise, create a full model for auto-generation
        return CreateFullTypeModel(derivedType, compilation, nullabilityEnabled);
    }

    private static TypeModel CreateMinimalTypeModel(
        INamedTypeSymbol type,
        Compilation compilation,
        bool nullabilityEnabled,
        bool hasOwnClonable)
    {
        var flags = TypeAnalyzer.GetStructureFlags(type);
        var hasParameterlessConstructor = TypeAnalyzer.HasParameterlessConstructor(type);
        
        var trustNullability = type.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == "FastCloner.SourceGenerator.Shared.FastClonerTrustNullabilityAttribute");

        return new TypeModel(
            TypeAnalyzer.GetNamespace(type),
            type.Name,
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            flags.IsStruct,
            flags.IsSealed,
            type.IsAbstract,
            type.IsRecord,
            flags.HasClonableBaseClass,
            CanHaveCircularReferences: false, // Will be determined by circular ref analysis if needed
            IsFastClonerAvailable: compilation.GetTypeByMetadataName("FastCloner.FastCloner") != null,
            Members: EquatableArray<MemberModel>.Empty,
            TypeParameters: new EquatableArray<string>(TypeAnalyzer.GetTypeParameters(type).ToArray()),
            TypeConstraints: new EquatableArray<string>(TypeAnalyzer.GetTypeConstraints(type).ToArray()),
            RelatedTypes: EquatableArray<TypeModel>.Empty,
            NestedTypes: EquatableArray<MemberModel>.Empty,
            DerivedTypes: EquatableArray<TypeModel>.Empty,
            nullabilityEnabled,
            trustNullability,
            IsRefLikeType: false, // Derived types from abstract base cannot be ref structs
            hasParameterlessConstructor);
    }

    private static TypeModel? CreateFullTypeModel(
        INamedTypeSymbol derivedType,
        Compilation compilation,
        bool nullabilityEnabled)
    {
        // Use similar logic to TypeModelFactory but without the abstract check
        var isFastClonerAvailable = compilation.GetTypeByMetadataName("FastCloner.FastCloner") != null;
        
        var memberAnalyses = MemberCollector.GetMembers(derivedType, compilation, nullabilityEnabled);
        
        var relatedTypes = new Dictionary<string, TypeModel>();
        var implicitCache = new Dictionary<ITypeSymbol, TypeModel?>(SymbolEqualityComparer.Default);
        var processingStack = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        
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
                    memberModel = memberModel with 
                    { 
                        TypeKind = MemberTypeKind.Implicit, 
                        RequiresFastCloner = false 
                    };
                    
                    if (implicitModel != null && !relatedTypes.ContainsKey(implicitModel.FullyQualifiedName))
                    {
                        relatedTypes[implicitModel.FullyQualifiedName] = implicitModel;
                        foreach (var rel in implicitModel.RelatedTypes)
                        {
                            if (!relatedTypes.ContainsKey(rel.FullyQualifiedName))
                                relatedTypes[rel.FullyQualifiedName] = rel;
                        }
                        foreach (var nested in implicitModel.NestedTypes)
                        {
                            if (!nestedTypes.ContainsKey(nested.TypeFullName))
                                nestedTypes[nested.TypeFullName] = nested;
                        }
                    }
                }
                else if (memberModel.TypeKind == MemberTypeKind.Implicit)
                {
                    memberModel = memberModel with 
                    { 
                        TypeKind = MemberTypeKind.Other, 
                        RequiresFastCloner = true 
                    };
                }
            }
            
            if (memberModel.TypeKind != MemberTypeKind.Implicit)
            {
                NestedTypeCollector.Collect(memberType, compilation, nullabilityEnabled, nestedTypes);

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
        
        var typeParameters = TypeAnalyzer.GetTypeParameters(derivedType);
        var typeConstraints = TypeAnalyzer.GetTypeConstraints(derivedType);
        var flags = TypeAnalyzer.GetStructureFlags(derivedType);
        
        // Check for members that require FastCloner when it's not available
        if (!isFastClonerAvailable)
        {
            var unclonableMembers = finalMembers
                .Where(m => m.RequiresFastCloner)
                .Select(m => m.Name)
                .ToList();
            
            if (unclonableMembers.Count > 0)
            {
                // Can't auto-generate for this type - it requires runtime cloning
                return null;
            }
        }

        var circRefLog = new List<string>();
        var canHaveCircularRefs = CircularReferenceAnalyzer.Analyze(derivedType, compilation, circRefLog);
        var hasParameterlessConstructor = TypeAnalyzer.HasParameterlessConstructor(derivedType);
        
        var trustNullability = derivedType.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == "FastCloner.SourceGenerator.Shared.FastClonerTrustNullabilityAttribute");
        
        return new TypeModel(
            TypeAnalyzer.GetNamespace(derivedType),
            derivedType.Name,
            derivedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            flags.IsStruct,
            flags.IsSealed,
            derivedType.IsAbstract,
            derivedType.IsRecord,
            flags.HasClonableBaseClass,
            canHaveCircularRefs,
            isFastClonerAvailable,
            new EquatableArray<MemberModel>(finalMembers.ToArray()),
            new EquatableArray<string>(typeParameters.ToArray()),
            new EquatableArray<string>(typeConstraints.ToArray()),
            new EquatableArray<TypeModel>(relatedTypes.Values.ToArray()),
            new EquatableArray<MemberModel>(nestedTypes.Values.ToArray()),
            EquatableArray<TypeModel>.Empty, // Derived types don't have their own derived types in this context
            nullabilityEnabled,
            trustNullability,
            IsRefLikeType: false, // Derived types from abstract base cannot be ref structs
            hasParameterlessConstructor,
            new EquatableArray<string>(circRefLog.ToArray()));
    }

    /// <summary>
    /// Visitor that scans the compilation for types derived from a given base type.
    /// </summary>
    private class DerivedTypeVisitor : SymbolVisitor
    {
        private readonly INamedTypeSymbol _baseType;
        private readonly Compilation _compilation;
        private readonly bool _nullabilityEnabled;
        private readonly List<TypeModel> _derivedTypes;
        private readonly HashSet<ITypeSymbol> _processedTypes;

        public DerivedTypeVisitor(
            INamedTypeSymbol baseType,
            Compilation compilation,
            bool nullabilityEnabled,
            List<TypeModel> derivedTypes,
            HashSet<ITypeSymbol> processedTypes)
        {
            _baseType = baseType;
            _compilation = compilation;
            _nullabilityEnabled = nullabilityEnabled;
            _derivedTypes = derivedTypes;
            _processedTypes = processedTypes;
        }

        public override void VisitNamespace(INamespaceSymbol symbol)
        {
            foreach (var member in symbol.GetMembers())
            {
                member.Accept(this);
            }
        }

        public override void VisitNamedType(INamedTypeSymbol symbol)
        {
            // Check if this type derives from our base type
            if (!symbol.IsAbstract && 
                symbol.TypeKind == TypeKind.Class &&
                IsDerivedFrom(symbol, _baseType) &&
                _processedTypes.Add(symbol))
            {
                var model = CreateTypeModelForDerived(symbol, _compilation, _nullabilityEnabled);
                if (model != null)
                {
                    _derivedTypes.Add(model);
                }
            }

            // Visit nested types
            foreach (var nestedType in symbol.GetTypeMembers())
            {
                nestedType.Accept(this);
            }
        }
    }
}

