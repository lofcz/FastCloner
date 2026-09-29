using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastCloner.SourceGenerator;

internal static class MemberCloneGenerator
{
    public static string GetMemberAssignment(CloneGeneratorContext context, MemberModel member, string sourceVar, string stateVar, string indent = "            ")
    {
        // Object-initializer assignments always run against constructor-created instances.
        // Weaver state is not data: the constructor already produced correct fresh state and
        // copying the source's state over it would corrupt weaver-generated code (issue #48).
        if (member.IsWeaverState)
            return string.Empty;

        string memberName = member.Name;
        string nf = (!member.IsNullable && !member.IsValueType) ? "!" : "";
        
        if (member.AccessorStrategy != NonPublicAccessorStrategy.None)
            return string.Empty;

        switch (member)
        {
            case { IsProperty: false, IsReadOnly: true }:
            case { IsProperty: true, HasGetter: true, HasSetter: false, IsInitOnly: false }:
                return string.Empty;
            default:
                if (member.IsShallowClone)
                {
                    return $"{memberName} = {sourceVar}.{memberName}";
                }
                
                switch (member.TypeKind)
                {
                    case MemberTypeKind.Safe:
                    {
                        return $"{memberName} = {sourceVar}.{memberName}";
                    }
                    case MemberTypeKind.Clonable:
                    {
                        string extensionClassName = GetExtensionClassName(member);
                        string access = $"{sourceVar}.{memberName}";
                        string stateArg = GetMemberStateVar(member, stateVar);
                        return $"{memberName} = {WrapOptOutSingleRead(context, member, access, value => $"{extensionClassName}.InternalFastDeepClone({value}, {stateArg})")}{nf}";
                    }
                    case MemberTypeKind.Implicit:
                    {
                        if (context.ShouldInline(member.TypeFullName) && 
                            context.TryGetImplicitTypeModel(member.TypeFullName, out var implicitModel))
                        {
                            bool inlineModelDefault = implicitModel.NeedsStateTracking;
                            bool inlineMemberNeedsState = context.NeedsCircularState(member.TypeFullName, inlineModelDefault) && context.NeedsStateTracking;
                            bool inlineAcceptsState = context.HelperAcceptsState(member.TypeFullName);
                            bool inlineShouldPassState = inlineMemberNeedsState || inlineAcceptsState || (stateVar != "null");
                            string inlineActualStateVar = inlineShouldPassState
                                ? context.HelperStateArgument(member, stateVar, inlineMemberNeedsState || stateVar != "null")
                                : "null";

                            return $"{memberName} = {GetImplicitCloneExpression(context, implicitModel, $"{sourceVar}.{memberName}", inlineActualStateVar, indent, member.IsNullable)}{nf}";
                        }

                        string helperMethodName = context.GetOrCreateHelperMethodName(member);
                        context.TryGetImplicitTypeModel(member.TypeFullName, out implicitModel);

                        bool modelDefault = implicitModel?.NeedsStateTracking ?? false;
                        bool memberNeedsState = context.NeedsCircularState(member.TypeFullName, modelDefault) && context.NeedsStateTracking;
                        bool isRegisteredType = helperMethodName == "Clone";
                        bool helperTakesState = context.HelperTakesState(helperMethodName, member.TypeFullName, memberNeedsState);
                        bool passState = helperTakesState || (isRegisteredType && stateVar != "null");
                        string actualStateVar = context.HelperStateArgument(member, stateVar, passState);

                        return $"{memberName} = {WrapOptOutSingleRead(context, member, $"{sourceVar}.{memberName}", value => GetHelperMethodCall(context, helperMethodName, value, passState, actualStateVar))}{nf}";
                    }
                    case MemberTypeKind.Collection:
                    case MemberTypeKind.Dictionary:
                    case MemberTypeKind.Array:
                    case MemberTypeKind.MultiDimArray:
                    {
                        string helperMethodName = context.GetOrCreateHelperMethodName(member);
                        bool acceptsState = context.HelperAcceptsState(member.TypeFullName);
                        bool defaultNeedsState = MemberNeedsCircularRefTracking(context, member);
                        string actualStateVar = context.HelperStateArgument(member, stateVar, defaultNeedsState);

                        return $"{memberName} = {WrapOptOutSingleRead(context, member, $"{sourceVar}.{memberName}", value => GetHelperMethodCall(context, helperMethodName, value, acceptsState, actualStateVar))}{nf}";
                    }
                    case MemberTypeKind.Object:
                    case MemberTypeKind.Other:
                    default:
                        context.NeedsClonerClass = true;
                        // The null-forgiving argument is safe: Cloner<T>.Clone null-guards its input.
                        // The ambient state is forwarded unchanged: this member's type is not cloned by
                        // generated code at all — Cloner<T> ends in the runtime cloner, which tracks
                        // references on its own terms and ignores the state it is handed. A member-level
                        // [FastClonerPreserveIdentity] override therefore cannot apply here, so no
                        // no-tracking state is passed (see the attribute docs).
                        return $"{memberName} = Cloner<{member.TypeFullName}>.Clone({sourceVar}.{memberName}!, {stateVar}){nf}";
                }
        }
    }

