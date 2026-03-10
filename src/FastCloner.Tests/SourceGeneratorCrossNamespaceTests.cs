using FastCloner.SourceGenerator.Shared;
using FastCloner.Tests.SubModels;
using System.Threading.Tasks;

// Types in a different namespace to test cross-namespace extension class resolution. #29.
namespace FastCloner.Tests.SubModels
{
    [FastClonerClonable]
    public class SubModelItem
    {
        public int Id { get; set; }
        public string? Label { get; set; }
    }

    [FastClonerClonable]
    public class NestedSubModel
    {
        public int Value { get; set; }
        public SubModelItem? Child { get; set; }
    }
}

namespace FastCloner.Tests
{
    [FastClonerClonable]
    public class CrossNamespaceContainer
    {
        public SubModelItem? Item { get; set; }
        public string? Tag { get; set; }
    }

    [FastClonerClonable]
    public class CrossNamespaceListContainer
    {
        public List<SubModelItem>? Items { get; set; }
    }

    [FastClonerClonable]
    public class CrossNamespaceNestedContainer
    {
        public NestedSubModel? Nested { get; set; }
        public int Code { get; set; }
    }
    public class SourceGeneratorCrossNamespaceTests
    {
        [Test]
        [SourceGeneratorCompatible]
        public async Task CrossNamespace_Direct_Property_Should_Deep_Clone()
        {
            CrossNamespaceContainer original = new CrossNamespaceContainer
            {
                Item = new SubModelItem { Id = 42, Label = "Original" },
                Tag = "test"
            };

            CrossNamespaceContainer clone = original.FastDeepClone();

            await Assert.That(clone).IsNotNull();
            await Assert.That(clone!.Item).IsNotNull();
            await Assert.That(clone.Item).IsNotSameReferenceAs(original.Item);
            await Assert.That(clone.Item!.Id).IsEqualTo(42);
            await Assert.That(clone.Item.Label).IsEqualTo("Original");
            await Assert.That(clone.Tag).IsEqualTo("test");

            clone.Item.Label = "Modified";
            await Assert.That(original.Item!.Label).IsEqualTo("Original");
        }

        [Test]
        [SourceGeneratorCompatible]
        public async Task CrossNamespace_Null_Property_Should_Remain_Null()
        {
            CrossNamespaceContainer original = new CrossNamespaceContainer { Item = null, Tag = "t" };
            CrossNamespaceContainer clone = original.FastDeepClone();

            await Assert.That(clone).IsNotNull();
            await Assert.That(clone!.Item).IsNull();
            await Assert.That(clone.Tag).IsEqualTo("t");
        }
        
        [Test]
        [SourceGeneratorCompatible]
        public async Task CrossNamespace_List_Should_Deep_Clone_Elements()
        {
            CrossNamespaceListContainer original = new CrossNamespaceListContainer
            {
                Items =
                [
                    new SubModelItem { Id = 1, Label = "A" },
                    new SubModelItem { Id = 2, Label = "B" },
                    new SubModelItem { Id = 3, Label = "C" }
                ]
            };

            CrossNamespaceListContainer clone = original.FastDeepClone();

            await Assert.That(clone).IsNotNull();
            await Assert.That(clone!.Items).IsNotNull();
            await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
            await Assert.That(clone.Items!.Count).IsEqualTo(3);
            await Assert.That(clone.Items[0].Id).IsEqualTo(1);
            await Assert.That(clone.Items[1].Label).IsEqualTo("B");
            await Assert.That(clone.Items[2].Id).IsEqualTo(3);

            clone.Items[0].Label = "Modified";
            await Assert.That(original.Items[0].Label).IsEqualTo("A");
        }

        [Test]
        [SourceGeneratorCompatible]
        public async Task CrossNamespace_Null_List_Should_Remain_Null()
        {
            CrossNamespaceListContainer original = new CrossNamespaceListContainer { Items = null };
            CrossNamespaceListContainer clone = original.FastDeepClone();

            await Assert.That(clone).IsNotNull();
            await Assert.That(clone!.Items).IsNull();
        }

        [Test]
        [SourceGeneratorCompatible]
        public async Task CrossNamespace_Nested_Reference_Should_Deep_Clone()
        {
            CrossNamespaceNestedContainer original = new CrossNamespaceNestedContainer
            {
                Nested = new NestedSubModel
                {
                    Value = 99,
                    Child = new SubModelItem { Id = 7, Label = "Deep" }
                },
                Code = 123
            };

            CrossNamespaceNestedContainer clone = original.FastDeepClone();

            await Assert.That(clone).IsNotNull();
            await Assert.That(clone!.Code).IsEqualTo(123);
            await Assert.That(clone.Nested).IsNotNull();
            await Assert.That(clone.Nested).IsNotSameReferenceAs(original.Nested);
            await Assert.That(clone.Nested!.Value).IsEqualTo(99);
            await Assert.That(clone.Nested.Child).IsNotNull();
            await Assert.That(clone.Nested.Child).IsNotSameReferenceAs(original.Nested!.Child);
            await Assert.That(clone.Nested.Child!.Id).IsEqualTo(7);
            await Assert.That(clone.Nested.Child.Label).IsEqualTo("Deep");

            clone.Nested.Child.Label = "Modified";
            await Assert.That(original.Nested.Child!.Label).IsEqualTo("Deep");
        }
    }
}