using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastCloner.SourceGenerator;

/// <summary>
/// Generates code for cloning individual members (assignments).
/// </summary>
internal static class MemberCloneGenerator
{
    public static string GetMemberAssignment(CloneGeneratorContext context, MemberModel member, string sourceVar, string stateVar, string indent = "            ")
    {
        var memberName = member.Name;

        // Skip read-only fields (can't assign)
        if (!member.IsProperty && member.IsReadOnly)
            return string.Empty;

        switch (member.TypeKind)
        {
            case MemberTypeKind.Safe:
                // Direct assignment for safe types (primitives, strings, etc.)
                // Both properties and fields can use object initializer syntax
                return $"{memberName} = {sourceVar}.{memberName}";

            case MemberTypeKind.Clonable:
            {
                // Always use InternalFastDeepClone for Clonable types to support circular references
                // Clonable types might have circular references even if the current type doesn't detect them
                // (e.g., CircularNodeD -> CircularNodeE, where E has circular refs with F)
                var extensionClassName = GetExtensionClassName(member.TypeFullName, context.Model.Namespace);
                
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
                    var inlineMemberNeedsState = context.NeedsCircularState(member.TypeFullName, inlineModelDefault) && context.CanHaveCircularReferences;
                    bool inlineShouldPassState = inlineMemberNeedsState || (stateVar != "null");
                    var inlineActualStateVar = inlineShouldPassState ? stateVar : "null";

                    return $"{memberName} = {GetImplicitCloneExpression(context, implicitModel, $"{sourceVar}.{memberName}", inlineActualStateVar, indent, member.IsNullable)}";
                }

                var helperMethodName = context.GetOrCreateHelperMethodName(member);
                // Re-fetch model if needed (though we checked it above for inlining, we might not have it if not inlining check skipped)
                context.TryGetImplicitTypeModel(member.TypeFullName, out implicitModel);
                
                // Assume implicitModel exists if TypeKind is Implicit, but handle safely
                bool modelDefault = implicitModel?.CanHaveCircularReferences ?? false;
                var memberNeedsState = context.NeedsCircularState(member.TypeFullName, modelDefault) && context.CanHaveCircularReferences;
                
                // If this is a registered type (method name is "Clone") and we're in a state-tracking context,
                // always pass state to use the private overload, even if the member type doesn't need state itself.
                // This is critical for circular reference tracking to work correctly.
                bool isRegisteredType = helperMethodName == "Clone";
                bool shouldPassState = memberNeedsState || (isRegisteredType && stateVar != "null");
                var actualStateVar = shouldPassState ? stateVar : "null";

                // Both properties and fields can use object initializer syntax
                return $"{memberName} = {GetHelperMethodCall(context, helperMethodName, $"{sourceVar}.{memberName}", shouldPassState, actualStateVar)}";
            }

            case MemberTypeKind.Collection:
            {
                var helperMethodName = context.GetOrCreateHelperMethodName(member);
                var memberNeedsState = MemberNeedsCircularRefTracking(context, member);
                var actualStateVar = memberNeedsState ? stateVar : "null";

                // Both properties and fields can use object initializer syntax
                return $"{memberName} = {GetHelperMethodCall(context, helperMethodName, $"{sourceVar}.{memberName}", memberNeedsState, actualStateVar)}";
            }

            case MemberTypeKind.Dictionary:
            {
                var helperMethodName = context.GetOrCreateHelperMethodName(member);
                var memberNeedsState = MemberNeedsCircularRefTracking(context, member);
                var actualStateVar = memberNeedsState ? stateVar : "null";

                // Both properties and fields can use object initializer syntax
                return $"{memberName} = {GetHelperMethodCall(context, helperMethodName, $"{sourceVar}.{memberName}", memberNeedsState, actualStateVar)}";
            }

            case MemberTypeKind.Array:
            {
                var helperMethodName = context.GetOrCreateHelperMethodName(member);
                var memberNeedsState = MemberNeedsCircularRefTracking(context, member);
                var actualStateVar = memberNeedsState ? stateVar : "null";

                // Both properties and fields can use object initializer syntax
                return $"{memberName} = {GetHelperMethodCall(context, helperMethodName, $"{sourceVar}.{memberName}", memberNeedsState, actualStateVar)}";
            }

            case MemberTypeKind.MultiDimArray:
            {
                // Multi-dimensional arrays are now fully supported by the source generator
                var helperMethodName = context.GetOrCreateHelperMethodName(member);
                var memberNeedsState = MemberNeedsCircularRefTracking(context, member);
                var actualStateVar = memberNeedsState ? stateVar : "null";

                // Both properties and fields can use object initializer syntax
                return $"{memberName} = {GetHelperMethodCall(context, helperMethodName, $"{sourceVar}.{memberName}", memberNeedsState, actualStateVar)}";
            }

            case MemberTypeKind.Object:
            case MemberTypeKind.Other:
            default:
                // Use Cloner<T> helper for generic/object/unknown types
                // This handles both FastCloner availability and safe type fallbacks
                context.NeedsClonerClass = true;

                // Both properties and fields can use object initializer syntax
                return $"{memberName} = Cloner<{member.TypeFullName}>.Clone({sourceVar}.{memberName}, {stateVar})";
        }
    }

    public static void WriteMemberCloning(CloneGeneratorContext context, MemberModel member, string resultVar, string sourceVar, string stateVar)
    {
        var memberName = member.Name;
        var sb = context.Source;

        // Skip read-only fields (can't assign in struct cloning)
        if (!member.IsProperty && member.IsReadOnly)
            return;
        
        // Skip init-only properties when using statement-based assignment
        // Init-only properties can only be assigned in object initializers, not individual statements
        if (member.IsProperty && member.IsInitOnly)
            return;

        switch (member.TypeKind)
        {
            case MemberTypeKind.Safe:
                // Direct assignment for safe types
                sb.AppendLine($"            {resultVar}.{memberName} = {sourceVar}.{memberName};");
                break;

            case MemberTypeKind.Clonable:
            {
                // Always use InternalFastDeepClone for Clonable types to support circular references
                // Clonable types might have circular references even if the current type doesn't detect them
                // (e.g., CircularNodeD -> CircularNodeE, where E has circular refs with F)
                var extensionClassName = GetExtensionClassName(member.TypeFullName, context.Model.Namespace);
                
                // If we don't have state, create a new one to ensure circular reference tracking works
                string actualStateVar = stateVar;
                if (stateVar == "null")
                {
                    // Create a new state variable for this cloning operation
                    // This ensures circular references in the member type are tracked
                    actualStateVar = "new FcGeneratedCloneState()";
                }
                
                sb.AppendLine($"            {resultVar}.{memberName} = {extensionClassName}.InternalFastDeepClone({sourceVar}.{memberName}, {actualStateVar});");
            }
                break;

            case MemberTypeKind.Implicit:
            {
                // Optimally inline if used only once
                if (context.ShouldInline(member.TypeFullName) && 
                    context.TryGetImplicitTypeModel(member.TypeFullName, out var implicitModel))
                {
                    bool inlineModelDefault = implicitModel.CanHaveCircularReferences;
                    var inlineMemberNeedsState = context.NeedsCircularState(member.TypeFullName, inlineModelDefault) && context.CanHaveCircularReferences;
                    bool inlineShouldPassState = inlineMemberNeedsState || (stateVar != "null");
                    var inlineActualStateVar = inlineShouldPassState ? stateVar : "null";

                    sb.AppendLine(GetImplicitCloneStatement(context, implicitModel, member.Name, resultVar, sourceVar, inlineActualStateVar, member.IsNullable));
                    break;
                }

                var helperMethodName = context.GetOrCreateHelperMethodName(member);
                context.TryGetImplicitTypeModel(member.TypeFullName, out implicitModel);
                bool modelDefault = implicitModel?.CanHaveCircularReferences ?? false;
                var memberNeedsState = context.NeedsCircularState(member.TypeFullName, modelDefault) && context.CanHaveCircularReferences;
                
                // If this is a registered type (method name is "Clone") and we're in a state-tracking context,
                // always pass state to use the private overload, even if the member type doesn't need state itself.
                // This is critical for circular reference tracking to work correctly.
                bool isRegisteredType = helperMethodName == "Clone";
                bool shouldPassState = memberNeedsState || (isRegisteredType && stateVar != "null");
                var actualStateVar = shouldPassState ? stateVar : "null";
                
                sb.AppendLine($"            {resultVar}.{memberName} = {GetHelperMethodCall(context, helperMethodName, $"{sourceVar}.{memberName}", shouldPassState, actualStateVar)};");
            }
                break;

            case MemberTypeKind.Collection:
            case MemberTypeKind.Dictionary:
            case MemberTypeKind.Array:
            case MemberTypeKind.MultiDimArray:
            {
                // All collection types (including multi-dimensional arrays) use helper methods
                var helperMethodName = context.GetOrCreateHelperMethodName(member);
                var memberNeedsState = MemberNeedsCircularRefTracking(context, member);
                var actualStateVar = memberNeedsState ? stateVar : "null";
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

        // Safe types don't need tracking
        if (member.TypeKind == MemberTypeKind.Safe)
            return false;

        // Types with FastClonerClonable attribute might have circular refs
        if (member.TypeKind == MemberTypeKind.Clonable)
            return true;

        // For collections/arrays, check element metadata
        if (member.TypeKind == MemberTypeKind.Collection || member.TypeKind == MemberTypeKind.Array || member.TypeKind == MemberTypeKind.MultiDimArray)
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

    internal static string GetImplicitCloneStatement(CloneGeneratorContext context, TypeModel implicitModel, string memberName, string resultVar, string sourceVar, string stateVar, bool isMemberNullable)
    {
        var sb = new StringBuilder();
        var sourceProp = $"{sourceVar}.{memberName}";
        
        // Use unique variable ID for safety
        string safeName = $"l_{memberName}_{context.GetNextVariableId()}";
        
        // 1. Capture local variable
        sb.AppendLine($"            var {safeName} = {sourceProp};");
        
        // 2. Check for null (unless struct or trusted non-nullable)
        // If TrustNullability is enabled on the parent context, and the member is NOT nullable, we trust it's not null.
        bool skipNullCheck = context.Model.TrustNullability && !isMemberNullable;

        if (!implicitModel.IsStruct && !skipNullCheck)
        {
            sb.AppendLine($"            if ({safeName} != null)");
            sb.AppendLine("            {");
        }
        
        // 3. Create instance
        var typeName = implicitModel.FullyQualifiedName;
        // Use object initializer for properties
        
        var assignmentIndent = (!implicitModel.IsStruct && !skipNullCheck) ? "                    " : "                ";
        
        // Use appropriate indentation for the new statement
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
        
        var assignments = new List<string>();
        foreach (var member in implicitModel.Members)
        {
             // Recurse using the captured local variable instead of sourceProp
             // This ensures we use the already-read value
             var assign = GetMemberAssignment(context, member, safeName, stateVar, assignmentIndent);
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
        // Optimization: if sourceVar is a simple identifier (variable), use it directly.
        // Otherwise (property access, indexer), capture it into a local using pattern matching to avoid repeated access/thread safety issues.
        bool isSimpleIdentifier = System.Text.RegularExpressions.Regex.IsMatch(sourceVar, "^[a-zA-Z0-9_]+$");
        
        string variableName = sourceVar;
        string wrapperPrefix = "";
        string wrapperSuffix = "";
        
        // Check trust nullability
        bool skipNullCheck = context.Model.TrustNullability && !isMemberNullable;

        if (!isSimpleIdentifier)
        {
            // Generate a safe variable name with unique ID to prevent scope collisions
            string safeBase = System.Text.RegularExpressions.Regex.Replace(sourceVar, "[^a-zA-Z0-9_]", "_");
            safeBase = System.Text.RegularExpressions.Regex.Replace(safeBase, "_+", "_").Trim('_');
            string safeName = $"l_{safeBase}_{context.GetNextVariableId()}";
            
            variableName = safeName;
            
            if (implicitModel.IsStruct)
            {
                // For structs: always true pattern
                // (source.Prop is var local ? new ... : default)
                // Note: default(Type) is safer than default
                wrapperPrefix = $"({sourceVar} is var {variableName} ? ";
                wrapperSuffix = $" : default({implicitModel.FullyQualifiedName}))";
            }
            else
            {
                if (skipNullCheck)
                {
                    // No null check, but still need to capture variable
                    wrapperPrefix = $"({sourceVar} is var {variableName} ? ";
                    wrapperSuffix = " : null)"; // Should technically not be reached if trusted? But expression needs else. 
                    // Wait, if we trust it's not null, we don't need the null check branch. 
                    // But 'is var x' always matches.
                    // If we trust it, we just return new X {...} using the captured variable.
                    // But we can't easily capture a variable in an expression without 'is var' pattern which requires a bool result or pattern matching switch.
                    // C# expression: (source.Prop is var local) is a bool. We can use it.
                    // (source.Prop is var local ? new ... : throw/null)
                    // If trusted, we can say it's never null. 
                    wrapperSuffix = " : null!)"; // or throw?
                }
                else
                {
                    // For classes: null check pattern
                    // (source.Prop is var local && local != null ? new ... : null)
                    wrapperPrefix = $"({sourceVar} is var {variableName} && {variableName} != null ? ";
                    wrapperSuffix = " : null)";
                }
            }
        }
        else
        {
            // Existing logic for null check if it's a class and already a variable
            if (!implicitModel.IsStruct && !skipNullCheck)
            {
                wrapperPrefix = $"{variableName} == null ? null : ";
                wrapperSuffix = "";
            }
        }

        var typeName = implicitModel.FullyQualifiedName;
        var innerIndent = indent + "    ";
        
        var assignments = new List<string>();
        foreach (var member in implicitModel.Members)
        {
             // For object initializer, we need assignments.
             // We can recurse into GetMemberAssignment.
             // Use variableName instead of sourceVar
             var assign = GetMemberAssignment(context, member, variableName, stateVar, innerIndent);
             if (!string.IsNullOrEmpty(assign))
             {
                 assignments.Add(assign);
             }
        }
        
        // Construct initializer
        string init;
        if (assignments.Count > 0)
        {
             init = $"new {typeName}\n{indent}{{\n{string.Join(",\n", assignments.Select(a => $"{indent}    {a}"))}\n{indent}}}";
        }
        else
        {
             init = $"new {typeName}()";
        }
        
        if (implicitModel.IsStruct && isSimpleIdentifier)
        {
            return init;
        }
        else
        {
            return wrapperPrefix + init + wrapperSuffix;
        }
    }

    private static string GetHelperMethodCall(CloneGeneratorContext context, string methodName, string sourceExpression, bool needsState, string stateVar = "null")
    {
        var typeParams = GetTypeParametersString(context.Model);

        if (needsState)
        {
            return $"{methodName}{typeParams}({sourceExpression}, {stateVar})";
        }
        else
        {
            return $"{methodName}{typeParams}({sourceExpression})";
        }
    }

    private static string GetTypeParametersString(TypeModel model)
    {
        if (model.TypeParameters.Count == 0)
            return string.Empty;

        return $"<{string.Join(", ", model.TypeParameters)}>";
    }

    /// <summary>
    /// Extracts the type name from a fully qualified name (e.g., "FastCloner.Tests.ClassWithCircularRefNoCtor" -> "ClassWithCircularRefNoCtor").
    /// </summary>
    private static string GetTypeNameFromFullName(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');
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
        var typeName = GetTypeNameFromFullName(typeFullName);
        
        if (string.IsNullOrEmpty(currentNamespace))
        {
            // If no namespace, extension class is at root level
            return $"{typeName}FastDeepCloneExtensions";
        }
        
        return $"global::{currentNamespace}.{typeName}FastDeepCloneExtensions";
    }
}
