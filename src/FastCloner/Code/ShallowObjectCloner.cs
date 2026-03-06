using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FastCloner.Code;

/// <summary>
/// Internal helper class used to perform shallow object cloning
/// </summary>
internal static class ShallowObjectCloner
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object CloneObject(object obj)
        => DirectCloneObject(obj);

#if MODERN
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "MemberwiseClone")]
    public static extern object DirectCloneObject(object obj);
#else
    public static object DirectCloneObject(object obj)
    {
        return cloneFunc(obj);
    }
    private static readonly Func<object, object> cloneFunc = CreateCloneFunc();

    private static Func<object, object> CreateCloneFunc()
    {
        MethodInfo methodInfo = typeof(object).GetPrivateMethod(nameof(MemberwiseClone))!;
        ParameterExpression p = Expression.Parameter(typeof(object));
        MethodCallExpression mce = Expression.Call(p, methodInfo);
        return Expression.Lambda<Func<object, object>>(mce, p).Compile();
    }
#endif
}