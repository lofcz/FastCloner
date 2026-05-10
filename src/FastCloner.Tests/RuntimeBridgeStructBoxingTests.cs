using System.Reflection;

namespace FastCloner.Tests;

public class RuntimeBridgeStructBoxingTests
{
    private struct StructWithPrivateField
    {
        // Used through reflection inside the bridge.
#pragma warning disable CS0649
        private int _privateField;
#pragma warning restore CS0649

        public int Read() => _privateField;
        public void Write(int value) => _privateField = value;
    }

    private struct StructWithPrivateInitOnlyField
    {
#pragma warning disable CS0649
        private readonly int _privateField;
#pragma warning restore CS0649

        public int Read() => _privateField;
    }

    private struct StructWithPrivateSetterProperty
    {
        public int Value { get; private set; }
        public void Set(int value) => Value = value;
    }

    private class ClassWithPrivateField
    {
#pragma warning disable CS0649
        private int _privateField;
#pragma warning restore CS0649

        public int Read() => _privateField;
        public void Write(int value) => _privateField = value;
    }

    [Test]
    public async Task RuntimeBridge_DeepCloneField_MutatesBoxedStruct()
    {
        StructWithPrivateField source = default;
        source.Write(42);

        // The source generator emits:
        //   object box = (object)result;
        //   __Bridge.DeepCloneField(source, box, fi);
        //   result = (T)box;
        // This test exercises the middle step: the bridge must mutate `box`.
        object boxedTarget = default(StructWithPrivateField);

        FieldInfo fi = typeof(StructWithPrivateField).GetField(
            "_privateField",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        FastClonerSourceGeneratorBridge.DeepCloneField(source, boxedTarget, fi);

        StructWithPrivateField unboxed = (StructWithPrivateField)boxedTarget;
        await Assert.That(unboxed.Read()).IsEqualTo(42);
    }

    [Test]
    public async Task RuntimeBridge_CopyField_MutatesBoxedStruct()
    {
        StructWithPrivateField source = default;
        source.Write(42);

        object boxedTarget = default(StructWithPrivateField);

        FieldInfo fi = typeof(StructWithPrivateField).GetField(
            "_privateField",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        FastClonerSourceGeneratorBridge.CopyField(source, boxedTarget, fi);

        StructWithPrivateField unboxed = (StructWithPrivateField)boxedTarget;
        await Assert.That(unboxed.Read()).IsEqualTo(42);
    }

    [Test]
    public async Task RuntimeBridge_DeepCloneField_MutatesBoxedStruct_InitOnlyField()
    {
        // Init-only fields are written through FieldInfo.SetValue, which also
        // needs to mutate the boxed payload (not a copy).
        StructWithPrivateInitOnlyField source = (StructWithPrivateInitOnlyField)
            System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(StructWithPrivateInitOnlyField));
        FieldInfo fi = typeof(StructWithPrivateInitOnlyField).GetField(
            "_privateField",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        fi.SetValueDirect(__makeref(source), 42);

        object boxedTarget = default(StructWithPrivateInitOnlyField);

        FastClonerSourceGeneratorBridge.DeepCloneField(source, boxedTarget, fi);

        StructWithPrivateInitOnlyField unboxed = (StructWithPrivateInitOnlyField)boxedTarget;
        await Assert.That(unboxed.Read()).IsEqualTo(42);
    }

    [Test]
    public async Task RuntimeBridge_DeepCloneProperty_MutatesBoxedStruct()
    {
        StructWithPrivateSetterProperty source = default;
        source.Set(42);

        object boxedTarget = default(StructWithPrivateSetterProperty);

        PropertyInfo pi = typeof(StructWithPrivateSetterProperty).GetProperty(
            nameof(StructWithPrivateSetterProperty.Value),
            BindingFlags.Public | BindingFlags.Instance)!;

        FastClonerSourceGeneratorBridge.DeepCloneProperty(source, boxedTarget, pi);

        StructWithPrivateSetterProperty unboxed = (StructWithPrivateSetterProperty)boxedTarget;
        await Assert.That(unboxed.Value).IsEqualTo(42);
    }

    [Test]
    public async Task RuntimeBridge_DeepCloneField_DoesNotMutateUnboxedStructLocal()
    {
        // Negative pin: passing a struct local directly (not a pre-existing box)
        // boxes it at the call site; the bridge mutates that fresh box, and the
        // local is unaffected. The emitter must therefore wrap the call in
        // box/mutate/unbox -- this test fails if someone simplifies the emitter
        // back to a direct call.
        StructWithPrivateField source = default;
        source.Write(42);

        StructWithPrivateField result = default;

        FieldInfo fi = typeof(StructWithPrivateField).GetField(
            "_privateField",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        FastClonerSourceGeneratorBridge.DeepCloneField(source, result, fi);

        await Assert.That(result.Read()).IsEqualTo(0);
    }

    [Test]
    public async Task RuntimeBridge_DeepCloneField_MutatesClassReceiver()
    {
        // Sanity: with a reference-type receiver the bridge has always worked,
        // because the `object` parameter holds the same reference as the local.
        ClassWithPrivateField source = new();
        source.Write(42);

        ClassWithPrivateField result = new();

        FieldInfo fi = typeof(ClassWithPrivateField).GetField(
            "_privateField",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        FastClonerSourceGeneratorBridge.DeepCloneField(source, result, fi);

        await Assert.That(result.Read()).IsEqualTo(42);
    }
}
