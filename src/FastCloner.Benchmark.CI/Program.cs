using BenchmarkDotNet.Running;
using FastCloner.Benchmark.CI.Reporting;

namespace FastCloner.Benchmark.CI;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (BenchmarkDiffReporter.TryRun(args, out int reportExitCode))
            return reportExitCode;

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return 0;
    }
}