    /// <summary>
    /// Adjusts how a member is populated depending on how the target instance was created
    /// (see https://github.com/lofcz/FastCloner/issues/48 — PostSharp's INotifyPropertyChanged
    /// aspect and weavers with the same shape).
    /// </summary>
    /// <param name="member">Member to adapt; replaced with an adjusted copy when needed.</param>
    /// <param name="instanceCreatedWithoutConstructor">
    /// True when the target instance was created via GetUninitializedObject: no constructor ran,
    /// so weaver-injected state (PostSharp aspect instances and the like) is missing and property
    /// setters may dereference it.
    /// </param>
    /// <returns>False when the member must not be populated at all.</returns>
    private static bool AdaptMemberForPopulation(ref MemberModel member, bool instanceCreatedWithoutConstructor)
    {
        if (member.IsWeaverState)
        {
            if (!instanceCreatedWithoutConstructor)
            {
                // Constructor-created instance: the constructor already initialized fresh weaver
                // state; copying anything over it (deep clone or reference) corrupts it.
                return false;
            }

            // Uninitialized instance: the state field would otherwise stay null and crash woven
            // accessors. Sharing the source's state is the best portable approximation — exactly
            // what Object.MemberwiseClone produces for instrumented types.
            member = member with { MemberBehavior = MemberCloneBehavior.Reference };
            return true;
        }

        if (instanceCreatedWithoutConstructor && member is { IsProperty: true, HasGetter: true }
            && (member.HasSetter || member.IsInitOnly))
        {
            // Never invoke property setters (or init accessors) on an instance that skipped its
            // constructors: weavers rewrite accessors to dereference ctor-initialized state, which
            // throws NullReferenceException inside weaver code. Auto-property storage is written
            // directly instead; properties without resolvable storage are skipped — their backing
            // fields, when collected as members, still carry the data.
            if (!member.HasBackingFieldStorage)
                return false;

            member = member with { AccessorStrategy = NonPublicAccessorStrategy.BackingField };
        }

        return true;
    }

