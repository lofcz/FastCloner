using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastCloner.SourceGenerator;

internal static class MemberCloneGenerator
{
    public static string GetMemberAssignment(CloneGeneratorContext context, MemberModel member, string sourceVar, string stateVar, string indent = "            ")
    {
        string memberName = member.Name;

        switch (member)
        {
            // Skip read-only fields (can't assign)
            case { IsProperty: false, IsReadOnly: true }:
            // Skip getter-only properties in object initializers - they need statement-based population
            case { IsProperty: true, HasGetter: true, HasSetter: false, IsInitOnly: false }:
                return string.Empty;
            default:
                switch (member.TypeKind)
                {
                    case MemberTypeKind.Safe:
                    {
                        // Direct assignment for safe types (primitives, strings, etc.)
                        // Both properties and fields can use object initializer syntax
                        return $"{memberName} = {sourceVar}.{memberName}";
                    }
                    case MemberTypeKind.Clonable:
                    {
                        // Always use InternalFastDeepClone for Clonable types to support circular references
                        // Clonable types might have circular references even if the current type doesn't detect them
                        // (e.g., CircularNodeD -> CircularNodeE, where E has circular refs with F)
                        string extensionClassName = GetExtensionClassName(member.TypeFullName, context.Model.Namespace);
                
                        // If we don't have state, create a new one to ensure circular reference tracking works
                        string actualStateVar = stateVar;
                        if (stateVar == "null")
                        {
                            // Create a new state variable for this cloning operation
                            // This ensures circular references in the member type are tracked
                            actualStateVar = "new FcGeneratedCloneState()";
                        }
                
                        // Both properties and fields can use object initializer syntax
                        return $"{memberName} = {extensionClassName}.InternalFastDeepClone({sourceVar}.{memberName}, {actualStateVar})";
                    }
                    case MemberTypeKind.Implicit:
                    {
                        // Optimally inline if used only once
                        if (context.ShouldInline(member.TypeFullName) && 
                            context.TryGetImplicitTypeModel(member.TypeFullName, out var implicitModel))
                        {
                            bool inlineModelDefault = implicitModel.CanHaveCircularReferences;
                            bool inlineMemberNeedsState = context.NeedsCircularState(member.TypeFullName, inlineModelDefault) && context.CanHaveCircularReferences;
                            bool inlineShouldPassState = inlineMemberNeedsState || (stateVar != "null");
                            string inlineActualStateVar = inlineShouldPassState ? stateVar : "null";

                            return $"{memberName} = {GetImplicitCloneExpression(context, implicitModel, $"{sourceVar}.{memberName}", inlineActualStateVar, indent, member.IsNullable)}";
                        }

                        string helperMethodName = context.GetOrCreateHelperMethodName(member);
                        // Re-fetch model if needed (though we checked it above for inlining, we might not have it if not inlining check skipped)
                        context.TryGetImplicitTypeModel(member.TypeFullName, out implicitModel);
                
                        // Assume implicitModel exists if TypeKind is Implicit, but handle safely
                        bool modelDefault = implicitModel?.CanHaveCircularReferences ?? false;
                        bool memberNeedsState = context.NeedsCircularState(member.TypeFullName, modelDefault) && context.CanHaveCircularReferences;
                
                        // If this is a registered type (method name is "Clone") and we're in a state-tracking context,
                        // always pass state to use the private overload, even if the member type doesn't need state itself.
                        // This is critical for circular reference tracking to work correctly.
                        bool isRegisteredType = helperMethodName == "Clone";
                        bool shouldPassState = memberNeedsState || (isRegisteredType && stateVar != "null");
                        string actualStateVar = shouldPassState ? stateVar : "null";

                        // Both properties and fields can use object initializer syntax
                        return $"{memberName} = {GetHelperMethodCall(context, helperMethodName, $"{sourceVar}.{memberName}", shouldPassState, actualStateVar)}";
                    }
                    case MemberTypeKind.Collection:
                    case MemberTypeKind.Dictionary:
                    case MemberTypeKind.Array:
                    case MemberTypeKind.MultiDimArray:
                    {
                        string helperMethodName = context.GetOrCreateHelperMethodName(member);
                        bool memberNeedsState = MemberNeedsCircularRefTracking(context, member);
                        string actualStateVar = memberNeedsState ? stateVar : "null";
                        
                        return $"{memberName} = {GetHelperMethodCall(context, helperMethodName, $"{sourceVar}.{memberName}", memberNeedsState, actualStateVar)}";
                    }
                    case MemberTypeKind.Object:
                    case MemberTypeKind.Other:
                    default:
                        // Use Cloner<T> helper for generic/object/unknown types
                        // This handles both FastCloner availability and safe type fallbacks
                        context.NeedsClonerClass = true;

                        return $"{memberName} = Cloner<{member.TypeFullName}>.Clone({sourceVar}.{memberName}, {stateVar})";
                }
        }
    }

