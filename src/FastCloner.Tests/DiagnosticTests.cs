using FastCloner.SourceGenerator;
using FastCloner.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace FastCloner.Tests;
[SourceGeneratorCompatible]
public class DiagnosticTests
{
    // This class tests that the Source Generator correctly reports diagnostics.
    // Since FCG004 is an error that breaks the build, we cannot test it by defining the class directly in the test project.
    // Instead, we use CSharpGeneratorDriver to run the generator on a code string in memory and verify the diagnostics.
    [Test]
    public async Task ClassWithUnclonableMember_And_NoFastClonerRuntime_Should_NOT_Report_FCG004_If_ImplicitlyClonable()
    {
        // Arrange
        string source = @"
using FastCloner.SourceGenerator.Shared;
using System.Collections.Generic;

namespace TestNamespace;

// UnclonableClass is effectively safe (only int member), so it should be implicitly clonable
public class UnclonableClass
{
    public int Value { get; set; }
}

[FastClonerClonable]
[FastClonerSimulateNoRuntime]
public class ClassWithUnclonableMember
{
    public UnclonableClass Member { get; set; }
}
";
        // Act
        ImmutableArray<Diagnostic> diagnostics = RunGenerator(source);

        // Assert
        Diagnostic? fcg004 = diagnostics.FirstOrDefault(d => d.Id == "FCG004");
        await Assert.That(fcg004).IsNull().Because("Should NOT report FCG004 because UnclonableClass is implicitly clonable (only safe members)");
    }

    [Test]
    public async Task ClassWithTrulyUnsafeMember_Should_Report_FCG004()
    {
        // Arrange
        string source = @"
using FastCloner.SourceGenerator.Shared;
using System;

namespace TestNamespace;

public class TrulyUnsafe
{
    public TrulyUnsafe(int x) { } // No parameterless ctor
}

[FastClonerClonable]
[FastClonerSimulateNoRuntime]
public class ClassWithUnsafe
{
    public TrulyUnsafe Member { get; set; }
}
";
        // Act
        ImmutableArray<Diagnostic> diagnostics = RunGenerator(source);

        // Assert
        Diagnostic? fcg004 = diagnostics.FirstOrDefault(d => d.Id == "FCG004");
        await Assert.That(fcg004).IsNotNull().Because("Should report FCG004 for truly unsafe type (no parameterless ctor)");
    }

    [Test]
    public async Task ClassWithHttpClient_Should_Report_FCG004()
    {
        // HttpClient is unsafe (internal state) and has members like BaseAddress (Uri) which are not implicitly clonable
        // (Uri has no parameterless ctor). So it should fail when FastCloner is missing.
        string source = @"
using FastCloner.SourceGenerator.Shared;
using System.Net.Http;

namespace TestNamespace;

[FastClonerClonable]
[FastClonerSimulateNoRuntime]
public class ClassWithHttpClient
{
    public HttpClient Client { get; set; }
}
";
        // Act
        ImmutableArray<Diagnostic> diagnostics = RunGenerator(source);

        // Assert
        Diagnostic? fcg004 = diagnostics.FirstOrDefault(d => d.Id == "FCG004");
        await Assert.That(fcg004).IsNotNull().Because("Should report FCG004 for HttpClient (complex BCL type)");
    }

    [Test]
    public async Task ClassWithIndirectHttpClient_Should_Report_FCG004()
    {
        // Wrapper contains HttpClient. Wrapper itself looks like a POCO, but its member is unsafe.
        // So Wrapper is NOT implicitly clonable.
        // So ClassWithIndirectHttpClient should fail because Wrapper requires FastCloner.
        string source = @"
using FastCloner.SourceGenerator.Shared;
using System.Net.Http;

namespace TestNamespace;

public class WrapperOfHttpClient
{
    public HttpClient Client { get; set; }
}

[FastClonerClonable]
[FastClonerSimulateNoRuntime]
public class ClassWithIndirectHttpClient
{
    public WrapperOfHttpClient Wrapper { get; set; }
}
";
        // Act
        ImmutableArray<Diagnostic> diagnostics = RunGenerator(source);

        // Assert
        Diagnostic? fcg004 = diagnostics.FirstOrDefault(d => d.Id == "FCG004");
        await Assert.That(fcg004).IsNotNull().Because("Should report FCG004 for indirect HttpClient (Wrapper contains unsafe member)");
        await Assert.That(fcg004.GetMessage()).Contains("Wrapper").Because("Should identify Wrapper as the offending member in the root type");
    }

