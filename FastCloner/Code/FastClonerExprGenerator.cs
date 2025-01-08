using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FastCloner.Code;

internal static class FastClonerExprGenerator
{
    internal static readonly ConcurrentDictionary<Type, Func<Type, bool, ExpressionPosition, object>> CustomTypeHandlers = [];
    
    private static readonly ConcurrentDictionary<FieldInfo, bool> _readonlyFields = new ConcurrentDictionary<FieldInfo, bool>();

    private static readonly MethodInfo _fieldSetMethod;
    static FastClonerExprGenerator() => _fieldSetMethod = typeof(FieldInfo).GetMethod(nameof(FieldInfo.SetValue), [typeof(object), typeof(object)])!;

    internal static object GenerateClonerInternal(Type realType, bool asObject) => GenerateProcessMethod(realType, asObject && realType.IsValueType());

    private static bool MemberIsIgnored(MemberInfo memberInfo)
    {
        DeepCloneIgnoreAttribute? attribute = memberInfo.GetCustomAttribute<DeepCloneIgnoreAttribute>();
        return attribute?.Ignored ?? false;
    }
    
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

    internal readonly record struct ExpressionPosition(int Depth, int Index)
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

    private delegate object ProcessMethodDelegate(Type type, bool unboxStruct, ExpressionPosition position);

    private static readonly FrozenDictionary<Type, ProcessMethodDelegate> KnownTypeProcessors = 
        new Dictionary<Type, ProcessMethodDelegate>
        {
            [typeof(ExpandoObject)] = (_, _, position) => GenerateExpandoObjectProcessor(position),
            [typeof(HttpRequestOptions)] = (_, _, position) => GenerateHttpRequestOptionsProcessor(position),
            [typeof(Array)] = (type, _, _) => GenerateProcessArrayMethod(type),
        }.ToFrozenDictionary();
    
    private static readonly AhoCorasick BadTypes = new AhoCorasick([
        "Castle.Proxies.",
        "System.Data.Entity.DynamicProxies.",
        "NHibernate.Proxy."
    ]);
    
    private static bool IsCloneable(Type type)
    {
        if (type.FullName is null)
        {
            return false;
        }
        
        return !BadTypes.ContainsAnyPattern(type.FullName);
    }
    
    private static List<MemberInfo> GetAllMembers(Type type)
    {
        List<MemberInfo> members = [];
        Type? currentType = type;
        
        while (currentType != null && currentType != typeof(ContextBoundObject))
        {
            members.AddRange(currentType.GetDeclaredFields());
            members.AddRange(currentType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(p => p is { CanRead: true, CanWrite: true } && p.GetIndexParameters().Length == 0)); // Exclude indexers
            currentType = currentType.BaseType();
        }
        
        return members;
    }
    
    private static object? GenerateProcessMethod(Type type, bool unboxStruct, ExpressionPosition position)
    {
        if (!IsCloneable(type))
            return null;
        
        if (KnownTypeProcessors.TryGetValue(type, out ProcessMethodDelegate? handler))
        {
            return handler.Invoke(type, unboxStruct, position);
        }