    public static void WriteMemberCloning(CloneGeneratorContext context, MemberModel member, string resultVar, string sourceVar, string stateVar, bool instanceCreatedWithoutConstructor = false)
    {
        if (!AdaptMemberForPopulation(ref member, instanceCreatedWithoutConstructor))
            return;

        string memberName = member.Name;
        string nf = (!member.IsNullable && !member.IsValueType) ? "!" : "";
        StringBuilder sb = context.Source;
        
        if (member.AccessorStrategy != NonPublicAccessorStrategy.None)
        {
            if (!NonPublicAccessorEmitter.CanCloneNonPublicMembers(context))
            {
                context.SkippedNonPublicMembers.Add(memberName);
                return;
            }

            NonPublicAccessorEmitter.WriteCloneCall(context, sb, member, resultVar, sourceVar, stateVar, "            ");
            return;
        }

        switch (member)
        {
            case { IsProperty: false, IsReadOnly: true }:
            case { IsProperty: true, IsInitOnly: true }:
                return;
            case { IsProperty: true, HasGetter: true, HasSetter: false, IsInitOnly: false }:
                WriteGetterOnlyCollectionPopulation(context, member, resultVar, sourceVar, stateVar);
                return;
            default:
                if (member.IsShallowClone)
                {
                    sb.AppendLine($"            {resultVar}.{memberName} = {sourceVar}.{memberName};");
                    return;
                }
                
                switch (member.TypeKind)
                {
                    case MemberTypeKind.Safe:
                        sb.AppendLine($"            {resultVar}.{memberName} = {sourceVar}.{memberName};");
                        break;

                    case MemberTypeKind.Clonable:
                    {
                        string extensionClassName = GetExtensionClassName(member);
                        string access = $"{sourceVar}.{memberName}";
                        string stateArg = GetMemberStateVar(member, stateVar);
                        sb.AppendLine($"            {resultVar}.{memberName} = {WrapOptOutSingleRead(context, member, access, value => $"{extensionClassName}.InternalFastDeepClone({value}, {stateArg})")}{nf};");
                    }
                        break;

                    case MemberTypeKind.Implicit:
                    {
                        if (context.ShouldInline(member.TypeFullName) && 
                            context.TryGetImplicitTypeModel(member.TypeFullName, out var implicitModel))
                        {
                            bool inlineModelDefault = implicitModel.NeedsStateTracking;
                            bool inlineMemberNeedsState = context.NeedsCircularState(member.TypeFullName, inlineModelDefault) && context.NeedsStateTracking;
                            bool inlineAcceptsState = context.HelperAcceptsState(member.TypeFullName);
                            bool inlineShouldPassState = inlineMemberNeedsState || inlineAcceptsState || (stateVar != "null");
                            string inlineActualStateVar = inlineShouldPassState
                                ? context.HelperStateArgument(member, stateVar, inlineMemberNeedsState || stateVar != "null")
                                : "null";

                            string statement = GetImplicitCloneStatement(context, implicitModel, member.Name, resultVar, sourceVar, inlineActualStateVar, member.IsNullable);
                            sb.AppendLine(statement);
                            break;
                        }

                        string helperMethodName = context.GetOrCreateHelperMethodName(member);
                        context.TryGetImplicitTypeModel(member.TypeFullName, out implicitModel);
                        bool modelDefault = implicitModel?.NeedsStateTracking ?? false;
                        bool memberNeedsState = context.NeedsCircularState(member.TypeFullName, modelDefault) && context.NeedsStateTracking;
                        bool isRegisteredType = helperMethodName == "Clone";
                        bool helperTakesState = context.HelperTakesState(helperMethodName, member.TypeFullName, memberNeedsState);
                        bool passState = helperTakesState || (isRegisteredType && stateVar != "null");
                        string actualStateVar = context.HelperStateArgument(member, stateVar, passState);

                        string call = WrapOptOutSingleRead(context, member, $"{sourceVar}.{memberName}", value => GetHelperMethodCall(context, helperMethodName, value, passState, actualStateVar));
                        sb.AppendLine($"            {resultVar}.{memberName} = {call}{nf};");
                    }
                        break;

                    case MemberTypeKind.Collection:
                    case MemberTypeKind.Dictionary:
                    case MemberTypeKind.Array:
                    case MemberTypeKind.MultiDimArray:
                    {
                        string helperMethodName = context.GetOrCreateHelperMethodName(member);
                        bool acceptsState = context.HelperAcceptsState(member.TypeFullName);
                        bool defaultNeedsState = MemberNeedsCircularRefTracking(context, member);
                        string actualStateVar = context.HelperStateArgument(member, stateVar, defaultNeedsState);
                        string call = WrapOptOutSingleRead(context, member, $"{sourceVar}.{memberName}", value => GetHelperMethodCall(context, helperMethodName, value, acceptsState, actualStateVar));
                        sb.AppendLine($"            {resultVar}.{memberName} = {call}{nf};");
                    }
                        break;

                    case MemberTypeKind.Object:
                    case MemberTypeKind.Other:
                    default:
                        context.NeedsClonerClass = true;
                        // The null-forgiving argument is safe: Cloner<T>.Clone null-guards its input.
                        // Raw state, exactly as in GetMemberAssignment: the runtime cloner tracks
                        // references on its own terms, so a member-level override cannot apply here.
                        sb.AppendLine($"            {resultVar}.{memberName} = Cloner<{member.TypeFullName}>.Clone({sourceVar}.{memberName}!, {stateVar}){nf};");
                        break;
                }

                break;
        }
    }
    
