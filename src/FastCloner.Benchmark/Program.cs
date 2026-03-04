using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using FastCloner.Benchmark.Ideas;

namespace FastCloner.Benchmark;

class Program
{
    static void Main(string[] args)
    {
        // this is used only for package "FastDeepCloner" which is not properly packed; todo: fork it and pack for a fair bench
        ManualConfig config = DefaultConfig.Instance
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        bool runIdeas = args.Any(arg => string.Equals(arg, "--ideas", StringComparison.OrdinalIgnoreCase));
        if (runIdeas)
        {
            IBenchmarkIdea[] ideas = GetIdeas();
            string? idea = args
                .FirstOrDefault(arg => arg.StartsWith("--idea=", StringComparison.OrdinalIgnoreCase));

            if (idea == null)
            {
                PrintIdeaUsage(ideas);
                return;
            }

            string ideaName = idea["--idea=".Length..].Trim();
            IBenchmarkIdea? matchedIdea = ideas.FirstOrDefault(
                x => string.Equals(x.Id, ideaName, StringComparison.OrdinalIgnoreCase));
            if (matchedIdea is not null)
            {
                BenchmarkRunner.Run(matchedIdea.BenchmarkType, config);
                return;
            }

            Console.WriteLine($"Unknown idea benchmark '{ideaName}'.");
            PrintIdeaUsage(ideas);
            return;
        }

        Summary summary = BenchmarkRunner.Run<BenchClone>(config);

        Console.WriteLine(summary);
    }

    private static IBenchmarkIdea[] GetIdeas()
    {
        return typeof(Program).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IBenchmarkIdea).IsAssignableFrom(t))
            .Select(t => (IBenchmarkIdea)Activator.CreateInstance(t)!)
            .OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void PrintIdeaUsage(IBenchmarkIdea[] ideas)
    {
        Console.WriteLine("When using --ideas, specify exactly one idea benchmark via --idea=<name>.");
        Console.WriteLine("Available values:");
        foreach (IBenchmarkIdea idea in ideas)
            Console.WriteLine($"  --idea={idea.Id}  ({idea.Description})");
        Console.WriteLine();
        Console.WriteLine("Example:");
        string exampleId = ideas.Length > 0 ? ideas[0].Id : "<name>";
        Console.WriteLine($"  dotnet run --project src/FastCloner.Benchmark/FastCloner.Benchmark.csproj -c Release -- --ideas --idea={exampleId}");
    }
}