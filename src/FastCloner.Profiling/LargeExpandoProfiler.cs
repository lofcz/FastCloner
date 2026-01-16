using System.Diagnostics;
using System.Dynamic;
using Cloner = global::FastCloner.FastCloner;

namespace FastCloner.Profiling;

public static class LargeExpandoProfiler
{
    public static void Run(string[] args)
    {
        Console.WriteLine("Large ExpandoObject Performance Analysis");
        Console.WriteLine("=========================================");
        Console.WriteLine();

        // Parse arguments
        bool interactive = !args.Contains("--no-interactive", StringComparer.OrdinalIgnoreCase);
        int iterations = 100_000;
        int propertyCount = 100;
        
        foreach (string arg in args)
        {
            if (arg.StartsWith("--iterations="))
                int.TryParse(arg[13..], out iterations);
            else if (arg.StartsWith("--properties="))
                int.TryParse(arg[13..], out propertyCount);
        }

        Console.WriteLine($"Iterations: {iterations:N0}");
        Console.WriteLine($"Property count: {propertyCount}");
        Console.WriteLine();

        // Create test objects
        dynamic largeExpando = CreateLargeExpando(propertyCount);
        dynamic onlyPrimitives = CreatePrimitiveOnlyExpando(propertyCount);
        dynamic onlyNested = CreateNestedOnlyExpando(propertyCount);
        dynamic onlyStrings = CreateStringOnlyExpando(propertyCount);

        // Warmup
        Console.WriteLine("Warming up (1000 iterations each)...");
        for (int i = 0; i < 1000; i++)
        {
            _ = Cloner.DeepClone(largeExpando);
            _ = Cloner.DeepClone(onlyPrimitives);
            _ = Cloner.DeepClone(onlyNested);
            _ = Cloner.DeepClone(onlyStrings);
        }
        Console.WriteLine("Warmup complete.");
        Console.WriteLine();

        if (interactive)
        {
            Console.WriteLine(">>> ATTACH PROFILER NOW <<<");
            Console.WriteLine("Press any key to start profiling...");
            Console.ReadKey(true);
            Console.WriteLine();
        }

        // Profile different ExpandoObject configurations
        Console.WriteLine("=== Mixed Properties (Original Benchmark) ===");
        ProfileExpando("Mixed large", largeExpando, iterations);
        Console.WriteLine();

        Console.WriteLine("=== Primitives Only (int/double/decimal) ===");
        ProfileExpando("Primitives only", onlyPrimitives, iterations);
        Console.WriteLine();

        Console.WriteLine("=== Strings Only ===");
        ProfileExpando("Strings only", onlyStrings, iterations);
        Console.WriteLine();

        Console.WriteLine("=== Nested ExpandoObjects Only ===");
        ProfileExpando("Nested only", onlyNested, iterations);
        Console.WriteLine();

        // Profile with different property counts
        Console.WriteLine("=== Scaling by Property Count ===");
        ProfileScaling(iterations / 10);
        Console.WriteLine();

        // Focused micro-benchmark for loop overhead
        Console.WriteLine("=== Loop Overhead Analysis ===");
        ProfileLoopOverhead(largeExpando, iterations);
        Console.WriteLine();

        Console.WriteLine("Profiling complete.");
        if (interactive)
        {
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey(true);
        }
    }

    private static void ProfileExpando(string name, dynamic expando, int iterations)
    {
        // Force GC to get clean memory measurements
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long memBefore = GC.GetTotalMemory(true);
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            _ = Cloner.DeepClone(expando);
        }

        sw.Stop();
        long memAfter = GC.GetTotalMemory(false);

        double usPerOp = sw.Elapsed.TotalMicroseconds / iterations;
        double bytesPerOp = (double)(memAfter - memBefore) / iterations;

        Console.WriteLine($"  {name,-20} {iterations:N0} iters in {sw.ElapsedMilliseconds:N0}ms ({usPerOp:F2}μs/op, ~{bytesPerOp:F0}B/op)");
    }

    private static void ProfileScaling(int iterationsPerTest)
    {
        int[] propertyCounts = { 10, 25, 50, 100, 200, 500 };

        foreach (int count in propertyCounts)
        {
            dynamic expando = CreateLargeExpando(count);
            
            // Warmup
            for (int i = 0; i < 100; i++)
                _ = Cloner.DeepClone(expando);

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
