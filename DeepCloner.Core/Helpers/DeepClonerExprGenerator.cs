using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace DeepCloner.Core.Helpers;

internal static class DeepClonerExprGenerator
{
    private static readonly ConcurrentDictionary<FieldInfo, bool> _readonlyFields = new ConcurrentDictionary<FieldInfo, bool>();

    private static readonly MethodInfo _fieldSetMethod;
    static DeepClonerExprGenerator() => _fieldSetMethod = typeof(FieldInfo).GetMethod(nameof(FieldInfo.SetValue), [typeof(object), typeof(object)])!;

    internal static object GenerateClonerInternal(Type realType, bool asObject) => GenerateProcessMethod(realType, asObject && realType.IsValueType());

    // today, I found that it is not required to do such complex things. Just SetValue is enough
    // is it new runtime changes, or I made incorrect assumptions earlier
    // slow, but hardcore method to set readonly field
    internal static void ForceSetField(FieldInfo field, object obj, object value)
    {
        var fieldInfo = field.GetType().GetPrivateField("m_fieldAttributes");

        // TODO: think about it
        // nothing to do :( we should a throw an exception, but it is no good for user
        if (fieldInfo == null) return;

        var ov = fieldInfo.GetValue(field);
        if (ov is not FieldAttributes fieldAttributes) return;

        // protect from parallel execution, when first thread set field readonly back, and second set it to write value
        lock (fieldInfo)
        {
            fieldInfo.SetValue(field, fieldAttributes & ~FieldAttributes.InitOnly);
            field.SetValue(obj, value);
            fieldInfo.SetValue(field, fieldAttributes | FieldAttributes.InitOnly);
        }
    }

    private readonly record struct ExpressionPosition(int Depth, int Index)
    {
        public ExpressionPosition Next() => this with { Index = Index + 1 };
        public ExpressionPosition Nested() => new ExpressionPosition(Depth + 1, 0);
    }

    private static LabelTarget CreateLoopLabel(ExpressionPosition position)
    {
        string str = $"Loop_{position.Depth}_{position.Index}";
        return Expression.Label(str);
    }

