using System.Dynamic;
using BenchmarkDotNet.Attributes;
using Force.DeepCloner;

namespace FastCloner.Benchmark;

[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchDynamic
{
    private dynamic _simple = null!;
    private dynamic _nested = null!;
    private dynamic _withCollections = null!;
    private dynamic _deepNested = null!;
    private dynamic _circular = null!;
    private StaticContainer _mixed = null!;
    private dynamic _large = null!;

    [GlobalSetup]
    public void Setup()
    {
        _simple = CreateSimpleExpando();
        _nested = CreateNestedExpando();
        _withCollections = CreateExpandoWithCollections();
        _deepNested = CreateDeepNestedExpando(10);
        _circular = CreateCircularExpando();
        _mixed = CreateMixedTypes();
        _large = CreateLargeExpando(100);
    }

    // Simple ExpandoObject
    /*[Benchmark, BenchmarkCategory("Simple")]
    public object FastCloner_Simple() => FastCloner.DeepClone(_simple);
    
    [Benchmark(Baseline = true), BenchmarkCategory("Simple")]
    public object? DeepCloner_Simple() => _simple.DeepClone();*/

    // Nested ExpandoObject
    [Benchmark, BenchmarkCategory("Nested")]
    public object FastCloner_Nested() => FastCloner.DeepClone(_nested);
    
    [Benchmark(Baseline = true), BenchmarkCategory("Nested")]
    public object? DeepCloner_Nested() => ((object)_nested).DeepClone();

    // ExpandoObject with Collections
    [Benchmark, BenchmarkCategory("Collections")]
    public object FastCloner_Collections() => FastCloner.DeepClone(_withCollections);
    
    [Benchmark(Baseline = true), BenchmarkCategory("Collections")]
    public object? DeepCloner_Collections() => ((object)_withCollections).DeepClone();

    // Deep Nested (10 levels)
    [Benchmark, BenchmarkCategory("DeepNested")]
    public object FastCloner_DeepNested() => FastCloner.DeepClone(_deepNested);
    
    [Benchmark(Baseline = true), BenchmarkCategory("DeepNested")]
    public object? DeepCloner_DeepNested() => ((object)_deepNested).DeepClone();

    // Circular Reference
    [Benchmark, BenchmarkCategory("Circular")]
    public object FastCloner_Circular() => FastCloner.DeepClone(_circular);
    
    [Benchmark(Baseline = true), BenchmarkCategory("Circular")]
    public object? DeepCloner_Circular() => ((object)_circular).DeepClone();

    // Mixed Dynamic + Static Types
    [Benchmark, BenchmarkCategory("Mixed")]
    public object? FastCloner_Mixed() => FastCloner.DeepClone(_mixed);
    
    [Benchmark(Baseline = true), BenchmarkCategory("Mixed")]
    public object? DeepCloner_Mixed() => _mixed.DeepClone();

    // Large ExpandoObject (100 properties)
    [Benchmark, BenchmarkCategory("Large")]
    public object FastCloner_Large() => FastCloner.DeepClone(_large);
    
    [Benchmark(Baseline = true), BenchmarkCategory("Large")]
    public object? DeepCloner_Large() => ((object)_large).DeepClone();

    #region Test Data Creation

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

    private static dynamic CreateExpandoWithCollections()
    {
        dynamic expando = new ExpandoObject();
        expando.Name = "Collection Test";
        
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
        
        var dict = new Dictionary<string, ExpandoObject>();
        for (int i = 0; i < 5; i++)
        {
            dynamic entry = new ExpandoObject();
            entry.Key = $"key_{i}";
            entry.Data = $"Data for key {i}";
            dict[$"entry_{i}"] = entry;
        }
        expando.Lookup = dict;
        
        expando.Numbers = new[] { 1, 2, 3, 4, 5 };
        expando.Strings = new List<string> { "a", "b", "c" };
        
        return expando;
    }

    private static dynamic CreateDeepNestedExpando(int depth)
    {
        dynamic current = new ExpandoObject();
        current.Level = depth;
        current.Name = $"Level_{depth}";
        current.Data = $"Data at depth {depth}";
        
        if (depth > 0)
            current.Child = CreateDeepNestedExpando(depth - 1);
        
        return current;
    }

    private static dynamic CreateCircularExpando()
    {
        dynamic parent = new ExpandoObject();
        parent.Name = "Parent";
        parent.Id = 1;
        
        dynamic child = new ExpandoObject();
        child.Name = "Child";
        child.Id = 2;
        child.Parent = parent;
        
        parent.Child = child;
        parent.Self = parent;
        
        return parent;
    }

    private static StaticContainer CreateMixedTypes()
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
        
        dynamicConfig.Container = container;
        return container;
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
                    dict[$"NestedProp_{i}"] = new
                    {
                        NestedId = i,
                        NestedValue = $"Nested {i}"
                    };
                    break;
            }
        }
        
        return expando;
    }

    #endregion
}

public class StaticContainer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public dynamic? DynamicData { get; set; }
    public List<StaticItem> Items { get; set; } = new();
}

public class StaticItem
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = "";
}
