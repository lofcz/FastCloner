using FastCloner.SourceGenerator.Shared;
using NUnit.Framework;
using System.Collections.Generic;

namespace FastCloner.Tests;

[TestFixture]
[SourceGeneratorCompatible]
public class ImplicitCollectionTests
{
    // Implicit type (no attribute)
    public class ImplicitItem
    {
        public int Value { get; set; }
    }

    public class ImplicitCollectionContainer
    {
        // List of implicit types - currently fails in source generator analysis
        public List<ImplicitItem> Items { get; set; }
    }

    [FastClonerClonable]
    [FastClonerSimulateNoRuntime]
    public class RootContainer
    {
        public ImplicitCollectionContainer? Middle { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public void ImplicitCollection_Should_Be_Cloned()
    {
        var original = new RootContainer
        {
            Middle = new ImplicitCollectionContainer
            {
                Items = new List<ImplicitItem>
                {
                    new ImplicitItem { Value = 1 },
                    new ImplicitItem { Value = 2 }
                }
            }
        };

        var clone = original.FastDeepClone();

        Assert.That(clone, Is.Not.Null);
        Assert.That(clone.Middle, Is.Not.Null);
        Assert.That(clone.Middle!.Items, Is.Not.Null);
        Assert.That(clone.Middle.Items.Count, Is.EqualTo(2));
        Assert.That(clone.Middle.Items, Is.Not.SameAs(original.Middle.Items));
        Assert.That(clone.Middle.Items[0], Is.Not.SameAs(original.Middle.Items[0]));
        Assert.That(clone.Middle.Items[0].Value, Is.EqualTo(1));
        
        // Modify original
        original.Middle.Items[0].Value = 99;
        
        // Verify independence
        Assert.That(clone.Middle.Items[0].Value, Is.EqualTo(1));
    }
}