    public static void WriteMemberCloning(CloneGeneratorContext context, MemberModel member, string resultVar, string sourceVar, string stateVar)
    {
        string memberName = member.Name;
        StringBuilder sb = context.Source;

        switch (member)
        {
            // Skip read-only fields (can't assign in struct cloning)
            case { IsProperty: false, IsReadOnly: true }:
            // Skip init-only properties when using statement-based assignment
            // Init-only properties can only be assigned in object initializers, not individual statements
            case { IsProperty: true, IsInitOnly: true }:
                return;
            // Handle getter-only collection properties - populate instead of replace
            case { IsProperty: true, HasGetter: true, HasSetter: false, IsInitOnly: false }:
                WriteGetterOnlyCollectionPopulation(context, member, resultVar, sourceVar, stateVar);
                return;
            default:
                switch (member.TypeKind)
                {
                    case MemberTypeKind.Safe:
                        sb.AppendLine($"            {resultVar}.{memberName} = {sourceVar}.{memberName};");
                        break;

                    case MemberTypeKind.Clonable:
                    {
                        string extensionClassName = GetExtensionClassName(member.TypeFullName, context.Model.Namespace);
                
                        string actualStateVar = stateVar;
                        if (stateVar == "null")
                        {
                            actualStateVar = "new FcGeneratedCloneState()";
                        }
                
                        sb.AppendLine($"            {resultVar}.{memberName} = {extensionClassName}.InternalFastDeepClone({sourceVar}.{memberName}, {actualStateVar});");
                    }
                        break;

                    case MemberTypeKind.Implicit:
                    {
                        if (context.ShouldInline(member.TypeFullName) && 
                            context.TryGetImplicitTypeModel(member.TypeFullName, out var implicitModel))
                        {
                            bool inlineModelDefault = implicitModel.CanHaveCircularReferences;
                            bool inlineMemberNeedsState = context.NeedsCircularState(member.TypeFullName, inlineModelDefault) && context.CanHaveCircularReferences;
                            bool inlineShouldPassState = inlineMemberNeedsState || (stateVar != "null");
                            string inlineActualStateVar = inlineShouldPassState ? stateVar : "null";

                            sb.AppendLine(GetImplicitCloneStatement(context, implicitModel, member.Name, resultVar, sourceVar, inlineActualStateVar, member.IsNullable));
                            break;
                        }

                        string helperMethodName = context.GetOrCreateHelperMethodName(member);
                        context.TryGetImplicitTypeModel(member.TypeFullName, out implicitModel);
                        bool modelDefault = implicitModel?.CanHaveCircularReferences ?? false;
                        bool memberNeedsState = context.NeedsCircularState(member.TypeFullName, modelDefault) && context.CanHaveCircularReferences;
                        bool isRegisteredType = helperMethodName == "Clone";
                        bool shouldPassState = memberNeedsState || (isRegisteredType && stateVar != "null");
                        string actualStateVar = shouldPassState ? stateVar : "null";
                
                        sb.AppendLine($"            {resultVar}.{memberName} = {GetHelperMethodCall(context, helperMethodName, $"{sourceVar}.{memberName}", shouldPassState, actualStateVar)};");
                    }
                        break;

                    case MemberTypeKind.Collection:
                    case MemberTypeKind.Dictionary:
                    case MemberTypeKind.Array:
                    case MemberTypeKind.MultiDimArray:
                    {
                        // All collection types (including multi-dimensional arrays) use helper methods
                        string helperMethodName = context.GetOrCreateHelperMethodName(member);
                        bool memberNeedsState = MemberNeedsCircularRefTracking(context, member);
                        string actualStateVar = memberNeedsState ? stateVar : "null";
                        sb.AppendLine($"            {resultVar}.{memberName} = {GetHelperMethodCall(context, helperMethodName, $"{sourceVar}.{memberName}", memberNeedsState, actualStateVar)};");
                    }
                        break;

                    case MemberTypeKind.Object:
                    case MemberTypeKind.Other:
                    default:
                        // Use Cloner<T> helper for generic/object/unknown types
                        context.NeedsClonerClass = true;
                        sb.AppendLine($"            {resultVar}.{memberName} = Cloner<{member.TypeFullName}>.Clone({sourceVar}.{memberName}, {stateVar});");
                        break;
                }

                break;
        }
    }
    
