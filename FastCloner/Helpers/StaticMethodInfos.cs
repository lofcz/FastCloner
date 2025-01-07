using System.Reflection;

namespace FastCloner.Helpers;

internal static class StaticMethodInfos
{
    internal static class DeepCloneStateMethods
    {
        internal static MethodInfo AddKnownRef { get; } = typeof(DeepCloneState).GetMethod(nameof(DeepCloneState.AddKnownRef))!;
    }

    internal static class DeepClonerGeneratorMethods
    {
        internal static MethodInfo CloneStructInternal { get; } =
            typeof(DeepClonerGenerator).GetMethod(nameof(DeepClonerGenerator.CloneStructInternal),
                                                  BindingFlags.NonPublic | BindingFlags.Static)!;
        internal static MethodInfo CloneClassInternal { get; } =
            typeof(DeepClonerGenerator).GetMethod(nameof(DeepClonerGenerator.CloneClassInternal),
                                                  BindingFlags.NonPublic | BindingFlags.Static)!;

        internal static MethodInfo MakeFieldCloneMethodInfo(Type fieldType) =>
            fieldType.IsValueType
                ? CloneStructInternal.MakeGenericMethod(fieldType)
                : CloneClassInternal;

        internal static MethodInfo GetClonerForValueType { get; } =
            typeof(DeepClonerGenerator).GetMethod(nameof(DeepClonerGenerator.GetClonerForValueType),
                                                  BindingFlags.NonPublic | BindingFlags.Static)!;
    }
}