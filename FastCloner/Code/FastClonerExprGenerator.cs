using System.Collections;
using System.Collections.Concurrent;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace FastCloner.Code;

internal static class FastClonerExprGenerator
{
    private static readonly ConcurrentDictionary<FieldInfo, bool> _readonlyFields = new ConcurrentDictionary<FieldInfo, bool>();

    private static readonly MethodInfo _fieldSetMethod;
    static FastClonerExprGenerator() => _fieldSetMethod = typeof(FieldInfo).GetMethod(nameof(FieldInfo.SetValue), [typeof(object), typeof(object)])!;

    internal static object GenerateClonerInternal(Type realType, bool asObject) => GenerateProcessMethod(realType, asObject && realType.IsValueType());

    // today, I found that it is not required to do such complex things. Just SetValue is enough
    // is it new runtime changes, or I made incorrect assumptions earlier
    // slow, but hardcore method to set readonly field
    internal static void ForceSetField(FieldInfo field, object obj, object value)
    {
        FieldInfo? fieldInfo = field.GetType().GetPrivateField("m_fieldAttributes");

        // TODO: think about it
        // nothing to do :( we should a throw an exception, but it is no good for user
        if (fieldInfo == null) return;

        object? ov = fieldInfo.GetValue(field);
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
        if (type == typeof(ExpandoObject))
        {
            return GenerateExpandoObjectProcessor(position);
        }
        
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
            Type[] genericArguments = type.GenericArguments();
            // current tuples contain only 8 arguments, but may be in future...
            // we'll write code that works with it
            if (genericArguments.Length < 10 && genericArguments.All(FastClonerSafeTypes.CanReturnSameObject))
            {
                return GenerateProcessTupleMethod(type);
            }
        }

        Type methodType = unboxStruct || type.IsClass() ? typeof(object) : type;

        List<Expression> expressionList = [];

        ParameterExpression from = Expression.Parameter(methodType);
        ParameterExpression fromLocal = from;
        ParameterExpression toLocal = Expression.Variable(type);
        ParameterExpression state = Expression.Parameter(typeof(FastCloneState));

