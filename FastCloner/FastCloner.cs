using FastCloner.Code;

namespace FastCloner;

/// <summary>
/// Extensions for object cloning
/// </summary>
public static class FastCloner
{
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
}