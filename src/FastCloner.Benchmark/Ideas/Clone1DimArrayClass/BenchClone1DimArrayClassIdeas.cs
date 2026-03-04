using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace FastCloner.Benchmark.Ideas.Clone1DimArrayClass;

[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
[InProcess]
[WarmupCount(3)]
[IterationCount(8)]
public class BenchClone1DimArrayClassIdeas : Idea1dArrayClassBenchmarkBase
{
    private static readonly Func<object?, IdeaCloneState, object?> CloneTracking = SimulatedCloneTracking;
    private static readonly Func<object?, object?> CloneNoTracking = SimulatedCloneNoTracking;

    [Benchmark(Baseline = true)]
    public int Original()
    {
        Clone1DimArrayClassIdeaMethods.Original(Source, Target, State, CloneTracking);
        return Target[Length - 1]?.Length ?? -1;
    }

    [Benchmark]
    public int NullFastPath()
    {
        Clone1DimArrayClassIdeaMethods.NullFastPath(Source, Target, State, CloneTracking);
        return Target[Length - 1]?.Length ?? -1;
    }

    [Benchmark]
    public int NullFastPathAndNoTracking()
    {
        Clone1DimArrayClassIdeaMethods.NullFastPathAndNoTracking(Source, Target, State, CloneTracking, CloneNoTracking);
        return Target[Length - 1]?.Length ?? -1;
    }

    private static object? SimulatedCloneTracking(object? obj, IdeaCloneState state)
    {
        if (obj == null)
            return null;

        // Keep a tiny bit of non-trivial work so this better resembles real clone paths.
        if (state.TrackReferences && obj is string s && s.Length == 0)
            return string.Empty;

        return obj;
    }

    private static object? SimulatedCloneNoTracking(object? obj)
    {
        return obj;
    }
}
