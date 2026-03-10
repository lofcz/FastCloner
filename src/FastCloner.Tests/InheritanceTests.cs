using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace FastCloner.Tests;
public class InheritanceTests(int maxRecursionDepth) : BaseTestFixture(maxRecursionDepth)
{
    public class C1 : IDisposable
    {
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Reviewed. Suppression is OK here.")]
        public int X;

        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Reviewed. Suppression is OK here.")]
        public int Y;

        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Reviewed. Suppression is OK here.")]
        public object O; // make it not safe

        public void Dispose()
        {
        }
    }

    public class C2 : C1
    {
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Reviewed. Suppression is OK here.")]
        public new int X;

        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Reviewed. Suppression is OK here.")]
        public int Z;
    }

    public class C1P : IDisposable
    {
        public int X { get; set; }

        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Reviewed. Suppression is OK here.")]
        public int Y { get; set; }

        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Reviewed. Suppression is OK here.")]
        public object O; // make it not safe

        public void Dispose()
        {
        }
    }

    public class C2P : C1P
    {
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Reviewed. Suppression is OK here.")]
        public new int X { get; set; }

        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Reviewed. Suppression is OK here.")]
        public int Z { get; set; }
    }

    public struct S1 : IDisposable
    {
        public C1 X { get; set; }

        public int F;

        public void Dispose()
        {
        }
    }

    public struct S2 : IDisposable
    {
        public IDisposable X { get; set; }

        public void Dispose()
        {
        }
    }

    public class C3
    {
        public C1 X { get; set; }
    }

    [Test]
    public async Task Descendant_Should_Be_Cloned()
    {
        C2 c2 = new C2();
        c2.X = 1;
        c2.Y = 2;
        c2.Z = 3;
        C1 c1 = c2;
        c1.X = 4;
        C1 cloned = c1.DeepClone();
        await Assert.That(cloned).IsTypeOf<C2>();
        await Assert.That(cloned.X).IsEqualTo(4);
        await Assert.That(cloned.Y).IsEqualTo(2);
        await Assert.That(((C2)cloned).Z).IsEqualTo(3);
        await Assert.That(((C2)cloned).X).IsEqualTo(1);
    }

    [Test]
    public async Task Class_Should_Be_Cloned_With_Parents()
    {
        C2P c2 = new C2P();
        c2.X = 1;
        c2.Y = 2;
        c2.Z = 3;
        C1P c1 = c2;
        c1.X = 4;
        C2P cloned = c2.DeepClone();
        c2.X = 100;
        c2.Y = 100;
        c2.Z = 100;
        c1.X = 100;
        await Assert.That(cloned).IsTypeOf<C2P>();
        await Assert.That(((C1P)cloned).X).IsEqualTo(4);
        await Assert.That(cloned.Y).IsEqualTo(2);
        await Assert.That(cloned.Z).IsEqualTo(3);
        await Assert.That(cloned.X).IsEqualTo(1);
    }

    public struct S3
    {
        public C1P X { get; set; }

        public C1P Y { get; set; }
    }

    [Test]
    public async Task Struct_Should_Be_Cloned_With_Class_With_Parents()
    {
        S3 c2 = new S3
        {
            X = new C1P(),
            Y = new C2P()
        };

        c2.X.X = 1;
        c2.X.Y = 2;
        c2.Y.X = 3;
        c2.Y.Y = 4;
        ((C2P)c2.Y).X = 5;
        ((C2P)c2.Y).Z = 6;
        S3 cloned = c2.DeepClone();
        c2.X.X = 100;
        c2.X.Y = 200;
        c2.Y.X = 300;
        c2.Y.Y = 400;
        ((C2P)c2.Y).X = 500;
        ((C2P)c2.Y).Z = 600;
        await Assert.That(cloned).IsTypeOf<S3>();
        await Assert.That(cloned.X.X).IsEqualTo(1);
        await Assert.That(cloned.X.Y).IsEqualTo(2);
        await Assert.That(cloned.Y.X).IsEqualTo(3);
        await Assert.That(cloned.Y.Y).IsEqualTo(4);
        await Assert.That(((C2P)cloned.Y).X).IsEqualTo(5);
        await Assert.That(((C2P)cloned.Y).Z).IsEqualTo(6);
    }

