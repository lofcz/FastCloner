namespace FastCloner;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class FastClonerSourceGeneratorBridgeAttribute(string proxyTypeFullName) : Attribute
{
    public string ProxyTypeFullName { get; } = proxyTypeFullName;
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class FastClonerSourceGeneratorBridgeMemberAttribute : Attribute;
