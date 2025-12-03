using System;
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
            
            var openType = typeof(MyCls<>);
            var closedType = openType.MakeGenericType(typeof(Bar));
            var instance = Activator.CreateInstance(closedType);
            
            var bar = new Bar { Name = "test" };
            closedType.GetProperty("Value").SetValue(instance, bar);
            
            // Find the generated extension method
            var extensionsType = typeof(IncludeAttributeTests).Assembly.GetType("FastCloner.Tests.MyClsFastDeepCloneExtensions");
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
}
