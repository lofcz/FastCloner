using System.Diagnostics.CodeAnalysis;

namespace DeepCloner.Core.Tests;

[TestFixture]
public class InheritanceSpec
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
    public void Descendant_Should_Be_Cloned()
    {
        C2? c2 = new C2();
        c2.X = 1;
        c2.Y = 2;
        c2.Z = 3;
        C1 c1 = c2;
        c1.X = 4;
        C1? cloned = c1.DeepClone();
        Assert.That(cloned, Is.TypeOf<C2>());
        Assert.That(cloned.X, Is.EqualTo(4));
        Assert.That(cloned.Y, Is.EqualTo(2));
        Assert.That(((C2)cloned).Z, Is.EqualTo(3));
        Assert.That(((C2)cloned).X, Is.EqualTo(1));
    }

    [Test]
    public void Class_Should_Be_Cloned_With_Parents()
    {
        C2P? c2 = new C2P();
        c2.X = 1;
        c2.Y = 2;
        c2.Z = 3;
        C1P c1 = c2;
        c1.X = 4;
        C2P? cloned = c2.DeepClone();
        c2.X = 100;
        c2.Y = 100;
        c2.Z = 100;
        c1.X = 100;
        Assert.That(cloned, Is.TypeOf<C2P>());
        Assert.That(((C1P)cloned).X, Is.EqualTo(4));
        Assert.That(cloned.Y, Is.EqualTo(2));
        Assert.That(cloned.Z, Is.EqualTo(3));
        Assert.That(cloned.X, Is.EqualTo(1));
    }

    public struct S3
    {
        public C1P X { get; set; }

        public C1P Y { get; set; }
    }

    [Test]
    public void Struct_Should_Be_Cloned_With_Class_With_Parents()
    {
        S3 c2 = new S3();
        c2.X = new C1P();
        c2.Y = new C2P();

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
        Assert.That(cloned, Is.TypeOf<S3>());
        Assert.That(cloned.X.X, Is.EqualTo(1));
        Assert.That(cloned.X.Y, Is.EqualTo(2));
        Assert.That(cloned.Y.X, Is.EqualTo(3));
        Assert.That(cloned.Y.Y, Is.EqualTo(4));
        Assert.That(((C2P)cloned.Y).X, Is.EqualTo(5));
        Assert.That(((C2P)cloned.Y).Z, Is.EqualTo(6));
    }

    [Test]
    public void Descendant_In_Array_Should_Be_Cloned()
    {
        C1? c1 = new C1();
        C2? c2 = new C2();
        C1[]? arr = [c1, c2];

        C1[]? cloned = arr.DeepClone();
        Assert.That(cloned[0], Is.TypeOf<C1>());
        Assert.That(cloned[1], Is.TypeOf<C2>());
    }

    [Test]
    public void Struct_Casted_To_Interface_Should_Be_Cloned()
    {
        S1 s1 = new S1();
        s1.F = 1;
        IDisposable? disp = s1 as IDisposable;
        IDisposable? cloned = disp.DeepClone();
        s1.F = 2;
        Assert.That(cloned, Is.TypeOf<S1>());
        Assert.That(((S1)cloned).F, Is.EqualTo(1));
    }

    public IDisposable CCC(IDisposable xx)
    {
        S1 x = (S1)xx;
        return x;
    }

    [Test]
    public void Class_Casted_To_Object_Should_Be_Cloned()
    {
        C3? c3 = new C3();
        c3.X = new C1();
        object obj = c3;
        object? cloned = obj.DeepClone();
        Assert.That(cloned, Is.TypeOf<C3>());
        Assert.That(c3, Is.Not.EqualTo(cloned));
        Assert.That(((C3)cloned).X, Is.Not.Null);
        Assert.That(((C3)cloned).X, Is.Not.EqualTo(c3.X));
    }

    [Test]
    public void Class_Casted_To_Interface_Should_Be_Cloned()
    {
        C1? c1 = new C1();
        IDisposable disp = c1;
        IDisposable? cloned = disp.DeepClone();
        Assert.That(c1, Is.Not.EqualTo(cloned));
        Assert.That(cloned, Is.TypeOf<C1>());
    }

    [Test]
    public void Struct_Casted_To_Interface_With_Class_As_Interface_Should_Be_Cloned()
    {
        S2 s2 = new S2();
        s2.X = new C1();
        IDisposable? disp = s2 as IDisposable;
        IDisposable? cloned = disp.DeepClone();
        Assert.That(cloned, Is.TypeOf<S2>());
        Assert.That(((S2)cloned).X, Is.TypeOf<C1>());
        Assert.That(((S2)cloned).X, Is.Not.EqualTo(s2.X));
    }

    [Test]
    public void Array_Of_Struct_Casted_To_Interface_Should_Be_Cloned()
    {
        S1 s1 = new S1();
        IDisposable[]? arr = [s1, s1];
        IDisposable[]? clonedArr = arr.DeepClone();
        Assert.That(clonedArr[0], Is.EqualTo(clonedArr[1]));
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
    public void Class_With_Safe_Class_Should_Be_Cloned()
    {
        V1? v = new V1();
        v.Safe = new Safe1();
        V1? v2 = v.DeepClone();
        Assert.That(v.Safe == v2.Safe, Is.False);
    }

    [Test]
    public void Class_With_Safe_Class_Should_Be_Cloned_No_Default_Constructor()
    {
        V2? v = new V2("X");
        v.Safe = new Safe1();
        V2? v2 = v.DeepClone();
        Assert.That(v.Safe == v2.Safe, Is.False);
    }

    [Test]
    public void Class_With_UnSafe_Class_Should_Be_Cloned()
    {
        V1? v = new V1();
        v.Safe = new Unsafe1();
        V1? v2 = v.DeepClone();
        Assert.That(v.Safe == v2.Safe, Is.False);
        Assert.That(v2.Safe.GetType(), Is.EqualTo(typeof(Unsafe1)));
    }

    [Test]
    public void Class_With_UnSafe_Class_Should_Be_Cloned_No_Default_Constructor()
    {
        V2? v = new V2("X");
        v.Safe = new Unsafe1();
        V2? v2 = v.DeepClone();
        Assert.That(v.Safe == v2.Safe, Is.False);
        Assert.That(v2.Safe.GetType(), Is.EqualTo(typeof(Unsafe1)));
    }
}