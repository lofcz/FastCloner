using FastCloner.Code;

namespace FastCloner.Tests;

[TestFixture(Low)]
[TestFixture(High)]
public class CircularTests(int maxRecursionDepth) : BaseTestFixture(maxRecursionDepth)
{
    public struct Wrapper
    {
        public C2 Ref { get; set; }
    }

    public class C2
    {
        public Wrapper W { get; set; }
    }

    public class C1
    {
        public int F { get; set; }

        public C1 A { get; set; }
    }

    public sealed class SealedLoop
    {
        public int Value { get; set; }
        public SealedLoop? Next { get; set; }
    }

    [Test]
    public void SimpleLoop_Should_Be_Handled()
    {
        C1 c1 = new C1();
        C1 c2 = new C1();
        c1.F = 1;
        c2.F = 2;
        c1.A = c2;
        c1.A.A = c1;
        C1 cloned = c1.DeepClone();

        Assert.That(cloned.A, Is.Not.Null);
        Assert.That(cloned.A.A.F, Is.EqualTo(cloned.F));
        Assert.That(cloned.A.A, Is.EqualTo(cloned));
    }

    [Test]
    public void SimpleLoop_Repeated_ExactType_Clone_Should_Be_Handled()
    {
        C1 c1 = new C1();
        C1 c2 = new C1();
        c1.F = 1;
        c2.F = 2;
        c1.A = c2;
        c1.A.A = c1;

        for (int i = 0; i < 5; i++)
        {
            C1 cloned = c1.DeepClone();
            Assert.That(cloned.A, Is.Not.Null);
            Assert.That(cloned.A.A, Is.EqualTo(cloned));
            Assert.That(cloned.A.A.F, Is.EqualTo(cloned.F));
        }
    }

    [Test]
    public void Object_Own_Loop_Should_Be_Handled()
    {
        C1 c1 = new C1
        {
            F = 1
        };
        c1.A = c1;
        C1 cloned = c1.DeepClone();

        Assert.That(cloned.A, Is.Not.Null);
        Assert.That(cloned.A.F, Is.EqualTo(cloned.F));
        Assert.That(cloned.A, Is.EqualTo(cloned));
    }

    [Test]
    public void Sealed_Object_Own_Loop_Should_Be_Handled()
    {
        SealedLoop root = new SealedLoop { Value = 1 };
        root.Next = root;

        SealedLoop cloned = root.DeepClone();

        Assert.That(cloned, Is.Not.SameAs(root));
        Assert.That(cloned.Next, Is.SameAs(cloned));
        Assert.That(cloned.Value, Is.EqualTo(1));
    }

    [Test]
    public void Array_Of_Same_Objects_Should_Be_Cloned()
    {
        C1 c1 = new C1();
        C1[] arr = [c1, c1, c1];
        c1.F = 1;
        C1[] cloned = arr.DeepClone();

        Assert.That(cloned.Length, Is.EqualTo(3));
        Assert.That(cloned[0], Is.EqualTo(cloned[1]));
        Assert.That(cloned[1], Is.EqualTo(cloned[2]));
    }

    [Test]
    public void StructWrappedReferenceLoop_Should_Be_Handled()
    {
        C2 root = new C2();
        root.W = new Wrapper { Ref = root };

        C2 cloned = root.DeepClone();

        Assert.That(cloned, Is.Not.Null);
        Assert.That(cloned, Is.Not.SameAs(root));
        Assert.That(cloned.W.Ref, Is.Not.Null);
        Assert.That(cloned.W.Ref, Is.EqualTo(cloned));
    }

    [Test]
    public void Internal_CloneClassInternal_Should_Reset_CallDepth_After_WorkList_Switch()
    {
        C1 root = new C1
        {
            F = 1,
            A = new C1
            {
                F = 2,
                A = new C1 { F = 3 }
            }
        };

        FastCloner.MaxRecursionDepth = 1;
        FastCloneState state = FastCloneState.Rent();
        try
        {
            C1? clone = (C1?)FastClonerGenerator.CloneClassInternal(root, state);

            Assert.That(clone, Is.Not.Null);
            Assert.That(state.UseWorkList, Is.True);
            Assert.That(state.CurrentDepth, Is.EqualTo(0));
        }
        finally
        {
            FastCloneState.Return(state);
        }
    }
}