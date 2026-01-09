using System.Diagnostics;
using System.Dynamic;
using Cloner = global::FastCloner.FastCloner;

namespace FastCloner.Profiling;

public static class Program
{
    private const int WarmupIterations = 100;
    private const int ProfileIterations = 10_000;

    public static void Main(string[] args)
    {
        LargeExpandoProfiler.Run(args);
        return;

        Console.WriteLine("FastCloner Dynamic Object Profiling");
        Console.WriteLine("====================================");
        Console.WriteLine();
        Console.WriteLine("Tip: Run with --large to focus on Large ExpandoObject performance.");
        Console.WriteLine();

        // Parse command-line arguments
        bool interactive = !args.Contains("--no-interactive", StringComparer.OrdinalIgnoreCase);
        int iterations = ProfileIterations;
        
        foreach (string arg in args)
        {
            if (int.TryParse(arg, out int customIterations))
            {
                iterations = customIterations;
                break;
            }
        }

        Console.WriteLine($"Warmup iterations: {WarmupIterations}");
        Console.WriteLine($"Profile iterations: {iterations}");
        Console.WriteLine($"Interactive mode: {interactive}");
        Console.WriteLine();

        // Warmup - let JIT compile everything
        Console.WriteLine("Warming up...");
        RunWarmup();
        Console.WriteLine("Warmup complete.");
        Console.WriteLine();

        if (interactive)
        {
            // Attach profiler message
            Console.WriteLine(">>> Attach your profiler now if not already attached <<<");
            Console.WriteLine("Press any key to start profiling run...");
            Console.ReadKey(true);
            Console.WriteLine();
        }

        // Profile each scenario
        ProfileSimpleExpandoObject(iterations);
        ProfileNestedExpandoObject(iterations);
        ProfileExpandoWithCollections(iterations);
        ProfileDeepNestedExpando(iterations);
        ProfileExpandoWithCircularReference(iterations);
        ProfileMixedDynamicAndStaticTypes(iterations);
        ProfileLargeExpandoObject(iterations);

        Console.WriteLine();
        Console.WriteLine("Profiling complete. Analyze the results in your profiler.");
        
        if (interactive)
        {
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey(true);
        }
    }

    private static void RunWarmup()
    {
        // Create sample objects for warmup
        dynamic simple = CreateSimpleExpando();
        dynamic nested = CreateNestedExpando();
        dynamic withCollections = CreateExpandoWithCollections();
        dynamic deep = CreateDeepNestedExpando(5);
        dynamic circular = CreateCircularExpando();
        (object staticObj, dynamic dynamicExpando) = CreateMixedTypes();
        dynamic large = CreateLargeExpando(50);

        for (int i = 0; i < WarmupIterations; i++)
        {
            _ = Cloner.DeepClone(simple);
            _ = Cloner.DeepClone(nested);
            _ = Cloner.DeepClone(withCollections);
            _ = Cloner.DeepClone(deep);
            _ = Cloner.DeepClone(circular);
            _ = Cloner.DeepClone(staticObj);
            _ = Cloner.DeepClone(large);
        }
    }

    #region Simple ExpandoObject

    private static void ProfileSimpleExpandoObject(int iterations)
    {
        Console.WriteLine("Profiling: Simple ExpandoObject");
        dynamic obj = CreateSimpleExpando();
        
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = Cloner.DeepClone(obj);
        }
        sw.Stop();
        
