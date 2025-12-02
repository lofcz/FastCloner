using System.Collections.Generic;

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
            var typeFullName = context.DequeuePendingHelperMethod();

            // Check if it's an implicit type cloner
            if (context.TryGetImplicitTypeModel(typeFullName, out var implicitModel))
            {
                WriteImplicitCloneMethod(context, implicitModel, context.GetMethodName(typeFullName));
                continue;
            }

            // Get the member model for this type
            if (!context.TryGetMemberModel(typeFullName, out var member))
                continue;

            // Generate appropriate helper method based on member type kind
            switch (member.TypeKind)
            {
                case MemberTypeKind.Array:
                    WriteArrayCloneMethod(context, member);
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
        var typeName = implicitModel.FullyQualifiedName;
        // Determine if we need state. 
        // If Root can't have circular refs, then we don't pass state down (it's null).
        // If Root CAN, then we check if ImplicitModel CAN.
        // If ImplicitModel CAN, we accept state.
        var needsState = implicitModel.CanHaveCircularReferences && context.CanHaveCircularReferences;
        var sb = context.Source;

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

            var assignments = new List<string>();
            foreach (var member in implicitModel.Members)
            {
                // Recursive call to GetMemberAssignment will trigger generation of dependencies
                var assign = MemberCloneGenerator.GetMemberAssignment(context, member, "source", needsState ? "state" : "null");
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
            foreach (var member in implicitModel.Members)
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

        var typeName = member.TypeFullName;
        var methodName = context.GetMethodName(typeName);
        var kind = member.CollectionKind;
        var needsState = MemberCloneGenerator.MemberNeedsCircularRefTracking(context, member);
        var isValueType = member.IsValueType;
        
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

        var isSafe = member.ElementIsSafe;
        var hasClonableAttr = member.ElementHasClonableAttr;
        var sb = context.Source;

        // Use pre-computed collection kind and concrete type
        var concreteType = member.ConcreteTypeFullName ?? typeName;
        
        // Determine operations based on kind
        var addMethod = "Add";
        var isQueue = kind == CollectionKind.Queue || kind == CollectionKind.ConcurrentQueue;
        var isStack = kind == CollectionKind.Stack || kind == CollectionKind.ConcurrentStack;
        var isLinkedList = kind == CollectionKind.LinkedList;
        
        if (isQueue) addMethod = "Enqueue";
        else if (isStack) addMethod = "Push";
        else if (isLinkedList) addMethod = "AddLast";

        // Determine if capacity can be passed to constructor
        // Only standard List, HashSet, Queue, Stack support new T(int capacity)
        var supportsCapacity = kind == CollectionKind.List || 
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
        var sb = context.Source;
        var isSafe = member.ElementIsSafe;
        var hasClonableAttr = member.ElementHasClonableAttr;

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
        var sb = context.Source;
        var isSafe = member.ElementIsSafe;
        var hasClonableAttr = member.ElementHasClonableAttr;

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

        if (context.TryGetMemberModel(member.ElementTypeName!, out var nestedModel))
        {
            // Use helper for nested collection/dictionary
            var helperName = context.GetOrCreateHelperMethodName(nestedModel);
            var elementNeedsState = MemberCloneGenerator.MemberNeedsCircularRefTracking(context, nestedModel);
            var actualStateVar = parentNeedsState ? "state" : "null";
            return GetHelperMethodCall(context, helperName, itemVar, elementNeedsState, actualStateVar);
        }

        if (context.TryGetImplicitTypeModel(member.ElementTypeName!, out var implicitModel))
        {
            // Use helper for implicit type
            var helperName = context.GetOrCreateHelperMethodName(implicitModel.FullyQualifiedName);
            var elementNeedsState = implicitModel.CanHaveCircularReferences && context.CanHaveCircularReferences;
            var actualStateVar = parentNeedsState ? "state" : "null";
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

        var typeName = member.TypeFullName;
        var methodName = context.GetMethodName(typeName);
        var kind = member.CollectionKind;
        var needsState = MemberCloneGenerator.MemberNeedsCircularRefTracking(context, member);
        var isValueType = member.IsValueType;

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

        var sb = context.Source;

        // Use pre-computed concrete type and kind
        var concreteType = member.ConcreteTypeFullName ?? typeName;

        // SortedDictionary and ConcurrentDictionary don't take capacity in constructor (or not just capacity)
        // SortedList DOES take capacity. Standard Dictionary DOES take capacity.
        var supportsCapacity = kind == CollectionKind.Dictionary || 
                               kind == CollectionKind.SortedList ||
                               kind == CollectionKind.List || 
                               kind == CollectionKind.None;

        var isConcurrent = kind == CollectionKind.ConcurrentDictionary;

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
        var (keyExpr, valExpr) = GetKeyValueExpressions(context, member, needsState);

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
        var sb = context.Source;
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
        var (keyExpr, valExpr) = GetKeyValueExpressions(context, member, needsState);
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
        var sb = context.Source;
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
        var (keyExpr, valExpr) = GetKeyValueExpressions(context, member, needsState);
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
            else if (context.TryGetMemberModel(member.KeyTypeName!, out var nestedKeyModel))
            {
                var helperName = context.GetOrCreateHelperMethodName(nestedKeyModel);
                var keyNeedsState = MemberCloneGenerator.MemberNeedsCircularRefTracking(context, nestedKeyModel);
                var actualStateVar = needsState ? "state" : "null";
                keyExpr = GetHelperMethodCall(context, helperName, "kvp.Key", keyNeedsState, actualStateVar);
            }
            else if (context.TryGetImplicitTypeModel(member.KeyTypeName!, out var implicitKeyModel))
            {
                var helperName = context.GetOrCreateHelperMethodName(implicitKeyModel.FullyQualifiedName);
                var keyNeedsState = implicitKeyModel.CanHaveCircularReferences && context.CanHaveCircularReferences;
                var actualStateVar = needsState ? "state" : "null";
                keyExpr = GetHelperMethodCall(context, helperName, "kvp.Key", keyNeedsState, actualStateVar);
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
            else if (context.TryGetMemberModel(member.ValueTypeName!, out var nestedValModel))
            {
                var helperName = context.GetOrCreateHelperMethodName(nestedValModel);
                var valNeedsState = MemberCloneGenerator.MemberNeedsCircularRefTracking(context, nestedValModel);
                var actualStateVar = needsState ? "state" : "null";
                valExpr = GetHelperMethodCall(context, helperName, "kvp.Value", valNeedsState, actualStateVar);
            }
            else if (context.TryGetImplicitTypeModel(member.ValueTypeName!, out var implicitValModel))
            {
                var helperName = context.GetOrCreateHelperMethodName(implicitValModel.FullyQualifiedName);
                var valNeedsState = implicitValModel.CanHaveCircularReferences && context.CanHaveCircularReferences;
                var actualStateVar = needsState ? "state" : "null";
                valExpr = GetHelperMethodCall(context, helperName, "kvp.Value", valNeedsState, actualStateVar);
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

        var typeName = member.TypeFullName;
        var methodName = context.GetMethodName(typeName);
        var isSafe = member.ElementIsSafe;
        var hasClonableAttr = member.ElementHasClonableAttr;
        var needsState = MemberCloneGenerator.MemberNeedsCircularRefTracking(context, member);
        var sb = context.Source;

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

        // Arrays use Length, not Count
        sb.AppendLine($"            var result = new {member.ElementTypeName}[source.Length];");

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
        else if (context.TryGetMemberModel(member.ElementTypeName!, out var nestedModel))
        {
            var helperName = context.GetOrCreateHelperMethodName(nestedModel);
            var elementNeedsState = MemberCloneGenerator.MemberNeedsCircularRefTracking(context, nestedModel);
            var actualStateVar = needsState ? "state" : "null";
            itemExpr = GetHelperMethodCall(context, helperName, "source[i]", elementNeedsState, actualStateVar);
        }
        else if (context.TryGetImplicitTypeModel(member.ElementTypeName!, out var implicitModel))
        {
            var helperName = context.GetOrCreateHelperMethodName(implicitModel.FullyQualifiedName);
            var elementNeedsState = implicitModel.CanHaveCircularReferences && context.CanHaveCircularReferences;
            var actualStateVar = needsState ? "state" : "null";
            itemExpr = GetHelperMethodCall(context, helperName, "source[i]", elementNeedsState, actualStateVar);
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

    private static void WriteHelperMethodSignature(CloneGeneratorContext context, string typeName, string methodName, bool needsState, bool isValueType)
    {
        var typeParams = GetTypeParametersString(context.Model);
        var constraints = GetTypeConstraintsString(context.Model);
        var sb = context.Source;
        
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

        if (needsState)
        {
            sb.AppendLine($"        private static {typeName}{typeSuffix} {methodName}{typeParams}({typeName}{typeSuffix} source, FcGeneratedCloneState? state){constraints}");
        }
        else
        {
            sb.AppendLine($"        private static {typeName}{typeSuffix} {methodName}{typeParams}({typeName}{typeSuffix} source){constraints}");
        }
    }

    private static string GetHelperMethodCall(CloneGeneratorContext context, string methodName, string sourceExpression, bool needsState, string stateVar = "null")
    {
        var typeParams = GetTypeParametersString(context.Model);

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
