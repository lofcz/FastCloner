using System.Diagnostics;
using System.Dynamic;
using Cloner = global::FastCloner.FastCloner;

namespace FastCloner.Profiling;

public static class LargeExpandoProfiler
{
    public static void Run(string[] args)
    {
        ProfileSessionOptions options = ProfilingCore.ParseSessionOptions(args, defaultWarmupIterations: 1000, defaultIterations: 100_000);
        int propertyCount = ProfilingCore.ParseIntArg(args, "--properties=", 100);
        ProfilingCore.PrintHeader("Large ExpandoObject Performance Analysis", options, ("Property count", propertyCount.ToString()));

        // Create test objects
        dynamic largeExpando = CreateLargeExpando(propertyCount);
        dynamic onlyPrimitives = CreatePrimitiveOnlyExpando(propertyCount);
        dynamic onlyNested = CreateNestedOnlyExpando(propertyCount);
        dynamic onlyStrings = CreateStringOnlyExpando(propertyCount);

        ProfilingCore.RunWarmupPhase(
            options.WarmupIterations,
            () => _ = Cloner.DeepClone(largeExpando),
            () => _ = Cloner.DeepClone(onlyPrimitives),
            () => _ = Cloner.DeepClone(onlyNested),
            () => _ = Cloner.DeepClone(onlyStrings));
        ProfilingCore.WaitForProfilerIfInteractive(options.Interactive);

        // Profile different ExpandoObject configurations
        Console.WriteLine("=== Mixed Properties (Original Benchmark) ===");
        ProfileExpando("Mixed large", largeExpando, options.Iterations);
        Console.WriteLine();

        Console.WriteLine("=== Primitives Only (int/double/decimal) ===");
        ProfileExpando("Primitives only", onlyPrimitives, options.Iterations);
        Console.WriteLine();

        Console.WriteLine("=== Strings Only ===");
        ProfileExpando("Strings only", onlyStrings, options.Iterations);
        Console.WriteLine();

        Console.WriteLine("=== Nested ExpandoObjects Only ===");
        ProfileExpando("Nested only", onlyNested, options.Iterations);
        Console.WriteLine();

        // Profile with different property counts
        Console.WriteLine("=== Scaling by Property Count ===");
        ProfileScaling(options.Iterations / 10);
        Console.WriteLine();

        // Focused micro-benchmark for loop overhead
        Console.WriteLine("=== Loop Overhead Analysis ===");
        ProfileLoopOverhead(largeExpando, options.Iterations);
        Console.WriteLine();

        Console.WriteLine("Profiling complete.");
        if (options.Interactive)
        {
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey(true);
        }
    }

    private static void ProfileExpando(string name, dynamic expando, int iterations)
    {
        _ = ProfilingCore.MeasureAndPrint(name, iterations, () => _ = Cloner.DeepClone(expando));
    }

    private static void ProfileScaling(int iterationsPerTest)
    {
        int[] propertyCounts = { 10, 25, 50, 100, 200, 500 };

        foreach (int count in propertyCounts)
        {
            dynamic expando = CreateLargeExpando(count);
            
            ProfilingCore.Warmup(100, () => _ = Cloner.DeepClone(expando));

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterationsPerTest; i++)
            {
                _ = Cloner.DeepClone(expando);
            }
            sw.Stop();

            double usPerOp = sw.Elapsed.TotalMicroseconds / iterationsPerTest;
            double usPerProperty = usPerOp / count;
            Console.WriteLine($"  {count,3} properties: {usPerOp,8:F2}μs/op ({usPerProperty:F3}μs/property)");
        }
    }

    private static void ProfileLoopOverhead(dynamic expando, int iterations)
    {
        var dict = (IDictionary<string, object?>)expando;
        int propCount = dict.Count;

        // Pure iteration (baseline)
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            int count = 0;
            foreach (var kvp in dict)
            {
                count++;
            }
        }
        sw.Stop();
        Console.WriteLine($"  Pure iteration:      {sw.Elapsed.TotalMicroseconds / iterations:F3}μs/op");

        // Iteration with GetType()
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            foreach (var kvp in dict)
            {
                if (kvp.Value != null)
                    _ = kvp.Value.GetType();
            }
        }
        sw.Stop();
        Console.WriteLine($"  + GetType():         {sw.Elapsed.TotalMicroseconds / iterations:F3}μs/op");

        // Full clone
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            _ = Cloner.DeepClone(expando);
        }
        sw.Stop();
        Console.WriteLine($"  Full clone:          {sw.Elapsed.TotalMicroseconds / iterations:F3}μs/op");
    }

    #region Test Data Creation

    private static dynamic CreateLargeExpando(int propertyCount)
    {
        dynamic expando = new ExpandoObject();
        var dict = (IDictionary<string, object?>)expando;

        for (int i = 0; i < propertyCount; i++)
        {
            switch (i % 5)
            {
                case 0:
                    dict[$"StringProp_{i}"] = $"String value {i}";
                    break;
                case 1:
                    dict[$"IntProp_{i}"] = i * 10;
                    break;
                case 2:
                    dict[$"DoubleProp_{i}"] = i * 1.5;
                    break;
                case 3:
                    dict[$"DateProp_{i}"] = DateTime.UtcNow.AddDays(i);
                    break;
                case 4:
                    dynamic nested = new ExpandoObject();
                    nested.NestedId = i;
                    nested.NestedValue = $"Nested {i}";
                    dict[$"NestedProp_{i}"] = nested;
                    break;
            }
        }

        return expando;
    }

    private static dynamic CreatePrimitiveOnlyExpando(int propertyCount)
    {
        dynamic expando = new ExpandoObject();
        var dict = (IDictionary<string, object?>)expando;

        for (int i = 0; i < propertyCount; i++)
        {
            switch (i % 3)
            {
                case 0:
                    dict[$"IntProp_{i}"] = i * 10;
                    break;
                case 1:
                    dict[$"DoubleProp_{i}"] = i * 1.5;
                    break;
                case 2:
                    dict[$"DecimalProp_{i}"] = (decimal)(i * 2.5);
                    break;
            }
        }

        return expando;
    }

    private static dynamic CreateStringOnlyExpando(int propertyCount)
    {
        dynamic expando = new ExpandoObject();
        var dict = (IDictionary<string, object?>)expando;

        for (int i = 0; i < propertyCount; i++)
        {
            dict[$"StringProp_{i}"] = $"String value {i} with some extra content to make it realistic";
        }

        return expando;
    }

    private static dynamic CreateNestedOnlyExpando(int propertyCount)
    {
        dynamic expando = new ExpandoObject();
        var dict = (IDictionary<string, object?>)expando;

        for (int i = 0; i < propertyCount; i++)
        {
            dynamic nested = new ExpandoObject();
            nested.Id = i;
            nested.Name = $"Nested object {i}";
            nested.Value = i * 1.5;
            dict[$"NestedProp_{i}"] = nested;
        }

        return expando;
    }

    #endregion
}
