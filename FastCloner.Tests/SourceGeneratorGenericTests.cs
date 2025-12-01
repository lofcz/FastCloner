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
    public class GenericClassWithConstraint<T> where T : class, new()
    {
        public T Value { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public void GenericClassWithConstraint_Should_Be_Cloned()
    {
        var original = new GenericClassWithConstraint<List<int>> { Value = new List<int> { 1, 2, 3 } };
        var clone = original.FastDeepClone();

        Assert.That(clone, Is.Not.Null);
        Assert.That(clone.Value, Is.Not.Null);
        Assert.That(clone.Value.Count, Is.EqualTo(3));
        Assert.That(clone.Value, Is.Not.SameAs(original.Value));
    }
}
