using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace FastCloner.Benchmark.Ideas.Clone2DimArray;

[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
[InProcess]
[WarmupCount(3)]
[IterationCount(8)]
public class BenchClone2DimArrayIdeas : Idea2dArrayBenchmarkBase
{
    [Benchmark(Baseline = true)]
    public int Original_Shallow()
    {
        Clone2DimArrayIdeaMethods.OriginalShallow(Source, Target);
        return Target[ToRows - 1, Cols - 1];
    }

    [Benchmark]
    public int Edited_Shallow()
    {
        Clone2DimArrayIdeaMethods.EditedShallow(Source, Target);
        return Target[ToRows - 1, Cols - 1];
    }
}
