using System;
using System.Text;

namespace FastCloner.SourceGenerator;

/// <summary>
/// Generates code for cloning individual members (assignments).
/// </summary>
internal static class MemberCloneGenerator
{
    public static string GetMemberAssignment(CloneGeneratorContext context, MemberModel member, string sourceVar, string stateVar)
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
                var helperMethodName = context.GetOrCreateHelperMethodName(member);
                context.TryGetImplicitTypeModel(member.TypeFullName, out var implicitModel);
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
                var helperMethodName = context.GetOrCreateHelperMethodName(member);
                context.TryGetImplicitTypeModel(member.TypeFullName, out var implicitModel);
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
