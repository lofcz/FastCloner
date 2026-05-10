using System.Text;
using FastCloner.SourceGenerator;

namespace FastCloner.Tests;

public class NonPublicAccessorEmitterTests
{
    private const string ProxyFqn = "FastCloner.SourceGenerated.__FastClonerSGBridgeProxy";
    private const string GlobalProxyFqn = "global::" + ProxyFqn;

    private static BridgeContract MakeBridgeContract() => new BridgeContract(
        BridgeTypeMetadataName: "FastCloner.FastClonerSourceGeneratorBridge",
        ProxyTypeFullName: ProxyFqn,
        Methods: new EquatableArray<BridgeMethodSpec>(
        [
            new BridgeMethodSpec("DeepCloneField", "void",
                new EquatableArray<string>(["object", "object", "global::System.Reflection.FieldInfo"])),
            new BridgeMethodSpec("CopyField", "void",
                new EquatableArray<string>(["object", "object", "global::System.Reflection.FieldInfo"])),
            new BridgeMethodSpec("DeepCloneProperty", "void",
                new EquatableArray<string>(["object", "object", "global::System.Reflection.PropertyInfo"])),
            new BridgeMethodSpec("ResolveDeclaredField", "global::System.Reflection.FieldInfo",
                new EquatableArray<string>(["global::System.Type", "string"])),
            new BridgeMethodSpec("ResolveDeclaredProperty", "global::System.Reflection.PropertyInfo",
                new EquatableArray<string>(["global::System.Type", "string"])),
        ]),
        IsAvailable: true);

    private static TypeModel MakeTypeModel(bool isStruct, string fullyQualifiedName, EquatableArray<MemberModel>? members = null) =>
        new TypeModel(
            Namespace: "Test",
            Name: "T",
            FullyQualifiedName: fullyQualifiedName,
            IsStruct: isStruct,
            IsSealed: false,
            IsAbstract: false,
            IsRecord: false,
            HasClonableBaseClass: false,
            CanHaveCircularReferences: false,
            NeedsStateTracking: false,
            IsFastClonerAvailable: true,
            Members: members ?? EquatableArray<MemberModel>.Empty,
            TypeParameters: EquatableArray<string>.Empty,
            TypeConstraints: EquatableArray<string>.Empty,
            RelatedTypes: EquatableArray<TypeModel>.Empty,
            NestedTypes: EquatableArray<MemberModel>.Empty,
            DerivedTypes: EquatableArray<TypeModel>.Empty,
            NullabilityEnabled: true,
            TrustNullability: false,
            TargetFramework: TargetFramework.NetStandard20);

    private static MemberModel MakePrivateField(string name = "_privateField", string type = "int", bool isShallow = false) =>
        new MemberModel(
            Name: name,
            TypeFullName: type,
            IsReadOnly: false,
            IsProperty: false,
            IsField: true,
            TypeKind: MemberTypeKind.Safe,
            ElementTypeName: null,
            KeyTypeName: null,
            ValueTypeName: null,
            ElementIsSafe: false,
            ElementHasClonableAttr: false,
            KeyIsSafe: false,
            KeyIsClonable: false,
            ValueIsSafe: false,
            ValueIsClonable: false,
            RequiresFastCloner: false,
            CollectionKind: CollectionKind.None,
            ConcreteTypeFullName: null,
            IsValueType: true,
            IsInitOnly: false,
            IsRequired: false,
            ArrayRank: 0,
            IsNullable: false,
            HasGetter: true,
            HasSetter: true,
            SetterIsAccessible: false,
            MemberBehavior: isShallow ? MemberCloneBehavior.Shallow : MemberCloneBehavior.Clone,
            AccessorStrategy: NonPublicAccessorStrategy.Field,
            GetterIsAccessible: false);

    private static MemberModel MakePrivateSetterProperty(string name = "PrivateSetterProp", string type = "int") =>
        new MemberModel(
            Name: name,
            TypeFullName: type,
            IsReadOnly: false,
            IsProperty: true,
            IsField: false,
            TypeKind: MemberTypeKind.Safe,
            ElementTypeName: null,
            KeyTypeName: null,
            ValueTypeName: null,
            ElementIsSafe: false,
            ElementHasClonableAttr: false,
            KeyIsSafe: false,
            KeyIsClonable: false,
            ValueIsSafe: false,
            ValueIsClonable: false,
            RequiresFastCloner: false,
            CollectionKind: CollectionKind.None,
            ConcreteTypeFullName: null,
            IsValueType: true,
            IsInitOnly: false,
            IsRequired: false,
            ArrayRank: 0,
            IsNullable: false,
            HasGetter: true,
            HasSetter: true,
            SetterIsAccessible: false,
            MemberBehavior: MemberCloneBehavior.Clone,
            AccessorStrategy: NonPublicAccessorStrategy.SetterMethod,
            GetterIsAccessible: true);

    [Test]
    public async Task RuntimeBridge_StructReceiver_FieldStorage_EmitsBoxMutateUnbox()
    {
        MemberModel member = MakePrivateField();
        TypeModel model = MakeTypeModel(isStruct: true, "global::Test.MyStruct", new EquatableArray<MemberModel>([member]));
        CloneGeneratorContext ctx = new CloneGeneratorContext(model, MakeBridgeContract());
        StringBuilder sb = new StringBuilder();

        NonPublicAccessorEmitter.WriteCloneCall(ctx, sb, member, "result", "source", "state", "            ");

        string emitted = sb.ToString();

        await Assert.That(emitted).Contains("object __nptarget_");
        await Assert.That(emitted).Contains("= (object)result;");
        await Assert.That(emitted).Contains($"{GlobalProxyFqn}.DeepCloneField(source,");
        await Assert.That(emitted).Contains("result = (global::Test.MyStruct)__nptarget_");

        // The bridge call must target the box, not the local 'result'.
        await Assert.That(emitted).DoesNotContain($"{GlobalProxyFqn}.DeepCloneField(source, result,");
    }

