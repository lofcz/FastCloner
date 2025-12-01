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
                return member.IsProperty
                    ? $"{memberName} = {sourceVar}.{memberName}"
                    : string.Empty;

            case MemberTypeKind.Clonable:
                // Use generated FastDeepClone for marked types
                return member.IsProperty
                    ? $"{memberName} = {sourceVar}.{memberName}?.FastDeepClone()"
                    : string.Empty;

            case MemberTypeKind.Implicit:
            {
                var helperMethodName = context.GetOrCreateHelperMethodName(member);
                context.TryGetImplicitTypeModel(member.TypeFullName, out var implicitModel);
                // Assume implicitModel exists if TypeKind is Implicit, but handle safely
                var memberNeedsState = (implicitModel?.CanHaveCircularReferences ?? false) && context.CanHaveCircularReferences;
                var actualStateVar = memberNeedsState ? stateVar : "null";

                return member.IsProperty
                    ? $"{memberName} = {GetHelperMethodCall(context, helperMethodName, $"{sourceVar}.{memberName}", memberNeedsState, actualStateVar)}"
                    : string.Empty;
            }

            case MemberTypeKind.Collection:
            {
                var helperMethodName = context.GetOrCreateHelperMethodName(member);
                var memberNeedsState = MemberNeedsCircularRefTracking(context, member);
                var actualStateVar = memberNeedsState ? stateVar : "null";

                return member.IsProperty
                    ? $"{memberName} = {GetHelperMethodCall(context, helperMethodName, $"{sourceVar}.{memberName}", memberNeedsState, actualStateVar)}"
                    : string.Empty;
            }

            case MemberTypeKind.Dictionary:
            {
                var helperMethodName = context.GetOrCreateHelperMethodName(member);
                var memberNeedsState = MemberNeedsCircularRefTracking(context, member);
                var actualStateVar = memberNeedsState ? stateVar : "null";

                return member.IsProperty
                    ? $"{memberName} = {GetHelperMethodCall(context, helperMethodName, $"{sourceVar}.{memberName}", memberNeedsState, actualStateVar)}"
                    : string.Empty;
            }

            case MemberTypeKind.Array:
            {
                var helperMethodName = context.GetOrCreateHelperMethodName(member);
                var memberNeedsState = MemberNeedsCircularRefTracking(context, member);
                var actualStateVar = memberNeedsState ? stateVar : "null";

                return member.IsProperty
                    ? $"{memberName} = {GetHelperMethodCall(context, helperMethodName, $"{sourceVar}.{memberName}", memberNeedsState, actualStateVar)}"
                    : string.Empty;
            }

            case MemberTypeKind.Object:
            case MemberTypeKind.Other:
            default:
                // Use Cloner<T> helper for generic/object/unknown types
                // This handles both FastCloner availability and safe type fallbacks
                context.NeedsClonerClass = true;
                return member.IsProperty
                    ? $"{memberName} = Cloner<{member.TypeFullName}>.Clone({sourceVar}.{memberName})"
                    : string.Empty;
        }
    }

    public static void WriteMemberCloning(CloneGeneratorContext context, MemberModel member, string resultVar, string sourceVar, string stateVar)
    {
        var memberName = member.Name;
        var sb = context.Source;

        // Skip read-only fields (can't assign in struct cloning)
        if (!member.IsProperty && member.IsReadOnly)
            return;

        switch (member.TypeKind)
        {
            case MemberTypeKind.Safe:
                // Direct assignment for safe types
                sb.AppendLine($"            {resultVar}.{memberName} = {sourceVar}.{memberName};");
                break;

            case MemberTypeKind.Clonable:
                // Use generated FastDeepClone for marked types
                sb.AppendLine($"            {resultVar}.{memberName} = {sourceVar}.{memberName}?.FastDeepClone();");
                break;

            case MemberTypeKind.Implicit:
            {
                var helperMethodName = context.GetOrCreateHelperMethodName(member);
                context.TryGetImplicitTypeModel(member.TypeFullName, out var implicitModel);
                var memberNeedsState = (implicitModel?.CanHaveCircularReferences ?? false) && context.CanHaveCircularReferences;
                var actualStateVar = memberNeedsState ? stateVar : "null";
                sb.AppendLine($"            {resultVar}.{memberName} = {GetHelperMethodCall(context, helperMethodName, $"{sourceVar}.{memberName}", memberNeedsState, actualStateVar)};");
            }
                break;

            case MemberTypeKind.Collection:
            case MemberTypeKind.Dictionary:
            case MemberTypeKind.Array:
            {
                var helperMethodName = context.GetOrCreateHelperMethodName(member);
                var memberNeedsState = MemberNeedsCircularRefTracking(context, member);
                var actualStateVar = memberNeedsState ? stateVar : "null";
                sb.AppendLine($"            {resultVar}.{memberName} = {GetHelperMethodCall(context, helperMethodName, $"{sourceVar}.{memberName}", memberNeedsState, actualStateVar)};");
            }
                break;

            case MemberTypeKind.Object:
            case MemberTypeKind.Other:
            default:
                // Use Cloner<T> helper
                context.NeedsClonerClass = true;
                sb.AppendLine($"            {resultVar}.{memberName} = Cloner<{member.TypeFullName}>.Clone({sourceVar}.{memberName});");
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
}
