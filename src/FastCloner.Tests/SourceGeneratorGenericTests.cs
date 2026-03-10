using System.Reflection;
using FastCloner.SourceGenerator.Shared;
using System.Threading.Tasks;

namespace FastCloner.Tests;
[SourceGeneratorCompatible]
public class SourceGeneratorGenericTests
{
    [FastClonerClonable]
    public class GenericClass<T>
    {
        public T Value { get; set; } = default!;
    }
    
    [Test]
    [SourceGeneratorCompatible]
    public async Task GenericClass_Should_Be_Cloned()
    {
        GenericClass<int> original = new GenericClass<int> { Value = 42 };
        GenericClass<int> clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone.Value).IsEqualTo(42);
        await Assert.That(clone).IsNotSameReferenceAs(original);
    }
    
    [FastClonerClonable]
    [FastClonerSimulateNoRuntime]
    public class GenericClassWithConstraint<T>
    {
        public T Value { get; set; }
    }

    public class SampleUnannotatedClass
    {
        public List<string> StringList { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task GenericClassWithConstraint_Should_Be_Cloned()
    {
        GenericClassWithConstraint<Dictionary<string, SampleUnannotatedClass>> myTest = new GenericClassWithConstraint<Dictionary<string, SampleUnannotatedClass>>();
        
        GenericClassWithConstraint<List<int>> original = new GenericClassWithConstraint<List<int>> { Value = [1, 2, 3] };
        GenericClassWithConstraint<List<int>> clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone.Value).IsNotNull();
        await Assert.That(clone.Value.Count).IsEqualTo(3);
        await Assert.That(clone.Value).IsNotSameReferenceAs(original.Value);
    }

    public class Bar
    {
        public string Name { get; set; }
    }

    [FastClonerClonable]
    [FastClonerInclude(typeof(Bar))]
    public class GenericClassWithInclude<T>
    {
        public T Value { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public async Task GenericClassWithInclude_Should_Clone_Included_Type_Via_Reflection()
    {
        // We use reflection to create the type and invoke the clone method
        // to ensure that GenericClassWithInclude<Bar> does not appear in the source code
        // as a GenericNameSyntax, which would be picked up by the standard collector.
        // This validates that FastClonerIncludeAttribute is working.
        
        Type openType = typeof(GenericClassWithInclude<>);
        Type closedType = openType.MakeGenericType(typeof(Bar));
        object? instance = Activator.CreateInstance(closedType);
        
        Bar bar = new Bar { Name = "test" };
        closedType.GetProperty("Value").SetValue(instance, bar);
        
        // Find the generated extension method
        // Namespace is FastCloner.Tests, Class is GenericClassWithIncludeFastDeepCloneExtensions
        Type? extensionsType = typeof(SourceGeneratorGenericTests).Assembly.GetType("FastCloner.Tests.GenericClassWithIncludeFastDeepCloneExtensions");
        await Assert.That(extensionsType).IsNotNull().Because("Generated extension class not found");

        MethodInfo? method = extensionsType.GetMethod("FastDeepClone");
        await Assert.That(method).IsNotNull().Because("FastDeepClone method not found");

        MethodInfo genericMethod = method.MakeGenericMethod(typeof(Bar));
        object? clone = genericMethod.Invoke(null, [instance]);
        
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone.GetType()).IsEqualTo(closedType);
        await Assert.That(clone).IsNotSameReferenceAs(instance);

        object? cloneValue = closedType.GetProperty("Value").GetValue(clone);
        await Assert.That(cloneValue).IsNotSameReferenceAs(bar); // Should be deep cloned if Bar is handled correctly
        await Assert.That(((Bar)cloneValue).Name).IsEqualTo("test");
    }
}