    [Test]
    public async Task RuntimeBridge_StructReceiver_ShallowField_EmitsCopyField_ThroughBox()
    {
        MemberModel member = MakePrivateField(isShallow: true);
        TypeModel model = MakeTypeModel(isStruct: true, "global::Test.MyStruct", new EquatableArray<MemberModel>([member]));
        CloneGeneratorContext ctx = new CloneGeneratorContext(model, MakeBridgeContract());
        StringBuilder sb = new StringBuilder();

        NonPublicAccessorEmitter.WriteCloneCall(ctx, sb, member, "result", "source", "state", "            ");

        string emitted = sb.ToString();

        await Assert.That(emitted).Contains("object __nptarget_");
        await Assert.That(emitted).Contains($"{GlobalProxyFqn}.CopyField(source,");
        await Assert.That(emitted).Contains("result = (global::Test.MyStruct)__nptarget_");
        await Assert.That(emitted).DoesNotContain($"{GlobalProxyFqn}.CopyField(source, result,");
    }

    [Test]
    public async Task RuntimeBridge_StructReceiver_PropertySetter_EmitsBoxMutateUnbox()
    {
        MemberModel member = MakePrivateSetterProperty();
        TypeModel model = MakeTypeModel(isStruct: true, "global::Test.MyStruct", new EquatableArray<MemberModel>([member]));
        CloneGeneratorContext ctx = new CloneGeneratorContext(model, MakeBridgeContract());
        StringBuilder sb = new StringBuilder();

        NonPublicAccessorEmitter.WriteCloneCall(ctx, sb, member, "result", "source", "state", "            ");

        string emitted = sb.ToString();

        await Assert.That(emitted).Contains("object __nptarget_");
        await Assert.That(emitted).Contains($"{GlobalProxyFqn}.DeepCloneProperty(source,");
        await Assert.That(emitted).Contains("result = (global::Test.MyStruct)__nptarget_");
        await Assert.That(emitted).DoesNotContain($"{GlobalProxyFqn}.DeepCloneProperty(source, result,");
    }

    [Test]
    public async Task RuntimeBridge_ClassReceiver_DoesNotBoxOrUnbox()
    {
        // Reference types should keep using the simple direct call -- boxing is a
        // no-op and the generator should not pay for an extra copy.
        MemberModel member = MakePrivateField();
        TypeModel model = MakeTypeModel(isStruct: false, "global::Test.MyClass", new EquatableArray<MemberModel>([member]));
        CloneGeneratorContext ctx = new CloneGeneratorContext(model, MakeBridgeContract());
        StringBuilder sb = new StringBuilder();

        NonPublicAccessorEmitter.WriteCloneCall(ctx, sb, member, "result", "source", "state", "            ");

        string emitted = sb.ToString();

        await Assert.That(emitted).Contains($"{GlobalProxyFqn}.DeepCloneField(source, result,");
        await Assert.That(emitted).DoesNotContain("__nptarget_");
        await Assert.That(emitted).DoesNotContain("(object)result");
    }

    [Test]
    public async Task RuntimeBridge_StructReceiver_MultipleCalls_UseDistinctBoxLocals()
    {
        // Two non-public members on the same struct must use distinct local
        // names so the emitted method compiles. The generator uses
        // GetNextVariableId() for this.
        MemberModel a = MakePrivateField("_a");
        MemberModel b = MakePrivateField("_b");
        TypeModel model = MakeTypeModel(isStruct: true, "global::Test.MyStruct",
            new EquatableArray<MemberModel>([a, b]));
        CloneGeneratorContext ctx = new CloneGeneratorContext(model, MakeBridgeContract());
        StringBuilder sb = new StringBuilder();

        NonPublicAccessorEmitter.WriteCloneCall(ctx, sb, a, "result", "source", "state", "            ");
        NonPublicAccessorEmitter.WriteCloneCall(ctx, sb, b, "result", "source", "state", "            ");

        string emitted = sb.ToString();

        // Find both __nptarget_<n> identifiers and verify they differ.
        System.Text.RegularExpressions.MatchCollection matches =
            System.Text.RegularExpressions.Regex.Matches(emitted, @"__nptarget_(\d+)");
        HashSet<string> ids = [];
        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            ids.Add(m.Groups[1].Value);
        }

        await Assert.That(ids.Count).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task TfmAtLeastNet8_DoesNotUseBridge_RegardlessOfStructness()
    {
        // Sanity: on .NET 8+ the emitter uses UnsafeAccessor and never boxes.
        MemberModel member = MakePrivateField();
        TypeModel model = MakeTypeModel(isStruct: true, "global::Test.MyStruct", new EquatableArray<MemberModel>([member]))
            with { TargetFramework = TargetFramework.Net8 };
        CloneGeneratorContext ctx = new CloneGeneratorContext(model, MakeBridgeContract());
        StringBuilder sb = new StringBuilder();

        NonPublicAccessorEmitter.WriteCloneCall(ctx, sb, member, "result", "source", "state", "            ");

        string emitted = sb.ToString();

        await Assert.That(emitted).DoesNotContain(GlobalProxyFqn);
        await Assert.That(emitted).DoesNotContain("__nptarget_");
    }
}
