using System;
using System.Reflection;
using FastCloner.SourceGenerator.Shared;
using FastCloner.Tests;
using System.Threading.Tasks;

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
    [SourceGeneratorCompatible]
    public class IncludeAttributeTests
    {
        [Test]
        [SourceGeneratorCompatible]
        public async Task GenericClassWithInclude_Should_Clone_Included_Type_Via_Reflection()
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
}