        if (CustomTypeHandlers.TryGetValue(type, out Func<Type, bool, ExpressionPosition, object>? contribHandler))
        {
            return contribHandler.Invoke(type, unboxStruct, position);
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

        ExpressionPosition currentPosition = position;
        IEnumerable<MemberInfo> members = GetAllMembers(type);
        
        foreach (MemberInfo member in members)
        {
            Type memberType = member switch
            {
                FieldInfo fi => fi.FieldType,
                PropertyInfo pi => pi.PropertyType,
                _ => throw new ArgumentException($"Unsupported member type: {member.GetType()}")
            };

            if (member is PropertyInfo piLocal)
            {
                DeepCloneIgnoreAttribute? attribute = piLocal.GetCustomAttribute<DeepCloneIgnoreAttribute>();
                
                if (attribute?.Ignored ?? false)
                {
                    expressionList.Add(Expression.Assign(
                        Expression.Property(toLocal, piLocal),
                        Expression.Default(piLocal.PropertyType)
                    ));
                }

                continue;
            }

            if (!FastClonerSafeTypes.CanReturnSameObject(memberType))
            {
                if (MemberIsIgnored(member))
                {
                    expressionList.Add(Expression.Assign(
                        Expression.MakeMemberAccess(toLocal, member),
                        Expression.Default(memberType)
                    ));
                    continue;
                }

                MethodInfo methodInfo = memberType.IsValueType()
                    ? typeof(FastClonerGenerator).GetPrivateStaticMethod(nameof(FastClonerGenerator.CloneStructInternal))!
                        .MakeGenericMethod(memberType)
                    : typeof(FastClonerGenerator).GetPrivateStaticMethod(nameof(FastClonerGenerator.CloneClassInternal))!;

                MemberExpression get = Expression.MakeMemberAccess(fromLocal, member);
                Expression call = Expression.Call(methodInfo, get, state);
            
                if (!memberType.IsValueType())
                    call = Expression.Convert(call, memberType);

                if (member is FieldInfo fieldInfo && _readonlyFields.GetOrAdd(fieldInfo, f => f.IsInitOnly))
                {
                    expressionList.Add(Expression.Call(
                        Expression.Constant(fieldInfo),
                        _fieldSetMethod,
                        Expression.Convert(toLocal, typeof(object)),
                        Expression.Convert(call, typeof(object))));
                }
                else
                {
                    expressionList.Add(Expression.Assign(Expression.MakeMemberAccess(toLocal, member), call));
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
    
    private static object GenerateHttpRequestOptionsProcessor(ExpressionPosition position)
    {
        ParameterExpression from = Expression.Parameter(typeof(object));
        ParameterExpression state = Expression.Parameter(typeof(FastCloneState));
        ParameterExpression result = Expression.Variable(typeof(HttpRequestOptions));
        ParameterExpression tempMessage = Expression.Variable(typeof(HttpRequestMessage));
        ParameterExpression fromOptions = Expression.Variable(typeof(HttpRequestOptions));
        
        ConstructorInfo constructor = typeof(HttpRequestMessage).GetConstructor(Type.EmptyTypes)!;

        BlockExpression block = Expression.Block(
            [result, tempMessage, fromOptions],
            Expression.Assign(fromOptions, Expression.Convert(from, typeof(HttpRequestOptions))),
            Expression.Assign(tempMessage, Expression.New(constructor)),
            Expression.Assign(result, Expression.Property(tempMessage, "Options")),
            Expression.Call(state, StaticMethodInfos.DeepCloneStateMethods.AddKnownRef, from, result),
            Expression.Assign(result, Expression.Convert(
                Expression.Call(fromOptions, typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance)!),
                typeof(HttpRequestOptions)
            )),
            Expression.Call(tempMessage, typeof(IDisposable).GetMethod("Dispose")!),
            result
        );

        return Expression.Lambda<Func<object, FastCloneState, object>>(block, from, state).Compile();
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
        
        LabelTarget returnNullLabel = Expression.Label(typeof(object));
        ConditionalExpression nullCheck = Expression.IfThen(
            Expression.Equal(from, Expression.Constant(null)),
            Expression.Return(returnNullLabel, Expression.Constant(null))
        );
        
        // For read-only collections
        bool isReadOnly = dictType.Name.Contains("ReadOnly", StringComparison.InvariantCultureIgnoreCase) || 
                          (dictType.IsGenericType && dictType.GetGenericTypeDefinition() == typeof(ReadOnlyDictionary<,>));

        Type innerDictType = isReadOnly
            ? typeof(Dictionary<,>).MakeGenericType(keyType, valueType)
            : dictType;
        
        // Check constructors
        ConstructorInfo? ctor = isReadOnly 
            ? dictType.GetConstructor([innerDictType])  // Get constructor that takes dictionary
            : dictType.GetConstructor(Type.EmptyTypes); // Get parameterless constructor

        // If we can't find appropriate constructor, bail out
        if (ctor is null)
        {
            return Expression.Lambda<Func<object, FastCloneState, object>>(
                from,
                from, 
                state
            ).Compile();
        }

        ParameterExpression result = Expression.Variable(dictType);
        ParameterExpression innerDict = isReadOnly 
            ? Expression.Variable(innerDictType)
            : result;

        // Create instance of inner dictionary
        BinaryExpression createInnerDict = Expression.Assign(
            innerDict,
            Expression.New(innerDictType.GetConstructor(Type.EmptyTypes)!)
        );

        // If ReadOnlyDictionary, use inner Dictionary
        Expression createResult = isReadOnly
            ? Expression.Assign(
                result,
                Expression.New(
                    dictType.GetConstructor([innerDictType])!,
                    innerDict
                )
            )
            : createInnerDict;
        
        // Add reference to state for cycle detection
        Expression addRef = Expression.Call(
            state,
            StaticMethodInfos.DeepCloneStateMethods.AddKnownRef,
            from,
            result
        );

        // Get Add/TryAdd method
        MethodInfo? addMethod = (innerDictType.IsGenericType
                                    ? innerDictType.GetMethod("Add", [keyType, valueType])
                                    : typeof(IDictionary).GetMethod("Add"))
                                ?? innerDictType.GetMethods()
                                    .FirstOrDefault(m => m.Name == "TryAdd" &&
                                                         m.GetParameters().Length == 2 &&
                                                         m.GetParameters()[0].ParameterType == keyType &&
                                                         m.GetParameters()[1].ParameterType == valueType);

        if (addMethod == null)
        {
            throw new InvalidOperationException($"Cannot find Add or TryAdd method for type {innerDictType.FullName}");
        }

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


        BlockExpression iterationBlock = isGeneric
            ? GenerateGenericDictionaryIteration(enumerator, keyType, valueType, keyCloneMethod, valueCloneMethod, innerDict, addMethod, state, position)
            : GenerateNonGenericDictionaryIteration(enumerator, keyCloneMethod, valueCloneMethod, innerDict, addMethod, state, position);

        Type enumerableType = isGeneric
            ? typeof(IEnumerable<>).MakeGenericType(typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType))
            : typeof(IDictionary);

        MethodInfo? getEnumeratorMethod = enumerableType.GetMethod("GetEnumerator");
        if (getEnumeratorMethod == null)
        {
            throw new InvalidOperationException($"Cannot find GetEnumerator method for type {enumerableType.FullName}");
        }
        
        Expression getEnumerator = Expression.Assign(
            enumerator,
            Expression.Convert(
                Expression.Call(
                    Expression.Convert(from, enumerableType),
                    getEnumeratorMethod
                ),
                enumeratorType
            )
        );
        
        // Combine into final expression
        BlockExpression block = Expression.Block(
            isReadOnly ? [result, innerDict, enumerator] : new[] { result, enumerator },
            nullCheck,
            createInnerDict,
            createResult,
            addRef,
            getEnumerator,
            iterationBlock,
            Expression.Label(returnNullLabel, Expression.Convert(result, typeof(object)))
        );
        
        return Expression.Lambda<Func<object, FastCloneState, object>>(
            block,
            from,
            state
        ).Compile();
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
        
        bool isReadOnly = type.Name.Contains("ReadOnly", StringComparison.InvariantCultureIgnoreCase);

        // Use HashSet as inner collection
        Type innerSetType = isReadOnly 
            ? typeof(HashSet<>).MakeGenericType(elementType)
            : type;

        ParameterExpression innerSet = isReadOnly 
            ? Expression.Variable(innerSetType)
            : local;

        // Initialize set
        BinaryExpression assign = Expression.Assign(
            innerSet, 
            Expression.New(innerSetType.GetConstructor(Type.EmptyTypes)!)
        );

        // Get Add method from inner set
        MethodInfo? addMethod = innerSetType.GetMethod("Add", [elementType]) ?? 
                               typeof(ISet<>).MakeGenericType(elementType).GetMethod("Add") ?? 
                               innerSetType.GetMethod("Add") ?? 
                               innerSetType.GetMethod("TryAdd");

        if (addMethod == null)
        {
            throw new InvalidOperationException($"Cannot find Add or TryAdd method for type {innerSetType.FullName}");
        }

        // Generate foreach block using inner set
        BlockExpression foreachBlock = GenerateForeachBlock(
            from, 
            elementType, 
            null, 
            cloneElementMethod, 
            null, 
            innerSet, 
            addMethod, 
            state, 
            position
        );

        // Create final ReadOnlySet if needed
        Expression finalAssign = isReadOnly
            ? Expression.Assign(
                local,
                Expression.New(
                    type.GetConstructor([innerSetType])!,
                    innerSet
                ))
            : Expression.Empty();

        Type funcType = typeof(Func<object, FastCloneState, object>);

        return Expression.Lambda(
            funcType, 
            Expression.Block(
                isReadOnly ? [local, innerSet] : new[] { local },
                assign,
                foreachBlock,
                finalAssign,
                local
            ), 
            from, 
            state
        ).Compile();
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