    [Test]
    public async Task Descendant_In_Array_Should_Be_Cloned()
    {
        C1 c1 = new C1();
        C2 c2 = new C2();
        C1[] arr = [c1, c2];

        C1[] cloned = arr.DeepClone();
        await Assert.That(cloned[0]).IsTypeOf<C1>();
        await Assert.That(cloned[1]).IsTypeOf<C2>();
    }

    [Test]
    public async Task Struct_Casted_To_Interface_Should_Be_Cloned()
    {
        S1 s1 = new S1();
        s1.F = 1;
        IDisposable? disp = s1 as IDisposable;
        IDisposable? cloned = disp.DeepClone();
        s1.F = 2;
        await Assert.That(cloned).IsTypeOf<S1>();
        await Assert.That(((S1)cloned).F).IsEqualTo(1);
    }

    public IDisposable Ccc(IDisposable xx)
    {
        S1 x = (S1)xx;
        return x;
    }

    [Test]
    public async Task Class_Casted_To_Object_Should_Be_Cloned()
    {
        C3 c3 = new C3
        {
            X = new C1()
        };
        object obj = c3;
        object cloned = obj.DeepClone();
        await Assert.That(cloned).IsTypeOf<C3>();
        await Assert.That(cloned).IsNotSameReferenceAs(c3);
        await Assert.That(((C3)cloned).X).IsNotNull();
        await Assert.That(((C3)cloned).X).IsNotSameReferenceAs(c3.X);
    }

    [Test]
    public async Task Class_Casted_To_Interface_Should_Be_Cloned()
    {
        C1 c1 = new C1();
        IDisposable disp = c1;
        IDisposable cloned = disp.DeepClone();
        await Assert.That(cloned).IsNotSameReferenceAs(c1);
        await Assert.That(cloned).IsTypeOf<C1>();
    }

    [Test]
    public async Task Struct_Casted_To_Interface_With_Class_As_Interface_Should_Be_Cloned()
    {
        S2 s2 = new S2();
        s2.X = new C1();
        IDisposable? disp = s2 as IDisposable;
        IDisposable? cloned = disp.DeepClone();
        await Assert.That(cloned).IsTypeOf<S2>();
        await Assert.That(((S2)cloned).X).IsTypeOf<C1>();
        await Assert.That(((S2)cloned).X).IsNotEqualTo(s2.X);
    }

    [Test]
    public async Task Array_Of_Struct_Casted_To_Interface_Should_Be_Cloned()
    {
        S1 s1 = new S1();
        IDisposable[] arr = [s1, s1];
        IDisposable[] clonedArr = arr.DeepClone();
        await Assert.That(clonedArr[0]).IsEqualTo(clonedArr[1]);
    }

    public class Safe1
    {
    }

    public class Safe2
    {
    }

    public class Unsafe1 : Safe1
    {
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Reviewed. Suppression is OK here.")]
        public object X;
    }

    public class V1
    {
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Reviewed. Suppression is OK here.")]
        public Safe1 Safe;
    }

    public class V2
    {
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Reviewed. Suppression is OK here.")]
        public Safe1 Safe;

        public V2(string x)
        {
        }
    }

    // these tests are overlapped by others, but for future can be helpful
    [Test]
    public async Task Class_With_Safe_Class_Should_Be_Cloned()
    {
        V1 v = new V1
        {
            Safe = new Safe1()
        };
        V1 v2 = v.DeepClone();
        await Assert.That(v.Safe == v2.Safe).IsFalse();
    }

    [Test]
    public async Task Class_With_Safe_Class_Should_Be_Cloned_No_Default_Constructor()
    {
        V2 v = new V2("X")
        {
            Safe = new Safe1()
        };
        V2 v2 = v.DeepClone();
        await Assert.That(v.Safe == v2.Safe).IsFalse();
    }

    [Test]
    public async Task Class_With_UnSafe_Class_Should_Be_Cloned()
    {
        V1 v = new V1
        {
            Safe = new Unsafe1()
        };
        V1 v2 = v.DeepClone();
        await Assert.That(v.Safe == v2.Safe).IsFalse();
        await Assert.That(v2.Safe.GetType()).IsEqualTo(typeof(Unsafe1));
    }

    [Test]
    public async Task Class_With_UnSafe_Class_Should_Be_Cloned_No_Default_Constructor()
    {
        V2 v = new V2("X")
        {
            Safe = new Unsafe1()
        };
        V2 v2 = v.DeepClone();
        await Assert.That(v.Safe == v2.Safe).IsFalse();
        await Assert.That(v2.Safe.GetType()).IsEqualTo(typeof(Unsafe1));
    }
}