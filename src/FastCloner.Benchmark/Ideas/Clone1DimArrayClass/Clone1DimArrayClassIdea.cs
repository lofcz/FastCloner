using FastCloner.Benchmark.Ideas;

namespace FastCloner.Benchmark.Ideas.Clone1DimArrayClass;

public sealed class Clone1DimArrayClassIdea : IBenchmarkIdea
{
    public string Id => "1d-array-class";
    public string Description => "Clone1DimArrayClassInternal original vs null/no-tracking variants";
    public Type BenchmarkType => typeof(BenchClone1DimArrayClassIdeas);
}
