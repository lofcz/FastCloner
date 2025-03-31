[![FastCloner](https://badgen.net/nuget/v/FastCloner?v=302&icon=nuget&label=FastCloner)](https://www.nuget.org/packages/FastCloner)
[![FastCloner.Contrib](https://badgen.net/nuget/v/FastCloner.Contrib?v=302&icon=nuget&label=FastCloner.Contrib)](https://www.nuget.org/packages/FastCloner.Contrib)


# FastCloner

<img align="left" width="128" height="128" alt="Te Reo Icon" src="https://github.com/user-attachments/assets/54f5be37-543a-411d-b6e6-90a77414926c" />
Fast deep cloning library for .NET 8+. Supports both deep and shallow cloning. Extensively tested, focused on performance and stability even on complicated object graphs. FastCloner is designed to work with as few gotchas as possible out of the box. The mapping is zero-config by default. Clone your objects and be done with it <em>fast</em>. FastCloner builds upon <a href="https://github.com/force-net/DeepCloner">DeepClone</a>.

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

⭐ **That's it!** _Feel free to map this method to your extension, so if you need to migrate in the future, it's just a matter of switching that method. We intentionally don't ship our own `.DeepClone()` extension method. If you don't have one yet, copy the following method into your project:_

```cs
[return: NotNullIfNotNull(nameof(obj))]
public static T? DeepClone<T>(this T? obj)
{
    return FastCloner.FastCloner.DeepClone(obj);
}
```

## Advanced usage

_The following examples assume you've copied the extension method above._

Sometimes, you might want to exclude certain fields & properties from cloning:
```csharp
private class TestPropsWithIgnored
{
    [FastClonerIgnore] // <-- decorate such members with [FastClonerIgnore]
    public string B { get; set; } = "My string";

    public int A { get; set; } = 10;
}

TestPropsWithIgnored original = new TestPropsWithIgnored { A = 42, B = "Test value" };
TestPropsWithIgnored clone = original.DeepClone(); // clone.B is null (default value of a given type)
```

Apart from deep cloning, FastCloner supports shallow cloning and deep cloning _to_ target:

```csharp
// the list is shared between the two instances
var clone = FastCloner.FastCloner.ShallowClone(new { Hello = "world", MyList = new List<int> { 1 } });
```

## Limitations

FastCloner uses caching by default, which makes evaluating properties harder. Cloning unmanaged resources, such as `IntPtr`s may result in side-effects, as there is no metadata for the length of buffers such pointers often point to. `ReadOnly` and `Immutable` collections are tested to behave well if they follow basic conventions. Many other features, such as cloning `Dictionary`ies properly while keeping hashcodes, `INotifyPropertyChanged`, `delegate`s, `event`s, `HttpRequest`s / responses, and others are supported. If something doesn't work out of the box, let me know in the [issues](https://github.com/lofcz/FastCloner/issues), the repository is actively maintained.

## Performance

FastCloner aims to _work correctly_ and meet reasonable expectations by default while being fast. Benchmarking results are available [here](https://github.com/lofcz/FastCloner/tree/next/FastCloner.Benchmark), check them out! By default, fast cloner relies on heavily cached reflection to work. An incremental source generator is currently in development as an opt-in alternative for performance-critical scenarios.

## License

[MIT](https://github.com/lofcz/FastCloner/blob/next/LICENSE), simple 💜
