using FastCloner.Code;

namespace FastCloner;

/// <summary>
/// Extensions for object cloning
/// </summary>
public static class FastCloner
{
    /// <summary>
    /// Cloning objects with nest level above this threshold uses iterative approach instead of recursion.
    /// </summary>
    public static int MaxRecursionDepth { get; set; } = 1_000;
    
    /// <summary>
    /// Performs deep (full) copy of object and related graph
    /// </summary>
    public static T? DeepClone<T>(T? obj) => FastClonerGenerator.CloneObject(obj);

    /// <summary>
    /// Performs deep (full) copy of object and related graph to existing object
    /// </summary>
    /// <returns>existing filled object</returns>
    /// <remarks>Method is valid only for classes, classes should be descendants in reality, not in declaration</remarks>
    public static TTo? DeepCloneTo<TFrom, TTo>(TFrom? objFrom, TTo? objTo) where TTo : class, TFrom => (TTo?)FastClonerGenerator.CloneObjectTo(objFrom, objTo, true);

    /// <summary>
    /// Performs shallow copy of object to existing object
    /// </summary>
    /// <returns>existing filled object</returns>
    /// <remarks>Method is valid only for classes, classes should be descendants in reality, not in declaration</remarks>
    public static TTo? ShallowCloneTo<TFrom, TTo>(TFrom? objFrom, TTo? objTo) where TTo : class, TFrom => (TTo?)FastClonerGenerator.CloneObjectTo(objFrom, objTo, false);

    /// <summary>
    /// Performs shallow (only new object returned, without cloning of dependencies) copy of object
    /// </summary>
    public static T? ShallowClone<T>(T? obj) => ShallowClonerGenerator.CloneObject(obj);

    /// <summary>
    /// Clears all cached information about classes, structs, types, and other CLR objects.
    /// </summary>
    public static void ClearCache() => FastClonerCache.ClearCache();

    /// <summary>
    /// Types given will always be ignored, regardless of <see cref="FastClonerIgnoreAttribute"/>.
    /// </summary>
    /// <param name="types">Types to ignore.</param>
    public static void IgnoreTypes(IEnumerable<Type> types)
    {
        // should someone modify this while we enumerate
        foreach (Type type in types.ToList())
        {
            FastClonerCache.AlwaysIgnoredTypes.TryAdd(type, true);
        }
    }

    /// <summary>
    /// Type given will always be ignored when cloning.
    /// </summary>
    /// <param name="type">Type to ignore</param>
    public static void IgnoreType(Type type)
    {
        FastClonerCache.AlwaysIgnoredTypes.TryAdd(type, true);
    }

    /// <summary>
    /// Returns currently always ignored types.
    /// </summary>
    public static HashSet<Type> GetIgnoredTypes()
    {
#if MODERN
        return FastClonerCache.AlwaysIgnoredTypes.Keys.ToHashSet();
#else
        return [..FastClonerCache.AlwaysIgnoredTypes.Keys];
#endif
    }

    /// <summary>
    /// Checks whether the given type is always ignored.
    /// </summary>
    public static bool IsTypeIgnored(Type type)
    {
        return FastClonerCache.AlwaysIgnoredTypes.TryGetValue(type, out _);
    }

    /// <summary>
    /// Removes all types from the always ignored types, this does not affect members annotated with <see cref="FastClonerIgnoreAttribute"/>.<br/>
    /// Note that this also clears the cache, and will have negative impact on cloning performance until the cache is repopulated.
    /// </summary>
    public static void ClearIgnoredTypes()
    {
        FastClonerCache.AlwaysIgnoredTypes.Clear();
        FastClonerCache.ClearCache(); 
    }
}