    private static void WriteGetterOnlyCollectionPopulation(CloneGeneratorContext context, MemberModel member, string resultVar, string sourceVar, string stateVar)
    {
        StringBuilder sb = context.Source;
        string memberName = member.Name;
        
        if (member.TypeKind != MemberTypeKind.Collection && member.TypeKind != MemberTypeKind.Dictionary)
            return;

        string targetVar = $"target_{context.GetNextVariableId()}";
        string sourceCollectionVar = $"sourceCollection_{context.GetNextVariableId()}";
        sb.AppendLine($"            var {targetVar} = {resultVar}.{memberName};");
        sb.AppendLine($"            var {sourceCollectionVar} = {sourceVar}.{memberName};");
        sb.AppendLine($"            if ({sourceCollectionVar} != null && {targetVar} != null)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {targetVar}.Clear();");
        
        if (member.IsShallowClone)
        {
            if (member.TypeKind == MemberTypeKind.Dictionary)
            {
                sb.AppendLine($"                foreach (var kvp in {sourceCollectionVar})");
                sb.AppendLine("                {");
                sb.AppendLine($"                    {targetVar}[kvp.Key!] = kvp.Value!;");
                sb.AppendLine("                }");
            }
            else
            {
                string addMethod = GetAddMethodForCollection(member.CollectionKind);
                sb.AppendLine($"                foreach (var item in {sourceCollectionVar})");
                sb.AppendLine("                {");
                sb.AppendLine($"                    {targetVar}.{addMethod}(item!);");
                sb.AppendLine("                }");
            }
        }
        else
        {
            string helperMethodName = context.GetOrCreateHelperMethodName(member);
            bool acceptsState = context.HelperAcceptsState(member.TypeFullName);
            bool defaultNeedsState = MemberNeedsCircularRefTracking(context, member);
            string actualStateVar = context.HelperStateArgument(member, stateVar, defaultNeedsState);
            string clonedVar = $"cloned_{context.GetNextVariableId()}";
            // The member itself was read once, above: this local is what the helper clones, so the
            // guard, the shallow copy and the deep clone can never disagree about the instance.
            string helperCall = GetHelperMethodCall(context, helperMethodName, sourceCollectionVar, acceptsState, actualStateVar);
            sb.AppendLine($"                var {clonedVar} = {helperCall};");
            sb.AppendLine($"                if ({clonedVar} != null)");
            sb.AppendLine("                {");
            
            if (member.TypeKind == MemberTypeKind.Dictionary)
            {
                sb.AppendLine($"                    foreach (var kvp in {clonedVar})");
                sb.AppendLine("                    {");
                sb.AppendLine($"                        {targetVar}[kvp.Key!] = kvp.Value!;");
                sb.AppendLine("                    }");
            }
            else
            {
                string addMethod = GetAddMethodForCollection(member.CollectionKind);
                sb.AppendLine($"                    foreach (var item in {clonedVar})");
                sb.AppendLine("                    {");
                sb.AppendLine($"                        {targetVar}.{addMethod}(item!);");
                sb.AppendLine("                    }");
            }
            
            sb.AppendLine("                }");
        }
        
        sb.AppendLine("            }");
    }
    
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
    /// State argument handed to a nested cloner for a member's subgraph.
    /// <br/><br/>
    /// A member-level <c>[FastClonerPreserveIdentity(false)]</c> must beat the child type's own
    /// <c>[FastClonerPreserveIdentity(true)]</c>, so such a member receives
    /// <c>FcGeneratedCloneState.SuppressIdentity(state)</c> instead of the ambient state.
    /// Forwarding the ambient state would track the member's subgraph through the parent's state,
    /// and passing <c>null</c> would let the callee allocate its own tracking state — both re-enable
    /// the identity preservation the member explicitly opted out of.
    /// <br/><br/>
    /// The suppressing state is a view over the same clone operation, so objects that are already
    /// being cloned stay visible through it and an active cycle that passes through this member — or
    /// points back at the container it came from — still resolves to the clone under construction.
    /// <br/><br/>
    /// The view is created only when the member holds a value: call sites route through
    /// <see cref="WrapOptOutSingleRead"/>, which skips the whole call for a null member.
    /// </summary>
    internal static string GetMemberStateVar(MemberModel member, string stateVar)
    {
        return member.PreserveIdentity == false
            ? GeneratedTypeNames.SuppressIdentity(stateVar)
            : stateVar;
    }

