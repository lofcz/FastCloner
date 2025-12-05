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
    /// </summary>
    public static bool NeedsFormatterServices(TypeModel model)
    {
        return !model.HasParameterlessConstructor && !model.IsStruct;
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
    /// Writes the code to instantiate a class, handling both parameterless and non-parameterless constructors.
    /// </summary>
    /// <param name="sb">StringBuilder to write to</param>
    /// <param name="typeName">Name of the type to instantiate</param>
    /// <param name="hasParameterlessConstructor">Whether the type has a parameterless constructor</param>
    public static void WriteInstanceCreation(StringBuilder sb, string typeName, bool hasParameterlessConstructor)
    {
        if (hasParameterlessConstructor)
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
    /// Writes the complete class clone body code.
    /// </summary>
    /// <param name="ctx">The clone generator context</param>
    /// <param name="typeName">Name of the type to clone</param>
    /// <param name="useState">Whether to use state tracking for circular references</param>
    /// <param name="stateVarName">Name of the state variable (null if not using state)</param>
    /// <param name="useNullConditional">Whether to use null-conditional operator (?.) when calling AddKnownRef</param>
    public static void WriteClassCloneBody(
        CloneGeneratorContext ctx,
        string typeName,
        bool useState,
        string? stateVarName = null,
        bool useNullConditional = false)
    {
        var sb = ctx.Source;
        var hasParameterlessConstructor = ctx.Model.HasParameterlessConstructor;
        
        if (useState)
        {
            // When tracking circular references, we must register the instance BEFORE cloning members
            // to avoid infinite recursion (StackOverflowException) in case of cycles.
            // This requires us to instantiate first, then register, then assign members.
            WriteInstanceCreation(sb, typeName, hasParameterlessConstructor);
            
            var stateVarForAdd = stateVarName ?? "state";
            var nullConditional = useNullConditional ? "?" : "";
            sb.AppendLine($"            {stateVarForAdd}{nullConditional}.AddKnownRef(source, result);");
            sb.AppendLine();

            foreach (var member in ctx.Model.Members)
            {
                MemberCloneGenerator.WriteMemberCloning(ctx, member, "result", "source", stateVarForAdd);
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
                    var assignment = MemberCloneGenerator.GetMemberAssignment(ctx, member, "source", "null");
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
                WriteInstanceCreation(sb, typeName, hasParameterlessConstructor);
                
                // Then assign members individually (no state needed for non-circular types)
                foreach (var member in ctx.Model.Members)
                {
                    MemberCloneGenerator.WriteMemberCloning(ctx, member, "result", "source", "null");
                }
            }
            
            sb.AppendLine();
            sb.AppendLine("            return result;");
        }
    }
}

