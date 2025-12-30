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
        bool needsState = implicitModel.CanHaveCircularReferences && context.CanHaveCircularReferences;
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
            if (implicitModel.IsStruct)
                sb.AppendLine($"                if (known != null) return ({typeName})known;");
            else
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
            sb.AppendLine("            state?.AddKnownRef(source, result);");
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
        bool needsState = MemberCloneGenerator.MemberNeedsCircularRefTracking(context, member);
        bool isValueType = member.IsValueType;
        
        // Handle special collections
        if (kind.ToString().StartsWith("Immutable"))
        {
            WriteImmutableCollectionCloneMethod(context, member, typeName, methodName, kind, needsState, isValueType);
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

        // Determine if capacity can be passed to constructor
        // Only standard List, HashSet, Queue, Stack support new T(int capacity)
        bool supportsCapacity = kind == CollectionKind.List || 
                                kind == CollectionKind.HashSet || 
                                kind == CollectionKind.Queue || 
                                kind == CollectionKind.Stack;

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

        // Optimize: if element type is safe, use IEnumerable constructor directly (works for List, HashSet, etc.)
        // Note: Queue/Stack/LinkedList also support IEnumerable ctor
        if (isSafe && !needsState)
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
                sb.AppendLine("            state?.AddKnownRef(source, result);");
                sb.AppendLine();
            }

            // For Stack, we need to take care to preserve the order:
            
            if (isStack)
            {
                sb.AppendLine("            // Stack iteration is Top->Bottom. Pushing in this order would reverse the stack.");
                sb.AppendLine("            // We need to push from Bottom->Top.");
                sb.AppendLine("            var temp = new global::System.Collections.Generic.List<object?>(source.Count);");
                sb.AppendLine("            foreach (var item in source)");
                sb.AppendLine("            {");
                // Clone item first
                string itemExpr = GetItemCloneExpression(context, member, "item", isSafe, hasClonableAttr, needsState);
                sb.AppendLine($"                temp.Add({itemExpr});");
                sb.AppendLine("            }");
                sb.AppendLine("            for (int i = temp.Count - 1; i >= 0; i--)");
                sb.AppendLine("            {");
                sb.AppendLine($"                result.Push(({member.ElementTypeName})temp[i]!);");
                sb.AppendLine("            }");
            }
            else
            {
                sb.AppendLine("            foreach (var item in source)");
                sb.AppendLine("            {");

                string itemExpr = GetItemCloneExpression(context, member, "item", isSafe, hasClonableAttr, needsState);

                sb.AppendLine($"                result.{addMethod}({itemExpr});");
                sb.AppendLine("            }");
            }
            
            sb.AppendLine();
            sb.AppendLine("            return result;");
        }

        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void WriteImmutableCollectionCloneMethod(CloneGeneratorContext context, MemberModel member, string typeName, string methodName, CollectionKind kind, bool needsState, bool isValueType)
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

        // Logic: Create a temporary List, clone items into it, then create Immutable collection
        sb.AppendLine($"            var temp = new global::System.Collections.Generic.List<{member.ElementTypeName}>();");
        
        // Loop and clone
        sb.AppendLine("            foreach (var item in source)");
        sb.AppendLine("            {");
        string itemExpr = GetItemCloneExpression(context, member, "item", isSafe, hasClonableAttr, needsState);
        sb.AppendLine($"                temp.Add({itemExpr});");
        sb.AppendLine("            }");

        // Handle Stack reversal
        if (kind == CollectionKind.ImmutableStack)
        {
            sb.AppendLine("            temp.Reverse();"); 
        }

        // Create immutable from temp
        // Map kind to factory method
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
            sb.AppendLine("            state?.AddKnownRef(source, result);");
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

        // Logic: Create a temporary List, clone items into it, then create ReadOnlyCollection
        sb.AppendLine($"            var temp = new global::System.Collections.Generic.List<{member.ElementTypeName}>(source.Count);");
        
        sb.AppendLine("            foreach (var item in source)");
        sb.AppendLine("            {");
        string itemExpr = GetItemCloneExpression(context, member, "item", isSafe, hasClonableAttr, needsState);
        sb.AppendLine($"                temp.Add({itemExpr});");
        sb.AppendLine("            }");

        sb.AppendLine($"            var result = new global::System.Collections.ObjectModel.ReadOnlyCollection<{member.ElementTypeName}>(temp);");

        if (needsState)
        {
            sb.AppendLine("            state?.AddKnownRef(source, result);");
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
            return $"{itemVar}?.FastDeepClone()";
        }

        if (context.TryGetMemberModel(member.ElementTypeName!, out MemberModel nestedModel))
        {
            // Use helper for nested collection/dictionary
            string helperName = context.GetOrCreateHelperMethodName(nestedModel);
            bool elementNeedsState = MemberCloneGenerator.MemberNeedsCircularRefTracking(context, nestedModel);
            string actualStateVar = parentNeedsState ? "state" : "null";
            return GetHelperMethodCall(context, helperName, itemVar, elementNeedsState, actualStateVar);
        }

        if (context.TryGetImplicitTypeModel(member.ElementTypeName!, out TypeModel implicitModel))
        {
            if (context.ShouldInline(implicitModel.FullyQualifiedName))
            {
                bool elementNeedsStateInline = implicitModel.CanHaveCircularReferences && context.CanHaveCircularReferences;
                string actualStateVarInline = parentNeedsState ? "state" : "null";
                // For collection items, we assume they are nullable (safe default) unless we know otherwise.
                // Since we don't have per-item nullability info easily here without updating MemberModel more deeply,
                // we default to isMemberNullable=true to be safe (keep null checks).
                return MemberCloneGenerator.GetImplicitCloneExpression(context, implicitModel, itemVar, actualStateVarInline, "                ", true);
            }

            // Use helper for implicit type
            string helperName = context.GetOrCreateHelperMethodName(implicitModel.FullyQualifiedName);
            bool elementNeedsState = implicitModel.CanHaveCircularReferences && context.CanHaveCircularReferences;
            string actualStateVar = parentNeedsState ? "state" : "null";
            return GetHelperMethodCall(context, helperName, itemVar, elementNeedsState, actualStateVar);
        }

        if (context.IsFastClonerAvailable)
        {
            // Use runtime FastCloner for elements that require it (e.g. generics, unclonable types)
            return $"({member.ElementTypeName}?)FastCloner.DeepClone({itemVar})";
        }

        // Fallback to shallow copy (should be caught by diagnostics if it was required)
        return itemVar;
    }

    private static void WriteDictionaryCloneMethod(CloneGeneratorContext context, MemberModel member)
    {
        if (member.KeyTypeName == null || member.ValueTypeName == null) return;

        string typeName = member.TypeFullName;
        string methodName = context.GetMethodName(typeName);
        CollectionKind kind = member.CollectionKind;
        bool needsState = MemberCloneGenerator.MemberNeedsCircularRefTracking(context, member);
        bool isValueType = member.IsValueType;

        // Handle special dictionaries
        if (kind.ToString().StartsWith("Immutable"))
        {
            WriteImmutableDictionaryCloneMethod(context, member, typeName, methodName, kind, needsState, isValueType);
            return;
        }

        if (kind == CollectionKind.ReadOnlyDictionary)
        {
            WriteReadOnlyDictionaryCloneMethod(context, member, typeName, methodName, needsState, isValueType);
            return;
        }

        StringBuilder sb = context.Source;

        // Use pre-computed concrete type and kind
        string concreteType = member.ConcreteTypeFullName ?? typeName;

        // SortedDictionary and ConcurrentDictionary don't take capacity in constructor (or not just capacity)
        // SortedList DOES take capacity. Standard Dictionary DOES take capacity.
        bool supportsCapacity = kind == CollectionKind.Dictionary || 
                                kind == CollectionKind.SortedList ||
                                kind == CollectionKind.List || 
                                kind == CollectionKind.None;

        bool isConcurrent = kind == CollectionKind.ConcurrentDictionary;

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

        // Pre-allocate capacity for Dictionary
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
            sb.AppendLine("            state?.AddKnownRef(source, result);");
            sb.AppendLine();
        }

        sb.AppendLine("            foreach (var kvp in source)");
        sb.AppendLine("            {");
        
        // Determine Key and Value expressions
        (string keyExpr, string valExpr) = GetKeyValueExpressions(context, member, needsState);

        if (isConcurrent)
        {
            sb.AppendLine($"                result.TryAdd({keyExpr}, {valExpr});");
        }
        else
        {
            sb.AppendLine($"                result.Add({keyExpr}, {valExpr});");
        }

        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            return result;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void WriteImmutableDictionaryCloneMethod(CloneGeneratorContext context, MemberModel member, string typeName, string methodName, CollectionKind kind, bool needsState, bool isValueType)
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

        // Logic: Create a temporary Dictionary, clone items into it, then create Immutable collection
        sb.AppendLine($"            var temp = new global::System.Collections.Generic.Dictionary<{member.KeyTypeName}, {member.ValueTypeName}>(source.Count);");
        
        sb.AppendLine("            foreach (var kvp in source)");
        sb.AppendLine("            {");
        (string keyExpr, string valExpr) = GetKeyValueExpressions(context, member, needsState);
        sb.AppendLine($"                temp.Add({keyExpr}, {valExpr});");
        sb.AppendLine("            }");

        string factoryMethod = kind == CollectionKind.ImmutableSortedDictionary
            ? "global::System.Collections.Immutable.ImmutableSortedDictionary.CreateRange(temp)"
            : "global::System.Collections.Immutable.ImmutableDictionary.CreateRange(temp)";

        sb.AppendLine($"            var result = {factoryMethod};");

        if (needsState)
        {
            sb.AppendLine("            state?.AddKnownRef(source, result);");
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

        // Logic: Create a temporary Dictionary, clone items into it, then create ReadOnlyDictionary
        sb.AppendLine($"            var temp = new global::System.Collections.Generic.Dictionary<{member.KeyTypeName}, {member.ValueTypeName}>(source.Count);");
        
        sb.AppendLine("            foreach (var kvp in source)");
        sb.AppendLine("            {");
        (string keyExpr, string valExpr) = GetKeyValueExpressions(context, member, needsState);
        sb.AppendLine($"                temp.Add({keyExpr}, {valExpr});");
        sb.AppendLine("            }");

        sb.AppendLine($"            var result = new global::System.Collections.ObjectModel.ReadOnlyDictionary<{member.KeyTypeName}, {member.ValueTypeName}>(temp);");

        if (needsState)
        {
            sb.AppendLine("            state?.AddKnownRef(source, result);");
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
                keyExpr = "kvp.Key?.FastDeepClone()";
            }
            else if (context.TryGetMemberModel(member.KeyTypeName!, out MemberModel nestedKeyModel))
            {
                string helperName = context.GetOrCreateHelperMethodName(nestedKeyModel);
                bool keyNeedsState = MemberCloneGenerator.MemberNeedsCircularRefTracking(context, nestedKeyModel);
                string actualStateVar = needsState ? "state" : "null";
                keyExpr = GetHelperMethodCall(context, helperName, "kvp.Key", keyNeedsState, actualStateVar);
            }
            else if (context.TryGetImplicitTypeModel(member.KeyTypeName!, out TypeModel implicitKeyModel))
            {
                if (context.ShouldInline(implicitKeyModel.FullyQualifiedName))
                {
                    bool keyNeedsState = implicitKeyModel.CanHaveCircularReferences && context.CanHaveCircularReferences;
                    string actualStateVar = needsState ? "state" : "null";
                    // Dictionary keys are generally not null, but safe default is true
                    keyExpr = MemberCloneGenerator.GetImplicitCloneExpression(context, implicitKeyModel, "kvp.Key", actualStateVar, "                ", true);
                }
                else
                {
                    string helperName = context.GetOrCreateHelperMethodName(implicitKeyModel.FullyQualifiedName);
                    bool keyNeedsState = implicitKeyModel.CanHaveCircularReferences && context.CanHaveCircularReferences;
                    string actualStateVar = needsState ? "state" : "null";
                    keyExpr = GetHelperMethodCall(context, helperName, "kvp.Key", keyNeedsState, actualStateVar);
                }
            }
            else if (context.IsFastClonerAvailable)
            {
                keyExpr = $"({member.KeyTypeName})FastCloner.DeepClone(kvp.Key)";
            }
        }

        string valExpr = "kvp.Value";
        if (!member.ValueIsSafe)
        {
            if (member.ValueIsClonable)
            {
                valExpr = "kvp.Value?.FastDeepClone()";
            }
            else if (context.TryGetMemberModel(member.ValueTypeName!, out MemberModel nestedValModel))
            {
                string helperName = context.GetOrCreateHelperMethodName(nestedValModel);
                bool valNeedsState = MemberCloneGenerator.MemberNeedsCircularRefTracking(context, nestedValModel);
                string actualStateVar = needsState ? "state" : "null";
                valExpr = GetHelperMethodCall(context, helperName, "kvp.Value", valNeedsState, actualStateVar);
            }
            else if (context.TryGetImplicitTypeModel(member.ValueTypeName!, out TypeModel implicitValModel))
            {
                if (context.ShouldInline(implicitValModel.FullyQualifiedName))
                {
                    bool valNeedsState = implicitValModel.CanHaveCircularReferences && context.CanHaveCircularReferences;
                    string actualStateVar = needsState ? "state" : "null";
                    // Dictionary values might be null
                    valExpr = MemberCloneGenerator.GetImplicitCloneExpression(context, implicitValModel, "kvp.Value", actualStateVar, "                ", true);
                }
                else
                {
                    string helperName = context.GetOrCreateHelperMethodName(implicitValModel.FullyQualifiedName);
                    bool valNeedsState = implicitValModel.CanHaveCircularReferences && context.CanHaveCircularReferences;
                    string actualStateVar = needsState ? "state" : "null";
                    valExpr = GetHelperMethodCall(context, helperName, "kvp.Value", valNeedsState, actualStateVar);
                }
            }
            else if (context.IsFastClonerAvailable)
            {
                valExpr = $"({member.ValueTypeName})FastCloner.DeepClone(kvp.Value)";
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
        bool needsState = MemberCloneGenerator.MemberNeedsCircularRefTracking(context, member);
        StringBuilder sb = context.Source;

        if (needsState)
        {
            context.NeedsStateClass = true;
        }

        // Arrays are always reference types
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

        // Create array - handle jagged arrays properly
        // For jagged arrays like int[][], the element type is int[] 
        // We need to create: new int[source.Length][]  (not new int[][source.Length])
        string arrayCreationExpr = GetArrayCreationExpression(member.ElementTypeName, "source.Length");
        sb.AppendLine($"            var result = {arrayCreationExpr};");

        if (needsState)
        {
            sb.AppendLine("            state?.AddKnownRef(source, result);");
            sb.AppendLine();
        }

        // Arrays use indexing, not Add()
        sb.AppendLine("            for (int i = 0; i < source.Length; i++)");
        sb.AppendLine("            {");

        string itemExpr;
        
        if (isSafe)
        {
            itemExpr = "source[i]";
        }
        else if (hasClonableAttr)
        {
            itemExpr = "source[i]?.FastDeepClone()";
        }
        else if (context.TryGetMemberModel(member.ElementTypeName!, out MemberModel nestedModel))
        {
            string helperName = context.GetOrCreateHelperMethodName(nestedModel);
            bool elementNeedsState = MemberCloneGenerator.MemberNeedsCircularRefTracking(context, nestedModel);
            string actualStateVar = needsState ? "state" : "null";
            itemExpr = GetHelperMethodCall(context, helperName, "source[i]", elementNeedsState, actualStateVar);
        }
        else if (context.TryGetImplicitTypeModel(member.ElementTypeName!, out TypeModel implicitModel))
        {
            if (context.ShouldInline(implicitModel.FullyQualifiedName))
            {
                bool elementNeedsState = implicitModel.CanHaveCircularReferences && context.CanHaveCircularReferences;
                string actualStateVar = needsState ? "state" : "null";
                // Array elements might be null
                itemExpr = MemberCloneGenerator.GetImplicitCloneExpression(context, implicitModel, "source[i]", actualStateVar, "                ", true);
            }
            else
            {
                string helperName = context.GetOrCreateHelperMethodName(implicitModel.FullyQualifiedName);
                bool elementNeedsState = implicitModel.CanHaveCircularReferences && context.CanHaveCircularReferences;
                string actualStateVar = needsState ? "state" : "null";
                itemExpr = GetHelperMethodCall(context, helperName, "source[i]", elementNeedsState, actualStateVar);
            }
        }
        else if (context.IsFastClonerAvailable)
        {
            // Use runtime FastCloner for elements that require it
            itemExpr = $"({member.ElementTypeName})FastCloner.DeepClone(source[i])";
        }
        else
        {
            // Fallback to shallow copy
            itemExpr = "source[i]";
        }

        sb.AppendLine($"                result[i] = {itemExpr};");
        sb.AppendLine("            }");
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
        bool needsState = MemberCloneGenerator.MemberNeedsCircularRefTracking(context, member);
        int rank = member.ArrayRank;
        StringBuilder sb = context.Source;

        if (needsState)
        {
            context.NeedsStateClass = true;
        }

        // Arrays are always reference types
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

        // Generate dimension length variables: len0, len1, len2, ...
        for (int d = 0; d < rank; d++)
        {
            sb.AppendLine($"            int len{d} = source.GetLength({d});");
        }
        sb.AppendLine();

        // Create the result array with the same dimensions
        // e.g., new T[len0, len1, len2]
        string dimList = string.Join(", ", Enumerable.Range(0, rank).Select(d => $"len{d}"));
        sb.AppendLine($"            var result = new {member.ElementTypeName}[{dimList}];");

        if (needsState)
        {
            sb.AppendLine("            state?.AddKnownRef(source, result);");
        }
        sb.AppendLine();

        // Optimization: if element type is safe, use Array.Copy
        if (isSafe)
        {
            sb.AppendLine("            // Element type is safe, use fast Array.Copy");
            sb.AppendLine("            global::System.Array.Copy(source, result, source.Length);");
        }
        else
        {
            // Generate nested loops for each dimension
            // for (int i0 = 0; i0 < len0; i0++)
            //     for (int i1 = 0; i1 < len1; i1++)
            //         ...
            //             result[i0, i1, ...] = Clone(source[i0, i1, ...]);

            for (int d = 0; d < rank; d++)
            {
                string indent = new string(' ', 12 + d * 4);
                sb.AppendLine($"{indent}for (int i{d} = 0; i{d} < len{d}; i{d}++)");
            }

            string innerIndent = new string(' ', 12 + rank * 4);
            string indexList = string.Join(", ", Enumerable.Range(0, rank).Select(d => $"i{d}"));

            // Determine how to clone each element
            string itemExpr;
            if (hasClonableAttr)
            {
                itemExpr = $"source[{indexList}]?.FastDeepClone()";
            }
            else if (context.TryGetMemberModel(member.ElementTypeName!, out MemberModel nestedModel))
            {
                string helperName = context.GetOrCreateHelperMethodName(nestedModel);
                bool elementNeedsState = MemberCloneGenerator.MemberNeedsCircularRefTracking(context, nestedModel);
                string actualStateVar = needsState ? "state" : "null";
                itemExpr = GetHelperMethodCall(context, helperName, $"source[{indexList}]", elementNeedsState, actualStateVar);
            }
            else if (context.TryGetImplicitTypeModel(member.ElementTypeName!, out TypeModel implicitModel))
            {
                if (context.ShouldInline(implicitModel.FullyQualifiedName))
                {
                    bool elementNeedsState = implicitModel.CanHaveCircularReferences && context.CanHaveCircularReferences;
                    string actualStateVar = needsState ? "state" : "null";
                    // Multi-dim array elements might be null
                    itemExpr = MemberCloneGenerator.GetImplicitCloneExpression(context, implicitModel, $"source[{indexList}]", actualStateVar, "                ", true);
                }
                else
                {
                    string helperName = context.GetOrCreateHelperMethodName(implicitModel.FullyQualifiedName);
                    bool elementNeedsState = implicitModel.CanHaveCircularReferences && context.CanHaveCircularReferences;
                    string actualStateVar = needsState ? "state" : "null";
                    itemExpr = GetHelperMethodCall(context, helperName, $"source[{indexList}]", elementNeedsState, actualStateVar);
                }
            }
            else if (context.IsFastClonerAvailable)
            {
                // Use runtime FastCloner for elements that require it
                itemExpr = $"({member.ElementTypeName})FastCloner.DeepClone(source[{indexList}])";
            }
            else
            {
                // Fallback to shallow copy
                itemExpr = $"source[{indexList}]";
            }

            sb.AppendLine($"{innerIndent}result[{indexList}] = {itemExpr};");
        }

        sb.AppendLine();
        sb.AppendLine("            return result;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>
    /// Creates the correct array instantiation expression for both regular and jagged arrays.
    /// For regular arrays like int[], creates: new int[size]
    /// For jagged arrays like int[][], creates: new int[size][] (not new int[][size] which is invalid)
    /// </summary>
    private static string GetArrayCreationExpression(string elementTypeName, string sizeExpression)
    {
        // Check if the element type is itself an array (jagged array scenario)
        // e.g., for int[][], elementTypeName is "int[]"
        int bracketIndex = elementTypeName.IndexOf('[');
        
        if (bracketIndex >= 0)
        {
            // Jagged array: element type contains brackets
            // Split into base type and trailing brackets
            // int[] → baseType="int", trailingBrackets="[]"
            // int[][] → baseType="int", trailingBrackets="[][]"
            string baseType = elementTypeName.Substring(0, bracketIndex);
            string trailingBrackets = elementTypeName.Substring(bracketIndex);
            
            // Create: new baseType[size]trailingBrackets
            // e.g., new int[source.Length][] for int[][] array
            return $"new {baseType}[{sizeExpression}]{trailingBrackets}";
        }
        else
        {
            // Regular array: element type has no brackets
            // Create: new elementTypeName[size]
            return $"new {elementTypeName}[{sizeExpression}]";
        }
    }

    private static void WriteHelperMethodSignature(CloneGeneratorContext context, string typeName, string methodName, bool needsState, bool isValueType)
    {
        string typeParams = GetTypeParametersString(context.Model);
        string constraints = GetTypeConstraintsString(context.Model);
        StringBuilder sb = context.Source;
        
        string typeSuffix = "?";
        if (isValueType)
        {
            typeSuffix = ""; // Value types are strict. If nullable, typeName has it.
        }
        else
        {
            // Reference types.
            if (typeName.EndsWith("?")) typeSuffix = ""; // Already has it
        }

        string staticMod = context.UseStaticMethods ? "static " : "";

        if (needsState)
        {
            sb.AppendLine($"        private {staticMod}{typeName}{typeSuffix} {methodName}{typeParams}({typeName}{typeSuffix} source, FcGeneratedCloneState? state){constraints}");
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
            return $"{methodName}{typeParams}({sourceExpression}, {stateVar})";
        }

        return $"{methodName}{typeParams}({sourceExpression})";
    }

    private static string GetTypeParametersString(TypeModel model)
    {
        if (model.TypeParameters.Count == 0)
            return string.Empty;

        return $"<{string.Join(", ", model.TypeParameters)}>";
    }

    private static string GetTypeConstraintsString(TypeModel model)
    {
        if (model.TypeConstraints.Count == 0)
            return string.Empty;

        return " " + string.Join(" ", model.TypeConstraints);
    }
}
