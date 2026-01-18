<div align="center">

<img width="512" alt="FastCloner" src="https://github.com/user-attachments/assets/9b6b82a3-892a-4607-9c57-6580ca856a37" />

# FastCloner

**The fastest and most reliable .NET deep cloning library.**    

[![FastCloner](https://shields.io/nuget/v/FastCloner?v=304&icon=nuget&label=FastCloner)](https://www.nuget.org/packages/FastCloner)
[![FastCloner](https://shields.io/nuget/v/FastCloner.SourceGenerator?v=304&icon=nuget&label=FastCloner.SourceGenerator)](https://www.nuget.org/packages/FastCloner.SourceGenerator)
[![License:MIT](https://img.shields.io/badge/License-MIT-34D058.svg)](https://opensource.org/license/mit)

The fastest deep cloning library, supporting anything from <code>.NET 4.6</code> to modern <code>.NET 10+</code> with no dependencies. FastCloner uses a unique source generator capable of analyzing object graphs and cloning objects without explicit annotations. For types that cannot be cloned, such as <code>HttpClient</code>, FastCloner uses a highly optimized reflection-based fallback. Zero dependencies, blazingly fast, built for developers who need cloning that _just works_.
 
</div>

## ✨ Features

- **The Fastest** - [Benchmarked](https://github.com/lofcz/FastCloner?tab=readme-ov-file#performance) to beat all other libraries with third-party independent benchmarks verifying the performance. **300x** speed-up vs `Newtonsoft.Json` and **160x** vs `System.Text.Json`
- **The Most Correct** - Cloning objects is hard: `<T>`, `abstract`, immutables, read-only, pointers, circular dependencies, deeply nested graphs.. we have over [700 tests](https://github.com/lofcz/FastCloner/tree/next/FastCloner.Tests) verifying correct behavior in these cases and we are transparent about the [limitations](https://github.com/lofcz/FastCloner?tab=readme-ov-file#limitations)
- **Novel Algorithm** - FastCloner recognizes that certain cloning code cannot be generated in certain scenarios and uses highly optimized reflection-based approach instead for these types - this only happens for the members that need this, not entire objects
- **Zero-Overhead Abstractions** - The generator uses call site analysis to eliminate indirection via inlining of generated methods. This ensures the generated code behaves like a single optimized block, just as if you hand-wrote it for maximum performance.
- **Embeddable** - FastCloner has no dependencies outside the standard library. Source generator and reflection parts can be installed independently
- **Gentle & Caring** - FastCloner detects standard attributes like `[NonSerialized]` making it easy to try without polluting codebase with custom attributes. Type usage graph for generics is built automatically producing performant cloning code without manual annotations
- **Easy Integration** - `FastDeepClone()` for AOT cloning, `DeepClone()` for reflection cloning. That's it!
- **Production Ready** - Used by projects like [Jobbr](https://jobbr.readthedocs.io/en/latest), [TarkovSP](https://sp-tarkov.com), and [WinPaletter](https://github.com/Abdelrhman-AK/WinPaletter), with over [200K downloads on NuGet](https://www.nuget.org/packages/fastCloner#usedby-body-tab)
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

// [FastClonerClonable] is only required on types where you call .FastDeepClone()
var original = new GenericClass<List<MyClass>> { Value = new List<MyClass> { new MyClass { StrVal = "hello world" } } };
var clone = original.FastDeepClone();
```

## Advanced Usage

### Customizing Clone Behavior

FastCloner supports behavior attributes that control how types and members are cloned:

| Behavior | Effect |
|----------|--------|
| `Clone` | Deep recursive copy |
| `Reference` | Return original instance unchanged |
| `Shallow` | `MemberwiseClone` without recursion |
| `Ignore` | Return `default` |

#### Compile-time (Attributes)

Apply attributes to **types** or **members**. Member-level attributes override type-level:

```csharp
[FastClonerReference]  // Type-level: all usages preserve reference
public class SharedService { }

public class MyClass
{
    public SharedService Svc { get; set; }      // Uses type-level → Reference
    
    [FastClonerBehavior(CloneBehavior.Clone)]   // Member-level override → Clone
    public SharedService ClonedSvc { get; set; }
    
    [FastClonerIgnore]                          // → null/default
    public CancellationToken Token { get; set; }
    
    [FastClonerShallow]                         // → Reference copied directly
    public ParentNode Parent { get; set; }
}
```

Shorthand attributes: `[FastClonerIgnore]`, `[FastClonerShallow]`, `[FastClonerReference]`  
Explicit: `[FastClonerBehavior(CloneBehavior.X)]`

#### Runtime (Reflection only)

Configure type behavior dynamically. Runtime settings are checked **before** attributes:

```csharp
FastCloner.FastCloner.SetTypeBehavior<MySingleton>(CloneBehavior.Reference);
FastCloner.FastCloner.ClearTypeBehavior<MySingleton>();    // Reset one
FastCloner.FastCloner.ClearAllTypeBehaviors();             // Reset all
```

> **Note**: Changing runtime behavior invalidates the cache. Try to configure once at startup, or use compile-time attributes when possible.

#### Precedence (highest to lowest)

1. Runtime `SetTypeBehavior<T>()` 
2. Member-level attribute
3. Type-level attribute on member's type
4. Default behavior

### Cache Management

```csharp
FastCloner.FastCloner.ClearCache();  // Free memory from reflection cache
```


### Generic Classes and Abstract Types

The source generator automatically discovers which concrete types your generic classes and abstract hierarchies are used with:

**Generic types** - The generator scans your codebase for usages like `MyClass<int>` or `MyClass<Customer>` and generates specialized cloning code:

```cs
[FastClonerClonable]
public class Container<T>
{
    public T Value { get; set; }
}

// Source generator finds this usage and generates cloning code for Container<int>
var container = new Container<int> { Value = 42 };
var clone = container.FastDeepClone();
```

**Abstract classes** - The generator automatically finds all concrete derived types in your codebase:

```cs
[FastClonerClonable]
public abstract class Animal
{
    public string Name { get; set; }
}

public class Dog : Animal
{
    public string Breed { get; set; }
}

public class Cat : Animal
{
    public bool IsIndoor { get; set; }
}

// Cloning via the abstract type works - the generator discovered Dog and Cat
Animal pet = new Dog { Name = "Buddy", Breed = "Labrador" };
Animal clone = pet.FastDeepClone(); // Returns a cloned Dog
```

### Explicitly Including Types

When a type is only used dynamically (not visible at compile time), use `[FastClonerInclude]` to ensure the generator creates cloning code for it:

```cs
[FastClonerClonable]
[FastClonerInclude(typeof(Customer), typeof(Order))] // Include types used dynamically
public class Wrapper<T>
{
    public T Value { get; set; }
}
```

For abstract classes, you can also use `[FastClonerInclude]` to add derived types that aren't in your codebase (e.g., from external assemblies):

```cs
[FastClonerClonable]
[FastClonerInclude(typeof(ExternalPlugin))] // Add external derived types
public abstract class Plugin
{
    public string Name { get; set; }
}
```

### Custom Cloning Context

For advanced scenarios, create a custom cloning context to explicitly register types you want to clone. This is useful when you need a centralized cloning entry point or want to clone types from external assemblies:

```cs
public class Customer
{
    public string Name { get; set; }
    public Address Address { get; set; }
}

public class Address
{
    public string City { get; set; }
}

// Create a context and register types to clone
[FastClonerRegister(typeof(Customer), typeof(Address))]
public partial class MyCloningContext : FastClonerContext { }
```

Using the context:
```cs
MyCloningContext ctx = new MyCloningContext();

// Clone with compile-time type safety
Customer clone = ctx.Clone(original);

// Check if a type is handled by this context
bool handled = ctx.IsHandled(typeof(Customer)); // true

// Try to clone (returns false for unregistered types)
if (ctx.TryClone(obj, out var cloned))
{
    // Successfully cloned
}
```

### Nullability Trust

The generator can be instructed to fully trust nullability annotations. When `[FastClonerTrustNullability]` attribute is applied, FastCloner will skip null checks for non-nullable reference types (e.g., `string` vs `string?`), assuming the contract is valid.

```csharp
[FastClonerClonable]
[FastClonerTrustNullability] // Skip null checks for non-nullable members
public class HighPerformanceDto
{
    public string Id { get; set; } // No null check generated
    public string? Details { get; set; } // Null check still generated
}
```

This eliminates branching and improves performance slightly. If a non-nullable property is actually null at runtime, this may result in a `NullReferenceException` in the generated code.

### Safe Handles

When you have a struct that acts as a handle to internal state or a singleton (where identity matters), use `[FastClonerSafeHandle]`. This tells FastCloner to shallow-copy the readonly fields instead of deep-cloning them, preserving the original internal references.

```csharp
[FastClonerSafeHandle]
public struct MyHandle
{
    private readonly object _internalState; // Preserved (shared), not deep cloned
    public int Value; // Cloned normally
}
```

This is the default behavior for system types like `System.Net.Http.Headers.HeaderDescriptor` to prevent breaking internal framework logic. Use this attribute if your custom structs behave similarly.

### Identity Preservation

By default, FastCloner prioritizes performance by not tracking object identity during cloning. This means if the same object instance appears multiple times in your graph, each reference becomes a separate clone.

For scenarios where you need to preserve object identity (e.g., shared references should remain shared in the clone), use `[FastClonerPreserveIdentity]`:

```csharp
[FastClonerClonable]
[FastClonerPreserveIdentity] // Enable identity tracking for this type
public class Document
{
    public User Author { get; set; }
    public User LastEditor { get; set; } // May reference the same User as Author
}

var doc = new Document { Author = user, LastEditor = user };
var clone = doc.FastDeepClone();
// clone.Author == clone.LastEditor (same cloned instance)
```

The attribute can be applied at type level or member level:

```csharp
[FastClonerClonable]
public class Container
{
    // Only this member tracks identity
    [FastClonerPreserveIdentity]
    public List<Node> Nodes { get; set; }
    
    // This member clones without identity tracking (faster)
    public List<Item> Items { get; set; }
}
```

You can also explicitly disable identity preservation for a member when the type has it enabled:

```csharp
[FastClonerClonable]
[FastClonerPreserveIdentity]
public class Graph
{
    public Node Root { get; set; }
    
    [FastClonerPreserveIdentity(false)] // Opt out for this member
    public List<string> Labels { get; set; }
}
```

> **Note**: Identity preservation adds overhead for tracking seen objects. Circular references are always detected regardless of this setting.

## Limitations

- Cloning unmanaged resources, such as `IntPtr`s may result in side-effects, as there is no metadata for the length of buffers such pointers often point to.
- `ReadOnly` and `Immutable` collections are tested to behave well if they follow basic conventions.
- With reflection, cloning deeply nested objects switches from recursion to iterative approach on the fly. The threshold for this can be configured by changing `FastCloner.MaxRecursionDepth`, iterative approach is marginally slower.

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

You can run the benchmark [locally](https://github.com/lofcz/FastCloner/blob/next/src/FastCloner.Benchmark/BenchMinimal.cs) to verify the results. There are also [third-party benchmarks](https://github.com/AnderssonPeter/Dolly?tab=readme-ov-file#benchmarks) in some of the competing libraries confirming these results.

### Build Times & IDE Performance

FastCloner's source generator is carefully engineered for zero impact on IDE responsiveness and swift build times.

- **Tiered Caching**: We use `ForAttributeWithMetadataName` for highly efficient filtering and strictly separate syntax analysis from code generation.
- **Smart Models**: Roslyn symbols are immediately projected into lightweight, cache-friendly `TypeModel` records. The generator never holds onto compilation symbols, allowing the incremental pipeline to perfectly cache previous results.
- **No Compilation Trashing**: We avoid expensive `CompilationProvider` combinations that break generator caching. Code generation only re-runs when your data models actually change, not on every keystroke or unrelated edit.
- **Allocation Free**: `EquatableArray` collections ensure that change detection is instant and creates no garbage collection pressure.

## Contributing

If you are looking to add new functionality, please open an issue first to verify your intent is aligned with the scope of the project. The library is covered by over [700 tests](https://github.com/lofcz/FastCloner/tree/next/src/FastCloner.Tests), please run them against your work before proposing changes. When reporting issues, providing a minimal reproduction we can plug in as a new test greatly reduces turnaround time.

## License

This library is licensed under the [MIT](https://github.com/lofcz/FastCloner/blob/next/LICENSE) license. 💜
