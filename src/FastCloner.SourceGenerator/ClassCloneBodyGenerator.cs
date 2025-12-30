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
        return model is { HasParameterlessConstructor: false, IsStruct: false, IsRecord: false };
    }

    /// <summary>
    /// Checks if any of the given types need FormatterServices.
    /// </summary>
    public static bool NeedsFormatterServices(IEnumerable<TypeModel> types)
    {
        foreach (TypeModel? type in types)
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
        StringBuilder sb = ctx.Source;
        bool hasParameterlessConstructor = ctx.Model.HasParameterlessConstructor;
        bool isRecord = ctx.Model.IsRecord;
        
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
            WriteInstanceCreation(ctx, sb, typeName, hasParameterlessConstructor, isRecord, sourceVarName);
            
            string stateVarForAdd = stateVarName ?? "state";
            string nullConditional = useNullConditional ? "?" : "";
            sb.AppendLine($"            {stateVarForAdd}{nullConditional}.AddKnownRef({sourceVarName}, result);");
            sb.AppendLine();

            foreach (MemberModel member in ctx.Model.Members)
            {
                MemberCloneGenerator.WriteMemberCloning(ctx, member, "result", sourceVarName, stateVarForAdd);
            }
            
            sb.AppendLine();
            sb.AppendLine("            return result;");
        }
        else
        {
            // For non-circular types, we can use mixed statement/initializer syntax
            if (hasParameterlessConstructor)
            {
                // Create instance start
                sb.Append($"            var result = new {typeName}");
                
                // Collect init-only and required properties for object initializer
                List<string> initOnlyMembers = [];
                foreach (MemberModel member in ctx.Model.Members)
                {
                    if (member is { IsProperty: true, IsInitOnly: true } || member.IsRequired)
                    {
                        string assignment = MemberCloneGenerator.GetMemberAssignment(ctx, member, sourceVarName, "null", "                ");
                        if (!string.IsNullOrEmpty(assignment))
                        {
                            initOnlyMembers.Add($"                {assignment}");
                        }
                    }
                }

                if (initOnlyMembers.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("            {");
                    sb.AppendLine(string.Join(",\n", initOnlyMembers));
                    sb.AppendLine("            };");
                }
                else
                {
                    sb.AppendLine("();");
                }

                // Use statements for everything else (better for JIT and null handling)
                foreach (MemberModel member in ctx.Model.Members)
                {
                    // Skip if already handled in initializer (init-only or required)
                    if (member is { IsProperty: true, IsInitOnly: true } || member.IsRequired)
                        continue;

                    if (!member.IsProperty || !member.IsInitOnly)
                    {
                        MemberCloneGenerator.WriteMemberCloning(ctx, member, "result", sourceVarName, "null");
                    }
                }
            }
            else
            {
                // Use FormatterServices.GetUninitializedObject to create instance without calling constructor
                WriteInstanceCreation(ctx, sb, typeName, hasParameterlessConstructor, isRecord, sourceVarName);
                
                // Then assign members individually (no state needed for non-circular types)
                foreach (MemberModel member in ctx.Model.Members)
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
    private static void WriteInstanceCreation(CloneGeneratorContext ctx, StringBuilder sb, string typeName, bool hasParameterlessConstructor, bool isRecord, string sourceVarName = "source")
    {
        if (isRecord)
        {
            // Records use 'with' expression for shallow copy (modifiable later)
            sb.AppendLine($"            var result = {sourceVarName} with {{ }};");
        }
        else if (hasParameterlessConstructor)
        {
            // Check for required members
            List<string> requiredMembers = [];
            foreach (MemberModel member in ctx.Model.Members)
            {
                if (member.IsRequired)
                {
                    // Assign default! to satisfy compiler contract
                    // We will assign real values later
                    requiredMembers.Add($"                {member.Name} = default!");
                }
            }

            if (requiredMembers.Count > 0)
            {
                sb.AppendLine($"            var result = new {typeName}");
                sb.AppendLine("            {");
                sb.AppendLine(string.Join(",\n", requiredMembers));
                sb.AppendLine("            };");
            }
            else
            {
                sb.AppendLine($"            var result = new {typeName}();");
            }
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
    /// Getter-only collections are handled separately via population.
    /// </summary>
    /// <param name="sourceVarName">The name of the source variable (usually "source" for classes, "src" for structs after null check)</param>
    private static void WriteRecordCloneBody(CloneGeneratorContext ctx, string typeName, string sourceVarName = "source")
    {
        StringBuilder sb = ctx.Source;
        
        // Collect members that need deep cloning (not safe types) and can be assigned
        List<string> deepCloneAssignments = [];
        // Collect getter-only collections that need population
        List<MemberModel> getterOnlyCollections = [];
        
        foreach (MemberModel member in ctx.Model.Members)
        {
            // Check for getter-only collection properties
            if (member is { IsProperty: true, HasGetter: true, HasSetter: false, IsInitOnly: false })
            {
                // These need to be handled via population, not 'with' expression
                if (member.TypeKind == MemberTypeKind.Collection || member.TypeKind == MemberTypeKind.Dictionary)
                {
                    getterOnlyCollections.Add(member);
                }
                continue;
            }
            
            // Skip safe types - they're already shallow copied by 'with'
            if (member.TypeKind == MemberTypeKind.Safe)
                continue;
            
            // Skip read-only members (that aren't getter-only collections, which we handled above)
            if (member.IsReadOnly)
                continue;
            
            string assignment = MemberCloneGenerator.GetMemberAssignment(ctx, member, sourceVarName, "null", "                ");
            if (!string.IsNullOrEmpty(assignment))
            {
                deepCloneAssignments.Add($"                {assignment}");
            }
        }
        
        // If we have getter-only collections, we need to use statement-based approach
        if (getterOnlyCollections.Count > 0)
        {
            // Create result using 'with' expression
            if (deepCloneAssignments.Count == 0)
            {
                sb.AppendLine($"            var result = {sourceVarName} with {{ }};");
            }
            else
            {
                sb.AppendLine($"            var result = {sourceVarName} with");
                sb.AppendLine("            {");
                sb.AppendLine(string.Join(",\n", deepCloneAssignments));
                sb.AppendLine("            };");
            }
            
            // Populate getter-only collections
            foreach (MemberModel member in getterOnlyCollections)
            {
                MemberCloneGenerator.WriteMemberCloning(ctx, member, "result", sourceVarName, "null");
            }
            
            sb.AppendLine();
            sb.AppendLine("            return result;");
        }
        else if (deepCloneAssignments.Count == 0)
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

