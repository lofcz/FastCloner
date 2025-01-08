using System.Collections.Concurrent;

namespace FastCloner.Code;

internal static class FastClonerCache
{
    private static readonly ClrCache<object?> classCache = new ClrCache<object?>();
    private static readonly ClrCache<object> structCache = new ClrCache<object>();
    private static readonly ClrCache<object> deepClassToCache = new ClrCache<object>();
    private static readonly ClrCache<object> shallowClassToCache = new ClrCache<object>();
    private static readonly ConcurrentLazyCache<object> typeConvertCache = new ConcurrentLazyCache<object>();

    public static object? GetOrAddClass(Type type, Func<Type, object?> valueFactory) => classCache.GetOrAdd(type, valueFactory);
    public static object GetOrAddStructAsObject(Type type, Func<Type, object> valueFactory) => structCache.GetOrAdd(type, valueFactory);
    public static object GetOrAddDeepClassTo(Type type, Func<Type, object> valueFactory) => deepClassToCache.GetOrAdd(type, valueFactory);
    public static object GetOrAddShallowClassTo(Type type, Func<Type, object> valueFactory) => shallowClassToCache.GetOrAdd(type, valueFactory);
    public static T GetOrAddConvertor<T>(Type from, Type to, Func<Type, Type, T> valueFactory) => (T)typeConvertCache.GetOrAdd(from, to, (f, t) => valueFactory(f, t));

    public static void ClearCache()
    {
        classCache.Clear();
        structCache.Clear();
        deepClassToCache.Clear();
        shallowClassToCache.Clear();
        typeConvertCache.Clear();
    }
    
    private class ClrCache<TValue>
    {
        private readonly ConcurrentDictionary<Type, Lazy<TValue>> cache = new ConcurrentDictionary<Type, Lazy<TValue>>();

        public TValue GetOrAdd(Type type, Func<Type, TValue> valueFactory)
        {
            Lazy<TValue> lazy = cache.GetOrAdd(type, t => new Lazy<TValue>(() => valueFactory(t), LazyThreadSafetyMode.ExecutionAndPublication));
            return lazy.Value;
        }

        public void Clear() => cache.Clear();
    }

    private class ConcurrentLazyCache<TValue>
    {
        private readonly ConcurrentDictionary<Tuple<Type, Type>, Lazy<TValue>> cache = [];

        public TValue GetOrAdd(Type from, Type to, Func<Type, Type, TValue> valueFactory)
        {
            Tuple<Type, Type> key = new Tuple<Type, Type>(from, to);
            Lazy<TValue> lazy = cache.GetOrAdd(key, tuple => new Lazy<TValue>(() => valueFactory(tuple.Item1, tuple.Item2), LazyThreadSafetyMode.ExecutionAndPublication));
            return lazy.Value;
        }

        public void Clear() => cache.Clear();
    }
}
