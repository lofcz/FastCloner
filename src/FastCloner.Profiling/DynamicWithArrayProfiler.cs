using Cloner = global::FastCloner.FastCloner;

namespace FastCloner.Profiling;

public static class DynamicWithArrayProfiler
{
    private const int Seed = 42;
    private const int DefaultWarmupIterations = 5_000;
    private const int DefaultProfileIterations = 100_000;

    public static void Run(string[] args)
    {
        ProfileSessionOptions options = ProfilingCore.ParseSessionOptions(args, DefaultWarmupIterations, DefaultProfileIterations);
        ProfilingCore.PrintHeader("DynamicWithArray Profiling (FastCloner)", options);

        ObjectWithDynamicProperties payload = CreateDynamicWithArray(new Random(Seed));

        ProfilingCore.RunWarmupPhase(options.WarmupIterations, () => _ = Cloner.DeepClone(payload));
        ProfilingCore.WaitForProfilerIfInteractive(options.Interactive, ">>> ATTACH VS PROFILER NOW <<<");
        _ = ProfilingCore.MeasureAndPrint("FastCloner", options.Iterations, () => _ = Cloner.DeepClone(payload));
    }

    private static ObjectWithDynamicProperties CreateDynamicWithArray(Random random)
    {
        object[] array = new object[100];
        for (int i = 0; i < array.Length; i++)
        {
            int mod = i % 3;
            array[i] = mod switch
            {
                0 => GenerateString(random, 100),
                1 => random.NextDouble(),
                _ => CreateSmallObject(random)
            };
        }

        return new ObjectWithDynamicProperties
        {
            Id = random.Next(),
            Name = GenerateString(random, 50),
            DynamicData = array,
            NestedDynamic = new object[]
            {
                CreateSmallObject(random),
                GenerateStringList(random, 5, 20),
                random.Next()
            }
        };
    }

    private static SmallObject CreateSmallObject(Random random)
    {
        return new SmallObject
        {
            Id = random.Next(),
            Name = GenerateString(random, 50),
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(365)),
            IsActive = random.Next(2) == 1,
            Score = random.NextDouble() * 100
        };
    }

    private static string GenerateString(Random random, int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 _-";
        char[] result = new char[length];
        for (int i = 0; i < length; i++)
            result[i] = chars[random.Next(chars.Length)];
        return new string(result);
    }

    private static List<string> GenerateStringList(Random random, int count, int stringLength)
    {
        List<string> list = new(count);
        for (int i = 0; i < count; i++)
            list.Add(GenerateString(random, stringLength));
        return list;
    }
}

public class ObjectWithDynamicProperties
{
    public int Id { get; set; }
    public string Name { get; set; } = String.Empty;
    public object DynamicData { get; set; } = null!;
    public object NestedDynamic { get; set; } = null!;
}

public class SmallObject
{
    public int Id { get; set; }
    public string Name { get; set; } = String.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public double Score { get; set; }
}