    private static void WriteGetterOnlyCollectionPopulation(CloneGeneratorContext context, MemberModel member, string resultVar, string sourceVar, string stateVar)
    {
        StringBuilder sb = context.Source;
        string memberName = member.Name;
        
        // Only handle Collection and Dictionary types - these are the only populatable types
        if (member.TypeKind != MemberTypeKind.Collection && member.TypeKind != MemberTypeKind.Dictionary)
            return;
        
        // Get the helper method that clones this collection type
        string helperMethodName = context.GetOrCreateHelperMethodName(member);
        bool memberNeedsState = MemberNeedsCircularRefTracking(context, member);
        string actualStateVar = memberNeedsState ? stateVar : "null";
        
        // Generate unique variable name for the cloned collection
        string clonedVar = $"cloned_{context.GetNextVariableId()}";
        
        // Check if source collection is null
        sb.AppendLine($"            if ({sourceVar}.{memberName} != null)");
        sb.AppendLine("            {");
        
        // Clone the source collection using the existing helper (handles all the complexity)
        string helperCall = GetHelperMethodCall(context, helperMethodName, $"{sourceVar}.{memberName}", memberNeedsState, actualStateVar);
        sb.AppendLine($"                var {clonedVar} = {helperCall};");
        sb.AppendLine($"                if ({clonedVar} != null)");
        sb.AppendLine("                {");
        
        // Clear target and copy from cloned collection
        sb.AppendLine($"                    {resultVar}.{memberName}.Clear();");
        
        if (member.TypeKind == MemberTypeKind.Dictionary)
        {
            // For dictionaries, copy key-value pairs
            sb.AppendLine($"                    foreach (var kvp in {clonedVar})");
            sb.AppendLine("                    {");
            // Use indexer - works for all dictionary types including ConcurrentDictionary
            sb.AppendLine($"                        {resultVar}.{memberName}[kvp.Key] = kvp.Value;");
            sb.AppendLine("                    }");
        }
        else
        {
            // For collections, use the appropriate add method based on collection kind
            string addMethod = GetAddMethodForCollection(member.CollectionKind);
            sb.AppendLine($"                    foreach (var item in {clonedVar})");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        {resultVar}.{memberName}.{addMethod}(item);");
            sb.AppendLine("                    }");
        }
        
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }
    
    /// <summary>
    /// Gets the appropriate Add method name for a collection kind.
    /// </summary>
    private static string GetAddMethodForCollection(CollectionKind kind)
    {
        return kind switch
        {
            CollectionKind.Queue or CollectionKind.ConcurrentQueue => "Enqueue",
            CollectionKind.Stack or CollectionKind.ConcurrentStack => "Push",
            CollectionKind.LinkedList => "AddLast",
            _ => "Add"
        };
    }

    /// <summary>
    /// Checks if a member needs circular reference tracking based on pre-analyzed metadata.
    /// If the root type analysis determined no circular references are possible, this always returns false.
    /// </summary>
    public static bool MemberNeedsCircularRefTracking(CloneGeneratorContext context, MemberModel member)
    {
        // If the root type can't have circular references, nothing needs tracking
        if (!context.CanHaveCircularReferences)
            return false;

        switch (member.TypeKind)
        {
            // Safe types don't need tracking
            case MemberTypeKind.Safe:
                return false;
            // Types with FastClonerClonable attribute might have circular refs
            case MemberTypeKind.Clonable:
                return true;
            // For collections/arrays, check element metadata
            case MemberTypeKind.Collection:
            case MemberTypeKind.Array:
            case MemberTypeKind.MultiDimArray:
            {
                // If element is safe, no tracking needed
                if (member.ElementIsSafe)
                    return false;
                // If element has clonable attribute, tracking needed
                if (member.ElementHasClonableAttr)
                    return true;
                break;
            }
        }

        // For other types, assume they might need tracking if not safe
        return true;
    }

    private static string GetImplicitCloneStatement(CloneGeneratorContext context, TypeModel implicitModel, string memberName, string resultVar, string sourceVar, string stateVar, bool isMemberNullable)
    {
        StringBuilder sb = new StringBuilder();
        string sourceProp = $"{sourceVar}.{memberName}";
        string safeName = $"l_{memberName}_{context.GetNextVariableId()}";
        
        // 1. Capture local variable
        sb.AppendLine($"            var {safeName} = {sourceProp};");
        
        // 2. Check for null (unless struct or trusted non-nullable)
        bool skipNullCheck = context.Model.TrustNullability && !isMemberNullable;

        if (!implicitModel.IsStruct && !skipNullCheck)
        {
            sb.AppendLine($"            if ({safeName} != null)");
            sb.AppendLine("            {");
        }
        
        // 3. Create instance
        string typeName = implicitModel.FullyQualifiedName;
        string assignmentIndent = !implicitModel.IsStruct && !skipNullCheck ? "                    " : "                ";
        
        if (!implicitModel.IsStruct && !skipNullCheck)
        {
            sb.AppendLine($"                {resultVar}.{memberName} = new {typeName}");
            sb.AppendLine("                {");
        }
        else
        {
            sb.AppendLine($"            {resultVar}.{memberName} = new {typeName}");
            sb.AppendLine("            {");
        }
        
        List<string> assignments = [];
        
        foreach (MemberModel member in implicitModel.Members)
        {
             string assign = GetMemberAssignment(context, member, safeName, stateVar, assignmentIndent);
             if (!string.IsNullOrEmpty(assign))
             {
                 assignments.Add($"{assignmentIndent}{assign}");
             }
        }
        
        if (assignments.Count > 0)
        {
            sb.AppendLine(string.Join(",\n", assignments));
        }
        
        if (!implicitModel.IsStruct && !skipNullCheck)
        {
            sb.AppendLine("                };");
        }
        else
        {
            sb.AppendLine("            };");
        }
        
        if (!implicitModel.IsStruct && !skipNullCheck)
        {
            sb.AppendLine("            }");
        }
        
        return sb.ToString().TrimEnd();
    }

