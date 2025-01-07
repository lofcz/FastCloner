using System.Linq.Expressions;
using System.Reflection;

namespace FastCloner.Code;

/// <summary>
/// Internal class but due implementation restriction should be public
/// </summary>
public abstract class ShallowObjectCloner
{
    /// <summary>
    /// Abstract method for real object cloning
    /// </summary>
    protected abstract object DoCloneObject(object obj);

    private static readonly ShallowObjectCloner _instance;

    /// <summary>
    /// Performs real shallow object clone
    /// </summary>
    public static object CloneObject(object obj) => _instance.DoCloneObject(obj);

    static ShallowObjectCloner() => _instance = new ShallowSafeObjectCloner();

    private class ShallowSafeObjectCloner : ShallowObjectCloner
    {
        private static readonly Func<object, object> _cloneFunc;

        static ShallowSafeObjectCloner()
        {
            MethodInfo? methodInfo = typeof(object).GetPrivateMethod(nameof(MemberwiseClone));
            ParameterExpression? p = Expression.Parameter(typeof(object));
            MethodCallExpression? mce = Expression.Call(p, methodInfo);
            _cloneFunc = Expression.Lambda<Func<object, object>>(mce, p).Compile();
        }

        protected override object DoCloneObject(object obj) => _cloneFunc(obj);
    }
}