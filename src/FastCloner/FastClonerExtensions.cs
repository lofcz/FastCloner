using FastCloner.Code;

namespace FastCloner;

/// <summary>
/// Extensions for object cloning
/// </summary>
internal static class FastClonerExtensions
{
    extension<T>(T obj)
    {
        /// <summary>
        /// Performs deep (full) copy of object and related graph
        /// </summary>
        public T DeepClone() => FastClonerGenerator.CloneObject(obj);

        /// <summary>
        /// Performs deep (full) copy of object and related graph to existing object
        /// </summary>
        /// <returns>existing filled object</returns>
        /// <remarks>Method is valid only for classes, classes should be descendants in reality, not in declaration</remarks>
        public TTo DeepCloneTo<TTo>(TTo objTo) where TTo : class, T => (TTo)FastClonerGenerator.CloneObjectTo(obj, objTo, true);

        /// <summary>
        /// Performs shallow copy of object to existing object
        /// </summary>
        /// <returns>existing filled object</returns>
        /// <remarks>Method is valid only for classes, classes should be descendants in reality, not in declaration</remarks>
        public TTo ShallowCloneTo<TTo>(TTo objTo) where TTo : class, T => (TTo)FastClonerGenerator.CloneObjectTo(obj, objTo, false);

        /// <summary>
        /// Performs shallow (only new object returned, without cloning of dependencies) copy of object
        /// </summary>
        public T ShallowClone() => ShallowClonerGenerator.CloneObject(obj);
    }
}