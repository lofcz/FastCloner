using System.Reflection;
using FastCloner.SourceGenerator.Shared;

namespace FastCloner.Tests;

[TestFixture]
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
    public void GenericClass_Should_Be_Cloned()
    {
        GenericClass<int> original = new GenericClass<int> { Value = 42 };
        GenericClass<int> clone = original.FastDeepClone();

        Assert.That(clone, Is.Not.Null);
        Assert.That(clone.Value, Is.EqualTo(42));
        Assert.That(clone, Is.Not.SameAs(original));
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
    public void GenericClassWithConstraint_Should_Be_Cloned()
    {
        GenericClassWithConstraint<Dictionary<string, SampleUnannotatedClass>> myTest = new GenericClassWithConstraint<Dictionary<string, SampleUnannotatedClass>>();
        
        GenericClassWithConstraint<List<int>> original = new GenericClassWithConstraint<List<int>> { Value = new List<int> { 1, 2, 3 } };
        GenericClassWithConstraint<List<int>> clone = original.FastDeepClone();

        Assert.That(clone, Is.Not.Null);
        Assert.That(clone.Value, Is.Not.Null);
        Assert.That(clone.Value.Count, Is.EqualTo(3));
        Assert.That(clone.Value, Is.Not.SameAs(original.Value));
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
    public void GenericClassWithInclude_Should_Clone_Included_Type_Via_Reflection()
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
        Assert.That(extensionsType, Is.Not.Null, "Generated extension class not found");
        
        MethodInfo? method = extensionsType.GetMethod("FastDeepClone");
        Assert.That(method, Is.Not.Null, "FastDeepClone method not found");
        
        MethodInfo genericMethod = method.MakeGenericMethod(typeof(Bar));
        object? clone = genericMethod.Invoke(null, new[] { instance });
        
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone.GetType(), Is.EqualTo(closedType));
        Assert.That(clone, Is.Not.SameAs(instance));
        
        object? cloneValue = closedType.GetProperty("Value").GetValue(clone);
        Assert.That(cloneValue, Is.Not.SameAs(bar)); // Should be deep cloned if Bar is handled correctly
        Assert.That(((Bar)cloneValue).Name, Is.EqualTo("test"));
    }
}