        if (!type.IsValueType())
        {
            MethodInfo methodInfo = typeof(object).GetPrivateMethod(nameof(MemberwiseClone))!;

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
        Type? tp = type;
        do
        {
            // don't do anything with this dark magic!
            if (tp == typeof(ContextBoundObject)) break;

            fi.AddRange(tp.GetDeclaredFields());
            tp = tp.BaseType();
        }
        while (tp != null);

        ExpressionPosition currentPosition = position;

        foreach (FieldInfo fieldInfo in fi)
        {
            if (!FastClonerSafeTypes.CanReturnSameObject(fieldInfo.FieldType))
            {
                MethodInfo methodInfo = fieldInfo.FieldType.IsValueType()
                    ? typeof(FastClonerGenerator).GetPrivateStaticMethod(nameof(FastClonerGenerator.CloneStructInternal))!
                        .MakeGenericMethod(fieldInfo.FieldType)
                    : typeof(FastClonerGenerator).GetPrivateStaticMethod(nameof(FastClonerGenerator.CloneClassInternal))!;

                MemberExpression get = Expression.Field(fromLocal, fieldInfo);

                // toLocal.Field = Clone...Internal(fromLocal.Field)
                Expression call = Expression.Call(methodInfo, get, state);
                if (!fieldInfo.FieldType.IsValueType())
                    call = Expression.Convert(call, fieldInfo.FieldType);

                // should handle specially
                // todo: think about optimization, but it rare case
                bool isReadonly = _readonlyFields.GetOrAdd(fieldInfo, f => f.IsInitOnly);
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

        Type funcType = typeof(Func<,,>).MakeGenericType(methodType, typeof(FastCloneState), methodType);

        List<ParameterExpression> blockParams = [];
        if (from != fromLocal) blockParams.Add(fromLocal);
        blockParams.Add(toLocal);

        return Expression.Lambda(funcType, Expression.Block(blockParams, expressionList), from, state).Compile();
    }
    
    private static object GenerateExpandoObjectProcessor(ExpressionPosition position)
    {
        ParameterExpression from = Expression.Parameter(typeof(object));
        ParameterExpression state = Expression.Parameter(typeof(FastCloneState));
        ParameterExpression result = Expression.Variable(typeof(ExpandoObject));
        
        BinaryExpression createNew = Expression.Assign(result, Expression.New(typeof(ExpandoObject)));
        
        ParameterExpression fromDict = Expression.Variable(typeof(IDictionary<string, object>));
        ParameterExpression resultDict = Expression.Variable(typeof(IDictionary<string, object>));
        
        BlockExpression block = Expression.Block(
            [result, fromDict, resultDict],
            createNew,
            Expression.Assign(fromDict, Expression.Convert(from, typeof(IDictionary<string, object>))),
            Expression.Assign(resultDict, Expression.Convert(result, typeof(IDictionary<string, object>))),
            Expression.Call(state, StaticMethodInfos.DeepCloneStateMethods.AddKnownRef, from, result),
            GenerateExpandoObjectCopyLoop(fromDict, resultDict, state, position),
            Expression.Convert(result, typeof(object))
        );

        return Expression.Lambda<Func<object, FastCloneState, object>>(block, from, state).Compile();
    }

    private static BlockExpression GenerateExpandoObjectCopyLoop(ParameterExpression fromDict, ParameterExpression resultDict, ParameterExpression state, ExpressionPosition position)
    {
        ParameterExpression enumerator = Expression.Variable(typeof(IEnumerator<KeyValuePair<string, object>>));
        ParameterExpression kvp = Expression.Variable(typeof(KeyValuePair<string, object>));
        ParameterExpression value = Expression.Variable(typeof(object));
        LabelTarget breakLabel = CreateLoopLabel(position);

        return Expression.Block(
            [enumerator, kvp, value],
            Expression.Assign(
                enumerator,
                Expression.Call(
                    fromDict,
                    typeof(IEnumerable<KeyValuePair<string, object>>).GetMethod("GetEnumerator")!
                )
            ),
            Expression.Loop(
                Expression.IfThenElse(
                    Expression.Call(enumerator, typeof(IEnumerator).GetMethod("MoveNext")!),
                    Expression.Block(
                        Expression.Assign(kvp, Expression.Property(enumerator, "Current")),
                        Expression.Assign(value, Expression.Property(kvp, "Value")),
                        Expression.Call(
                            resultDict,
                            typeof(IDictionary<string, object>).GetMethod("Add")!,
                            Expression.Property(kvp, "Key"),
                            Expression.Condition(
                                Expression.TypeIs(value, typeof(Delegate)),
                                value,
                                Expression.Call(
                                    typeof(FastClonerGenerator).GetMethod("CloneClassInternal", BindingFlags.NonPublic | BindingFlags.Static)!,
                                    value,
                                    state
                                )
                            )
                        )
                    ),
                    Expression.Break(breakLabel)
                ),
                breakLabel
            )
        );
    }

    private static object GenerateProcessDictionaryMethod(Type type, ExpressionPosition position)
    {
        Type[] genericArguments = type.GenericArguments();
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
            Type[] kvpArguments = genericArg.GetGenericArguments();
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
        ParameterExpression from = Expression.Parameter(typeof(object));
        ParameterExpression state = Expression.Parameter(typeof(FastCloneState));
        ParameterExpression local = Expression.Variable(dictType);

        // Initialize dictionary
        BinaryExpression assign = Expression.Assign(local, Expression.New(dictType.GetConstructor(Type.EmptyTypes)!));

        // Get Add/TryAdd method
        MethodInfo? addMethod = (dictType.IsGenericType ? dictType.GetMethod("Add", [keyType, valueType]) : typeof(IDictionary).GetMethod("Add")) ?? dictType.GetMethods().FirstOrDefault(m => m.Name == "TryAdd" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == keyType && m.GetParameters()[1].ParameterType == valueType);

        // Setup enumerator
        Type enumeratorType = isGeneric
            ? typeof(IEnumerator<>).MakeGenericType(typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType))
            : typeof(IDictionaryEnumerator);

        ParameterExpression enumerator = Expression.Variable(enumeratorType);

        // Get clone methods
        MethodInfo keyCloneMethod = keyType.IsValueType()
            ? typeof(FastClonerGenerator).GetPrivateStaticMethod(nameof(FastClonerGenerator.CloneStructInternal))!.MakeGenericMethod(keyType)
            : typeof(FastClonerGenerator).GetPrivateStaticMethod(nameof(FastClonerGenerator.CloneClassInternal))!;

        MethodInfo valueCloneMethod = valueType.IsValueType()
            ? typeof(FastClonerGenerator).GetPrivateStaticMethod(nameof(FastClonerGenerator.CloneStructInternal))!.MakeGenericMethod(valueType)
            : typeof(FastClonerGenerator).GetPrivateStaticMethod(nameof(FastClonerGenerator.CloneClassInternal))!;

        // Generate iteration logic
        BlockExpression iterationBlock = isGeneric
            ? GenerateGenericDictionaryIteration(enumerator, keyType, valueType, keyCloneMethod, valueCloneMethod, local, addMethod, state, position)
            : GenerateNonGenericDictionaryIteration(enumerator, keyCloneMethod, valueCloneMethod, local, addMethod, state, position);

        // Combine into final expression
        BlockExpression block = Expression.Block(
            [local, enumerator],
            assign,
            Expression.Assign(enumerator, GetEnumeratorExpression(from, dictType, isGeneric, enumeratorType)),
            iterationBlock,
            local);

        Type funcType = typeof(Func<object, FastCloneState, object>);
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
        PropertyInfo current = enumerator.Type.GetProperty(nameof(IEnumerator<object>.Current))!;
        LabelTarget breakLabel = CreateLoopLabel(position);
        Type dictionaryType = local.Type;
        bool isSingleGenericParameter = dictionaryType.GetGenericArguments().Length is 1;

        if (isSingleGenericParameter)
        {
            Type singleGenericType = dictionaryType.GetGenericArguments()[0];
            if (singleGenericType.IsGenericType && singleGenericType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                Type[] kvpTypes = singleGenericType.GetGenericArguments();
                ParameterExpression kvp = Expression.Variable(singleGenericType);
                BinaryExpression assignKvp = Expression.Assign(kvp, Expression.Property(enumerator, current));
                NewExpression newKvp = Expression.New(
                    singleGenericType.GetConstructor([kvpTypes[0], kvpTypes[1]])!,
                    Expression.Convert(
                        Expression.Call(keyCloneMethod, Expression.Property(kvp, "Key"), state),
                        kvpTypes[0]),
                    Expression.Convert(
                        Expression.Call(valueCloneMethod, Expression.Property(kvp, "Value"), state),
                        kvpTypes[1]));
                MethodInfo collectionAddMethod = dictionaryType.GetMethod("Add", [singleGenericType])!;
                MethodCallExpression addKvp = Expression.Call(local, collectionAddMethod, newKvp);

                LoopExpression loop = Expression.Loop(
                    Expression.IfThenElse(
                        Expression.Call(enumerator, typeof(IEnumerator).GetMethod(nameof(IEnumerator.MoveNext))!),
                        Expression.Block([kvp], assignKvp, addKvp),
                        Expression.Break(breakLabel)),
                    breakLabel);

                return Expression.Block(loop);
            }
        }
        {
            Type kvpType = typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType);
            ParameterExpression kvp = Expression.Variable(kvpType);
            ParameterExpression key = Expression.Variable(keyType);
            ParameterExpression value = Expression.Variable(valueType);

            BinaryExpression assignKvp = Expression.Assign(kvp, Expression.Property(enumerator, current));
            BinaryExpression assignKey = Expression.Assign(
                key,
                Expression.Convert(
                    Expression.Call(keyCloneMethod, Expression.Property(kvp, "Key"), state),
                    keyType));
            BinaryExpression assignValue = Expression.Assign(
                value,
                Expression.Convert(
                    Expression.Call(valueCloneMethod, Expression.Property(kvp, "Value"), state),
                    valueType));

            MethodCallExpression addKvp = Expression.Call(local, addMethod, key, value);

            LoopExpression loop = Expression.Loop(
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
        MemberExpression current = Expression.Property(enumerator, nameof(IDictionaryEnumerator.Entry));
        ParameterExpression key = Expression.Variable(typeof(object));
        ParameterExpression value = Expression.Variable(typeof(object));

        BinaryExpression assignKey = Expression.Assign(
            key,
            Expression.Call(
                keyCloneMethod,
                Expression.Property(current, "Key"),
                state));

        BinaryExpression assignValue = Expression.Assign(
            value,
            Expression.Call(
                valueCloneMethod,
                Expression.Property(current, "Value"),
                state));

        MethodCallExpression addEntry = Expression.Call(local, addMethod, key, value);
        LabelTarget breakLabel = CreateLoopLabel(position);

        LoopExpression loop = Expression.Loop(
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
        Type elementType = type.GenericArguments()[0];

        MethodInfo cloneElementMethod = elementType.IsValueType()
            ? typeof(FastClonerGenerator).GetPrivateStaticMethod(nameof(FastClonerGenerator.CloneStructInternal))!.MakeGenericMethod(elementType)
            : typeof(FastClonerGenerator).GetPrivateStaticMethod(nameof(FastClonerGenerator.CloneClassInternal))!;

        ParameterExpression from = Expression.Parameter(typeof(object));
        ParameterExpression state = Expression.Parameter(typeof(FastCloneState));

        ParameterExpression local = Expression.Variable(type);
        BinaryExpression assign = Expression.Assign(local, Expression.New(type.GetConstructor(Type.EmptyTypes)!));

        MethodInfo? addMethod = type.GetMethod("Add", [elementType]) ?? typeof(ISet<>).GetMethod("Add") ?? type.GetMethod("Add") ?? type.GetMethod("TryAdd");

        BlockExpression foreachBlock = GenerateForeachBlock(from, elementType, null, cloneElementMethod, null, local, addMethod, state, position);
        Type funcType = typeof(Func<object, FastCloneState, object>);

        return Expression.Lambda(funcType, Expression.Block([local], assign, foreachBlock, local), from, state).Compile();
    }

    private static object GenerateProcessArrayMethod(Type type)
    {
        Type? elementType = type.GetElementType();
        int rank = type.GetArrayRank();

        MethodInfo methodInfo;

        // multidim or not zero-based arrays
        if (rank != 1 || type != elementType.MakeArrayType())
        {
            if (rank == 2 && type == elementType.MakeArrayType(2))
            {
                // small optimization for 2 dim arrays
                methodInfo = typeof(FastClonerGenerator).GetPrivateStaticMethod(nameof(FastClonerGenerator.Clone2DimArrayInternal))!.MakeGenericMethod(elementType);
            }
            else
            {
                methodInfo = typeof(FastClonerGenerator).GetPrivateStaticMethod(nameof(FastClonerGenerator.CloneAbstractArrayInternal))!;
            }
        }
        else
        {
            string methodName = nameof(FastClonerGenerator.Clone1DimArrayClassInternal);
            if (FastClonerSafeTypes.CanReturnSameObject(elementType)) methodName = nameof(FastClonerGenerator.Clone1DimArraySafeInternal);
            else if (elementType.IsValueType()) methodName = nameof(FastClonerGenerator.Clone1DimArrayStructInternal);
            methodInfo = typeof(FastClonerGenerator).GetPrivateStaticMethod(methodName)!.MakeGenericMethod(elementType);
        }

        ParameterExpression from = Expression.Parameter(typeof(object));
        ParameterExpression state = Expression.Parameter(typeof(FastCloneState));
        MethodCallExpression call = Expression.Call(methodInfo, Expression.Convert(from, type), state);

        Type funcType = typeof(Func<,,>).MakeGenericType(typeof(object), typeof(FastCloneState), typeof(object));

        return Expression.Lambda(funcType, call, from, state).Compile();
    }

    private static BlockExpression GenerateForeachBlock(ParameterExpression from, Type keyType, Type? valueType, MethodInfo cloneKeyMethod, MethodInfo? cloneValueMethod, ParameterExpression local, MethodInfo addMethod, ParameterExpression state, ExpressionPosition position)
    {
        Type enumeratorType = typeof(IEnumerator<>).MakeGenericType(valueType == null ? keyType : typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType));

        ParameterExpression enumerator = Expression.Variable(enumeratorType);
        MethodInfo moveNext = typeof(IEnumerator).GetMethod(nameof(IEnumerator.MoveNext))!;
        PropertyInfo current = enumeratorType.GetProperty(nameof(IEnumerator.Current))!;

        MethodInfo getEnumerator = typeof(IEnumerable).GetMethod(nameof(IEnumerable.GetEnumerator))!;

        LabelTarget breakLabel = CreateLoopLabel(position);

        LoopExpression loop = Expression.Loop(
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
        ParameterExpression element = Expression.Variable(elementType);
        BinaryExpression assignElement = Expression.Assign(
            element,
            Expression.Convert(
                Expression.Call(cloneElementMethod, Expression.Property(enumerator, current), state),
                elementType
            )
        );
        MethodCallExpression addElement = Expression.Call(local, addMethod, element);

        return Expression.Block([element], assignElement, addElement);
    }


    private static BlockExpression GenerateDictionaryAddBlock(ParameterExpression enumerator, PropertyInfo current, Type keyType, Type valueType, MethodInfo cloneKeyMethod, MethodInfo cloneValueMethod, ParameterExpression local, MethodInfo addMethod, ParameterExpression state)
    {
        ParameterExpression kvp = Expression.Variable(typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType));
        BinaryExpression assignKvp = Expression.Assign(kvp, Expression.Property(enumerator, current));

        ParameterExpression key = Expression.Variable(keyType);
        ParameterExpression value = Expression.Variable(valueType);

        BinaryExpression assignKey = Expression.Assign(
            key,
            Expression.Convert(
                Expression.Call(cloneKeyMethod, Expression.Property(kvp, "Key"), state),
                keyType));

        BinaryExpression assignValue = Expression.Assign(
            value,
            Expression.Convert(
                Expression.Call(cloneValueMethod, Expression.Property(kvp, "Value"), state),
                valueType));

        MethodCallExpression addKvp = Expression.Call(local, addMethod, key, value);

        return Expression.Block([kvp, key, value], assignKvp, assignKey, assignValue, addKvp);
    }

    private static object GenerateProcessTupleMethod(Type type)
    {
        ParameterExpression from = Expression.Parameter(typeof(object));
        ParameterExpression state = Expression.Parameter(typeof(FastCloneState));

        ParameterExpression local = Expression.Variable(type);
        BinaryExpression assign = Expression.Assign(local, Expression.Convert(from, type));

        Type funcType = typeof(Func<object, FastCloneState, object>);

        int tupleLength = type.GenericArguments().Length;

        BinaryExpression constructor = Expression.Assign(
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