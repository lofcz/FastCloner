using FastCloner.SourceGenerator.Shared;
using System.Threading.Tasks;

namespace FastCloner.Tests.TypeNameConflict;

/// <summary>
/// Regression test for https://github.com/AnyMindGroup/FastCloner/issues/37
/// A user-defined "Type" in the same namespace as a FastClonerContext must not
/// shadow System.Type in generated code. The source generator must emit
/// global::System.Type instead of unqualified Type.
/// If the generator emits unqualified "Type", this file will fail to compile.
/// </summary>
public enum Type
{
    A,
    B,
    C
}

public class Widget
{
    public string Name { get; set; } = "";
    public Type Kind { get; set; }
}

[FastClonerRegister(typeof(Widget))]
public partial class WidgetCloningContext : FastClonerContext { }

public class TypeNameConflictTests
{
    [Test]
    public async Task Context_IsHandled_Should_Work_When_Type_Name_Is_Shadowed()
    {
        FastClonerContext ctx = new WidgetCloningContext();

        await Assert.That(ctx.IsHandled(typeof(Widget))).IsTrue();
        await Assert.That(ctx.IsHandled(typeof(string))).IsFalse();
    }

    [Test]
    public async Task Context_TryClone_Should_Work_When_Type_Name_Is_Shadowed()
    {
        FastClonerContext ctx = new WidgetCloningContext();
        Widget original = new Widget { Name = "gear", Kind = Type.B };

        await Assert.That(ctx.TryClone(original, out object? clone)).IsTrue();
        Widget cloned = (Widget)clone!;
        await Assert.That(cloned).IsNotSameReferenceAs(original);
        await Assert.That(cloned.Name).IsEqualTo("gear");
        await Assert.That(cloned.Kind).IsEqualTo(Type.B);
    }

    [Test]
    public async Task Context_Clone_Should_Work_When_Type_Name_Is_Shadowed()
    {
        WidgetCloningContext ctx = new WidgetCloningContext();
        Widget original = new Widget { Name = "spring", Kind = Type.C };

        Widget? cloned = ctx.Clone(original);
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned).IsNotSameReferenceAs(original);
        await Assert.That(cloned!.Name).IsEqualTo("spring");
        await Assert.That(cloned.Kind).IsEqualTo(Type.C);
    }
}
