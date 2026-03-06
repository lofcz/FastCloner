using System.Collections.Concurrent;
using System.Collections;
using System.Reflection;

namespace FastCloner.Code;

internal static class StaticMethodInfos
{
    internal static class DeepCloneStateProperties
    {
        internal static readonly PropertyInfo UseWorkList = typeof(FastCloneState).GetProperty(nameof(FastCloneState.UseWorkList))!;
    }

    internal static class DeepCloneStateMethods
    {
        internal static readonly MethodInfo AddKnownRef = typeof(FastCloneState).GetMethod(nameof(FastCloneState.AddKnownRef))!;
    }

    internal static class DeepClonerGeneratorMethods
    {
        internal static readonly MethodInfo CloneStructInternal =
            typeof(FastClonerGenerator).GetMethod(nameof(FastClonerGenerator.CloneStructInternal),
                                                  BindingFlags.NonPublic | BindingFlags.Static)!;
        internal static readonly MethodInfo CloneClassInternalExact =
            typeof(FastClonerGenerator).GetMethod(nameof(FastClonerGenerator.CloneClassInternalExact),
                                                  BindingFlags.NonPublic | BindingFlags.Static)!;
        internal static readonly MethodInfo CloneClassInternal =
            typeof(FastClonerGenerator).GetMethod(nameof(FastClonerGenerator.CloneClassInternal),
                                                  BindingFlags.NonPublic | BindingFlags.Static)!;
        internal static readonly MethodInfo CloneClassInternalNoTracking =
            typeof(FastClonerGenerator).GetMethod(nameof(FastClonerGenerator.CloneClassInternalNoTracking),
                                                  BindingFlags.NonPublic | BindingFlags.Static)!;
        internal static readonly MethodInfo CloneClassShallowAndTrack =
            typeof(FastClonerGenerator).GetMethod(nameof(FastClonerGenerator.CloneClassShallowAndTrack),
                                                  BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly ConcurrentDictionary<IntPtr, MethodInfo> structCloneMethodCache = new ConcurrentDictionary<IntPtr, MethodInfo>();
        private static readonly ConcurrentDictionary<IntPtr, MethodInfo> exactClassCloneMethodCache = new ConcurrentDictionary<IntPtr, MethodInfo>();

        internal static MethodInfo MakeFieldCloneMethodInfo(Type fieldType) =>
            fieldType.IsValueType
                ? MakeStructCloneMethodInfo(fieldType)
                : CloneClassInternal;

        internal static MethodInfo MakeExactClassCloneMethodInfo(Type classType)
            => exactClassCloneMethodCache.GetOrAdd(classType.TypeHandle.Value, _ => CloneClassInternalExact.MakeGenericMethod(classType));

        internal static MethodInfo MakeStructCloneMethodInfo(Type valueType)
            => structCloneMethodCache.GetOrAdd(valueType.TypeHandle.Value, _ => CloneStructInternal.MakeGenericMethod(valueType));
    }

    internal static class CommonMethods
    {
        internal static readonly MethodInfo MemberwiseClone =
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance)!;
        internal static readonly MethodInfo Dispose =
            typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose))!;
        internal static readonly MethodInfo EnumeratorMoveNext =
            typeof(IEnumerator).GetMethod(nameof(IEnumerator.MoveNext))!;
        internal static readonly MethodInfo ObjectGetType =
            typeof(object).GetMethod(nameof(GetType))!;
    }
}