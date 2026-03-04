using BenchmarkDotNet.Attributes;

namespace FastCloner.Benchmark.Ideas.Clone1DimArrayClass;

public abstract class Idea1dArrayClassBenchmarkBase
{
    [Params(200_000)]
    public int Length { get; set; }

    [Params(0, 80)]
    public int NullPercent { get; set; }

    [Params(true, false)]
    public bool TrackReferences { get; set; }

    protected string?[] Source = null!;
    protected string?[] Target = null!;
    protected IdeaCloneState State = null!;

    [GlobalSetup]
    public void Setup()
    {
        Source = new string?[Length];
        Target = new string?[Length];
        State = new IdeaCloneState(TrackReferences);

        // Deterministic distribution to keep runs stable.
        int step = NullPercent <= 0 ? int.MaxValue : Math.Max(1, 100 / NullPercent);
        for (int i = 0; i < Length; i++)
        {
            bool isNull = NullPercent > 0 && i % step == 0;
            Source[i] = isNull ? null : "item_" + i.ToString();
        }
    }
}
