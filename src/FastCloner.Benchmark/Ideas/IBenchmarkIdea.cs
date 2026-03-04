namespace FastCloner.Benchmark.Ideas;

public interface IBenchmarkIdea
{
    string Id { get; }
    string Description { get; }
    Type BenchmarkType { get; }
}
