using FastCloner.SourceGenerator.Shared;
using FastCloner.Tests.SubModels;

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
    #region Container types referencing cross-namespace clonables

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

    #endregion

    [TestFixture]
    public class SourceGeneratorCrossNamespaceTests
    {
        #region Direct property tests

        [Test]
        [SourceGeneratorCompatible]
        public void CrossNamespace_Direct_Property_Should_Deep_Clone()
        {
            CrossNamespaceContainer original = new CrossNamespaceContainer
            {
                Item = new SubModelItem { Id = 42, Label = "Original" },
                Tag = "test"
            };

            CrossNamespaceContainer clone = original.FastDeepClone();

            Assert.That(clone, Is.Not.Null);
            Assert.That(clone!.Item, Is.Not.Null);
            Assert.That(clone.Item, Is.Not.SameAs(original.Item));
            Assert.That(clone.Item!.Id, Is.EqualTo(42));
            Assert.That(clone.Item.Label, Is.EqualTo("Original"));
            Assert.That(clone.Tag, Is.EqualTo("test"));

            clone.Item.Label = "Modified";
            Assert.That(original.Item!.Label, Is.EqualTo("Original"));
        }

        [Test]
        [SourceGeneratorCompatible]
        public void CrossNamespace_Null_Property_Should_Remain_Null()
        {
            CrossNamespaceContainer original = new CrossNamespaceContainer { Item = null, Tag = "t" };
            CrossNamespaceContainer clone = original.FastDeepClone();

            Assert.That(clone, Is.Not.Null);
            Assert.That(clone!.Item, Is.Null);
            Assert.That(clone.Tag, Is.EqualTo("t"));
        }

        #endregion

        #region Collection of cross-namespace elements

        [Test]
        [SourceGeneratorCompatible]
        public void CrossNamespace_List_Should_Deep_Clone_Elements()
        {
            CrossNamespaceListContainer original = new CrossNamespaceListContainer
            {
                Items = new List<SubModelItem>
                {
                    new SubModelItem { Id = 1, Label = "A" },
                    new SubModelItem { Id = 2, Label = "B" },
                    new SubModelItem { Id = 3, Label = "C" }
                }
            };

            CrossNamespaceListContainer clone = original.FastDeepClone();

            Assert.That(clone, Is.Not.Null);
            Assert.That(clone!.Items, Is.Not.Null);
            Assert.That(clone.Items, Is.Not.SameAs(original.Items));
            Assert.That(clone.Items!.Count, Is.EqualTo(3));
            Assert.That(clone.Items[0].Id, Is.EqualTo(1));
            Assert.That(clone.Items[1].Label, Is.EqualTo("B"));
            Assert.That(clone.Items[2].Id, Is.EqualTo(3));

            clone.Items[0].Label = "Modified";
            Assert.That(original.Items[0].Label, Is.EqualTo("A"));
        }

        [Test]
        [SourceGeneratorCompatible]
        public void CrossNamespace_Null_List_Should_Remain_Null()
        {
            CrossNamespaceListContainer original = new CrossNamespaceListContainer { Items = null };
            CrossNamespaceListContainer clone = original.FastDeepClone();

            Assert.That(clone, Is.Not.Null);
            Assert.That(clone!.Items, Is.Null);
        }

        #endregion

        #region Nested cross-namespace references (two levels)

        [Test]
        [SourceGeneratorCompatible]
        public void CrossNamespace_Nested_Reference_Should_Deep_Clone()
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

            Assert.That(clone, Is.Not.Null);
            Assert.That(clone!.Code, Is.EqualTo(123));
            Assert.That(clone.Nested, Is.Not.Null);
            Assert.That(clone.Nested, Is.Not.SameAs(original.Nested));
            Assert.That(clone.Nested!.Value, Is.EqualTo(99));
            Assert.That(clone.Nested.Child, Is.Not.Null);
            Assert.That(clone.Nested.Child, Is.Not.SameAs(original.Nested!.Child));
            Assert.That(clone.Nested.Child!.Id, Is.EqualTo(7));
            Assert.That(clone.Nested.Child.Label, Is.EqualTo("Deep"));

            clone.Nested.Child.Label = "Modified";
            Assert.That(original.Nested.Child!.Label, Is.EqualTo("Deep"));
        }

        #endregion
    }
}
