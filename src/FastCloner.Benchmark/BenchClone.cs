using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace FastCloner.Benchmark;

[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class BenchClone
{
    private ComplexModel model = null!;

    [GlobalSetup]
    public void Setup()
    {
        model = TestDataGenerator.CreateSampleModel();
    }

    [Benchmark(Baseline = true)]
    public ComplexModel FastCloner_SourceGen()
    {
        return model.FastDeepClone();
    }

    [Benchmark]
    public ComplexModel IDeepCloneable_SourceGen()
    {
        return model.DeepClone();
    }

    [Benchmark]
    public ComplexModel Mapperly_SourceGen()
    {
        return MapperlyCloner.Clone(model);
    }
}
