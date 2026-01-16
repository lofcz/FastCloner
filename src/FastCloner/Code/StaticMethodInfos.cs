using System.Reflection;

namespace FastCloner.Code;

internal static class StaticMethodInfos
{
    internal static class DeepCloneStateMethods
    {
        internal static readonly MethodInfo AddKnownRef = typeof(FastCloneState).GetMethod(nameof(FastCloneState.AddKnownRef))!;
    }

    internal static class DeepClonerGeneratorMethods
    {
        internal static readonly MethodInfo CloneStructInternal =
            typeof(FastClonerGenerator).GetMethod(nameof(FastClonerGenerator.CloneStructInternal),
                                                  BindingFlags.NonPublic | BindingFlags.Static)!;
        internal static readonly MethodInfo CloneClassInternal =
            typeof(FastClonerGenerator).GetMethod(nameof(FastClonerGenerator.CloneClassInternal),
                                                  BindingFlags.NonPublic | BindingFlags.Static)!;

        internal static MethodInfo MakeFieldCloneMethodInfo(Type fieldType) =>
            fieldType.IsValueType
                ? CloneStructInternal.MakeGenericMethod(fieldType)
                : CloneClassInternal;

        internal static readonly MethodInfo GetClonerForValueType =
            typeof(FastClonerGenerator).GetMethod(nameof(FastClonerGenerator.GetClonerForValueType),
                                                  BindingFlags.NonPublic | BindingFlags.Static)!;
    }
}