    /// <summary>
    /// Wraps a clone call for <paramref name="member"/> so an opted-out member is read exactly once
    /// and its suppressing view is created only when it holds a value.
    /// <br/><br/>
    /// <c>source.Member is { } value ? Clone(value, SuppressIdentity(state)) : null</c> evaluates the
    /// member once. The obvious alternative - <c>source.Member == null ? null : Clone(source.Member, …)</c> -
    /// reads it twice, which is wrong for a getter that has side effects, returns a fresh instance per
    /// call, or is served by a non-public accessor method. The pattern also stays an expression, so it
    /// works unchanged in object initializers and in accessor argument positions.
    /// <br/><br/>
    /// <paramref name="buildCall"/> receives the expression that names the member's value: the pattern
    /// variable for an opted-out member, the member access itself otherwise.
    /// </summary>
    internal static string WrapOptOutSingleRead(CloneGeneratorContext context, MemberModel member, string memberAccess, Func<string, string> buildCall)
    {
        if (!context.MemberNeedsSingleReadGuard(member))
        {
            return buildCall(memberAccess);
        }

        string value = context.NextPatternVariableName();
        return $"{memberAccess} is {{ }} {value} ? {buildCall(value)} : null";
    }

    /// <summary>
    /// Whether the type modelled by <paramref name="member"/> needs reference tracking for its own
    /// default behaviour. A member-level <c>[FastClonerPreserveIdentity]</c> override is expressed
    /// through the state argument (<see cref="GetMemberStateVar"/>) and the helper capability
    /// (<see cref="CloneGeneratorContext.HelperAcceptsState"/>), not by this decision.
    /// </summary>
    public static bool MemberNeedsCircularRefTracking(CloneGeneratorContext context, MemberModel member)
    {
        if (member.PreserveIdentity is not null)
        {
            return member.PreserveIdentity.Value;
        }
        
        if (!context.NeedsStateTracking)
            return false;

        switch (member.TypeKind)
        {
            case MemberTypeKind.Safe:
                return false;
            case MemberTypeKind.Clonable:
                return true;
            case MemberTypeKind.Collection:
            case MemberTypeKind.Array:
            case MemberTypeKind.MultiDimArray:
            {
                if (member.ElementIsSafe)
                    return false;
                if (member.ElementHasClonableAttr)
                    return true;
                break;
            }
        }
        
        return true;
    }

