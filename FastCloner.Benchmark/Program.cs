using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace FastCloner.Benchmark;

class Program
{
    static void Main(string[] args)
    {
        // this is used only for package "FastDeepCloner" which is not properly packed; todo: fork it and pack for a fair bench
        ManualConfig config = DefaultConfig.Instance
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);
        
        Summary summary = BenchmarkRunner.Run<BenchMinimal>(config);
        Console.WriteLine(summary);
    }
}