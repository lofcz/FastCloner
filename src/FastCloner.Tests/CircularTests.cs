using FastCloner.Code;
using System.Threading.Tasks;

namespace FastCloner.Tests;
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
    public async Task SimpleLoop_Should_Be_Handled()
    {
        C1 c1 = new C1();
        C1 c2 = new C1();
        c1.F = 1;
        c2.F = 2;
        c1.A = c2;
        c1.A.A = c1;
        C1 cloned = c1.DeepClone();

        await Assert.That(cloned.A).IsNotNull();
        await Assert.That(cloned.A.A.F).IsEqualTo(cloned.F);
        await Assert.That(cloned.A.A).IsEqualTo(cloned);
    }

    [Test]
    public async Task SimpleLoop_Repeated_ExactType_Clone_Should_Be_Handled()
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
            await Assert.That(cloned.A).IsNotNull();
            await Assert.That(cloned.A.A).IsEqualTo(cloned);
            await Assert.That(cloned.A.A.F).IsEqualTo(cloned.F);
        }
    }

    [Test]
    public async Task Object_Own_Loop_Should_Be_Handled()
    {
        C1 c1 = new C1
        {
            F = 1
        };
        c1.A = c1;
        C1 cloned = c1.DeepClone();

        await Assert.That(cloned.A).IsNotNull();
        await Assert.That(cloned.A.F).IsEqualTo(cloned.F);
        await Assert.That(cloned.A).IsEqualTo(cloned);
    }

    [Test]
    public async Task Sealed_Object_Own_Loop_Should_Be_Handled()
    {
        SealedLoop root = new SealedLoop { Value = 1 };
        root.Next = root;

        SealedLoop cloned = root.DeepClone();

        await Assert.That(cloned).IsNotSameReferenceAs(root);
        await Assert.That(cloned.Next).IsSameReferenceAs(cloned);
        await Assert.That(cloned.Value).IsEqualTo(1);
    }

    [Test]
    public async Task Array_Of_Same_Objects_Should_Be_Cloned()
    {
        C1 c1 = new C1();
        C1[] arr = [c1, c1, c1];
        c1.F = 1;
        C1[] cloned = arr.DeepClone();

        await Assert.That(cloned.Length).IsEqualTo(3);
        await Assert.That(cloned[0]).IsEqualTo(cloned[1]);
        await Assert.That(cloned[1]).IsEqualTo(cloned[2]);
    }

    [Test]
    public async Task StructWrappedReferenceLoop_Should_Be_Handled()
    {
        C2 root = new C2();
        root.W = new Wrapper { Ref = root };

        C2 cloned = root.DeepClone();

        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned).IsNotSameReferenceAs(root);
        await Assert.That(cloned.W.Ref).IsNotNull();
        await Assert.That(cloned.W.Ref).IsEqualTo(cloned);
    }

    [Test]
    public async Task Internal_CloneClassInternal_Should_Reset_CallDepth_After_WorkList_Switch()
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

        int previousMaxRecursionDepth = FastCloner.MaxRecursionDepth;
        FastCloner.MaxRecursionDepth = 1;
        FastCloneState state = FastCloneState.Rent();
        try
        {
            C1? clone = (C1?)FastClonerGenerator.CloneClassInternal(root, state);

            await Assert.That(clone).IsNotNull();
            await Assert.That(state.UseWorkList).IsTrue();
            await Assert.That(state.CurrentDepth).IsEqualTo(0);
        }
        finally
        {
            FastCloneState.Return(state);
            FastCloner.MaxRecursionDepth = previousMaxRecursionDepth;
        }
    }
}