    private static string GetImplicitCloneStatement(CloneGeneratorContext context, TypeModel implicitModel, string memberName, string resultVar, string sourceVar, string stateVar, bool isMemberNullable)
    {
        StringBuilder sb = new StringBuilder();
        string sourceProp = $"{sourceVar}.{memberName}";
        string safeName = $"l_{memberName}_{context.GetNextVariableId()}";

        sb.AppendLine($"            var {safeName} = {sourceProp};");

        bool skipNullCheck = context.Model.TrustNullability && !isMemberNullable;

        if (!implicitModel.IsStruct && !skipNullCheck)
        {
            sb.AppendLine($"            if ({safeName} != null)");
            sb.AppendLine("            {");
        }

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

    /// <summary>
    /// Expression form of the state-aware routing for a member's value, or an empty string when the
    /// member's kind is not cloned by generated helpers.
    /// <br/><br/>
    /// Paths that need an expression rather than an assignment statement — the .NET 8+ non-public
    /// accessor path — use this so they route exactly like the ordinary member path, including the
    /// member-level identity opt-out, instead of silently falling back to the runtime cloner.
    /// </summary>
    internal static string GetGeneratedMemberCloneExpression(CloneGeneratorContext context, MemberModel member, string sourceExpression, string stateVar)
    {
        switch (member.TypeKind)
        {
            case MemberTypeKind.Collection:
            case MemberTypeKind.Dictionary:
            case MemberTypeKind.Array:
            case MemberTypeKind.MultiDimArray:
            {
                string helperName = context.GetOrCreateHelperMethodName(member);
                bool acceptsState = context.HelperAcceptsState(member.TypeFullName);
                bool defaultNeedsState = MemberNeedsCircularRefTracking(context, member);
                string stateArg = context.HelperStateArgument(member, stateVar, defaultNeedsState);
                return WrapOptOutSingleRead(context, member, sourceExpression, value => GetHelperMethodCall(context, helperName, value, acceptsState, stateArg));
            }

            case MemberTypeKind.Implicit:
            {
                // Only routable when the implicit model is already known to this context: otherwise no
                // helper body would be emitted for it and the call would not compile.
                if (!context.TryGetImplicitTypeModel(member.TypeFullName, out TypeModel implicitModel))
                {
                    return string.Empty;
                }

                string helperName = context.GetOrCreateHelperMethodName(member);
                bool modelDefault = implicitModel.NeedsStateTracking;
                bool memberNeedsState = context.NeedsCircularState(member.TypeFullName, modelDefault) && context.NeedsStateTracking;
                bool isRegisteredType = helperName == "Clone";
                bool passState = context.HelperTakesState(helperName, member.TypeFullName, memberNeedsState)
                                 || (isRegisteredType && stateVar != "null");
                string stateArg = context.HelperStateArgument(member, stateVar, passState);
                return WrapOptOutSingleRead(context, member, sourceExpression, value => GetHelperMethodCall(context, helperName, value, passState, stateArg));
            }

            default:
                return string.Empty;
        }
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
    /// Returns the precomputed extension class FQN for a clonable member type.
    /// </summary>
    private static string GetExtensionClassName(MemberModel member)
    {
        return member.ClonableExtensionClass!;
    }

    /// <summary>
    /// Returns the precomputed extension class FQN for a clonable collection element type.
    /// </summary>
    public static string GetExtensionClassNameForType(MemberModel member)
    {
        return member.ElementClonableExtensionClass!;
    }

    /// <summary>
    /// Emits a fully-qualified static call to a generated clonable's InternalFastDeepClone.
    /// Extension-method syntax (<c>x.FastDeepClone()</c>) is never used: it only resolves when
    /// the extension class's namespace is imported, which generated helpers do not do (issue #57).
    /// </summary>
    public static string GetClonableCloneExpression(string extensionClassFqn, string sourceExpression, string stateArg)
    {
        return $"{extensionClassFqn}.InternalFastDeepClone({sourceExpression}, {stateArg})!";
    }
}
