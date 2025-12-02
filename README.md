<div align="center">

<img width="512" alt="FastCloner" src="https://github.com/user-attachments/assets/9b6b82a3-892a-4607-9c57-6580ca856a37" />

# FastCloner

**The fastest and most reliable .NET deep cloning library.**    

[![Infidex](https://shields.io/nuget/v/FastCloner?v=304&icon=nuget&label=FastCloner)](https://www.nuget.org/packages/FastCloner)
[![Infidex](https://shields.io/nuget/v/FastCloner.SourceGenerator?v=304&icon=nuget&label=FastCloner.SourceGenerator)](https://www.nuget.org/packages/FastCloner.SourceGenerator)
[![License:MIT](https://img.shields.io/badge/License-MIT-34D058.svg)](https://opensource.org/license/mit)

The fastest deep cloning library, supporting anything from <code>.NET 4.6</code> to modern <code>.NET 10+</code> with no dependencies. FastCloner uses a unique source generator capable of analyzing object graphs and cloning object without explicit annotations. For types that cannot be cloned, such as <code>HttpClient</code>, FastCloner uses a highly optimized reflection-based fallback. Zero dependencies, blazingly fast, built for developers who need cloning that _just works_.
 
</div>

## ✨ Features

- **The Fastest** - Benchmarked to beat all other libraries with third-party independent benchmarks verifing the speed
- **The Most Correct** - Cloning objects is hard: `<T>`, `abstract`, immutables, read-only, pointers, circular dependencies, deeply nested graphs.. we have over [500 tests](https://github.com/lofcz/FastCloner/tree/next/FastCloner.Tests) verifying correct behavior in these cases and we are transparent about the [limitations](https://github.com/lofcz/FastCloner?tab=readme-ov-file#limitations)
- **Novel Algorithm** - FastCloner recognizes that certain that cloning code cannot be generated in certain scenarios and uses highly optimized reflection based approach instead for these types - this only happens for the members that need this, not entire objects
- **Embeddable** - FastCloner has no dependencies outside the standard library. Source generator and reflection parts can be installed independently
- **Gentle & Caring** - FastCloner detects standard attributes like `[NonSerialized]` making it easy to try without polluting codebase with custom attributes. Type usage graph for generics is built automatically producing performant cloning code without manual annotations
- **Easy Integration** - `FastDeepClone()` for AOT cloning, `DeepClone()` for reflection cloning. That's it!
- **Production Ready** - Used by projects like [Jobbr](https://jobbr.readthedocs.io/en/latest), [TarkovSP](https://sp-tarkov.com), and [WinPaletter](https://github.com/Abdelrhman-AK/WinPaletter), with over [150K downloads on NuGet](https://www.nuget.org/packages/fastCloner#usedby-body-tab)
## Getting Started

Install the package via NuGet:

```powershell
dotnet add package FastCloner # Reflection
dotnet add package FastCloner.SourceGenerator # AOT
```

### Clone via Reflection

```csharp
using FastCloner.Code;
var clone = FastCloner.FastCloner.DeepClone(new { Hello = "world", MyList = new List<int> { 1 } });
```

For convenience, add the following method to your project. We intentionally don't ship this extension to make switching from/to FastCloner easier:

```cs
[return: NotNullIfNotNull(nameof(obj))]
public static T? DeepClone<T>(this T? obj)
{
    return FastCloner.FastCloner.DeepClone(obj);
}
```

### Clone via Source Generator

```cs
[FastClonerClonable]
public class GenericClass<T>
{
    public T Value { get; set; }
}

public class MyClass
{
    public string StrVal { get; set; }
}

// only classes where FastDeepClone() extension method should be generated
// need to use [FastClonerClonable]!
var original = new GenericClass<List<MyClass>> { Value = new List<MyClass> { new MyClass { StrVal = "hello world" } } };
var clone = original.FastDeepClone();
```

## Advanced usage

Sometimes, you might want to exclude certain fields/events/properties from cloning:
```csharp
private class TestPropsWithIgnored
{
    [FastClonerIgnore] // <-- decorate with [FastClonerIgnore] or [NonSerialized]
    public string B { get; set; } = "My string";
    public int A { get; set; } = 10;
}

TestPropsWithIgnored original = new TestPropsWithIgnored { A = 42, B = "Test value" };
TestPropsWithIgnored clone = original.DeepClone(); // clone.B is null (default value of a given type)
```

You might also need to exclude certain types from being cloned ever. To do that, put offending types on a blacklist:
```cs
FastCloner.FastCloner.IgnoreType(typeof(PropertyChangedEventHandler)); // or FastCloner.FastCloner.IgnoreTypes([ .. ])
```

If needed, the types can be removed from the blacklist later:
```cs
// note: doing this invalidates precompiled expressions and clears the cache,
// performance will be negatively affected until the cache is repopulated
FastCloner.FastCloner.ClearIgnoredTypes();
```

Cache can be invalidated to reduce the memory footprint, if needed:

```csharp
FastCloner.FastCloner.ClearCache();
```

## Limitations

- Cloning unmanaged resources, such as `IntPtr`s may result in side-effects, as there is no metadata for the length of buffers such pointers often point to.
- `ReadOnly` and `Immutable` collections are tested to behave well if they follow basic conventions.
- With reflection, cloning deeply nested objects switches from recursion to iterative approach on-fly. The threshold for this can be configured by changing `FastCloner.MaxRecursionDepth`, iterative approach is marginally slower.

## Performance

FastCloner is the _fastest_ deep cloning library. It was benchmarked against every library capable of cloning objects I've been able to find:
```md
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.7271)
Intel Core i7-8700 CPU 3.20GHz (Max: 3.19GHz) (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.100

| Method             | Mean        | Error      | StdDev     | Median      | Ratio  | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |------------:|-----------:|-----------:|------------:|-------:|--------:|-----:|-------:|-------:|----------:|------------:|
| FastCloner         |    10.25 ns |   0.219 ns |   0.183 ns |    10.24 ns |   1.00 |    0.02 |    1 | 0.0115 |      - |      72 B |        1.00 |
| DeepCopier         |    23.37 ns |   0.448 ns |   0.582 ns |    23.29 ns |   2.28 |    0.07 |    2 | 0.0115 |      - |      72 B |        1.00 |
| DeepCopy           |    40.56 ns |   3.589 ns |  10.583 ns |    43.56 ns |   3.96 |    1.03 |    3 | 0.0115 |      - |      72 B |        1.00 |
| DeepCopyExpression |   126.05 ns |   3.355 ns |   9.892 ns |   129.32 ns |  12.30 |    0.98 |    4 | 0.0356 |      - |     224 B |        3.11 |
| AutoMapper         |   135.07 ns |   6.097 ns |  17.976 ns |   143.16 ns |  13.18 |    1.76 |    5 | 0.0114 |      - |      72 B |        1.00 |
| DeepCloner         |   261.42 ns |  14.113 ns |  41.614 ns |   282.99 ns |  25.51 |    4.06 |    6 | 0.0367 |      - |     232 B |        3.22 |
| ObjectCloner       |   336.89 ns |  14.249 ns |  42.012 ns |   355.28 ns |  32.87 |    4.12 |    7 | 0.0534 |      - |     336 B |        4.67 |
| MessagePack        |   499.71 ns |  20.831 ns |  61.420 ns |   524.63 ns |  48.75 |    6.02 |    8 | 0.0315 |      - |     200 B |        2.78 |
| ProtobufNet        |   898.60 ns |  34.925 ns | 102.978 ns |   934.13 ns |  87.67 |   10.11 |    9 | 0.0782 |      - |     496 B |        6.89 |
| NClone             |   904.75 ns |  33.559 ns |  98.949 ns |   919.05 ns |  88.27 |    9.73 |    9 | 0.1488 |      - |     936 B |       13.00 |
| SystemTextJson     | 1,687.39 ns |  70.341 ns | 201.821 ns | 1,766.14 ns | 164.63 |   19.79 |   10 | 0.1755 |      - |    1120 B |       15.56 |
| NewtonsoftJson     | 3,147.66 ns | 109.097 ns | 321.676 ns | 3,269.96 ns | 307.10 |   31.67 |   11 | 0.7286 | 0.0038 |    4592 B |       63.78 |
| FastDeepCloner     | 3,970.90 ns | 155.503 ns | 458.505 ns | 4,128.09 ns | 387.41 |   45.01 |   12 | 0.2060 |      - |    1304 B |       18.11 |
| AnyCloneBenchmark  | 5,102.40 ns | 239.089 ns | 704.959 ns | 5,370.93 ns | 497.81 |   68.98 |   13 | 0.9003 |      - |    5656 B |       78.56 |
```

You can run the benchmark [locally](https://github.com/lofcz/FastCloner/blob/next/FastCloner.Benchmark/BenchMinimal.cs) to verify the results. There are also [third-party benchmarks](https://github.com/AnderssonPeter/Dolly?tab=readme-ov-file#benchmarks) in some of competing libraries confirming these results.

## Contributing

If you are looking to add new functionality, please open an issue first to verify your intent is aligned with the scope of the project. The library is covered by over [500 tests](https://github.com/lofcz/FastCloner/tree/next/FastCloner.Tests), please run them against your work before proposing changes. When reporting issues, providing a minimal reproduction we can plug in as a new test greatly reduces turnaround time.

## License

This library is licensed under the [MIT](https://github.com/lofcz/FastCloner/blob/next/LICENSE) license. 💜
