using System.Collections.Generic;
using System.Text;

namespace FastCloner.SourceGenerator;

/// <summary>
/// Helper class for generating class clone body code.
/// Shared between ContextCodeGenerator and CloneCodeGenerator to avoid duplication.
/// </summary>
internal static class ClassCloneBodyGenerator
{
    /// <summary>
    /// Checks if FormatterServices using statement is needed for the given type.
    /// Records don't need FormatterServices since they have copy constructors.
    /// </summary>
    public static bool NeedsFormatterServices(TypeModel model)
    {
        return !model.HasParameterlessConstructor && !model.IsStruct && !model.IsRecord;
    }

    /// <summary>
    /// Checks if any of the given types need FormatterServices.
    /// </summary>
    public static bool NeedsFormatterServices(IEnumerable<TypeModel> types)
    {
        foreach (var type in types)
        {
            if (NeedsFormatterServices(type))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Writes the complete class clone body code.
    /// </summary>
    /// <param name="ctx">The clone generator context</param>
    /// <param name="typeName">Name of the type to clone</param>
    /// <param name="useState">Whether to use state tracking for circular references</param>
    /// <param name="stateVarName">Name of the state variable (null if not using state)</param>
    /// <param name="useNullConditional">Whether to use null-conditional operator (?.) when calling AddKnownRef</param>
    /// <param name="sourceVarName">Name of the source variable (usually "source" for classes, "src" for structs after null check)</param>
    public static void WriteClassCloneBody(
        CloneGeneratorContext ctx,
        string typeName,
        bool useState,
        string? stateVarName = null,
        bool useNullConditional = false,
        string sourceVarName = "source")
    {
        var sb = ctx.Source;
        var hasParameterlessConstructor = ctx.Model.HasParameterlessConstructor;
        var isRecord = ctx.Model.IsRecord;
        
        // For records without circular references, use the idiomatic 'with' expression
        if (isRecord && !useState)
        {
            WriteRecordCloneBody(ctx, typeName, sourceVarName);
            return;
        }
        
        if (useState)
        {
            // When tracking circular references, we must register the instance BEFORE cloning members
            // to avoid infinite recursion (StackOverflowException) in case of cycles.
            // This requires us to instantiate first, then register, then assign members.
            WriteInstanceCreation(sb, typeName, hasParameterlessConstructor, isRecord, sourceVarName);
            
            var stateVarForAdd = stateVarName ?? "state";
            var nullConditional = useNullConditional ? "?" : "";
            sb.AppendLine($"            {stateVarForAdd}{nullConditional}.AddKnownRef({sourceVarName}, result);");
            sb.AppendLine();

            foreach (var member in ctx.Model.Members)
            {
                MemberCloneGenerator.WriteMemberCloning(ctx, member, "result", sourceVarName, stateVarForAdd);
            }
            
            sb.AppendLine();
            sb.AppendLine("            return result;");
        }
        else
        {
            // For non-circular types, we can use object initializer syntax if constructor exists
            if (hasParameterlessConstructor)
            {
                sb.AppendLine($"            var result = new {typeName}");
                sb.AppendLine("            {");

                var memberAssignments = new List<string>();
                foreach (var member in ctx.Model.Members)
                {
                    var assignment = MemberCloneGenerator.GetMemberAssignment(ctx, member, sourceVarName, "null");
                    if (!string.IsNullOrEmpty(assignment))
                    {
                        memberAssignments.Add($"                {assignment}");
                    }
                }

                if (memberAssignments.Count > 0)
                {
                    sb.AppendLine(string.Join(",\n", memberAssignments));
                }

                sb.AppendLine("            };");
            }
            else
            {
                // Use FormatterServices.GetUninitializedObject to create instance without calling constructor
                WriteInstanceCreation(sb, typeName, hasParameterlessConstructor, isRecord, sourceVarName);
                
                // Then assign members individually (no state needed for non-circular types)
                foreach (var member in ctx.Model.Members)
                {
                    MemberCloneGenerator.WriteMemberCloning(ctx, member, "result", sourceVarName, "null");
                }
            }
            
            sb.AppendLine();
            sb.AppendLine("            return result;");
        }
    }

    /// <summary>
    /// Writes the code to instantiate a class, handling both parameterless and non-parameterless constructors.
    /// For records, uses the 'with' expression for shallow copy.
    /// </summary>
    /// <param name="sourceVarName">The name of the source variable (usually "source" for classes, "src" for structs after null check)</param>
    private static void WriteInstanceCreation(StringBuilder sb, string typeName, bool hasParameterlessConstructor, bool isRecord, string sourceVarName = "source")
    {
        if (isRecord)
        {
            // Records use 'with' expression for shallow copy (modifiable later)
            sb.AppendLine($"            var result = {sourceVarName} with {{ }};");
        }
        else if (hasParameterlessConstructor)
        {
            sb.AppendLine($"            var result = new {typeName}();");
        }
        else
        {
            // Use FormatterServices.GetUninitializedObject to create instance without calling constructor
            sb.AppendLine($"            var result = ({typeName})FormatterServices.GetUninitializedObject(typeof({typeName}));");
        }
    }

    /// <summary>
    /// Writes the clone body for records using the 'with' expression.
    /// Only includes members that need deep cloning in the 'with' expression.
    /// </summary>
    /// <param name="sourceVarName">The name of the source variable (usually "source" for classes, "src" for structs after null check)</param>
    private static void WriteRecordCloneBody(CloneGeneratorContext ctx, string typeName, string sourceVarName = "source")
    {
        var sb = ctx.Source;
        
        // Collect members that need deep cloning (not safe types)
        var deepCloneAssignments = new List<string>();
        foreach (var member in ctx.Model.Members)
        {
            // Skip safe types - they're already shallow copied by 'with'
            if (member.TypeKind == MemberTypeKind.Safe)
                continue;
            
            // Skip read-only members
            if (member.IsReadOnly)
                continue;
            
            var assignment = MemberCloneGenerator.GetMemberAssignment(ctx, member, sourceVarName, "null");
            if (!string.IsNullOrEmpty(assignment))
            {
                deepCloneAssignments.Add($"                {assignment}");
            }
        }
        
        if (deepCloneAssignments.Count == 0)
        {
            // All members are safe - simple shallow copy
            sb.AppendLine($"            return {sourceVarName} with {{ }};");
        }
        else
        {
            // Use 'with' expression with only the members that need deep cloning
            sb.AppendLine($"            return {sourceVarName} with");
            sb.AppendLine("            {");
            sb.AppendLine(string.Join(",\n", deepCloneAssignments));
            sb.AppendLine("            };");
        }
    }
}

