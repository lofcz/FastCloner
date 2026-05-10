namespace FastCloner.SourceGenerator;

internal sealed record BridgeMethodSpec(
    string Name,
    string ReturnTypeFqn,
    EquatableArray<string> ParameterTypeFqns)
{
    public bool IsVoid => ReturnTypeFqn == "void";
}

internal sealed record BridgeContract(
    string BridgeTypeMetadataName,
    string ProxyTypeFullName,
    EquatableArray<BridgeMethodSpec> Methods,
    bool IsAvailable)
{
    public static BridgeContract Empty { get; } = new BridgeContract(
        BridgeTypeMetadataName: string.Empty,
        ProxyTypeFullName: string.Empty,
        Methods: EquatableArray<BridgeMethodSpec>.Empty,
        IsAvailable: false);
}
