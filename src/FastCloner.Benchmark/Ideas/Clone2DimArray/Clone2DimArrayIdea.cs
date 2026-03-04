using FastCloner.Benchmark.Ideas;

namespace FastCloner.Benchmark.Ideas.Clone2DimArray;

public sealed class Clone2DimArrayIdea : IBenchmarkIdea
{
    public string Id => "2d-array";
    public string Description => "Clone2DimArrayInternal original vs locality-optimized shallow copy";
    public Type BenchmarkType => typeof(BenchClone2DimArrayIdeas);
}
