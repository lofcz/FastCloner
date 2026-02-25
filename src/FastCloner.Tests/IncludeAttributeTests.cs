using System;
using System.Reflection;
using FastCloner.SourceGenerator.Shared;
using FastCloner.Tests;
using NUnit.Framework;

namespace FastCloner.Tests
{
    public class Bar
    {
        public string Name { get; set; }
    }

    [FastClonerClonable]
    [FastClonerInclude(typeof(Bar), typeof(string), typeof(List<string>))]
    public class MyCls<T>
    {
        public T Value { get; set; }
    }

    [TestFixture]
    [SourceGeneratorCompatible]
    public class IncludeAttributeTests
    {
        [Test]
        [SourceGeneratorCompatible]
        public void GenericClassWithInclude_Should_Clone_Included_Type_Via_Reflection()
        {
            // We use reflection to create the type and invoke the clone method
            // to ensure that MyCls<Bar> does not appear in the source code
            // as a GenericNameSyntax, which would be picked up by the standard collector.
            // This validates that FastClonerIncludeAttribute is working.
            
            Type openType = typeof(MyCls<>);
            Type closedType = openType.MakeGenericType(typeof(Bar));
            object? instance = Activator.CreateInstance(closedType);
            
            Bar bar = new Bar { Name = "test" };
            closedType.GetProperty("Value").SetValue(instance, bar);
            
            // Find the generated extension method
            Type? extensionsType = typeof(IncludeAttributeTests).Assembly.GetType("FastCloner.Tests.MyClsFastDeepCloneExtensions");
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
}
