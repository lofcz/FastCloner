using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastCloner.SourceGenerator;

/// <summary>
/// Generates helper methods for collections, dictionaries, and arrays.
/// </summary>
internal static class CollectionHelperGenerator
{
    public static void GenerateHelpers(CloneGeneratorContext context)
    {
        // Use queue to handle recursive dependencies (implicit type A might use implicit type B)
        while (context.HasPendingHelperMethods)
        {
            string typeFullName = context.DequeuePendingHelperMethod();

            // Check if it's an implicit type cloner
            if (context.TryGetImplicitTypeModel(typeFullName, out TypeModel implicitModel))
            {
                WriteImplicitCloneMethod(context, implicitModel, context.GetMethodName(typeFullName));
                continue;
            }

            // Get the member model for this type
            if (!context.TryGetMemberModel(typeFullName, out MemberModel member))
                continue;

            // Generate appropriate helper method based on member type kind
            switch (member.TypeKind)
            {
                case MemberTypeKind.Array:
                    WriteArrayCloneMethod(context, member);
                    break;

                case MemberTypeKind.MultiDimArray:
                    WriteMultiDimArrayCloneMethod(context, member);
                    break;

                case MemberTypeKind.Collection:
                    WriteCollectionCloneMethod(context, member);
                    break;

                case MemberTypeKind.Dictionary:
                    WriteDictionaryCloneMethod(context, member);
                    break;

                // Other types don't get helper methods (handled inline or via FastCloner runtime)
            }
        }
    }

