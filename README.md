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
var clone = FastCloner.DeepClone(new { Hello = "world", MyList = new List<int> { 1 } });
```

⭐ **That's it!** _Feel free to map this method to your extension so if you need to migrate in the future it's a matter of just switching that method. We intentionally don't ship our own `.DeepClone()` extension method._

## Advanced usage

Appart from deep cloning, FastCloner supports shallow cloning and deep cloning _to_ target:

```csharp
// the list is shared between the two instances
var clone = FastCloner.ShallowClone(new { Hello = "world", MyList = new List<int> { 1 } });
```

## Limitations

FastCloner uses caching by default which makes evaluating properties harder. Unmanaged resources, such as `IntPtr`s can't be cloned as there are no metadata for length. `ReadOnly` collections are tested to behave well as long as they follow basic conventions. Many other features, such as cloning `Dictionary`ies properly while keeping hashcodes, `INotifyPropertyChanged`, `delegate`s, `event`s, `HttpRequest`s / responses, and others are supported. If something doesn't work out of the box let me know in the [issues](https://github.com/lofcz/FastCloner/issues), the repository is actively maintained.

## License

[MIT](https://github.com/lofcz/FastCloner/blob/next/LICENSE), simple 💜
