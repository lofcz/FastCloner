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
        var original = new GenericClass<int> { Value = 42 };
        var clone = original.FastDeepClone();

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
        var myTest = new GenericClassWithConstraint<Dictionary<string, SampleUnannotatedClass>>();
        
        var original = new GenericClassWithConstraint<List<int>> { Value = new List<int> { 1, 2, 3 } };
        var clone = original.FastDeepClone();

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
        
        var openType = typeof(GenericClassWithInclude<>);
        var closedType = openType.MakeGenericType(typeof(Bar));
        var instance = Activator.CreateInstance(closedType);
        
        var bar = new Bar { Name = "test" };
        closedType.GetProperty("Value").SetValue(instance, bar);
        
        // Find the generated extension method
        // Namespace is FastCloner.Tests, Class is GenericClassWithIncludeFastDeepCloneExtensions
        var extensionsType = typeof(SourceGeneratorGenericTests).Assembly.GetType("FastCloner.Tests.GenericClassWithIncludeFastDeepCloneExtensions");
        Assert.That(extensionsType, Is.Not.Null, "Generated extension class not found");
        
        var method = extensionsType.GetMethod("FastDeepClone");
        Assert.That(method, Is.Not.Null, "FastDeepClone method not found");
        
        var genericMethod = method.MakeGenericMethod(typeof(Bar));
        var clone = genericMethod.Invoke(null, new[] { instance });
        
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone.GetType(), Is.EqualTo(closedType));
        Assert.That(clone, Is.Not.SameAs(instance));
        
        var cloneValue = closedType.GetProperty("Value").GetValue(clone);
        Assert.That(cloneValue, Is.Not.SameAs(bar)); // Should be deep cloned if Bar is handled correctly
        Assert.That(((Bar)cloneValue).Name, Is.EqualTo("test"));
    }
}
