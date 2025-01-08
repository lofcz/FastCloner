using System.Collections.Concurrent;

namespace FastCloner.Code;

internal static class FastClonerCache
{
    private static readonly ConcurrentDictionary<Type, Lazy<object>> classCache = new ConcurrentDictionary<Type, Lazy<object>>();
    private static readonly ConcurrentDictionary<Type, Lazy<object>> structCache = new ConcurrentDictionary<Type, Lazy<object>>();
    private static readonly ConcurrentDictionary<Type, Lazy<object>> deepClassToCache = new ConcurrentDictionary<Type, Lazy<object>>();
    private static readonly ConcurrentDictionary<Type, Lazy<object>> shallowClassToCache = new ConcurrentDictionary<Type, Lazy<object>>();
    private static readonly ConcurrentDictionary<Tuple<Type, Type>, Lazy<object>> typeConvertCache = new ConcurrentDictionary<Tuple<Type, Type>, Lazy<object>>();
    
    public static object GetOrAddClass(Type type, Func<Type, object> valueFactory)
    {
        Lazy<object> lazy = classCache.GetOrAdd(type, t => new Lazy<object>(() => valueFactory(t), LazyThreadSafetyMode.ExecutionAndPublication));
        return lazy.Value;
    }

    public static object GetOrAddStructAsObject(Type type, Func<Type, object> valueFactory)
    {
        Lazy<object> lazy = structCache.GetOrAdd(type, t => new Lazy<object>(() => valueFactory(t), LazyThreadSafetyMode.ExecutionAndPublication));
        return lazy.Value;
    }

    public static object GetOrAddDeepClassTo(Type type, Func<Type, object> valueFactory)
    {
        Lazy<object> lazy = deepClassToCache.GetOrAdd(type, t => new Lazy<object>(() => valueFactory(t), LazyThreadSafetyMode.ExecutionAndPublication));
        return lazy.Value;
    }

    public static object GetOrAddShallowClassTo(Type type, Func<Type, object> valueFactory)
    {
        Lazy<object> lazy = shallowClassToCache.GetOrAdd(type, t => new Lazy<object>(() => valueFactory(t), LazyThreadSafetyMode.ExecutionAndPublication));
        return lazy.Value;
    }

    public static T GetOrAddConvertor<T>(Type from, Type to, Func<Type, Type, T> adder)
    {
        Tuple<Type, Type> key = new Tuple<Type, Type>(from, to);
        Lazy<object> lazy = typeConvertCache.GetOrAdd(key, tuple => new Lazy<object>(() => adder(tuple.Item1, tuple.Item2), LazyThreadSafetyMode.ExecutionAndPublication));
        return (T)lazy.Value;
    }

    /// <summary>
    /// This method can be used when we switch between safe / unsafe variants (for testing)
    /// </summary>
    public static void ClearCache()
    {
        classCache.Clear();
        structCache.Clear();
        deepClassToCache.Clear();
        shallowClassToCache.Clear();
        typeConvertCache.Clear();
    }
}