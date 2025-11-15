using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace FastCloner.SourceGenerator;

/// <summary>
/// Generates FastDeepClone() extension methods for types marked with [FastClonerClonable].
/// </summary>
internal sealed class CloneCodeGenerator
{
    private readonly TypeModel _model;
    private readonly StringBuilder _source = new();
    private readonly Dictionary<string, string> _typeNameToMethodName = new();
    private readonly HashSet<string> _neededHelperMethods = new();
    private readonly Dictionary<string, MemberModel> _typeNameToMemberModel = new();
    private readonly bool _canHaveCircularReferences;
    private readonly bool _isFastClonerAvailable;
    private bool _needsStateClass = false;

    public CloneCodeGenerator(TypeModel model)
    {
        _model = model;
        _canHaveCircularReferences = model.CanHaveCircularReferences;
        _isFastClonerAvailable = model.IsFastClonerAvailable;
    }
    
    /// <summary>
    /// Checks if a member needs circular reference tracking based on pre-analyzed metadata.
    /// If the root type analysis determined no circular references are possible, this always returns false.
    /// </summary>
    private bool MemberNeedsCircularRefTracking(MemberModel member)
    {
        // If the root type can't have circular references, nothing needs tracking
        if (!_canHaveCircularReferences)
            return false;
        
        // Safe types don't need tracking
        if (member.TypeKind == MemberTypeKind.Safe)
            return false;
        
        // Types with FastClonerClonable attribute might have circular refs
        if (member.TypeKind == MemberTypeKind.Clonable)
            return true;
        
        // For collections/arrays, check element metadata
        if (member.TypeKind == MemberTypeKind.Collection || member.TypeKind == MemberTypeKind.Array)
        {
            // If element is safe, no tracking needed
            if (member.ElementIsSafe)
                return false;
            // If element has clonable attribute, tracking needed
            if (member.ElementHasClonableAttr)
                return true;
        }
        
        // For other types, assume they might need tracking if not safe
        return true;
    }
    
    /// <summary>
    /// Checks if the FastCloner reflection library is available in the compilation.
    /// </summary>
    private static bool IsFastClonerAvailable(Compilation compilation)
    {
        // Check if FastCloner.FastCloner type exists with DeepClone method
        var fastClonerType = compilation.GetTypeByMetadataName("FastCloner.FastCloner");
        if (fastClonerType == null)
            return false;
        
        // Check if DeepClone method exists
        var deepCloneMethod = fastClonerType.GetMembers("DeepClone")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.IsStatic && m.TypeParameters.Length == 1);
        
