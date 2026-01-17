using Microsoft.CodeAnalysis.Diagnostics;

namespace FastCloner.SourceGenerator;

internal enum TargetFramework
{
    NetStandard20 = 0,
    Net5 = 5,
    Net6 = 6,
    Net7 = 7,
    Net8 = 8,
    Net9 = 9,
    Net10 = 10
}

internal static class TargetFrameworkDetector
{
    public static TargetFramework Detect(AnalyzerConfigOptionsProvider optionsProvider)
    {
        if (optionsProvider.GlobalOptions.TryGetValue("build_property.TargetFramework", out string? tfm) 
            && !string.IsNullOrEmpty(tfm))
        {
            return Parse(tfm);
        }
        
        return TargetFramework.NetStandard20;
    }
    
    public static TargetFramework Parse(string tfm)
    {
        int dashIndex = tfm.IndexOf('-');
        if (dashIndex > 0)
            tfm = tfm.Substring(0, dashIndex);

        if (tfm.StartsWith("net") && tfm.Length > 3)
        {
            string version = tfm.Substring(3);

            int dotIndex = version.IndexOf('.');
            string majorStr = dotIndex > 0 ? version.Substring(0, dotIndex) : version;
            
            if (int.TryParse(majorStr, out int major))
            {
                switch (major)
                {
                    case >= 10:
                        return TargetFramework.Net10;
                    case >= 9:
                        return TargetFramework.Net9;
                    case >= 8:
                        return TargetFramework.Net8;
                    case >= 7:
                        return TargetFramework.Net7;
                    case >= 6:
                        return TargetFramework.Net6;
                    case >= 5:
                        return TargetFramework.Net5;
                }
            }
        }
        
        return TargetFramework.NetStandard20;
    }
}
