using BenchmarkDotNet.Attributes;
using Force.DeepCloner;

namespace FastCloner.Benchmark;

[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class Bench2DArray
{
    private int[,] testData;
    private const int SIZE = 100;
    
    [GlobalSetup]
    public void Setup()
    {
        testData = new int[SIZE, SIZE];
        
        for (int i = 0; i < SIZE; i++)
        {
            for (int j = 0; j < SIZE; j++)
            {
                testData[i, j] = i * j;
            }
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

    [Benchmark]
    public object? ArrayCopy()
    {
        int[,] clone = new int[SIZE, SIZE];
        Array.Copy(testData, clone, testData.Length);
        return clone;
    }

    [Benchmark]
    public object? ManualCopy()
    {
        int[,] clone = new int[SIZE, SIZE];
        for (int i = 0; i < SIZE; i++)
        {
            for (int j = 0; j < SIZE; j++)
            {
                clone[i, j] = testData[i, j];
            }
        }
        return clone;
    }
}