    private static void WriteImplicitCloneMethod(CloneGeneratorContext context, TypeModel implicitModel, string methodName)
    {
        string typeName = implicitModel.FullyQualifiedName;
        // Determine if we need state.
        // If Root can't have circular refs, then we don't pass state down (it's null).
        // If Root CAN, then we check if ImplicitModel CAN.
        // If ImplicitModel CAN, we accept state.
        // A member-level [FastClonerPreserveIdentity(false)] also requires the parameter, so the
        // opt-out can be handed to this helper even when no default behaviour needs state.
        bool needsState = context.HelperAcceptsState(typeName);
        StringBuilder sb = context.Source;

        if (needsState)
        {
            context.NeedsStateClass = true;
        }

        WriteHelperMethodSignature(context, typeName, methodName, needsState, implicitModel.IsStruct);
        sb.AppendLine("        {");
        
        // For non-nullable structs, source cannot be null.
        if (!implicitModel.IsStruct) 
        {
            sb.AppendLine("            if (source == null) return null;");
        }

        if (needsState)
        {
            sb.AppendLine("            if (state != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                var known = state.GetKnownRef(source);");
            sb.AppendLine($"                if (known != null) return ({typeName})known;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        if (implicitModel.IsStruct)
        {
            sb.AppendLine($"            var result = source;");
        }
        else
        {
            sb.AppendLine($"            var result = new {typeName}");
            sb.AppendLine("            {");

            List<string> assignments = [];
            foreach (MemberModel member in implicitModel.Members)
            {
                // Recursive call to GetMemberAssignment will trigger generation of dependencies
                string assign = MemberCloneGenerator.GetMemberAssignment(context, member, "source", needsState ? "state" : "null", "                ");
                if (!string.IsNullOrEmpty(assign))
                {
                    assignments.Add($"                {assign}");
                }
            }

            if (assignments.Count > 0)
            {
                sb.AppendLine(string.Join(",\n", assignments));
            }

            sb.AppendLine("            };");
        }

        if (needsState)
        {
            sb.AppendLine($"            var registration_1 = state?.RegisterKnownRef(source, result);");
        }

        // struct logic for members?
        if (implicitModel.IsStruct)
        {
            foreach (MemberModel member in implicitModel.Members)
            {
                // We use WriteMemberCloning to handle assignment statements
                MemberCloneGenerator.WriteMemberCloning(context, member, "result", "source", needsState ? "state" : "null");
            }
        }

        if (needsState)
        {
            sb.AppendLine($"            state?.CompleteKnownRef(registration_1);");
        }

        sb.AppendLine();
        sb.AppendLine("            return result;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void WriteCollectionCloneMethod(CloneGeneratorContext context, MemberModel member)
    {
        if (member.ElementTypeName == null) return;

        string typeName = member.TypeFullName;
        string methodName = context.GetMethodName(typeName);
        CollectionKind kind = member.CollectionKind;
        bool needsState = context.HelperAcceptsState(typeName);
        bool defaultNeedsState = context.HelperDefaultNeedsState(typeName);
        bool isValueType = member.IsValueType;
        
        // Handle special collections
        if (kind.ToString().StartsWith("Immutable"))
        {
            WriteImmutableCollectionCloneMethod(context, member, typeName, methodName, kind, needsState, defaultNeedsState, isValueType);
            return;
        }

        if (kind == CollectionKind.ReadOnlyCollection)
        {
            WriteReadOnlyCollectionCloneMethod(context, member, typeName, methodName, needsState, isValueType);
            return;
        }

        bool isSafe = member.ElementIsSafe;
        bool hasClonableAttr = member.ElementHasClonableAttr;
        StringBuilder sb = context.Source;

        // Use pre-computed collection kind and concrete type
        string concreteType = member.ConcreteTypeFullName ?? typeName;
        
        // Determine operations based on kind
        string addMethod = "Add";
        bool isQueue = kind == CollectionKind.Queue || kind == CollectionKind.ConcurrentQueue;
        bool isStack = kind == CollectionKind.Stack || kind == CollectionKind.ConcurrentStack;
        bool isLinkedList = kind == CollectionKind.LinkedList;
        
        if (isQueue) addMethod = "Enqueue";
        else if (isStack) addMethod = "Push";
        else if (isLinkedList) addMethod = "AddLast";

        // Capacity pre-allocation requires a VERIFIED .ctor(int capacity) on the concrete type
        // (Collection<T>, BindingList<T> and many custom collections have none, issue #50)
        // and a Count property on the source (e.g. IEnumerable<T> does not have one).
        bool supportsCapacity = member.ConcreteHasCapacityCtor && member.CollectionHasCount;

        if (needsState)
        {
            context.NeedsStateClass = true;
        }

        WriteHelperMethodSignature(context, typeName, methodName, needsState, isValueType);
        sb.AppendLine("        {");
        
        if (!isValueType)
        {
            sb.AppendLine("            if (source == null) return null;");
        }

        if (needsState)
        {
            sb.AppendLine("            if (state != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                var known = state.GetKnownRef(source);");
            sb.AppendLine($"                if (known != null) return ({typeName})known;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        // Optimize: if element type is safe, use the copy constructor directly.
        // Requires a ctor VERIFIED to copy (Collection<T>'s IList<T> ctor wraps the source instead)
        // and is skipped for stacks: enumerating a stack yields top->bottom, so pushing that
        // sequence into a new stack would reverse it.
        // Keyed off the helper's *default* requirement: widening the signature so an opt-out can be
        // handed down must not disable the fast path for ordinary calls.
        if (isSafe && !defaultNeedsState && member.ConcreteHasCopyCtor && !isStack)
        {
            sb.AppendLine($"            return new {concreteType}(source);");
        }
        else
        {
            // Pre-allocate capacity for common collection types (List, HashSet, etc. support Count)
            if (supportsCapacity)
            {
                sb.AppendLine($"            var result = new {concreteType}(source.Count);");
            }
            else
            {
                sb.AppendLine($"            var result = new {concreteType}();");
            }

            if (needsState)
            {
                sb.AppendLine($"            var registration_2 = state?.RegisterKnownRef(source, result);");
                sb.AppendLine();
            }
            
            if (isStack)
            {
                sb.AppendLine("            // Stack iteration is Top->Bottom. Pushing in this order would reverse the stack.");
                sb.AppendLine("            // We need to push from Bottom->Top.");
                sb.AppendLine("            var temp = new global::System.Collections.Generic.List<object?>(source.Count);");
                sb.AppendLine("            foreach (var item in source)");
                sb.AppendLine("            {");

                string itemExpr = GetItemCloneExpression(context, member, "item", isSafe, hasClonableAttr, needsState);
                sb.AppendLine($"                var clonedItem = {itemExpr};");
                sb.AppendLine("                temp.Add(clonedItem!);");
                sb.AppendLine("            }");
                sb.AppendLine("            for (int i = temp.Count - 1; i >= 0; i--)");
                sb.AppendLine("            {");
                sb.AppendLine($"                result.Push(({member.ElementTypeName})temp[i]!);");
                sb.AppendLine("            }");
            }
            else if (kind == CollectionKind.List && member.CollectionHasIndexer)
            {
                // CollectionsMarshal.SetCount/AsSpan only accept List<T>; other indexable
                // collections in the List bucket (Collection<T>, BindingList<T>, ...) use indexed Add.
                if (context.TargetFramework >= TargetFramework.Net5 && member.ConcreteIsList)
                {
                    sb.AppendLine("            global::System.Runtime.InteropServices.CollectionsMarshal.SetCount(result, source.Count);");
                    sb.AppendLine("            var span = global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(result);");
                    sb.AppendLine("            for (int i = 0; i < source.Count; i++)");
                    sb.AppendLine("            {");
                    string itemExpr = GetItemCloneExpression(context, member, "source[i]", isSafe, hasClonableAttr, needsState);
                    sb.AppendLine($"                var clonedItem = {itemExpr};");
                    sb.AppendLine("                span[i] = clonedItem!;");
                    sb.AppendLine("            }");
                }
                else
                {
                    sb.AppendLine("            for (int i = 0; i < source.Count; i++)");
                    sb.AppendLine("            {");
                    string itemExpr = GetItemCloneExpression(context, member, "source[i]", isSafe, hasClonableAttr, needsState);
                    sb.AppendLine($"                var clonedItem = {itemExpr};");
                    sb.AppendLine("                result.Add(clonedItem!);");
                    sb.AppendLine("            }");
                }
            }
            else
            {
                sb.AppendLine("            foreach (var item in source)");
                sb.AppendLine("            {");

                string itemExpr = GetItemCloneExpression(context, member, "item", isSafe, hasClonableAttr, needsState);

                sb.AppendLine($"                var clonedItem = {itemExpr};");
                sb.AppendLine($"                result.{addMethod}(clonedItem!);");
                sb.AppendLine("            }");
            }
            
            if (needsState)
            {
                sb.AppendLine($"            state?.CompleteKnownRef(registration_2);");
            }

            sb.AppendLine();
            sb.AppendLine("            return result;");
        }

        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void WriteImmutableCollectionCloneMethod(CloneGeneratorContext context, MemberModel member, string typeName, string methodName, CollectionKind kind, bool needsState, bool defaultNeedsState, bool isValueType)
    {
        StringBuilder sb = context.Source;
        bool isSafe = member.ElementIsSafe;
        bool hasClonableAttr = member.ElementHasClonableAttr;

        if (needsState)
            context.NeedsStateClass = true;

        WriteHelperMethodSignature(context, typeName, methodName, needsState, isValueType);
        sb.AppendLine("        {");
        
        if (!isValueType)
        {
            sb.AppendLine("            if (source == null) return null;");
        }
        
        // Keyed off the helper's *default* requirement, not on the widened signature: a helper that
        // gained a state parameter only so an opt-out can be handed down must still return the
        // source for a safe immutable collection when it is invoked normally.
        if (isSafe && !defaultNeedsState)
        {
            sb.AppendLine("            return source;");
            sb.AppendLine("        }");
            sb.AppendLine();
            return;
        }
        
        if (needsState)
        {
            sb.AppendLine("            if (state != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                var known = state.GetKnownRef(source);");
            sb.AppendLine($"                if (known != null) return ({typeName})known;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }
        
        sb.AppendLine($"            var temp = new global::System.Collections.Generic.List<{member.ElementTypeName}>();");

        sb.AppendLine("            foreach (var item in source)");
        sb.AppendLine("            {");
        string itemExpr = GetItemCloneExpression(context, member, "item", isSafe, hasClonableAttr, needsState);
        sb.AppendLine($"                var clonedItem = {itemExpr};");
        sb.AppendLine("                temp.Add(clonedItem!);");
        sb.AppendLine("            }");
        
        if (kind == CollectionKind.ImmutableStack)
        {
            sb.AppendLine("            temp.Reverse();"); 
        }
        
        string factoryMethod = kind switch
        {
            CollectionKind.ImmutableList => "global::System.Collections.Immutable.ImmutableList.CreateRange(temp)",
            CollectionKind.ImmutableArray => "global::System.Collections.Immutable.ImmutableArray.CreateRange(temp)",
            CollectionKind.ImmutableHashSet => "global::System.Collections.Immutable.ImmutableHashSet.CreateRange(temp)",
            CollectionKind.ImmutableSortedSet => "global::System.Collections.Immutable.ImmutableSortedSet.CreateRange(temp)",
            CollectionKind.ImmutableQueue => "global::System.Collections.Immutable.ImmutableQueue.CreateRange(temp)",
            CollectionKind.ImmutableStack => "global::System.Collections.Immutable.ImmutableStack.CreateRange(temp)",
            _ => "global::System.Collections.Immutable.ImmutableList.CreateRange(temp)" // default fallback
        };

        sb.AppendLine($"            var result = {factoryMethod};");

        if (needsState)
        {
            sb.AppendLine($"            var registration_3 = state?.RegisterKnownRef(source, result);");

            sb.AppendLine($"            state?.CompleteKnownRef(registration_3);");
        }

        sb.AppendLine("            return result;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void WriteReadOnlyCollectionCloneMethod(CloneGeneratorContext context, MemberModel member, string typeName, string methodName, bool needsState, bool isValueType)
    {
        StringBuilder sb = context.Source;
        bool isSafe = member.ElementIsSafe;
        bool hasClonableAttr = member.ElementHasClonableAttr;

        if (needsState)
            context.NeedsStateClass = true;

        WriteHelperMethodSignature(context, typeName, methodName, needsState, isValueType);
        sb.AppendLine("        {");
        
        if (!isValueType)
        {
            sb.AppendLine("            if (source == null) return null;");
        }
        
        if (needsState)
        {
            sb.AppendLine("            if (state != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                var known = state.GetKnownRef(source);");
            sb.AppendLine($"                if (known != null) return ({typeName})known;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        sb.AppendLine($"            var temp = new global::System.Collections.Generic.List<{member.ElementTypeName}>(source.Count);");
        
        sb.AppendLine("            foreach (var item in source)");
        sb.AppendLine("            {");
        string itemExpr = GetItemCloneExpression(context, member, "item", isSafe, hasClonableAttr, needsState);
        sb.AppendLine($"                var clonedItem = {itemExpr};");
        sb.AppendLine("                temp.Add(clonedItem!);");
        sb.AppendLine("            }");

        sb.AppendLine($"            var result = new global::System.Collections.ObjectModel.ReadOnlyCollection<{member.ElementTypeName}>(temp);");

        if (needsState)
        {
            sb.AppendLine($"            var registration_4 = state?.RegisterKnownRef(source, result);");

            sb.AppendLine($"            state?.CompleteKnownRef(registration_4);");
        }

        sb.AppendLine("            return result;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static string GetItemCloneExpression(CloneGeneratorContext context, MemberModel member, string itemVar, bool isSafe, bool hasClonableAttr, bool parentNeedsState)
    {
        if (isSafe)
        {
            return itemVar;
        }

        if (hasClonableAttr)
        {
            string extensionClassName = MemberCloneGenerator.GetExtensionClassNameForType(member);
            string stateArg = parentNeedsState ? "state" : "null";
            return MemberCloneGenerator.GetClonableCloneExpression(extensionClassName, itemVar, stateArg);
        }

        if (context.TryGetMemberModel(member.ElementTypeName!, out MemberModel nestedModel))
        {
            string helperName = context.GetOrCreateHelperMethodName(nestedModel);
            bool elementNeedsState = context.HelperTakesState(helperName, nestedModel.TypeFullName, MemberCloneGenerator.MemberNeedsCircularRefTracking(context, nestedModel));
            string actualStateVar = parentNeedsState ? "state" : "null";
            return GetHelperMethodCall(context, helperName, itemVar, elementNeedsState, actualStateVar);
        }

        if (context.TryGetImplicitTypeModel(member.ElementTypeName!, out TypeModel implicitModel))
        {
            if (context.ShouldInline(implicitModel.FullyQualifiedName))
            {
                string actualStateVarInline = parentNeedsState ? "state" : "null";
                return MemberCloneGenerator.GetImplicitCloneExpression(context, implicitModel, itemVar, actualStateVarInline, "                ", true) + "!";
            }
            
            string helperName = context.GetOrCreateHelperMethodName(implicitModel.FullyQualifiedName);
            bool elementNeedsState = context.HelperTakesState(helperName, implicitModel.FullyQualifiedName, implicitModel.NeedsStateTracking && context.NeedsStateTracking);
            string actualStateVar = parentNeedsState ? "state" : "null";
            return GetHelperMethodCall(context, helperName, itemVar, elementNeedsState, actualStateVar);
        }

        return context.IsFastClonerAvailable ?
            $"({member.ElementTypeName}){CloneGeneratorContext.FastClonerDeepCloneCall(itemVar)}!" :
            itemVar;
    }

    private static void WriteDictionaryCloneMethod(CloneGeneratorContext context, MemberModel member)
    {
        if (member.KeyTypeName == null || member.ValueTypeName == null) return;

        string typeName = member.TypeFullName;
        string methodName = context.GetMethodName(typeName);
        CollectionKind kind = member.CollectionKind;
        bool needsState = context.HelperAcceptsState(typeName);
        bool defaultNeedsState = context.HelperDefaultNeedsState(typeName);
        bool isValueType = member.IsValueType;
        
        if (kind.ToString().StartsWith("Immutable"))
        {
            WriteImmutableDictionaryCloneMethod(context, member, typeName, methodName, kind, needsState, defaultNeedsState, isValueType);
            return;
        }

        if (kind == CollectionKind.ReadOnlyDictionary)
        {
            WriteReadOnlyDictionaryCloneMethod(context, member, typeName, methodName, needsState, isValueType);
            return;
        }

        StringBuilder sb = context.Source;
        string concreteType = member.ConcreteTypeFullName ?? typeName;
        // Capacity pre-allocation requires a VERIFIED .ctor(int capacity) on the concrete type
        // (a Dictionary<K,V> subclass doesn't inherit its base's constructors).
        bool supportsCapacity = member.ConcreteHasCapacityCtor;
        bool isConcurrent = kind == CollectionKind.ConcurrentDictionary;
        bool keysAreSafe = member.KeyIsSafe;
        bool valuesAreSafe = member.ValueIsSafe;

        if (needsState)
        {
            context.NeedsStateClass = true;
        }

        WriteHelperMethodSignature(context, typeName, methodName, needsState, isValueType);
        sb.AppendLine("        {");
        
        if (!isValueType)
        {
            sb.AppendLine("            if (source == null) return null;");
        }
        
        // The copy-ctor fast path requires a ctor VERIFIED to copy (only the exact BCL Dictionary;
        // subclasses don't inherit it and custom dictionary-taking ctors may wrap the source).
        // Keyed off the default requirement, never off the widened signature capability.
        if (keysAreSafe && valuesAreSafe && !defaultNeedsState && kind == CollectionKind.Dictionary && member.ConcreteHasCopyCtor)
        {
            sb.AppendLine($"            return new {concreteType}(source);");
            sb.AppendLine("        }");
            sb.AppendLine();
            return;
        }

        if (needsState)
        {
            sb.AppendLine("            if (state != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                var known = state.GetKnownRef(source);");
            sb.AppendLine($"                if (known != null) return ({typeName})known;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }
        
        if (supportsCapacity)
        {
            sb.AppendLine($"            var result = new {concreteType}(source.Count);");
        }
        else
        {
            sb.AppendLine($"            var result = new {concreteType}();");
        }

        if (needsState)
        {
            sb.AppendLine($"            var registration_5 = state?.RegisterKnownRef(source, result);");
            sb.AppendLine();
        }

        sb.AppendLine("            foreach (var kvp in source)");
        sb.AppendLine("            {");
        
        (string keyExpr, string valExpr) = GetKeyValueExpressions(context, member, needsState);
        sb.AppendLine($"                var clonedKey = {keyExpr};");
        sb.AppendLine($"                var clonedValue = {valExpr};");

        if (isConcurrent)
        {
            sb.AppendLine("                result.TryAdd(clonedKey!, clonedValue!);");
        }
        else
        {
            sb.AppendLine("                result.Add(clonedKey!, clonedValue!);");
        }

        sb.AppendLine("            }");

        if (needsState)
        {
            sb.AppendLine($"            state?.CompleteKnownRef(registration_5);");
        }

        sb.AppendLine();
        sb.AppendLine("            return result;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void WriteImmutableDictionaryCloneMethod(CloneGeneratorContext context, MemberModel member, string typeName, string methodName, CollectionKind kind, bool needsState, bool defaultNeedsState, bool isValueType)
    {
        StringBuilder sb = context.Source;
        bool keysAreSafe = member.KeyIsSafe;
        bool valuesAreSafe = member.ValueIsSafe;
        
        if (needsState) context.NeedsStateClass = true;

        WriteHelperMethodSignature(context, typeName, methodName, needsState, isValueType);
        sb.AppendLine("        {");
        
        if (!isValueType)
        {
            sb.AppendLine("            if (source == null) return null;");
        }
        
        // Keyed off the default requirement: a helper widened only so an opt-out can be handed down
        // must still return the source for a safe immutable dictionary.
        if (keysAreSafe && valuesAreSafe && !defaultNeedsState)
        {
            sb.AppendLine("            return source;");
            sb.AppendLine("        }");
            sb.AppendLine();
            return;
        }
        
        if (needsState)
        {
            sb.AppendLine("            if (state != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                var known = state.GetKnownRef(source);");
            sb.AppendLine($"                if (known != null) return ({typeName})known;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }
        
        sb.AppendLine($"            var temp = new global::System.Collections.Generic.Dictionary<{member.KeyTypeName}, {member.ValueTypeName}>(source.Count);");
        
        sb.AppendLine("            foreach (var kvp in source)");
        sb.AppendLine("            {");
        (string keyExpr, string valExpr) = GetKeyValueExpressions(context, member, needsState);
        sb.AppendLine($"                var clonedKey = {keyExpr};");
        sb.AppendLine($"                var clonedValue = {valExpr};");
        sb.AppendLine("                temp.Add(clonedKey!, clonedValue!);");
        sb.AppendLine("            }");

        string factoryMethod = kind == CollectionKind.ImmutableSortedDictionary
            ? "global::System.Collections.Immutable.ImmutableSortedDictionary.CreateRange(temp)"
            : "global::System.Collections.Immutable.ImmutableDictionary.CreateRange(temp)";

        sb.AppendLine($"            var result = {factoryMethod};");

        if (needsState)
        {
            sb.AppendLine($"            var registration_6 = state?.RegisterKnownRef(source, result);");

            sb.AppendLine($"            state?.CompleteKnownRef(registration_6);");
        }

        sb.AppendLine("            return result;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void WriteReadOnlyDictionaryCloneMethod(CloneGeneratorContext context, MemberModel member, string typeName, string methodName, bool needsState, bool isValueType)
    {
        StringBuilder sb = context.Source;
        if (needsState) context.NeedsStateClass = true;

        WriteHelperMethodSignature(context, typeName, methodName, needsState, isValueType);
        sb.AppendLine("        {");
        
        if (!isValueType)
        {
            sb.AppendLine("            if (source == null) return null;");
        }
        
        if (needsState)
        {
            sb.AppendLine("            if (state != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                var known = state.GetKnownRef(source);");
            sb.AppendLine($"                if (known != null) return ({typeName})known;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }
        
        sb.AppendLine($"            var temp = new global::System.Collections.Generic.Dictionary<{member.KeyTypeName}, {member.ValueTypeName}>(source.Count);");
        
        sb.AppendLine("            foreach (var kvp in source)");
        sb.AppendLine("            {");
        (string keyExpr, string valExpr) = GetKeyValueExpressions(context, member, needsState);
        sb.AppendLine($"                var clonedKey = {keyExpr};");
        sb.AppendLine($"                var clonedValue = {valExpr};");
        sb.AppendLine("                temp.Add(clonedKey!, clonedValue!);");
        sb.AppendLine("            }");

        sb.AppendLine($"            var result = new global::System.Collections.ObjectModel.ReadOnlyDictionary<{member.KeyTypeName}, {member.ValueTypeName}>(temp);");

        if (needsState)
        {
            sb.AppendLine($"            var registration_7 = state?.RegisterKnownRef(source, result);");

            sb.AppendLine($"            state?.CompleteKnownRef(registration_7);");
        }

        sb.AppendLine("            return result;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static (string keyExpr, string valExpr) GetKeyValueExpressions(CloneGeneratorContext context, MemberModel member, bool needsState)
    {
        string keyExpr = "kvp.Key";
        if (!member.KeyIsSafe)
        {
            if (member.KeyIsClonable)
            {
                string actualStateVar = needsState ? "state" : "null";
                keyExpr = MemberCloneGenerator.GetClonableCloneExpression(member.KeyClonableExtensionClass!, "kvp.Key", actualStateVar);
            }
            else if (context.TryGetMemberModel(member.KeyTypeName!, out MemberModel nestedKeyModel))
            {
                string helperName = context.GetOrCreateHelperMethodName(nestedKeyModel);
                bool keyNeedsState = context.HelperTakesState(helperName, nestedKeyModel.TypeFullName, MemberCloneGenerator.MemberNeedsCircularRefTracking(context, nestedKeyModel));
                string actualStateVar = needsState ? "state" : "null";
                keyExpr = GetHelperMethodCall(context, helperName, "kvp.Key", keyNeedsState, actualStateVar);
            }
            else if (context.TryGetImplicitTypeModel(member.KeyTypeName!, out TypeModel implicitKeyModel))
            {
                if (context.ShouldInline(implicitKeyModel.FullyQualifiedName))
                {
                    string actualStateVar = needsState ? "state" : "null";
                    keyExpr = MemberCloneGenerator.GetImplicitCloneExpression(context, implicitKeyModel, "kvp.Key", actualStateVar, "                ", true) + "!";
                }
                else
                {
                    string helperName = context.GetOrCreateHelperMethodName(implicitKeyModel.FullyQualifiedName);
                    bool keyNeedsState = context.HelperTakesState(helperName, implicitKeyModel.FullyQualifiedName, implicitKeyModel.NeedsStateTracking && context.NeedsStateTracking);
                    string actualStateVar = needsState ? "state" : "null";
                    keyExpr = GetHelperMethodCall(context, helperName, "kvp.Key", keyNeedsState, actualStateVar);
                }
            }
            else if (context.IsFastClonerAvailable)
            {
                keyExpr = $"({member.KeyTypeName}){CloneGeneratorContext.FastClonerDeepCloneCall("kvp.Key")}!";
            }
        }

        string valExpr = "kvp.Value";
        if (!member.ValueIsSafe)
        {
            if (member.ValueIsClonable)
            {
                string actualStateVar = needsState ? "state" : "null";
                valExpr = MemberCloneGenerator.GetClonableCloneExpression(member.ValueClonableExtensionClass!, "kvp.Value", actualStateVar);
            }
            else if (context.TryGetMemberModel(member.ValueTypeName!, out MemberModel nestedValModel))
            {
                string helperName = context.GetOrCreateHelperMethodName(nestedValModel);
                bool valNeedsState = context.HelperTakesState(helperName, nestedValModel.TypeFullName, MemberCloneGenerator.MemberNeedsCircularRefTracking(context, nestedValModel));
                string actualStateVar = needsState ? "state" : "null";
                valExpr = GetHelperMethodCall(context, helperName, "kvp.Value", valNeedsState, actualStateVar);
            }
            else if (context.TryGetImplicitTypeModel(member.ValueTypeName!, out TypeModel implicitValModel))
            {
                if (context.ShouldInline(implicitValModel.FullyQualifiedName))
                {
                    string actualStateVar = needsState ? "state" : "null";
                    valExpr = MemberCloneGenerator.GetImplicitCloneExpression(context, implicitValModel, "kvp.Value", actualStateVar, "                ", true) + "!";
                }
                else
                {
                    string helperName = context.GetOrCreateHelperMethodName(implicitValModel.FullyQualifiedName);
                    bool valNeedsState = context.HelperTakesState(helperName, implicitValModel.FullyQualifiedName, implicitValModel.NeedsStateTracking && context.NeedsStateTracking);
                    string actualStateVar = needsState ? "state" : "null";
                    valExpr = GetHelperMethodCall(context, helperName, "kvp.Value", valNeedsState, actualStateVar);
                }
            }
            else if (context.IsFastClonerAvailable)
            {
                valExpr = $"({member.ValueTypeName}){CloneGeneratorContext.FastClonerDeepCloneCall("kvp.Value")}!";
            }
        }
        
        return (keyExpr, valExpr);
    }

    private static void WriteArrayCloneMethod(CloneGeneratorContext context, MemberModel member)
    {
        if (member.ElementTypeName == null) return;

        string typeName = member.TypeFullName;
        string methodName = context.GetMethodName(typeName);
        bool isSafe = member.ElementIsSafe;
        bool hasClonableAttr = member.ElementHasClonableAttr;
        bool needsState = context.HelperAcceptsState(typeName);
        StringBuilder sb = context.Source;

        if (needsState)
        {
            context.NeedsStateClass = true;
        }
        
        WriteHelperMethodSignature(context, typeName, methodName, needsState, false);
        sb.AppendLine("        {");
        sb.AppendLine("            if (source == null) return null;");

        if (needsState)
        {
            sb.AppendLine("            if (state != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                var known = state.GetKnownRef(source);");
            sb.AppendLine($"                if (known != null) return ({typeName})known;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        string arrayCreationExpr = GetArrayCreationExpression(member.ElementTypeName, "source.Length");
        sb.AppendLine($"            var result = {arrayCreationExpr};");

        if (needsState)
        {
            sb.AppendLine($"            var registration_8 = state?.RegisterKnownRef(source, result);");
            sb.AppendLine();
        }

        if (isSafe)
        {
            sb.AppendLine("            global::System.Array.Copy(source, result, source.Length);");
        }
        else
        {
            sb.AppendLine("            for (int i = 0; i < source.Length; i++)");
            sb.AppendLine("            {");

            string itemExpr;
            
            if (hasClonableAttr)
            {
                string actualStateVar = needsState ? "state" : "null";
                itemExpr = MemberCloneGenerator.GetClonableCloneExpression(
                    MemberCloneGenerator.GetExtensionClassNameForType(member), "source[i]", actualStateVar);
            }
            else if (context.TryGetMemberModel(member.ElementTypeName!, out MemberModel nestedModel))
            {
                string helperName = context.GetOrCreateHelperMethodName(nestedModel);
                bool elementNeedsState = context.HelperTakesState(helperName, nestedModel.TypeFullName, MemberCloneGenerator.MemberNeedsCircularRefTracking(context, nestedModel));
                string actualStateVar = needsState ? "state" : "null";
                itemExpr = GetHelperMethodCall(context, helperName, "source[i]", elementNeedsState, actualStateVar);
            }
            else if (context.TryGetImplicitTypeModel(member.ElementTypeName!, out TypeModel implicitModel))
            {
                if (context.ShouldInline(implicitModel.FullyQualifiedName))
                {
                    string actualStateVar = needsState ? "state" : "null";
                    itemExpr = MemberCloneGenerator.GetImplicitCloneExpression(context, implicitModel, "source[i]", actualStateVar, "                ", true) + "!";
                }
                else
                {
                    string helperName = context.GetOrCreateHelperMethodName(implicitModel.FullyQualifiedName);
                    bool elementNeedsState = context.HelperTakesState(helperName, implicitModel.FullyQualifiedName, implicitModel.NeedsStateTracking && context.NeedsStateTracking);
                    string actualStateVar = needsState ? "state" : "null";
                    itemExpr = GetHelperMethodCall(context, helperName, "source[i]", elementNeedsState, actualStateVar);
                }
            }
            else if (context.IsFastClonerAvailable)
            {
                itemExpr = $"({member.ElementTypeName}){CloneGeneratorContext.FastClonerDeepCloneCall("source[i]")}!";
            }
            else
            {
                itemExpr = "source[i]";
            }

            sb.AppendLine($"                var clonedItem = {itemExpr};");
            sb.AppendLine("                result[i] = clonedItem!;");
            sb.AppendLine("            }");
        }

        if (needsState)
        {
            sb.AppendLine($"            state?.CompleteKnownRef(registration_8);");
        }

        sb.AppendLine();
        sb.AppendLine("            return result;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void WriteMultiDimArrayCloneMethod(CloneGeneratorContext context, MemberModel member)
    {
        if (member.ElementTypeName == null) return;

        string typeName = member.TypeFullName;
        string methodName = context.GetMethodName(typeName);
        bool isSafe = member.ElementIsSafe;
        bool hasClonableAttr = member.ElementHasClonableAttr;
        bool needsState = context.HelperAcceptsState(typeName);
        int rank = member.ArrayRank;
        StringBuilder sb = context.Source;

        if (needsState)
        {
            context.NeedsStateClass = true;
        }

        WriteHelperMethodSignature(context, typeName, methodName, needsState, false);
        sb.AppendLine("        {");
        sb.AppendLine("            if (source == null) return null;");

        if (needsState)
        {
            sb.AppendLine("            if (state != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                var known = state.GetKnownRef(source);");
            sb.AppendLine($"                if (known != null) return ({typeName})known;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }
        
        for (int d = 0; d < rank; d++)
        {
            sb.AppendLine($"            int len{d} = source.GetLength({d});");
        }
        sb.AppendLine();
        
        string dimList = string.Join(", ", Enumerable.Range(0, rank).Select(d => $"len{d}"));
        sb.AppendLine($"            var result = new {member.ElementTypeName}[{dimList}];");

        if (needsState)
        {
            sb.AppendLine($"            var registration_9 = state?.RegisterKnownRef(source, result);");
        }
        sb.AppendLine();
        
        if (isSafe)
        {
            sb.AppendLine("            global::System.Array.Copy(source, result, source.Length);");
        }
        else
        {
            for (int d = 0; d < rank; d++)
            {
                string indent = new string(' ', 12 + d * 4);
                sb.AppendLine($"{indent}for (int i{d} = 0; i{d} < len{d}; i{d}++)");
                sb.AppendLine($"{indent}{{");
            }

            string innerIndent = new string(' ', 12 + rank * 4);
            string indexList = string.Join(", ", Enumerable.Range(0, rank).Select(d => $"i{d}"));
            
            string itemExpr;
            if (hasClonableAttr)
            {
                string actualStateVar = needsState ? "state" : "null";
                itemExpr = MemberCloneGenerator.GetClonableCloneExpression(
                    MemberCloneGenerator.GetExtensionClassNameForType(member), $"source[{indexList}]", actualStateVar);
            }
            else if (context.TryGetMemberModel(member.ElementTypeName!, out MemberModel nestedModel))
            {
                string helperName = context.GetOrCreateHelperMethodName(nestedModel);
                bool elementNeedsState = context.HelperTakesState(helperName, nestedModel.TypeFullName, MemberCloneGenerator.MemberNeedsCircularRefTracking(context, nestedModel));
                string actualStateVar = needsState ? "state" : "null";
                itemExpr = GetHelperMethodCall(context, helperName, $"source[{indexList}]", elementNeedsState, actualStateVar);
            }
            else if (context.TryGetImplicitTypeModel(member.ElementTypeName!, out TypeModel implicitModel))
            {
                if (context.ShouldInline(implicitModel.FullyQualifiedName))
                {
                    string actualStateVar = needsState ? "state" : "null";
                    itemExpr = MemberCloneGenerator.GetImplicitCloneExpression(context, implicitModel, $"source[{indexList}]", actualStateVar, "                ", true) + "!";
                }
                else
                {
                    string helperName = context.GetOrCreateHelperMethodName(implicitModel.FullyQualifiedName);
                    bool elementNeedsState = context.HelperTakesState(helperName, implicitModel.FullyQualifiedName, implicitModel.NeedsStateTracking && context.NeedsStateTracking);
                    string actualStateVar = needsState ? "state" : "null";
                    itemExpr = GetHelperMethodCall(context, helperName, $"source[{indexList}]", elementNeedsState, actualStateVar);
                }
            }
            else if (context.IsFastClonerAvailable)
            {
                itemExpr = $"({member.ElementTypeName}){CloneGeneratorContext.FastClonerDeepCloneCall($"source[{indexList}]")}!";
            }
            else
            {
                itemExpr = $"source[{indexList}]";
            }

            sb.AppendLine($"{innerIndent}var clonedItem = {itemExpr};");
            sb.AppendLine($"{innerIndent}result[{indexList}] = clonedItem!;");

            for (int d = rank - 1; d >= 0; d--)
            {
                string indent = new string(' ', 12 + d * 4);
                sb.AppendLine($"{indent}}}");
            }
        }

        if (needsState)
        {
            sb.AppendLine($"            state?.CompleteKnownRef(registration_9);");
        }

        sb.AppendLine();
        sb.AppendLine("            return result;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }
    
    private static string GetArrayCreationExpression(string elementTypeName, string sizeExpression)
    {
        // Nullable array elements ("T[]?", "T[,]?") cannot use the "new T[n][]" rewrite —
        // the size belongs after the full element type: new T[]?[n].
        if (elementTypeName.EndsWith("?", System.StringComparison.Ordinal) && elementTypeName.IndexOf('[') >= 0)
        {
            return $"new {elementTypeName}[{sizeExpression}]";
        }

        int bracketIndex = elementTypeName.IndexOf('[');
        
        if (bracketIndex >= 0)
        {
            string baseType = elementTypeName.Substring(0, bracketIndex);
            string trailingBrackets = elementTypeName.Substring(bracketIndex);
            return $"new {baseType}[{sizeExpression}]{trailingBrackets}";
        }
        
        return $"new {elementTypeName}[{sizeExpression}]";
    }

    private static void WriteHelperMethodSignature(CloneGeneratorContext context, string typeName, string methodName, bool needsState, bool isValueType)
    {
        string typeParams = GetTypeParametersString(context.Model);
        string constraints = GetTypeConstraintsString(context.Model);
        StringBuilder sb = context.Source;
        
        string typeSuffix = "?";
        if (isValueType)
        {
            typeSuffix = "";
        }
        else
        {
            if (typeName.EndsWith("?")) typeSuffix = "";
        }

        string staticMod = context.UseStaticMethods ? "static " : "";

        if (needsState)
        {
            sb.AppendLine($"        private {staticMod}{typeName}{typeSuffix} {methodName}{typeParams}({typeName}{typeSuffix} source, {GeneratedTypeNames.CloneState}? state){constraints}");
        }
        else
        {
            sb.AppendLine($"        private {staticMod}{typeName}{typeSuffix} {methodName}{typeParams}({typeName}{typeSuffix} source){constraints}");
        }
    }

    private static string GetHelperMethodCall(CloneGeneratorContext context, string methodName, string sourceExpression, bool needsState, string stateVar = "null")
    {
        string typeParams = GetTypeParametersString(context.Model);

        if (needsState)
        {
            return $"{methodName}{typeParams}({sourceExpression}, {stateVar})!";
        }

        return $"{methodName}{typeParams}({sourceExpression})!";
    }

    private static string GetTypeParametersString(TypeModel model)
    {
        return model.TypeParameters.Count == 0 ? string.Empty : $"<{string.Join(", ", model.TypeParameters)}>";
    }

    private static string GetTypeConstraintsString(TypeModel model)
    {
        if (model.TypeConstraints.Count == 0)
            return string.Empty;

        return " " + string.Join(" ", model.TypeConstraints);
    }
}