    internal static string GetImplicitCloneExpression(CloneGeneratorContext context, TypeModel implicitModel, string sourceVar, string stateVar, string indent = "            ", bool isMemberNullable = true)
    {
        bool isSimpleIdentifier = System.Text.RegularExpressions.Regex.IsMatch(sourceVar, "^[a-zA-Z0-9_]+$");
        
        string variableName = sourceVar;
        string wrapperPrefix = "";
        string wrapperSuffix = "";
        
        bool skipNullCheck = context.Model.TrustNullability && !isMemberNullable;

        if (!isSimpleIdentifier)
        {
            string safeBase = System.Text.RegularExpressions.Regex.Replace(sourceVar, "[^a-zA-Z0-9_]", "_");
            safeBase = System.Text.RegularExpressions.Regex.Replace(safeBase, "_+", "_").Trim('_');
            string safeName = $"l_{safeBase}_{context.GetNextVariableId()}";
            
            variableName = safeName;
            
            if (implicitModel.IsStruct)
            {
                wrapperPrefix = $"({sourceVar} is var {variableName} ? ";
                wrapperSuffix = $" : default({implicitModel.FullyQualifiedName}))";
            }
            else
            {
                if (skipNullCheck)
                {
                    wrapperPrefix = $"({sourceVar} is var {variableName} ? ";
                    wrapperSuffix = " : null!)";
                }
                else
                {
                    wrapperPrefix = $"({sourceVar} is var {variableName} && {variableName} != null ? ";
                    wrapperSuffix = " : null)";
                }
            }
        }
        else
        {
            if (!implicitModel.IsStruct && !skipNullCheck)
            {
                wrapperPrefix = $"{variableName} == null ? null : ";
                wrapperSuffix = "";
            }
        }

        string typeName = implicitModel.FullyQualifiedName;
        string innerIndent = indent + "    ";
        
        List<string> assignments = [];
        
        foreach (MemberModel member in implicitModel.Members)
        {
             string assign = GetMemberAssignment(context, member, variableName, stateVar, innerIndent);
             if (!string.IsNullOrEmpty(assign))
             {
                 assignments.Add(assign);
             }
        }

        string init = assignments.Count > 0 ? 
            $"new {typeName}\n{indent}{{\n{string.Join(",\n", assignments.Select(a => $"{indent}    {a}"))}\n{indent}}}" : 
            $"new {typeName}()";
        
        if (implicitModel.IsStruct && isSimpleIdentifier)
        {
            return init;
        }

        return wrapperPrefix + init + wrapperSuffix;
    }

    private static string GetHelperMethodCall(CloneGeneratorContext context, string methodName, string sourceExpression, bool needsState, string stateVar = "null")
    {
        string typeParams = GetTypeParametersString(context.Model);
        return needsState ? $"{methodName}{typeParams}({sourceExpression}, {stateVar})" : $"{methodName}{typeParams}({sourceExpression})";
    }

    private static string GetTypeParametersString(TypeModel model)
    {
        return model.TypeParameters.Count == 0 ? string.Empty : $"<{string.Join(", ", model.TypeParameters)}>";
    }
    
    private static string GetTypeNameFromFullName(string fullName)
    {
        int lastDot = fullName.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < fullName.Length - 1)
        {
            return fullName.Substring(lastDot + 1);
        }
        return fullName;
    }

    /// <summary>
    /// Gets the extension class name for a type. Extension classes are generated in the same namespace as the type.
    /// For nested types, the namespace is the containing namespace (not including containing types).
    /// </summary>
    private static string GetExtensionClassName(string typeFullName, string currentNamespace)
    {
        string typeName = GetTypeNameFromFullName(typeFullName);
        
        return string.IsNullOrEmpty(currentNamespace) ?
            $"{typeName}FastDeepCloneExtensions" : 
            $"global::{currentNamespace}.{typeName}FastDeepCloneExtensions";
    }
}