    [Test]
    public async Task GenericClass_And_NoFastClonerRuntime_Should_NOT_Report_FCG004()
    {
        // Arrange
        string source = @"
using FastCloner.SourceGenerator.Shared;
using System.Collections.Generic;

namespace TestNamespace;

[FastClonerClonable]
[FastClonerSimulateNoRuntime]
public class GenericClass<T>
{
    public T Value { get; set; }
}
";
        // Act
        ImmutableArray<Diagnostic> diagnostics = RunGenerator(source);

        // Assert
        Diagnostic? fcg004 = diagnostics.FirstOrDefault(d => d.Id == "FCG004");
        await Assert.That(fcg004).IsNull().Because("Should NOT report FCG004 for generic types, as they now fallback gracefully");
    }
    
    [Test]
    public async Task GenericListClass_Should_Use_FastCloner_For_Items()
    {
        // This test verifies that we can clone a generic list of unclonable items using FastCloner runtime
        // Note: In the test environment, FastCloner runtime IS available, so we don't get FCG004 here.
        
        GenericListClass<UnclonableClass> original = new GenericListClass<UnclonableClass>
        {
            Items =
            [
                new UnclonableClass { Value = 1 },
                new UnclonableClass { Value = 2 }
            ]
        };

        GenericListClass<UnclonableClass> clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone.Items).IsNotNull();
        await Assert.That(clone.Items.Count).IsEqualTo(2);
        await Assert.That(clone.Items[0]).IsNotSameReferenceAs(original.Items[0]); // Should be deep cloned
    }

    [Test]
    public async Task GeneratedCode_ForCollectionWithNonClonableElements_ShouldNotProduceNullableWarnings()
    {
        string source = @"
#nullable enable
using FastCloner.SourceGenerator.Shared;
using System.Collections.Generic;

namespace TestNamespace;

public class NonClonableItem
{
    public NonClonableItem(int x) { Value = x; }
    public int Value { get; set; }
}

[FastClonerClonable]
public class ClassWithNonClonableCollection
{
    public List<NonClonableItem> Items { get; set; } = new();
}
";
        (ImmutableArray<Diagnostic> _, ImmutableArray<Diagnostic> compilationDiags) = RunGeneratorAndCompile(source);

        List<Diagnostic> nullableWarnings = compilationDiags
            .Where(d => d.Id is "CS8604" or "CS8600" or "CS8601" or "CS8603")
            .Where(d => d.Severity == DiagnosticSeverity.Warning)
            .ToList();

        await Assert.That(nullableWarnings).IsEmpty().Because("Generated code should not produce nullable warnings. " +
            $"Found: {string.Join("; ", nullableWarnings.Select(d => $"{d.Id}: {d.GetMessage()}"))}");
    }

    [Test]
    public async Task GeneratedCode_ForArrayWithNonClonableElements_ShouldNotProduceNullableWarnings()
    {
        string source = @"
#nullable enable
using FastCloner.SourceGenerator.Shared;

namespace TestNamespace;

public class NonClonableItem
{
    public NonClonableItem(int x) { Value = x; }
    public int Value { get; set; }
}

[FastClonerClonable]
public class ClassWithNonClonableArray
{
    public NonClonableItem[] Items { get; set; } = [];
}
";
        (ImmutableArray<Diagnostic> _, ImmutableArray<Diagnostic> compilationDiags) = RunGeneratorAndCompile(source);

        List<Diagnostic> nullableWarnings = compilationDiags
            .Where(d => d.Id is "CS8604" or "CS8600" or "CS8601" or "CS8603")
            .Where(d => d.Severity == DiagnosticSeverity.Warning)
            .ToList();

        await Assert.That(nullableWarnings).IsEmpty().Because("Generated code should not produce nullable warnings for arrays. " +
            $"Found: {string.Join("; ", nullableWarnings.Select(d => $"{d.Id}: {d.GetMessage()}"))}");
    }

    [Test]
    public async Task GeneratedCode_ForDictionaryWithNonClonableValues_ShouldNotProduceNullableWarnings()
    {
        string source = @"
#nullable enable
using FastCloner.SourceGenerator.Shared;
using System.Collections.Generic;

namespace TestNamespace;

public class NonClonableItem
{
    public NonClonableItem(int x) { Value = x; }
    public int Value { get; set; }
}

[FastClonerClonable]
public class ClassWithNonClonableDictionary
{
    public Dictionary<string, NonClonableItem> Items { get; set; } = new();
}
";
        (ImmutableArray<Diagnostic> _, ImmutableArray<Diagnostic> compilationDiags) = RunGeneratorAndCompile(source);

        List<Diagnostic> nullableWarnings = compilationDiags
            .Where(d => d.Id is "CS8604" or "CS8600" or "CS8601" or "CS8603")
            .Where(d => d.Severity == DiagnosticSeverity.Warning)
            .ToList();

        await Assert.That(nullableWarnings).IsEmpty().Because("Generated code should not produce nullable warnings for dictionaries. " +
            $"Found: {string.Join("; ", nullableWarnings.Select(d => $"{d.Id}: {d.GetMessage()}"))}");
    }

    [Test]
    public async Task GeneratedCode_ForNullableGetterOnlyObservableCollection_ShouldNotProduceNullableWarnings()
    {
        string source = @"
#nullable enable
using FastCloner.SourceGenerator.Shared;
using System.Collections.ObjectModel;

namespace TestNamespace;

public class InternalClass
{
}

[FastClonerClonable]
public class TestClass1
{
    public ObservableCollection<InternalClass>? Items2
    {
        get;
    }
}";
        (ImmutableArray<Diagnostic> _, ImmutableArray<Diagnostic> compilationDiags) = RunGeneratorAndCompile(source);

        List<Diagnostic> nullableWarnings = compilationDiags
            .Where(d => d.Id is "CS8604" or "CS8602" or "CS8600" or "CS8601" or "CS8603")
            .Where(d => d.Severity == DiagnosticSeverity.Warning)
            .ToList();

        await Assert.That(nullableWarnings).IsEmpty().Because("Generated code should not produce nullable warnings for nullable getter-only ObservableCollection members. " +
            $"Found: {string.Join("; ", nullableWarnings.Select(d => $"{d.Id}: {d.GetMessage()}"))}");
    }

    [Test]
    public async Task GeneratedCode_ForNonNullableGetterOnlyObservableCollection_ShouldNotProduceNullableWarnings()
    {
        string source = @"
#nullable enable
using FastCloner.SourceGenerator.Shared;
using System.Collections.ObjectModel;

namespace TestNamespace;

public class InternalClass
{
}

[FastClonerClonable]
public class TestClass2
{
    public TestClass2()
    {
        Items1 = [];
    }

    public ObservableCollection<InternalClass> Items1
    {
        get;
    }
}";
        (ImmutableArray<Diagnostic> _, ImmutableArray<Diagnostic> compilationDiags) = RunGeneratorAndCompile(source);

        List<Diagnostic> nullableWarnings = compilationDiags
            .Where(d => d.Id is "CS8604" or "CS8602" or "CS8600" or "CS8601" or "CS8603")
            .Where(d => d.Severity == DiagnosticSeverity.Warning)
            .ToList();

        await Assert.That(nullableWarnings).IsEmpty().Because("Generated code should not produce nullable warnings for non-nullable getter-only ObservableCollection members. " +
            $"Found: {string.Join("; ", nullableWarnings.Select(d => $"{d.Id}: {d.GetMessage()}"))}");
    }

    // Helper method to run the generator (returns only generator diagnostics)
    private static ImmutableArray<Diagnostic> RunGenerator(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        
        List<MetadataReference> references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(FastClonerClonableAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("netstandard").Location)
        ];

        CSharpCompilation compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        FastClonerIncrementalGenerator generator = new FastClonerIncrementalGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGenerators(compilation);
        GeneratorDriverRunResult result = driver.GetRunResult();

        return result.Diagnostics;
    }

    /// <summary>
    /// Runs the generator and compiles the result, returning both generator and compilation diagnostics.
    /// Includes FastCloner runtime reference so DeepClone fallback code is generated.
    /// </summary>
    private static (ImmutableArray<Diagnostic> GeneratorDiags, ImmutableArray<Diagnostic> CompilationDiags) RunGeneratorAndCompile(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

        List<MetadataReference> references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(FastClonerClonableAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(FastCloner).Assembly.Location),
            MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Collections").Location),
            MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.ObjectModel").Location),
            MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("netstandard").Location)
        ];

        CSharpCompilation compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        FastClonerIncrementalGenerator generator = new FastClonerIncrementalGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> generatorDiags);

        ImmutableArray<Diagnostic> compilationDiags = outputCompilation.GetDiagnostics();
        return (generatorDiags, compilationDiags);
    }

    public class UnclonableClass
    {
        public int Value { get; set; }
    }

    // Test for List<T> where T is generic - should use FastCloner
    [FastClonerClonable]
    public class GenericListClass<T>
    {
        public List<T> Items { get; set; }
    }
}