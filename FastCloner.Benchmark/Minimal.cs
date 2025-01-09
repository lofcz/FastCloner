using BenchmarkDotNet.Attributes;
using Force.DeepCloner;

namespace FastCloner.Benchmark;

[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class Minimal
{
    private TestObject testData;
    
    [GlobalSetup]
    public void Setup()
    {
        testData = new TestObject
        {
            Id = 1,
            Name = "Test",
            NestedObject = new NestedObject
            {
                Value = 42,
                Description = "Nested test"
            }
        };
    }
    
    [Benchmark(Baseline = true)]
    public object? FastCloner()
    {
        return global::FastCloner.FastCloner.DeepClone(testData);
    }

    [Benchmark]
    public object? DeepCopier()
    {
        return global::DeepCopier.Copier.Copy(testData);
    }

    [Benchmark]
    public object? DeepCopy()
    {
        return global::DeepCopy.DeepCopier.Copy(testData);
    }

    [Benchmark]
    public object DeepCopyExpression()
    {
        return global::DeepCopy.ObjectCloner.Clone(testData);
    }

    [Benchmark]
    public object? FastDeepCloner()
    {
        return global::FastDeepCloner.DeepCloner.Clone(testData);
    }
    
    [Benchmark]
    public object? DeepCloner()
    {
        return testData.DeepClone();
    }
    
    public class TestObject
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public NestedObject NestedObject { get; set; }
    }

    public class NestedObject
    {
        public int Value { get; set; }
        public string Description { get; set; }
    }
}