# FastCloner.SourceGenerator

High-performance source generator for deep cloning objects at compile time.

## Installation

```xml
dotnet add package FastCloner.SourceGenerator
```

The package is automatically configured as a build-time dependency and won't propagate to consuming projects.

## Usage

1. Mark your classes with `[FastClonerClonable]` attribute:

```csharp
using FastCloner.SourceGenerator.Shared;

[FastClonerClonable]
public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
    public List<string> Hobbies { get; set; }
}
```

2. The generator will create a `FastDeepClone()` extension method:

```csharp
var original = new Person { Name = "John", Age = 30, Hobbies = new() { "Reading" } };
var clone = original.FastDeepClone();
```

## Features

- ✅ **Zero reflection** - all cloning logic is generated at compile time
- ✅ **Circular reference detection** - automatically handled when needed
- ✅ **High performance** - up to 60% faster than competing source generators
- ✅ **Collections support** - List, Dictionary, Arrays, etc.
- ✅ **Nullable reference types** - full support
- ✅ **No partial requirement** - works with any class or struct

## Requirements

- .NET Standard 2.0+ or .NET 5+
- C# 9.0+ (for source generators)

