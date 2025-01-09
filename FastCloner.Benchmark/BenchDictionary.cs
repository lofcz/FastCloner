using BenchmarkDotNet.Attributes;
using Force.DeepCloner;

namespace FastCloner.Benchmark;

[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class BenchDictionary
{
    private Dictionary<SimpleKey, string> testData;
    
    [GlobalSetup]
    public void Setup()
    {
        testData = new Dictionary<SimpleKey, string>();
        
        for (int i = 0; i < 1000; i++)
        {
            SimpleKey key = new SimpleKey { Id = i, Name = $"Key{i}" };
            testData.Add(key, $"Value{i}");
        }
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
}

public class SimpleKey
{
    public int Id { get; set; }
    public string Name { get; set; }
}