        Console.WriteLine($"  {iterations} iterations in {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds * 1000.0 / iterations:F2}μs/op)");
    }

    private static dynamic CreateSimpleExpando()
    {
        dynamic expando = new ExpandoObject();
        expando.Name = "Test Object";
        expando.Id = 42;
        expando.IsActive = true;
        expando.CreatedAt = DateTime.UtcNow;
        expando.Price = 99.99m;
        return expando;
    }

    #endregion

    #region Nested ExpandoObject

    private static void ProfileNestedExpandoObject(int iterations)
    {
        Console.WriteLine("Profiling: Nested ExpandoObject");
        dynamic obj = CreateNestedExpando();
        
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = Cloner.DeepClone(obj);
        }
        sw.Stop();
        
        Console.WriteLine($"  {iterations} iterations in {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds * 1000.0 / iterations:F2}μs/op)");
    }

    private static dynamic CreateNestedExpando()
    {
        dynamic root = new ExpandoObject();
        root.Name = "Root";
        root.Level = 0;
        
        dynamic child1 = new ExpandoObject();
        child1.Name = "Child1";
        child1.Level = 1;
        child1.Data = "Some data for child 1";
        
        dynamic child2 = new ExpandoObject();
        child2.Name = "Child2";
        child2.Level = 1;
        child2.Value = 123.456;
        
        dynamic grandchild = new ExpandoObject();
        grandchild.Name = "Grandchild";
        grandchild.Level = 2;
        grandchild.Tags = new[] { "tag1", "tag2", "tag3" };
        
        child1.Child = grandchild;
        root.Children = new List<object> { child1, child2 };
        
        return root;
    }

    #endregion

    #region ExpandoObject with Collections

    private static void ProfileExpandoWithCollections(int iterations)
    {
        Console.WriteLine("Profiling: ExpandoObject with Collections");
        dynamic obj = CreateExpandoWithCollections();
        
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = Cloner.DeepClone(obj);
        }
        sw.Stop();
        
        Console.WriteLine($"  {iterations} iterations in {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds * 1000.0 / iterations:F2}μs/op)");
    }

    private static dynamic CreateExpandoWithCollections()
    {
        dynamic expando = new ExpandoObject();
        expando.Name = "Collection Test";
        
        // List of ExpandoObjects
        var items = new List<ExpandoObject>();
        for (int i = 0; i < 10; i++)
        {
            dynamic item = new ExpandoObject();
            item.Index = i;
            item.Label = $"Item {i}";
            item.Value = i * 10.5;
            items.Add(item);
        }
        expando.Items = items;
        
        // Dictionary with ExpandoObject values
        var dict = new Dictionary<string, ExpandoObject>();
        for (int i = 0; i < 5; i++)
        {
            dynamic entry = new ExpandoObject();
            entry.Key = $"key_{i}";
            entry.Data = $"Data for key {i}";
            dict[$"entry_{i}"] = entry;
        }
        expando.Lookup = dict;
        
        // Simple arrays and lists
        expando.Numbers = new[] { 1, 2, 3, 4, 5 };
        expando.Strings = new List<string> { "a", "b", "c" };
        
        return expando;
    }

    #endregion

    #region Deep Nested ExpandoObject

    private static void ProfileDeepNestedExpando(int iterations)
    {
        Console.WriteLine("Profiling: Deep Nested ExpandoObject (10 levels)");
        dynamic obj = CreateDeepNestedExpando(10);
        
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = Cloner.DeepClone(obj);
        }
        sw.Stop();
        
        Console.WriteLine($"  {iterations} iterations in {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds * 1000.0 / iterations:F2}μs/op)");
    }

    private static dynamic CreateDeepNestedExpando(int depth)
    {
        dynamic current = new ExpandoObject();
        current.Level = depth;
        current.Name = $"Level_{depth}";
        current.Data = $"Data at depth {depth}";
        
        if (depth > 0)
        {
            current.Child = CreateDeepNestedExpando(depth - 1);
        }
        
        return current;
    }

    #endregion

    #region ExpandoObject with Circular Reference

    private static void ProfileExpandoWithCircularReference(int iterations)
    {
        Console.WriteLine("Profiling: ExpandoObject with Circular Reference");
        dynamic obj = CreateCircularExpando();
        
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = Cloner.DeepClone(obj);
        }
        sw.Stop();
        
        Console.WriteLine($"  {iterations} iterations in {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds * 1000.0 / iterations:F2}μs/op)");
    }

    private static dynamic CreateCircularExpando()
    {
        dynamic parent = new ExpandoObject();
        parent.Name = "Parent";
        parent.Id = 1;
        
        dynamic child = new ExpandoObject();
        child.Name = "Child";
        child.Id = 2;
        child.Parent = parent; // Circular reference back to parent
        
        parent.Child = child;
        parent.Self = parent; // Self-reference
        
        return parent;
    }

    #endregion

    #region Mixed Dynamic and Static Types

    private static void ProfileMixedDynamicAndStaticTypes(int iterations)
    {
        Console.WriteLine("Profiling: Mixed Dynamic and Static Types");
        (object obj, dynamic _) = CreateMixedTypes();
        
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = Cloner.DeepClone(obj);
        }
        sw.Stop();
        
        Console.WriteLine($"  {iterations} iterations in {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds * 1000.0 / iterations:F2}μs/op)");
    }

    private static (StaticContainer, dynamic) CreateMixedTypes()
    {
        dynamic dynamicConfig = new ExpandoObject();
        dynamicConfig.Setting1 = "Value1";
        dynamicConfig.Setting2 = 42;
        dynamicConfig.Nested = new ExpandoObject();
        dynamicConfig.Nested.SubSetting = "SubValue";
        
        var container = new StaticContainer
        {
            Id = 100,
            Name = "Container",
            DynamicData = dynamicConfig,
            Items = new List<StaticItem>
            {
                new() { ItemId = 1, ItemName = "Item1" },
                new() { ItemId = 2, ItemName = "Item2" },
            }
        };
        
        // Add reference from dynamic back to static
        dynamicConfig.Container = container;
        
        return (container, dynamicConfig);
    }

    #endregion

    #region Large ExpandoObject

    private static void ProfileLargeExpandoObject(int iterations)
    {
        Console.WriteLine("Profiling: Large ExpandoObject (100 properties)");
        dynamic obj = CreateLargeExpando(100);
        
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = Cloner.DeepClone(obj);
        }
        sw.Stop();
        
        Console.WriteLine($"  {iterations} iterations in {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds * 1000.0 / iterations:F2}μs/op)");
    }

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

    #endregion
}

/// <summary>
/// Static type for mixed dynamic/static profiling scenarios
/// </summary>
public class StaticContainer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public dynamic? DynamicData { get; set; }
    public List<StaticItem> Items { get; set; } = new();
}

/// <summary>
/// Static item type for collection tests
/// </summary>
public class StaticItem
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = "";
}
