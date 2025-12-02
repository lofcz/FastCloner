using System.Reflection;

namespace FastCloner.Code;

internal static class StaticMethodInfos
{
    internal static class DeepCloneStateMethods
    {
        internal static MethodInfo AddKnownRef { get; } = typeof(FastCloneState).GetMethod(nameof(FastCloneState.AddKnownRef))!;
    }

    internal static class DeepClonerGeneratorMethods
    {
        internal static MethodInfo CloneStructInternal { get; } =
            typeof(FastClonerGenerator).GetMethod(nameof(FastClonerGenerator.CloneStructInternal),
                                                  BindingFlags.NonPublic | BindingFlags.Static)!;
        internal static MethodInfo CloneClassInternal { get; } =
            typeof(FastClonerGenerator).GetMethod(nameof(FastClonerGenerator.CloneClassInternal),
                                                  BindingFlags.NonPublic | BindingFlags.Static)!;

        internal static MethodInfo MakeFieldCloneMethodInfo(Type fieldType) =>
            fieldType.IsValueType
                ? CloneStructInternal.MakeGenericMethod(fieldType)
                : CloneClassInternal;

        internal static MethodInfo GetClonerForValueType { get; } =
            typeof(FastClonerGenerator).GetMethod(nameof(FastClonerGenerator.GetClonerForValueType),
                                                  BindingFlags.NonPublic | BindingFlags.Static)!;
    }
}