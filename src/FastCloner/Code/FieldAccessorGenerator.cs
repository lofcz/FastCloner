using System.Linq.Expressions;
using System.Reflection;

namespace FastCloner.Code;

internal static class FieldAccessorGenerator
{
    internal static Action<object, object> GetFieldSetter(FieldInfo field)
    {
        return (Action<object, object>)FastClonerCache.GetOrAddField(field.DeclaringType!, field.Name, _ => CreateFieldSetter(field))!;
    }

    private static Action<object, object> CreateFieldSetter(FieldInfo field)
    {
        ParameterExpression targetParam = Expression.Parameter(typeof(object), "target");
        ParameterExpression valueParam = Expression.Parameter(typeof(object), "value");

        Type declaringType = field.DeclaringType!;

        Expression body;

        if (field.IsInitOnly)
        {
            MethodInfo setValueMethod = typeof(FieldInfo).GetMethod(nameof(FieldInfo.SetValue), [typeof(object), typeof(object)])!;
            UnaryExpression valueCast = Expression.Convert(valueParam, typeof(object));
            body = Expression.Call(Expression.Constant(field), setValueMethod, targetParam, valueCast);
        }
        else
        {
            Expression target = declaringType.IsValueType
                ? Expression.Unbox(targetParam, declaringType)
                : Expression.Convert(targetParam, declaringType);

            UnaryExpression valueCast = Expression.Convert(valueParam, field.FieldType);
            body = Expression.Assign(Expression.Field(target, field), valueCast);
        }

        Expression<Action<object, object>> lambda = Expression.Lambda<Action<object, object>>(
            body,
            targetParam,
            valueParam
        );

        return lambda.Compile();
    }
}
