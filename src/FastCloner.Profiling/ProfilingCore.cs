using System.Diagnostics;

namespace FastCloner.Profiling;

internal readonly record struct ProfileSessionOptions(bool Interactive, int WarmupIterations, int Iterations);

internal static class ProfilingCore
{
    internal static bool HasFlag(string[] args, string flag)
    {
        foreach (string arg in args)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(arg, flag))
                return true;
        }

        return false;
    }

    internal static int ParseIntArg(string[] args, string prefix, int defaultValue)
    {
        foreach (string arg in args)
        {
            if (!arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            if (Int32.TryParse(arg[prefix.Length..], out int parsed) && parsed > 0)
                return parsed;
        }

        return defaultValue;
    }

    internal static ProfileSessionOptions ParseSessionOptions(
        string[] args,
        int defaultWarmupIterations,
        int defaultIterations)
    {
        bool interactive = !HasFlag(args, "--no-interactive");
        int warmupIterations = ParseIntArg(args, "--warmup=", defaultWarmupIterations);
        int iterations = ParseIntArg(args, "--iterations=", defaultIterations);
        return new ProfileSessionOptions(interactive, warmupIterations, iterations);
    }

    internal static void PrintHeader(
        string title,
        ProfileSessionOptions options,
        params (string Label, string Value)[] extraLines)
    {
        Console.WriteLine(title);
        Console.WriteLine(new string('=', title.Length));
        Console.WriteLine($"Warmup iterations: {options.WarmupIterations:N0}");
        Console.WriteLine($"Profile iterations: {options.Iterations:N0}");
        foreach ((string label, string value) in extraLines)
            Console.WriteLine($"{label}: {value}");
        Console.WriteLine();
    }

    internal static void RunWarmupPhase(int iterations, params Action[] actions)
    {
        Console.WriteLine("Warming up...");
        foreach (Action action in actions)
            Warmup(iterations, action);
        Console.WriteLine("Warmup complete.");
        Console.WriteLine();
    }

    internal static void WaitForProfilerIfInteractive(bool interactive, string banner = ">>> ATTACH PROFILER NOW <<<")
    {
        if (!interactive)
            return;

        Console.WriteLine(banner);
        Console.WriteLine("Press any key to start...");
        Console.ReadKey(true);
        Console.WriteLine();
    }

    internal static void Warmup(int iterations, Action action)
    {
        for (int i = 0; i < iterations; i++)
            action();
    }

    internal static ProfileResult Measure(int iterations, Action action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
            action();

        stopwatch.Stop();
        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

        return new ProfileResult(stopwatch.Elapsed, iterations, allocatedAfter - allocatedBefore);
    }

    internal static ProfileResult MeasureAndPrint(string name, int iterations, Action action)
    {
        ProfileResult result = Measure(iterations, action);
        Console.WriteLine(
            $"{name,-20} {result.Iterations:N0} iters in {result.Elapsed.TotalMilliseconds:N0} ms " +
            $"({result.MicrosecondsPerOperation:F2} us/op, {result.BytesPerOperation:F0} B/op)");
        return result;
    }
}

internal readonly struct ProfileResult(TimeSpan elapsed, int iterations, long allocatedBytes)
{
    internal TimeSpan Elapsed { get; } = elapsed;
    internal int Iterations { get; } = iterations;
    internal long AllocatedBytes { get; } = allocatedBytes;
    internal double MicrosecondsPerOperation => Elapsed.TotalMilliseconds * 1000d / Iterations;
    internal double BytesPerOperation => (double)AllocatedBytes / Iterations;
}
