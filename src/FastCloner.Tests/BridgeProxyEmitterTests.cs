using FastCloner.SourceGenerator;

namespace FastCloner.Tests;

public class BridgeProxyEmitterTests
{
    private static BridgeContract MakeContract() => new BridgeContract(
        BridgeTypeMetadataName: "FastCloner.FastClonerSourceGeneratorBridge",
        ProxyTypeFullName: "FastCloner.SourceGenerated.__FastClonerSGBridgeProxy",
        Methods: new EquatableArray<BridgeMethodSpec>(
        [
            new BridgeMethodSpec(
                Name: "DeepCloneField",
                ReturnTypeFqn: "void",
                ParameterTypeFqns: new EquatableArray<string>(["object", "object", "global::System.Reflection.FieldInfo"])),
            new BridgeMethodSpec(
                Name: "ResolveDeclaredField",
                ReturnTypeFqn: "global::System.Reflection.FieldInfo",
                ParameterTypeFqns: new EquatableArray<string>(["global::System.Type", "string"])),
        ]),
        IsAvailable: true);

    [Test]
    public async Task Emits_namespace_and_proxy_type_from_contract_proxy_fqn()
    {
        BridgeContract contract = MakeContract();

        string source = BridgeProxyEmitter.Emit(contract);

        await Assert.That(source).Contains("namespace FastCloner.SourceGenerated");
        await Assert.That(source).Contains("internal static class __FastClonerSGBridgeProxy");
    }

    [Test]
    public async Task Emits_action_delegate_for_void_returning_method()
    {
        BridgeContract contract = MakeContract();

        string source = BridgeProxyEmitter.Emit(contract);

        await Assert.That(source).Contains(
            "internal static readonly global::System.Action<object, object, global::System.Reflection.FieldInfo> DeepCloneField =");
    }

    [Test]
    public async Task Emits_func_delegate_for_value_returning_method()
    {
        BridgeContract contract = MakeContract();

        string source = BridgeProxyEmitter.Emit(contract);

        await Assert.That(source).Contains(
            "internal static readonly global::System.Func<global::System.Type, string, global::System.Reflection.FieldInfo> ResolveDeclaredField =");
    }

    [Test]
    public async Task Emits_resolve_static_method_with_parameter_typeof_list()
    {
        BridgeContract contract = MakeContract();

        string source = BridgeProxyEmitter.Emit(contract);

        await Assert.That(source).Contains(
            "ResolveStaticMethod(\"DeepCloneField\", typeof(object), typeof(object), typeof(global::System.Reflection.FieldInfo))");
        await Assert.That(source).Contains(
            "ResolveStaticMethod(\"ResolveDeclaredField\", typeof(global::System.Type), typeof(string))");
    }

    [Test]
    public async Task Emits_const_bridge_type_name_from_contract()
    {
        BridgeContract contract = MakeContract();

        string source = BridgeProxyEmitter.Emit(contract);

        await Assert.That(source).Contains(
            "private const string BridgeTypeName = \"FastCloner.FastClonerSourceGeneratorBridge\";");
    }

    [Test]
    public async Task Output_is_byte_stable_for_identical_input()
    {
        BridgeContract contract = MakeContract();

        string a = BridgeProxyEmitter.Emit(contract);
        string b = BridgeProxyEmitter.Emit(contract);

        await Assert.That(a).IsEqualTo(b);
    }

    [Test]
    public async Task Should_not_emit_under_net8_or_newer()
    {
        BridgeContract contract = MakeContract();

        bool net10 = BridgeProxyEmitter.ShouldEmit(TargetFramework.Net10, contract);
        bool net8 = BridgeProxyEmitter.ShouldEmit(TargetFramework.Net8, contract);

        await Assert.That(net10).IsFalse();
        await Assert.That(net8).IsFalse();
    }

    [Test]
    public async Task Should_emit_under_older_tfm_when_contract_available()
    {
        BridgeContract contract = MakeContract();

        bool ns20 = BridgeProxyEmitter.ShouldEmit(TargetFramework.NetStandard20, contract);

        await Assert.That(ns20).IsTrue();
    }

    [Test]
    public async Task Should_not_emit_when_contract_unavailable()
    {
        bool empty = BridgeProxyEmitter.ShouldEmit(TargetFramework.NetStandard20, BridgeContract.Empty);

        await Assert.That(empty).IsFalse();
    }
}