        return deepCloneMethod != null;
    }
    
    /// <summary>
    /// Determines if the type graph can have circular references by analyzing the dependency graph.
    /// </summary>
    private static bool CanHaveCircularReferences(INamedTypeSymbol rootType, Compilation compilation, List<string> log)
    {
        log.Add($"=== Analyzing circular references for {rootType.ToDisplayString()} ===");
        
        // Structs can't have circular references (value types)
        if (rootType.IsValueType)
        {
            log.Add("  -> Type is a struct (value type), cannot have circular references");
            return false;
        }
        
        // Build a set of all reference types that can be reached from this type's MEMBERS
        // (not including the root type itself)
        var reachableTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        
        log.Add($"  -> Collecting reachable reference types from {rootType.Name} members...");
        
        // First check if rootType has a direct self-reference (member of same type)
        bool hasDirectSelfReference = HasDirectSelfReference(rootType, rootType, compilation, log);
        
        // Then collect types reachable from members (excluding rootType itself)
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
    
    /// <summary>
    /// Checks if a type has a direct member that references itself.
    /// </summary>
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
    
    /// <summary>
    /// Collects reference types reachable from a type's members, excluding the root type itself.
    /// </summary>
    private static void CollectReachableReferenceTypesFromMembers(
        INamedTypeSymbol type,
        HashSet<ITypeSymbol> reachableTypes,
        HashSet<ITypeSymbol> visited,
        Compilation compilation,
        List<string> log,
        INamedTypeSymbol rootType)
    {
        // Analyze members of the type
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
        
        // Only track reference types (not value types, primitives, etc.)
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
        
        // Get the underlying type if nullable
        var underlyingType = type.WithNullableAnnotation(NullableAnnotation.None);
        
        if (underlyingType is INamedTypeSymbol namedType)
        {
            // Don't add the root type itself to reachable types
            if (!SymbolEqualityComparer.Default.Equals(namedType, rootType))
            {
                log.Add($"     [ADD] {typeDisplayName}");
                reachableTypes.Add(namedType);
            }
            else
            {
                log.Add($"     [SKIP] {typeDisplayName} (root type - checking for self-reference separately)");
            }
            
            // Check if it's a collection - analyze element type
            if (TypeAnalyzer.IsCollectionType(namedType))
            {
                var elementType = TypeAnalyzer.GetCollectionElementType(namedType, compilation);
                if (elementType != null)
                {
                    log.Add($"       -> Collection element type: {elementType.ToDisplayString()}");
                    CollectReachableReferenceTypes(elementType, reachableTypes, visited, compilation, log, rootType);
                }
            }
            else if (TypeAnalyzer.IsDictionaryType(namedType))
            {
                var dictTypes = TypeAnalyzer.GetDictionaryTypes(namedType, compilation);
                if (dictTypes.HasValue)
                {
                    log.Add($"       -> Dictionary key type: {dictTypes.Value.KeyType.ToDisplayString()}");
                    log.Add($"       -> Dictionary value type: {dictTypes.Value.ValueType.ToDisplayString()}");
                    CollectReachableReferenceTypes(dictTypes.Value.KeyType, reachableTypes, visited, compilation, log, rootType);
                    CollectReachableReferenceTypes(dictTypes.Value.ValueType, reachableTypes, visited, compilation, log, rootType);
                }
            }
            else if (!SymbolEqualityComparer.Default.Equals(namedType, rootType))
            {
                // For other types (not the root), check their members
                log.Add($"       -> Analyzing members of {namedType.Name}...");
                foreach (var member in namedType.GetMembers())
                {
                    if (member.IsStatic || member.IsImplicitlyDeclared)
                        continue;
                    
                    ITypeSymbol? memberType = null;
                    if (member is IPropertySymbol property && property.GetMethod != null && property.SetMethod != null)
                    {
                        memberType = property.Type;
                        log.Add($"         - Property {member.Name}: {memberType.ToDisplayString()}");
                    }
                    else if (member is IFieldSymbol field && !field.IsConst && !field.IsStatic)
                    {
                        memberType = field.Type;
                        log.Add($"         - Field {member.Name}: {memberType.ToDisplayString()}");
                    }
                    
                    if (memberType != null)
                    {
                        CollectReachableReferenceTypes(memberType, reachableTypes, visited, compilation, log, rootType);
                    }
                }
            }
        }
        else if (underlyingType.TypeKind == TypeKind.Array)
        {
            var elementType = ((IArrayTypeSymbol)underlyingType).ElementType;
            log.Add($"       -> Array element type: {elementType.ToDisplayString()}");
            CollectReachableReferenceTypes(elementType, reachableTypes, visited, compilation, log, rootType);
        }
    }
    
    private static bool CanReferenceType(ITypeSymbol fromType, ITypeSymbol toType, Compilation compilation, List<string> log)
    {
        if (SymbolEqualityComparer.Default.Equals(fromType, toType))
        {
            log.Add($"     [MATCH] {fromType.ToDisplayString()} can reference {toType.ToDisplayString()} (same type)");
            return true;
        }
        
        if (fromType is INamedTypeSymbol namedType)
        {
            log.Add($"     [CHECK] Checking if {fromType.ToDisplayString()} can reference {toType.ToDisplayString()}...");
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
                    var underlyingMemberType = memberType.WithNullableAnnotation(NullableAnnotation.None);
                    if (SymbolEqualityComparer.Default.Equals(underlyingMemberType, toType))
                    {
                        log.Add($"       [MATCH] Found member {member.Name} of type {toType.ToDisplayString()}");
                        return true;
                    }
                }
            }
            log.Add($"       [NO MATCH] No members reference {toType.ToDisplayString()}");
        }
        
        return false;
    }

    public string Generate()
    {
        WriteFileHeader();
        WriteUsings();
        WriteNamespace();
        WriteExtensionClass();
        WriteFileFooter();

        return _source.ToString();
    }

    private void WriteFileHeader()
    {
        _source.AppendLine("// <auto-generated/>");
        _source.AppendLine($"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        _source.AppendLine("#nullable enable");
        _source.AppendLine();
    }

    private void WriteUsings()
    {
        _source.AppendLine("using System;");
        _source.AppendLine("using System.Collections.Generic;");
        _source.AppendLine("using System.Reflection;");
        
        // Only add FastCloner using if the library is available
        if (_isFastClonerAvailable)
        {
            _source.AppendLine("using FastCloner;");
        }
        
        // Add using for the type's namespace if it's not the global namespace
        if (!string.IsNullOrEmpty(_model.Namespace))
        {
            // The type is in the same namespace, so we don't need an extra using
            // But we need to ensure the namespace is properly set up
        }
        
        _source.AppendLine();
    }

    private void WriteNamespace()
    {
        var ns = _model.Namespace;
        if (!string.IsNullOrEmpty(ns))
        {
            _source.AppendLine($"namespace {ns}");
            _source.AppendLine("{");
        }
    }

    private void WriteExtensionClass()
    {
        // Use the fully qualified name from TypeModel
        var typeName = _model.FullyQualifiedName;
        var fullTypeName = _model.FullyQualifiedName;

        _source.AppendLine("    /// <summary>");
        _source.AppendLine($"    /// Extension methods for cloning {_model.Name}.");
        _source.AppendLine("    /// </summary>");
        _source.AppendLine($"    public static partial class {_model.Name}FastDeepCloneExtensions");
        _source.AppendLine("    {");

        // Generate public FastDeepClone() without state parameter
        WritePublicFastDeepCloneMethod(typeName, fullTypeName);

        // Only generate InternalFastDeepClone if circular references are possible
        // (for types without circular refs, the public method inlines the logic directly)
        if (_canHaveCircularReferences)
        {
            WritePrivateFastDeepCloneMethod(typeName, fullTypeName);
        }

        // Generate helper methods for nested types
        WriteHelperMethods();

        // Generate FastCloneState helper class (private nested class)
        WriteFastCloneStateClass();

        _source.AppendLine("    }");
    }

    private void WritePublicFastDeepCloneMethod(string typeName, string fullTypeName)
    {
        _source.AppendLine($"        /// <summary>");
        _source.AppendLine($"        /// Performs deep clone of {_model.Name}. This is a source generated method.");
        _source.AppendLine($"        /// </summary>");
        _source.AppendLine($"        /// <param name=\"source\">The object to clone.</param>");
        _source.AppendLine($"        public static {typeName}? FastDeepClone(this {typeName}? source)");
        _source.AppendLine("        {");
        
        // Optimization: When no circular references are possible, inline the clone logic directly
        // instead of calling through InternalFastDeepClone (saves a method call)
        if (!_canHaveCircularReferences)
        {
            _source.AppendLine("            if (source == null) return null;");
            WriteCloneBody(typeName, fullTypeName, false);
        }
        else
        {
            _source.AppendLine("            return InternalFastDeepClone(source, null);");
        }
        
        _source.AppendLine("        }");
        _source.AppendLine();
    }

    private void WritePrivateFastDeepCloneMethod(string typeName, string fullTypeName)
    {
        _source.AppendLine($"        /// <summary>");
        _source.AppendLine($"        /// Performs deep clone of {_model.Name} with circular reference tracking.");
        _source.AppendLine($"        /// </summary>");
        _source.AppendLine($"        /// <param name=\"source\">The object to clone.</param>");
        _source.AppendLine($"        /// <param name=\"state\">State for circular reference tracking. If null, a new state is created.</param>");
        _source.AppendLine($"        internal static {typeName}? InternalFastDeepClone(this {typeName}? source, object? state)");
        _source.AppendLine("        {");
        _source.AppendLine("            if (source == null) return null;");
        
        // Only use state if circular references are possible
        if (_canHaveCircularReferences)
        {
            _needsStateClass = true;
            // Cast state to local type or create new one - allows sharing state across different extension classes
            _source.AppendLine("            var localState = (state as FcGeneratedCloneState) ?? new FcGeneratedCloneState();");

        if (!_model.IsStruct)
        {
                _source.AppendLine("            var known = localState.GetKnownRef(source);");
            _source.AppendLine($"            if (known != null) return ({typeName})known;");
        }

            // Use localState in WriteCloneBody - need to pass it as a variable name
            WriteCloneBody(typeName, fullTypeName, true, "localState");
        }
        else
        {
            // If circular references aren't possible, state can remain null and won't be used
            WriteCloneBody(typeName, fullTypeName, false);
        }

        _source.AppendLine("        }");
        _source.AppendLine();
    }

    private void WriteCloneBody(string typeName, string fullTypeName, bool useState, string? stateVarName = null)
    {
        if (_model.IsStruct)
        {
            WriteStructCloneBody(typeName, stateVarName ?? "state");
        }
        else
        {
            WriteClassCloneBody(typeName, fullTypeName, useState, stateVarName);
        }
    }

    private void WriteStructCloneBody(string typeName, string stateVarName = "state")
    {
        _source.AppendLine($"            var result = source;");
        _source.AppendLine();

        foreach (var member in _model.Members)
        {
            WriteMemberCloning(member, "result", "source", stateVarName);
        }

        _source.AppendLine("            return result;");
    }

    private void WriteClassCloneBody(string typeName, string fullTypeName, bool useState, string? stateVarName = null)
    {
        // Use object initializer syntax instead of MemberwiseClone for better performance
        // Create new instance using object initializer
        _source.AppendLine($"            var result = new {typeName}");
        _source.AppendLine("            {");
        
        // Generate property/field assignments in object initializer
        var memberAssignments = new List<string>();
        foreach (var member in _model.Members)
        {
            // Pass null if state isn't needed, or use the provided stateVarName (or "state" as default)
            var stateVar = useState ? (stateVarName ?? "state") : "null";
            var assignment = GetMemberAssignment(member, "source", stateVar);
            if (!string.IsNullOrEmpty(assignment))
            {
                memberAssignments.Add($"                {assignment}");
            }
        }
        
        if (memberAssignments.Count > 0)
        {
            _source.AppendLine(string.Join(",\n", memberAssignments));
        }
        
        _source.AppendLine("            };");
        if (useState)
        {
            var stateVarForAdd = stateVarName ?? "state";
            _source.AppendLine($"            {stateVarForAdd}?.AddKnownRef(source, result);");
        }
        _source.AppendLine();
        _source.AppendLine("            return result;");
    }

    private string GetMemberAssignment(MemberModel member, string sourceVar, string stateVar)
    {
        var memberName = member.Name;
        
        // Skip read-only fields (can't assign)
        if (!member.IsProperty && member.IsReadOnly)
            return string.Empty;

        switch (member.TypeKind)
        {
            case MemberTypeKind.Safe:
                // Direct assignment for safe types (primitives, strings, etc.)
                return member.IsProperty 
                    ? $"{memberName} = {sourceVar}.{memberName}"
                    : string.Empty;
            
            case MemberTypeKind.Clonable:
                // Use generated FastDeepClone for marked types
                return member.IsProperty
                    ? $"{memberName} = {sourceVar}.{memberName}?.FastDeepClone()"
                    : string.Empty;
            
            case MemberTypeKind.Collection:
                {
                    var helperMethodName = GetOrCreateHelperMethodName(member);
                    var memberNeedsState = MemberNeedsCircularRefTracking(member);
                    var actualStateVar = memberNeedsState ? stateVar : "null";
                    
                    return member.IsProperty
                        ? $"{memberName} = {GetHelperMethodCall(helperMethodName, $"{sourceVar}.{memberName}", memberNeedsState, actualStateVar)}"
                        : string.Empty;
                }
            
            case MemberTypeKind.Dictionary:
                {
                    var helperMethodName = GetOrCreateHelperMethodName(member);
                    var memberNeedsState = MemberNeedsCircularRefTracking(member);
                    var actualStateVar = memberNeedsState ? stateVar : "null";
                    
                    return member.IsProperty
                        ? $"{memberName} = {GetHelperMethodCall(helperMethodName, $"{sourceVar}.{memberName}", memberNeedsState, actualStateVar)}"
                        : string.Empty;
                }
            
            case MemberTypeKind.Array:
                {
                    var helperMethodName = GetOrCreateHelperMethodName(member);
                    var memberNeedsState = MemberNeedsCircularRefTracking(member);
                    var actualStateVar = memberNeedsState ? stateVar : "null";
                    
                    return member.IsProperty
                        ? $"{memberName} = {GetHelperMethodCall(helperMethodName, $"{sourceVar}.{memberName}", memberNeedsState, actualStateVar)}"
                        : string.Empty;
                }
            
            case MemberTypeKind.Object:
                // System.Object - use runtime FastCloner if available
                if (_isFastClonerAvailable)
                {
                    return member.IsProperty
                        ? $"{memberName} = ({member.TypeFullName})FastCloner.DeepClone({sourceVar}.{memberName})"
                        : string.Empty;
                }
                else
                {
                    // FastCloner not available - shallow copy
                    return member.IsProperty
                        ? $"{memberName} = {sourceVar}.{memberName}"
                        : string.Empty;
                }
            
            case MemberTypeKind.Other:
            default:
                // For other types without FastCloner, do shallow copy
                return member.IsProperty
                    ? $"{memberName} = {sourceVar}.{memberName}"
                    : string.Empty;
        }
    }
    
    /// <summary>
    /// Gets or creates a helper method name for a member, tracking it in _neededHelperMethods.
    /// </summary>
    private string GetOrCreateHelperMethodName(MemberModel member)
    {
        var typeKey = member.TypeFullName;
        
        if (_typeNameToMethodName.TryGetValue(typeKey, out var existingMethod))
        {
            return existingMethod;
        }
        
        // Generate a unique helper method name based on the type
        var methodName = $"FastClonerSgClone{GetCleanTypeName(member.TypeFullName)}";
        _typeNameToMethodName[typeKey] = methodName;
        _neededHelperMethods.Add(typeKey);
        
        // Store the member model for later use when generating helper methods
        if (!_typeNameToMemberModel.ContainsKey(typeKey))
        {
            _typeNameToMemberModel[typeKey] = member;
        }
        
        return methodName;
    }
    
    /// <summary>
    /// Cleans a type name string for use in method names by replacing special characters.
    /// </summary>
    private static string GetCleanTypeName(string typeName)
    {
        return typeName
            .Replace("global::", "")
            .Replace('<', '_')
            .Replace('>', '_')
            .Replace(',', '_')
            .Replace(' ', '_')
            .Replace('.', '_')
            .Replace('[', '_')
            .Replace(']', '_')
            .Replace('?', '_')
            .Replace(':', '_');
    }

    private void WriteMemberCloning(MemberModel member, string resultVar, string sourceVar, string stateVar)
    {
        var memberName = member.Name;
        
        // Skip read-only fields (can't assign in struct cloning)
        if (!member.IsProperty && member.IsReadOnly)
            return;

        switch (member.TypeKind)
        {
            case MemberTypeKind.Safe:
                // Direct assignment for safe types
                _source.AppendLine($"            {resultVar}.{memberName} = {sourceVar}.{memberName};");
                break;
            
            case MemberTypeKind.Clonable:
                // Use generated FastDeepClone for marked types
                _source.AppendLine($"            {resultVar}.{memberName} = {sourceVar}.{memberName}?.FastDeepClone();");
                break;
            
            case MemberTypeKind.Collection:
            case MemberTypeKind.Dictionary:
            case MemberTypeKind.Array:
                {
                    var helperMethodName = GetOrCreateHelperMethodName(member);
                    var memberNeedsState = MemberNeedsCircularRefTracking(member);
                    var actualStateVar = memberNeedsState ? stateVar : "null";
                    _source.AppendLine($"            {resultVar}.{memberName} = {GetHelperMethodCall(helperMethodName, $"{sourceVar}.{memberName}", memberNeedsState, actualStateVar)};");
                }
                break;
            
            case MemberTypeKind.Object:
                // System.Object - use runtime FastCloner if available
                if (_isFastClonerAvailable)
                {
                    _source.AppendLine($"            {resultVar}.{memberName} = ({member.TypeFullName})FastCloner.DeepClone({sourceVar}.{memberName});");
                }
                else
                {
                    // FastCloner not available - shallow copy
                    _source.AppendLine($"            {resultVar}.{memberName} = {sourceVar}.{memberName};");
                }
                break;
            
            case MemberTypeKind.Other:
            default:
                // For other types, shallow copy
                _source.AppendLine($"            {resultVar}.{memberName} = {sourceVar}.{memberName};");
                break;
        }
    }

    private void WriteHelperMethods()
    {
        // Only generate helper methods that are actually used
        foreach (var typeFullName in _neededHelperMethods)
        {
            // Get the member model for this type
            if (!_typeNameToMemberModel.TryGetValue(typeFullName, out var member))
                continue;
            
            // Generate appropriate helper method based on member type kind
            switch (member.TypeKind)
            {
                case MemberTypeKind.Array:
                    WriteArrayCloneMethod(member);
                    break;
                
                case MemberTypeKind.Collection:
                    WriteCollectionCloneMethod(member);
                    break;
                
                case MemberTypeKind.Dictionary:
                    WriteDictionaryCloneMethod(member);
                    break;
                
                // Other types don't get helper methods (handled inline or via FastCloner runtime)
            }
        }
        
        // Note: _needsStateClass is set individually by each helper method only when it actually needs state
        // This ensures we don't generate unused state tracking code when circular references aren't possible
    }

    private void WriteCollectionCloneMethod(MemberModel member)
    {
        if (member.ElementTypeName == null) return;

        var typeName = member.TypeFullName;
        var methodName = _typeNameToMethodName[typeName];
        var isSafe = member.ElementIsSafe;
        var hasClonableAttr = member.ElementHasClonableAttr;
        var needsState = MemberNeedsCircularRefTracking(member);
        
        if (needsState)
        {
            _needsStateClass = true;
        }

        WriteHelperMethodSignature(typeName, methodName, needsState);
        _source.AppendLine("        {");
        _source.AppendLine("            if (source == null) return null;");
        
        if (needsState)
        {
            _source.AppendLine("            if (state != null)");
            _source.AppendLine("            {");
            _source.AppendLine("                var known = state.GetKnownRef(source);");
            _source.AppendLine($"                if (known != null) return ({typeName})known;");
            _source.AppendLine("            }");
        _source.AppendLine();
        }
        
        // Optimize: if element type is safe, use IEnumerable constructor directly (works for List, HashSet, etc.)
        if (isSafe && !needsState)
        {
            _source.AppendLine($"            return new {typeName}(source);");
        }
        else
        {
            // Pre-allocate capacity for common collection types (List, HashSet, etc. support Count)
            _source.AppendLine($"            var result = new {typeName}(source.Count);");
            
            if (needsState)
            {
                _source.AppendLine("            state?.AddKnownRef(source, result);");
        _source.AppendLine();
            }
            
        _source.AppendLine("            foreach (var item in source)");
        _source.AppendLine("            {");

        if (isSafe)
        {
            _source.AppendLine("                result.Add(item);");
        }
        else if (hasClonableAttr)
        {
                _source.AppendLine($"                result.Add(item?.FastDeepClone());");
        }
        else
        {
                // For other element types, shallow copy (or use FastCloner runtime if available)
                _source.AppendLine("                result.Add(item);");
        }

        _source.AppendLine("            }");
        _source.AppendLine();
        _source.AppendLine("            return result;");
        }
        
        _source.AppendLine("        }");
        _source.AppendLine();
    }

    private void WriteDictionaryCloneMethod(MemberModel member)
    {
        if (member.KeyTypeName == null || member.ValueTypeName == null) return;

        var typeName = member.TypeFullName;
        var methodName = _typeNameToMethodName[typeName];
        var needsState = MemberNeedsCircularRefTracking(member);
        
        if (needsState)
        {
            _needsStateClass = true;
        }

        WriteHelperMethodSignature(typeName, methodName, needsState);
        _source.AppendLine("        {");
        _source.AppendLine("            if (source == null) return null;");
        
        if (needsState)
        {
            _source.AppendLine("            if (state != null)");
            _source.AppendLine("            {");
            _source.AppendLine("                var known = state.GetKnownRef(source);");
            _source.AppendLine($"                if (known != null) return ({typeName})known;");
            _source.AppendLine("            }");
            _source.AppendLine();
        }
        
        // Pre-allocate capacity for Dictionary
        _source.AppendLine($"            var result = new {typeName}(source.Count);");
        
        if (needsState)
        {
            _source.AppendLine("            state?.AddKnownRef(source, result);");
            _source.AppendLine();
        }
        
        _source.AppendLine("            foreach (var kvp in source)");
        _source.AppendLine("            {");

        // For dictionaries, we always shallow copy keys and values (they're usually primitives/strings)
        // If complex types are used, they should be marked with [FastClonerClonable]
        _source.AppendLine($"                result.Add(kvp.Key, kvp.Value);");
        _source.AppendLine("            }");
        _source.AppendLine();
        _source.AppendLine("            return result;");
        _source.AppendLine("        }");
        _source.AppendLine();
    }

    private void WriteArrayCloneMethod(MemberModel member)
    {
        if (member.ElementTypeName == null) return;

        var typeName = member.TypeFullName;
        var methodName = _typeNameToMethodName[typeName];
        var isSafe = member.ElementIsSafe;
        var hasClonableAttr = member.ElementHasClonableAttr;
        var needsState = MemberNeedsCircularRefTracking(member);
        
        if (needsState)
        {
            _needsStateClass = true;
        }

        WriteHelperMethodSignature(typeName, methodName, needsState);
        _source.AppendLine("        {");
        _source.AppendLine("            if (source == null) return null;");
        
        if (needsState)
        {
            _source.AppendLine("            if (state != null)");
            _source.AppendLine("            {");
            _source.AppendLine("                var known = state.GetKnownRef(source);");
            _source.AppendLine($"                if (known != null) return ({typeName})known;");
            _source.AppendLine("            }");
        _source.AppendLine();
        }
        
        // Arrays use Length, not Count
        _source.AppendLine($"            var result = new {member.ElementTypeName}[source.Length];");
        
        if (needsState)
        {
            _source.AppendLine("            state?.AddKnownRef(source, result);");
        _source.AppendLine();
        }
        
        // Arrays use indexing, not Add()
        _source.AppendLine("            for (int i = 0; i < source.Length; i++)");
        _source.AppendLine("            {");

        if (isSafe)
        {
            _source.AppendLine("                result[i] = source[i];");
        }
        else if (hasClonableAttr)
        {
            _source.AppendLine($"                result[i] = source[i]?.FastDeepClone();");
        }
        else
        {
            // For other element types, shallow copy
            _source.AppendLine("                result[i] = source[i];");
        }

        _source.AppendLine("            }");
        _source.AppendLine();
        _source.AppendLine("            return result;");
        _source.AppendLine("        }");
        _source.AppendLine();
    }

    /// <summary>
    /// Writes the signature for a helper clone method, conditionally including state parameter.
    /// Note: methodName already includes the "FastClonerSgClone" prefix.
    /// </summary>
    private void WriteHelperMethodSignature(string typeName, string methodName, bool needsState)
    {
        if (needsState)
        {
            _source.AppendLine($"        private static {typeName}? {methodName}({typeName}? source, FcGeneratedCloneState? state)");
        }
        else
        {
            _source.AppendLine($"        private static {typeName}? {methodName}({typeName}? source)");
        }
    }

    /// <summary>
    /// Generates a call to a helper clone method, conditionally including state parameter.
    /// Note: methodName already includes the "FastClonerSgClone" prefix.
    /// </summary>
    private string GetHelperMethodCall(string methodName, string sourceExpression, bool needsState, string stateVar = "null")
    {
        if (needsState)
        {
            return $"{methodName}({sourceExpression}, {stateVar})";
        }
        else
        {
            return $"{methodName}({sourceExpression})";
        }
    }

    private void WriteFastCloneStateClass()
    {
        // Only generate state class if it's actually needed
        if (!_needsStateClass)
            return;
            
        _source.AppendLine();
        _source.AppendLine("        /// <summary>");
        _source.AppendLine("        /// State for tracking circular references during cloning.");
        _source.AppendLine("        /// </summary>");
        _source.AppendLine("        private class FcGeneratedCloneState");
        _source.AppendLine("        {");
        _source.AppendLine("            private readonly Dictionary<object, object> _knownRefs = new Dictionary<object, object>();");
        _source.AppendLine();
        _source.AppendLine("            public void AddKnownRef(object original, object clone)");
        _source.AppendLine("            {");
        _source.AppendLine("                if (original != null)");
        _source.AppendLine("                {");
        _source.AppendLine("                    _knownRefs[original] = clone;");
        _source.AppendLine("                }");
        _source.AppendLine("            }");
        _source.AppendLine();
        _source.AppendLine("            public object? GetKnownRef(object original)");
        _source.AppendLine("            {");
        _source.AppendLine("                if (original == null) return null;");
        _source.AppendLine("                return _knownRefs.TryGetValue(original, out var clone) ? clone : null;");
        _source.AppendLine("            }");
        _source.AppendLine("        }");
    }

    private void WriteFileFooter()
    {
        if (!string.IsNullOrEmpty(_model.Namespace))
        {
            _source.AppendLine("}");
        }
    }
}
