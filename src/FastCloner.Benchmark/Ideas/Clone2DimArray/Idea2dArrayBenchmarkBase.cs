using BenchmarkDotNet.Attributes;

namespace FastCloner.Benchmark.Ideas.Clone2DimArray;

public abstract class Idea2dArrayBenchmarkBase
{
    // Keep one realistic baseline case that stresses row-prefix copies:
    // same column count, different row count.
    [Params(2048)]
    public int FromRows { get; set; }

    [Params(1536)]
    public int ToRows { get; set; }

    [Params(256)]
    public int Cols { get; set; }

    protected int[,] Source = null!;
    protected int[,] Target = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (FromRows <= 0 || ToRows <= 0 || Cols <= 0)
            throw new InvalidOperationException("Benchmark dimensions must be positive.");

        Source = new int[FromRows, Cols];
        Target = new int[ToRows, Cols];

        for (int i = 0; i < FromRows; i++)
        {
            for (int j = 0; j < Cols; j++)
            {
                Source[i, j] = (i * 31) ^ j;
            }
        }
    }
}
