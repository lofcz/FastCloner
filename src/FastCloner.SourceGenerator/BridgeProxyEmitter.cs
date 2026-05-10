using System.Text;

namespace FastCloner.SourceGenerator;

internal static class BridgeProxyEmitter
{
    public const string HintName = "FastCloner_SGBridgeProxy.g.cs";

    public static bool ShouldEmit(TargetFramework targetFramework, BridgeContract contract)
    {
        if (targetFramework >= TargetFramework.Net8)
            return false;
        
        return contract is { IsAvailable: true, Methods.Count: > 0 };
    }
    
    public static string Emit(BridgeContract contract)
    {
        string proxyFqn = contract.ProxyTypeFullName;
        int lastDot = proxyFqn.LastIndexOf('.');
        string proxyNamespace = lastDot >= 0 ? proxyFqn.Substring(0, lastDot) : string.Empty;
        string proxyTypeName = lastDot >= 0 ? proxyFqn.Substring(lastDot + 1) : proxyFqn;

        StringBuilder sb = new StringBuilder(2048);
        
        sb.AppendLine("#nullable disable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine();

        bool hasNamespace = proxyNamespace.Length > 0;
        if (hasNamespace)
        {
            sb.Append("namespace ").Append(proxyNamespace).AppendLine();
            sb.AppendLine("{");
        }

        string indent = hasNamespace ? "    " : string.Empty;
        
        sb.Append(indent).AppendLine("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        sb.Append(indent).AppendLine("[global::System.Runtime.CompilerServices.CompilerGenerated]");
        sb.Append(indent).Append("internal static class ").AppendLine(proxyTypeName);
        sb.Append(indent).AppendLine("{");

        string body = hasNamespace ? indent + "    " : "    ";

        sb.Append(body).Append("private const string BridgeTypeName = \"").Append(contract.BridgeTypeMetadataName).AppendLine("\";");
        sb.AppendLine();
        sb.Append(body).AppendLine("private static readonly Type BridgeType = ResolveBridgeType();");
        
        foreach (BridgeMethodSpec method in contract.Methods)
        {
            sb.AppendLine();
            string delegateType = BuildDelegateType(method);
            string parameterTypeofList = BuildTypeofList(method.ParameterTypeFqns);

            sb.Append(body).Append("internal static readonly ").Append(delegateType).Append(' ').Append(method.Name).AppendLine(" =");
            sb.Append(body).Append("    (").Append(delegateType).AppendLine(")Delegate.CreateDelegate(");
            sb.Append(body).Append("        typeof(").Append(delegateType).AppendLine("),");
            sb.Append(body).Append("        ResolveStaticMethod(\"").Append(method.Name).Append('\"');
            if (parameterTypeofList.Length > 0)
            {
                sb.Append(", ").Append(parameterTypeofList);
            }
            sb.AppendLine("));");
        }

        sb.AppendLine();
        sb.Append(body).AppendLine("private static Type ResolveBridgeType()");
        sb.Append(body).AppendLine("{");
        sb.Append(body).AppendLine("    Assembly runtimeAssembly = typeof(global::FastCloner.Code.FastClonerGenerator).Assembly;");
        sb.Append(body).AppendLine("    Type type = runtimeAssembly.GetType(BridgeTypeName, throwOnError: false);");
        sb.Append(body).AppendLine("    if (type == null)");
        sb.Append(body).AppendLine("    {");
        sb.Append(body).AppendLine("        throw new InvalidOperationException(");
        sb.Append(body).AppendLine("            \"FastCloner source generator: the runtime bridge type '\" + BridgeTypeName +");
        sb.Append(body).AppendLine("            \"' was not found in assembly '\" + runtimeAssembly.FullName +");
        sb.Append(body).AppendLine("            \"'. The FastCloner runtime version may be incompatible with the source generator. \" +");
        sb.Append(body).AppendLine("            \"Update the FastCloner package to a matching version.\");");
        sb.Append(body).AppendLine("    }");
        sb.Append(body).AppendLine("    return type;");
        sb.Append(body).AppendLine("}");

        sb.AppendLine();
        sb.Append(body).AppendLine("private static MethodInfo ResolveStaticMethod(string name, params Type[] parameterTypes)");
        sb.Append(body).AppendLine("{");
        sb.Append(body).AppendLine("    const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;");
        sb.Append(body).AppendLine("    MethodInfo method = BridgeType.GetMethod(name, flags, binder: null, types: parameterTypes, modifiers: null);");
        sb.Append(body).AppendLine("    if (method == null)");
        sb.Append(body).AppendLine("    {");
        sb.Append(body).AppendLine("        throw new InvalidOperationException(");
        sb.Append(body).AppendLine("            \"FastCloner source generator: bridge method '\" + name +");
        sb.Append(body).AppendLine("            \"' was not found on '\" + BridgeType.FullName +");
        sb.Append(body).AppendLine("            \"'. The FastCloner runtime version may be incompatible with the source generator.\");");
        sb.Append(body).AppendLine("    }");
        sb.Append(body).AppendLine("    return method;");
        sb.Append(body).AppendLine("}");

        sb.Append(indent).AppendLine("}");

        if (hasNamespace)
        {
            sb.AppendLine("}");
        }

        return sb.ToString();
    }
    
    private static string BuildDelegateType(BridgeMethodSpec method)
    {
        if (method.IsVoid)
        {
            if (method.ParameterTypeFqns.Count == 0)
            {
                return "global::System.Action";
            }

            return "global::System.Action<" + Join(method.ParameterTypeFqns) + ">";
        }

        if (method.ParameterTypeFqns.Count == 0)
        {
            return "global::System.Func<" + method.ReturnTypeFqn + ">";
        }

        return "global::System.Func<" + Join(method.ParameterTypeFqns) + ", " + method.ReturnTypeFqn + ">";
    }
    
    private static string BuildTypeofList(EquatableArray<string> parameterTypeFqns)
    {
        string[]? items = parameterTypeFqns.GetArray();
        if (items is null || items.Length == 0)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < items.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append("typeof(").Append(items[i]).Append(')');
        }
        return sb.ToString();
    }

    private static string Join(EquatableArray<string> items)
    {
        string[]? array = items.GetArray();
        if (array is null || array.Length == 0)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < array.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(array[i]);
        }
        return sb.ToString();
    }
}
