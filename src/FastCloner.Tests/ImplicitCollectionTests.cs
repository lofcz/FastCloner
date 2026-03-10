using FastCloner.SourceGenerator.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FastCloner.Tests;
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
    public async Task ImplicitCollection_Should_Be_Cloned()
    {
        RootContainer original = new RootContainer
        {
            Middle = new ImplicitCollectionContainer
            {
                Items =
                [
                    new ImplicitItem { Value = 1 },
                    new ImplicitItem { Value = 2 }
                ]
            }
        };

        RootContainer clone = original.FastDeepClone();

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone.Middle).IsNotNull();
        await Assert.That(clone.Middle!.Items).IsNotNull();
        await Assert.That(clone.Middle.Items.Count).IsEqualTo(2);
        await Assert.That(clone.Middle.Items).IsNotSameReferenceAs(original.Middle.Items);
        await Assert.That(clone.Middle.Items[0]).IsNotSameReferenceAs(original.Middle.Items[0]);
        await Assert.That(clone.Middle.Items[0].Value).IsEqualTo(1);

        // Modify original
        original.Middle.Items[0].Value = 99;
        
        // Verify independence
        await Assert.That(clone.Middle.Items[0].Value).IsEqualTo(1);
    }
}