    internal static object GenerateProcessMethod(Type realType, bool asObject) => GenerateProcessMethod(realType, asObject && realType.IsValueType(), new ExpressionPosition(0, 0));
    public static bool IsSetType(Type type) => type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISet<>));
    private static bool IsDictionaryType(Type type) => typeof(IDictionary).IsAssignableFrom(type) || type.GetInterfaces().Any(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(IDictionary<,>) || i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));

    private static object GenerateProcessMethod(Type type, bool unboxStruct, ExpressionPosition position)
    {
        if (IsDictionaryType(type))
        {
            return GenerateProcessDictionaryMethod(type, position);
        }

        if (IsSetType(type))
        {
            return GenerateProcessSetMethod(type, position);
        }

        if (type.IsArray)
        {
            return GenerateProcessArrayMethod(type);
        }

        if (type.FullName != null && type.FullName.StartsWith("System.Tuple`"))
        {
            // if not safe type it is no guarantee that some type will contain reference to
            // this tuple. In usual way, we're creating new object, setting reference for it
            // and filling data. For tuple, we will fill data before creating object
            // (in constructor arguments)
            var genericArguments = type.GenericArguments();
            // current tuples contain only 8 arguments, but may be in future...
            // we'll write code that works with it
            if (genericArguments.Length < 10 && genericArguments.All(DeepClonerSafeTypes.CanReturnSameObject))
            {
                return GenerateProcessTupleMethod(type);
            }
        }

        var methodType = unboxStruct || type.IsClass() ? typeof(object) : type;

        var expressionList = new List<Expression>();

        ParameterExpression from = Expression.Parameter(methodType);
        var fromLocal = from;
        var toLocal = Expression.Variable(type);
        var state = Expression.Parameter(typeof(DeepCloneState));

        if (!type.IsValueType())
        {
            var methodInfo = typeof(object).GetPrivateMethod(nameof(MemberwiseClone))!;

            // to = (T)from.MemberwiseClone()
            expressionList.Add(Expression.Assign(toLocal, Expression.Convert(Expression.Call(from, methodInfo), type)));

            fromLocal = Expression.Variable(type);
            // fromLocal = (T)from
            expressionList.Add(Expression.Assign(fromLocal, Expression.Convert(from, type)));

            // added from -> to binding to ensure reference loop handling
            // structs cannot loop here
            // state.AddKnownRef(from, to)
            expressionList.Add(Expression.Call(state, StaticMethodInfos.DeepCloneStateMethods.AddKnownRef, from, toLocal));
        }
        else
        {
            if (unboxStruct)
            {
                // toLocal = (T)from;
                expressionList.Add(Expression.Assign(toLocal, Expression.Unbox(from, type)));
                fromLocal = Expression.Variable(type);
                // fromLocal = toLocal; // structs, it is ok to copy
                expressionList.Add(Expression.Assign(fromLocal, toLocal));
            }
            else
            {
                // toLocal = from
                expressionList.Add(Expression.Assign(toLocal, from));
            }
        }

        List<FieldInfo> fi = [];
        var tp = type;
        do
        {
            // don't do anything with this dark magic!
            if (tp == typeof(ContextBoundObject)) break;

            fi.AddRange(tp.GetDeclaredFields());
            tp = tp.BaseType();
        }
        while (tp != null);

        var currentPosition = position;

        foreach (var fieldInfo in fi)
        {
            if (!DeepClonerSafeTypes.CanReturnSameObject(fieldInfo.FieldType))
            {
                var methodInfo = fieldInfo.FieldType.IsValueType()
                    ? typeof(DeepClonerGenerator).GetPrivateStaticMethod(nameof(DeepClonerGenerator.CloneStructInternal))!
                        .MakeGenericMethod(fieldInfo.FieldType)
                    : typeof(DeepClonerGenerator).GetPrivateStaticMethod(nameof(DeepClonerGenerator.CloneClassInternal))!;

                var get = Expression.Field(fromLocal, fieldInfo);

                // toLocal.Field = Clone...Internal(fromLocal.Field)
                Expression call = Expression.Call(methodInfo, get, state);
                if (!fieldInfo.FieldType.IsValueType())
                    call = Expression.Convert(call, fieldInfo.FieldType);

                // should handle specially
                // todo: think about optimization, but it rare case
                var isReadonly = _readonlyFields.GetOrAdd(fieldInfo, f => f.IsInitOnly);
                if (isReadonly)
                {
                    expressionList.Add(Expression.Call(
                                           Expression.Constant(fieldInfo),
                                           _fieldSetMethod,
                                           Expression.Convert(toLocal, typeof(object)),
                                           Expression.Convert(call, typeof(object))));
                }
                else
                {
                    expressionList.Add(Expression.Assign(Expression.Field(toLocal, fieldInfo), call));
                }

                currentPosition = currentPosition.Next();
            }
        }

        expressionList.Add(Expression.Convert(toLocal, methodType));

        var funcType = typeof(Func<,,>).MakeGenericType(methodType, typeof(DeepCloneState), methodType);

        var blockParams = new List<ParameterExpression>();
        if (from != fromLocal) blockParams.Add(fromLocal);
        blockParams.Add(toLocal);

        return Expression.Lambda(funcType, Expression.Block(blockParams, expressionList), from, state).Compile();
    }

    private static object GenerateProcessDictionaryMethod(Type type, ExpressionPosition position)
    {
        var genericArguments = type.GenericArguments();
        return genericArguments.Length switch
        {
            0 => GenerateDictionaryProcessor(type, typeof(object), typeof(object), false, position),
            1 => HandleSingleGenericArgument(type, genericArguments[0], position),
            2 => GenerateDictionaryProcessor(type, genericArguments[0], genericArguments[1], true, position),
            _ => throw new ArgumentException($"Unexpected number of generic arguments: {genericArguments.Length}")
        };
    }

    private static object HandleSingleGenericArgument(Type type, Type genericArg, ExpressionPosition position)
    {
        if (genericArg.IsGenericType && genericArg.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
        {
            var kvpArguments = genericArg.GetGenericArguments();
            return GenerateDictionaryProcessor(type, kvpArguments[0], kvpArguments[1], true, position);
        }

        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            return GenerateDictionaryProcessor(type, typeof(object), genericArg, true, position);
        }

        throw new ArgumentException($"Unsupported dictionary type with single generic argument: {type.FullName}");
    }

    private static object GenerateDictionaryProcessor(Type dictType, Type keyType, Type valueType, bool isGeneric, ExpressionPosition position)
    {
        var from = Expression.Parameter(typeof(object));
        var state = Expression.Parameter(typeof(DeepCloneState));
        var local = Expression.Variable(dictType);

        // Initialize dictionary
        var assign = Expression.Assign(local, Expression.New(dictType.GetConstructor(Type.EmptyTypes)!));

        // Get Add/TryAdd method
        var addMethod = (dictType.IsGenericType ? dictType.GetMethod("Add", [keyType, valueType]) : typeof(IDictionary).GetMethod("Add")) ?? dictType.GetMethods().FirstOrDefault(m => m.Name == "TryAdd" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == keyType && m.GetParameters()[1].ParameterType == valueType);

        // Setup enumerator
        var enumeratorType = isGeneric
            ? typeof(IEnumerator<>).MakeGenericType(typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType))
            : typeof(IDictionaryEnumerator);

        var enumerator = Expression.Variable(enumeratorType);

        // Get clone methods
        var keyCloneMethod = keyType.IsValueType()
            ? typeof(DeepClonerGenerator).GetPrivateStaticMethod(nameof(DeepClonerGenerator.CloneStructInternal))!.MakeGenericMethod(keyType)
            : typeof(DeepClonerGenerator).GetPrivateStaticMethod(nameof(DeepClonerGenerator.CloneClassInternal))!;

        var valueCloneMethod = valueType.IsValueType()
            ? typeof(DeepClonerGenerator).GetPrivateStaticMethod(nameof(DeepClonerGenerator.CloneStructInternal))!.MakeGenericMethod(valueType)
            : typeof(DeepClonerGenerator).GetPrivateStaticMethod(nameof(DeepClonerGenerator.CloneClassInternal))!;

        // Generate iteration logic
        var iterationBlock = isGeneric
            ? GenerateGenericDictionaryIteration(enumerator, keyType, valueType, keyCloneMethod, valueCloneMethod, local, addMethod, state, position)
            : GenerateNonGenericDictionaryIteration(enumerator, keyCloneMethod, valueCloneMethod, local, addMethod, state, position);

        // Combine into final expression
        var block = Expression.Block(
            [local, enumerator],
            assign,
            Expression.Assign(enumerator, GetEnumeratorExpression(from, dictType, isGeneric, enumeratorType)),
            iterationBlock,
            local);

        var funcType = typeof(Func<object, DeepCloneState, object>);
        return Expression.Lambda(funcType, block, from, state).Compile();
    }

    private static Expression GetEnumeratorExpression(ParameterExpression from, Type dictType, bool isGeneric, Type enumeratorType)
    {
        if (isGeneric)
        {
            return Expression.Convert(
                Expression.Call(
                    Expression.Convert(from, typeof(IEnumerable)),
                    typeof(IEnumerable).GetMethod(nameof(IEnumerable.GetEnumerator))!),
                enumeratorType);
        }

        return Expression.Convert(
            Expression.Call(
                Expression.Convert(from, typeof(IDictionary)),
                typeof(IDictionary).GetMethod(nameof(IDictionary.GetEnumerator))!),
            typeof(IDictionaryEnumerator));
    }

    private static BlockExpression GenerateGenericDictionaryIteration(ParameterExpression enumerator, Type keyType, Type valueType, MethodInfo keyCloneMethod, MethodInfo valueCloneMethod, ParameterExpression local, MethodInfo addMethod, ParameterExpression state, ExpressionPosition position)
    {
        var current = enumerator.Type.GetProperty(nameof(IEnumerator<object>.Current))!;
        var breakLabel = CreateLoopLabel(position);
        var dictionaryType = local.Type;
        var isSingleGenericParameter = dictionaryType.GetGenericArguments().Length is 1;

        if (isSingleGenericParameter)
        {
            var singleGenericType = dictionaryType.GetGenericArguments()[0];
            if (singleGenericType.IsGenericType && singleGenericType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var kvpTypes = singleGenericType.GetGenericArguments();
                var kvp = Expression.Variable(singleGenericType);
                var assignKvp = Expression.Assign(kvp, Expression.Property(enumerator, current));
                var newKvp = Expression.New(
                    singleGenericType.GetConstructor([kvpTypes[0], kvpTypes[1]])!,
                    Expression.Convert(
                        Expression.Call(keyCloneMethod, Expression.Property(kvp, "Key"), state),
                        kvpTypes[0]),
                    Expression.Convert(
                        Expression.Call(valueCloneMethod, Expression.Property(kvp, "Value"), state),
                        kvpTypes[1]));
                var collectionAddMethod = dictionaryType.GetMethod("Add", [singleGenericType])!;
                var addKvp = Expression.Call(local, collectionAddMethod, newKvp);

                var loop = Expression.Loop(
                    Expression.IfThenElse(
                        Expression.Call(enumerator, typeof(IEnumerator).GetMethod(nameof(IEnumerator.MoveNext))!),
                        Expression.Block([kvp], assignKvp, addKvp),
                        Expression.Break(breakLabel)),
                    breakLabel);

                return Expression.Block(loop);
            }
        }
        {
            var kvpType = typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType);
            var kvp = Expression.Variable(kvpType);
            var key = Expression.Variable(keyType);
            var value = Expression.Variable(valueType);

            var assignKvp = Expression.Assign(kvp, Expression.Property(enumerator, current));
            var assignKey = Expression.Assign(
                key,
                Expression.Convert(
                    Expression.Call(keyCloneMethod, Expression.Property(kvp, "Key"), state),
                    keyType));
            var assignValue = Expression.Assign(
                value,
                Expression.Convert(
                    Expression.Call(valueCloneMethod, Expression.Property(kvp, "Value"), state),
                    valueType));

            var addKvp = Expression.Call(local, addMethod, key, value);

            var loop = Expression.Loop(
                Expression.IfThenElse(
                    Expression.Call(enumerator, typeof(IEnumerator).GetMethod(nameof(IEnumerator.MoveNext))!),
                    Expression.Block([kvp, key, value],
                        assignKvp,
                        assignKey,
                        assignValue,
                        addKvp),
                    Expression.Break(breakLabel)),
                breakLabel);

            return Expression.Block(loop);
        }
    }

    private static BlockExpression GenerateNonGenericDictionaryIteration(ParameterExpression enumerator, MethodInfo keyCloneMethod, MethodInfo valueCloneMethod, ParameterExpression local, MethodInfo addMethod, ParameterExpression state, ExpressionPosition position)
    {
        var current = Expression.Property(enumerator, nameof(IDictionaryEnumerator.Entry));
        var key = Expression.Variable(typeof(object));
        var value = Expression.Variable(typeof(object));

        var assignKey = Expression.Assign(
            key,
            Expression.Call(
                keyCloneMethod,
                Expression.Property(current, "Key"),
                state));

        var assignValue = Expression.Assign(
            value,
            Expression.Call(
                valueCloneMethod,
                Expression.Property(current, "Value"),
                state));

        var addEntry = Expression.Call(local, addMethod, key, value);
        var breakLabel = CreateLoopLabel(position);

        var loop = Expression.Loop(
            Expression.IfThenElse(
                Expression.Call(enumerator, typeof(IEnumerator).GetMethod(nameof(IEnumerator.MoveNext))!),
                Expression.Block(
                    [key, value],
                    assignKey,
                    assignValue,
                    addEntry),
                Expression.Break(breakLabel)),
            breakLabel);

        return Expression.Block(loop);
    }

    private static object GenerateProcessSetMethod(Type type, ExpressionPosition position)
    {
        var elementType = type.GenericArguments()[0];

        var cloneElementMethod = elementType.IsValueType()
            ? typeof(DeepClonerGenerator).GetPrivateStaticMethod(nameof(DeepClonerGenerator.CloneStructInternal))!.MakeGenericMethod(elementType)
            : typeof(DeepClonerGenerator).GetPrivateStaticMethod(nameof(DeepClonerGenerator.CloneClassInternal))!;

        ParameterExpression from = Expression.Parameter(typeof(object));
        var state = Expression.Parameter(typeof(DeepCloneState));

        var local = Expression.Variable(type);
        var assign = Expression.Assign(local, Expression.New(type.GetConstructor(Type.EmptyTypes)!));

        var addMethod = type.GetMethod("Add", [elementType]) ?? typeof(ISet<>).GetMethod("Add") ?? type.GetMethod("Add") ?? type.GetMethod("TryAdd");

        var foreachBlock = GenerateForeachBlock(from, elementType, null, cloneElementMethod, null, local, addMethod, state, position);
        var funcType = typeof(Func<object, DeepCloneState, object>);

        return Expression.Lambda(funcType, Expression.Block([local], assign, foreachBlock, local), from, state).Compile();
    }

    private static object GenerateProcessArrayMethod(Type type)
    {
        var elementType = type.GetElementType();
        var rank = type.GetArrayRank();

        MethodInfo methodInfo;

        // multidim or not zero-based arrays
        if (rank != 1 || type != elementType.MakeArrayType())
        {
            if (rank == 2 && type == elementType.MakeArrayType(2))
            {
                // small optimization for 2 dim arrays
                methodInfo = typeof(DeepClonerGenerator).GetPrivateStaticMethod(nameof(DeepClonerGenerator.Clone2DimArrayInternal))!.MakeGenericMethod(elementType);
            }
            else
            {
                methodInfo = typeof(DeepClonerGenerator).GetPrivateStaticMethod(nameof(DeepClonerGenerator.CloneAbstractArrayInternal))!;
            }
        }
        else
        {
            var methodName = nameof(DeepClonerGenerator.Clone1DimArrayClassInternal);
            if (DeepClonerSafeTypes.CanReturnSameObject(elementType)) methodName = nameof(DeepClonerGenerator.Clone1DimArraySafeInternal);
            else if (elementType.IsValueType()) methodName = nameof(DeepClonerGenerator.Clone1DimArrayStructInternal);
            methodInfo = typeof(DeepClonerGenerator).GetPrivateStaticMethod(methodName)!.MakeGenericMethod(elementType);
        }

        ParameterExpression from = Expression.Parameter(typeof(object));
        var state = Expression.Parameter(typeof(DeepCloneState));
        var call = Expression.Call(methodInfo, Expression.Convert(from, type), state);

        var funcType = typeof(Func<,,>).MakeGenericType(typeof(object), typeof(DeepCloneState), typeof(object));

        return Expression.Lambda(funcType, call, from, state).Compile();
    }

    private static BlockExpression GenerateForeachBlock(ParameterExpression from, Type keyType, Type? valueType, MethodInfo cloneKeyMethod, MethodInfo? cloneValueMethod, ParameterExpression local, MethodInfo addMethod, ParameterExpression state, ExpressionPosition position)
    {
        var enumeratorType = typeof(IEnumerator<>).MakeGenericType(valueType == null ? keyType : typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType));

        var enumerator = Expression.Variable(enumeratorType);
        var moveNext = typeof(IEnumerator).GetMethod(nameof(IEnumerator.MoveNext))!;
        var current = enumeratorType.GetProperty(nameof(IEnumerator.Current))!;

        var getEnumerator = typeof(IEnumerable).GetMethod(nameof(IEnumerable.GetEnumerator))!;

        var breakLabel = CreateLoopLabel(position);

        var loop = Expression.Loop(
            Expression.IfThenElse(
                Expression.Call(enumerator, moveNext),
                Expression.Block(
                    valueType is null
                        ? GenerateSetAddBlock(enumerator, current, keyType, cloneKeyMethod, local, addMethod, state)
                        : GenerateDictionaryAddBlock(enumerator, current, keyType, valueType, cloneKeyMethod, cloneValueMethod!, local, addMethod, state)),
                Expression.Break(breakLabel)),
            breakLabel);

        return Expression.Block(
            [enumerator],
            Expression.Assign(
                enumerator,
                Expression.Convert(
                    Expression.Call(Expression.Convert(from, typeof(IEnumerable)), getEnumerator),
                    enumeratorType)),
            loop);
    }

    private static BlockExpression GenerateSetAddBlock(ParameterExpression enumerator, PropertyInfo current, Type elementType, MethodInfo cloneElementMethod, ParameterExpression local, MethodInfo addMethod, ParameterExpression state)
    {
        var element = Expression.Variable(elementType);
        var assignElement = Expression.Assign(
            element,
            Expression.Convert(
                Expression.Call(cloneElementMethod, Expression.Property(enumerator, current), state),
                elementType
            )
        );
        var addElement = Expression.Call(local, addMethod, element);

        return Expression.Block([element], assignElement, addElement);
    }


    private static BlockExpression GenerateDictionaryAddBlock(ParameterExpression enumerator, PropertyInfo current, Type keyType, Type valueType, MethodInfo cloneKeyMethod, MethodInfo cloneValueMethod, ParameterExpression local, MethodInfo addMethod, ParameterExpression state)
    {
        var kvp = Expression.Variable(typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType));
        var assignKvp = Expression.Assign(kvp, Expression.Property(enumerator, current));

        var key = Expression.Variable(keyType);
        var value = Expression.Variable(valueType);

        var assignKey = Expression.Assign(
            key,
            Expression.Convert(
                Expression.Call(cloneKeyMethod, Expression.Property(kvp, "Key"), state),
                keyType));

        var assignValue = Expression.Assign(
            value,
            Expression.Convert(
                Expression.Call(cloneValueMethod, Expression.Property(kvp, "Value"), state),
                valueType));

        var addKvp = Expression.Call(local, addMethod, key, value);

        return Expression.Block([kvp, key, value], assignKvp, assignKey, assignValue, addKvp);
    }

    private static object GenerateProcessTupleMethod(Type type)
    {
        ParameterExpression from = Expression.Parameter(typeof(object));
        var state = Expression.Parameter(typeof(DeepCloneState));

        var local = Expression.Variable(type);
        var assign = Expression.Assign(local, Expression.Convert(from, type));

        var funcType = typeof(Func<object, DeepCloneState, object>);

        var tupleLength = type.GenericArguments().Length;

        var constructor = Expression.Assign(
            local,
            Expression.New(type.GetPublicConstructors().First(x => x.GetParameters().Length == tupleLength),
                           type.GetPublicProperties().OrderBy(x => x.Name)
                               .Where(x => x.CanRead && x.Name.StartsWith("Item") && char.IsDigit(x.Name[4]))
                               .Select(x => Expression.Property(local, x.Name))));

        return Expression.Lambda(
            funcType,
            Expression.Block(
                [local],
                             assign, constructor, Expression.Call(state, StaticMethodInfos.DeepCloneStateMethods.AddKnownRef, from, local),
                             from),
            from, state).Compile();
    }
}