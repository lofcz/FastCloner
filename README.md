[![FastCloner](https://badgen.net/nuget/v/FastCloner?v=302&icon=nuget&label=FastCloner)](https://www.nuget.org/packages/FastCloner)
[![FastCloner.Contrib](https://badgen.net/nuget/v/FastCloner.Contrib?v=302&icon=nuget&label=FastCloner.Contrib)](https://www.nuget.org/packages/FastCloner.Contrib)

# FastCloner

<img align="left" width="128" height="128" alt="Te Reo Icon" src="https://github.com/user-attachments/assets/54f5be37-543a-411d-b6e6-90a77414926c" />
Fast deep cloning library, supporting anything from <code>.NET 4.6</code> to modern <code>.NET 8+</code>. Implements both deep and shallow cloning. Extensively tested, focused on performance and stability even on complicated object graphs. FastCloner is designed to work with as few gotchas as possible out of the box. The mapping is zero-config by default. Clone your objects and be done with it <em>fast</em>. FastCloner builds upon <a href="https://github.com/force-net/DeepCloner">DeepClone</a>.

<br/><br/>

## Getting Started

Install the package via NuGet:

```powershell
dotnet add package FastCloner
dotnet add package FastCloner.Contrib # only required for some special types, such as Fonts
```

Clone your objects:

```csharp
using FastCloner.Code;
var clone = FastCloner.FastCloner.DeepClone(new { Hello = "world", MyList = new List<int> { 1 } });
```

⭐ **That's it!** _For convenience, please add the following method to your project. We intentionally don't ship this extension to make switching from/to FastCloner easier:_

```cs
[return: NotNullIfNotNull(nameof(obj))]
public static T? DeepClone<T>(this T? obj)
{
    return FastCloner.FastCloner.DeepClone(obj);
}
```

## Advanced usage

_The following examples assume you've copied the extension method above._

Sometimes, you might want to exclude certain fields (including event synthesized) and properties from cloning:
```csharp
private class TestPropsWithIgnored
{
    [FastClonerIgnore] // <-- decorate with [FastClonerIgnore]
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

Apart from deep cloning, FastCloner supports shallow cloning and deep cloning _to_ target:

```csharp
// the list is shared between the two instances
var clone = FastCloner.FastCloner.ShallowClone(new { Hello = "world", MyList = new List<int> { 1 } });
```

## Limitations

FastCloner uses caching by default, which makes evaluating properties harder. Cloning unmanaged resources, such as `IntPtr`s may result in side-effects, as there is no metadata for the length of buffers such pointers often point to. `ReadOnly` and `Immutable` collections are tested to behave well if they follow basic conventions. Many other features, such as cloning `Dictionary`ies properly while keeping hashcodes, `INotifyPropertyChanged`, `delegate`s, `event`s, `HttpRequest`s / responses, and others are supported. If something doesn't work out of the box, let me know in the [issues](https://github.com/lofcz/FastCloner/issues), the repository is actively maintained.

Cache can be invalidated to reduce the memory footprint, if needed:

```csharp
FastCloner.FastCloner.ClearCache();
```

## Performance

FastCloner aims to _work correctly_ and meet reasonable expectations by default while being fast. Benchmarking results are available [here](https://github.com/lofcz/FastCloner/tree/next/FastCloner.Benchmark), check them out! By default, FastCloner relies on heavily cached reflection to work. An incremental source generator is currently in development as an opt-in alternative for performance-critical scenarios.

## Contributing

If you are looking to add new functionality, please open an issue first to verify your intent is aligned with the scope of the project. The library is covered by [~300 tests](https://github.com/lofcz/FastCloner/tree/next/FastCloner.Tests), please run them against your work before proposing changes. When reporting issues, providing a minimal reproduction we can plug in as a new test greatly reduces turnaround time.

## License

This library is licensed under the [MIT](https://github.com/lofcz/FastCloner/blob/next/LICENSE) license. 💜
