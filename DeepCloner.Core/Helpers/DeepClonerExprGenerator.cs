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

    private static object GenerateProcessMethod(Type type, bool unboxStruct)
    {
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
            }
        }

        expressionList.Add(Expression.Convert(toLocal, methodType));

        var funcType = typeof(Func<,,>).MakeGenericType(methodType, typeof(DeepCloneState), methodType);

        var blockParams = new List<ParameterExpression>();
        if (from != fromLocal) blockParams.Add(fromLocal);
        blockParams.Add(toLocal);

        return Expression.Lambda(funcType, Expression.Block(blockParams, expressionList), from, state).Compile();
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