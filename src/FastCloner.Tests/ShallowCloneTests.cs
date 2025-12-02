using FastCloner.Tests.Objects;

namespace FastCloner.Tests;

[TestFixture(Low)]
[TestFixture(High)]
public class ShallowCloneTests(int maxRecursionDepth) : BaseTestFixture(maxRecursionDepth)
{
    [Test]
    public void SimpleObject_Should_Be_Cloned()
    {
        TestObject1 obj = new TestObject1 { Int = 42, Byte = 42, Short = 42, Long = 42, DateTime = new DateTime(2001, 01, 01), Char = 'X', Decimal = 1.2m, Double = 1.3, Float = 1.4f, String = "test1", UInt = 42, ULong = 42, UShort = 42, Bool = true, IntPtr = new IntPtr(42), UIntPtr = new UIntPtr(42), Enum = AttributeTargets.Delegate };

        TestObject1 cloned = obj.ShallowClone();
        Assert.That(cloned.Byte, Is.EqualTo(42));
        Assert.That(cloned.Short, Is.EqualTo(42));
        Assert.That(cloned.UShort, Is.EqualTo(42));
        Assert.That(cloned.Int, Is.EqualTo(42));
        Assert.That(cloned.UInt, Is.EqualTo(42));
        Assert.That(cloned.Long, Is.EqualTo(42));
        Assert.That(cloned.ULong, Is.EqualTo(42));
        Assert.That(cloned.Decimal, Is.EqualTo(1.2));
        Assert.That(cloned.Double, Is.EqualTo(1.3));
        Assert.That(cloned.Float, Is.EqualTo(1.4f));
        Assert.That(cloned.Char, Is.EqualTo('X'));
        Assert.That(cloned.String, Is.EqualTo("test1"));
        Assert.That(cloned.DateTime, Is.EqualTo(new DateTime(2001, 1, 1)));
        Assert.That(cloned.Bool, Is.EqualTo(true));
        Assert.That(cloned.IntPtr, Is.EqualTo(new IntPtr(42)));
        Assert.That(cloned.UIntPtr, Is.EqualTo(new UIntPtr(42)));
        Assert.That(cloned.Enum, Is.EqualTo(AttributeTargets.Delegate));
    }

    private class C1
    {
        public object X { get; set; }
    }

    [Test]
    public void Reference_Should_Not_Be_Copied()
    {
        C1 c1 = new C1
        {
            X = new object()
        };
        C1 clone = c1.ShallowClone();
        Assert.That(clone.X, Is.EqualTo(c1.X));
    }

    private struct S1 : IDisposable
    {
        public int X;

        public void Dispose()
        {
        }
    }

    [Test]
    public void Struct_Should_Be_Cloned()
    {
        S1 c1 = new S1();
        c1.X = 1;
        S1 clone = c1.ShallowClone();
        c1.X = 2;
        Assert.That(clone.X, Is.EqualTo(1));
    }

    [Test]
    public void Struct_As_Object_Should_Be_Cloned()
    {
        S1 c1 = new S1();
        c1.X = 1;
        S1 clone = (S1)((IDisposable)c1).ShallowClone();
        c1.X = 2;
        Assert.That(clone.X, Is.EqualTo(1));
    }

    [Test]
    public void Struct_As_Interface_Should_Be_Cloned()
    {
        IDoable? c1 = new DoableStruct1() as IDoable;
        Assert.That(c1.Do(), Is.EqualTo(1));
        Assert.That(c1.Do(), Is.EqualTo(2));
        IDoable clone = c1.ShallowClone();
        Assert.That(c1.Do(), Is.EqualTo(3));
        Assert.That(clone.Do(), Is.EqualTo(3));
    }

    [Test]
    public void Struct_As_Interface_Should_Be_Cloned_For_DeepClone_Too()
    {
        IDoable? c1 = new DoableStruct1() as IDoable;
        Assert.That(c1.Do(), Is.EqualTo(1));
        Assert.That(c1.Do(), Is.EqualTo(2));
        IDoable clone = c1.DeepClone();
        Assert.That(c1.Do(), Is.EqualTo(3));
        Assert.That(clone.Do(), Is.EqualTo(3));
    }

    [Test]
    public void Struct_As_Interface_Should_Be_Cloned_In_Object()
    {
        IDoable? c1 = new DoableStruct1() as IDoable;
        Tuple<IDoable> t = new Tuple<IDoable>(c1);
        Assert.That(t.Item1.Do(), Is.EqualTo(1));
        Assert.That(t.Item1.Do(), Is.EqualTo(2));
        Tuple<IDoable> clone = t.ShallowClone();
        Assert.That(t.Item1.Do(), Is.EqualTo(3));
        // shallow clone do not copy object
        Assert.That(clone.Item1.Do(), Is.EqualTo(4));
    }

    [Test]
    public void Struct_As_Interface_Should_Be_Cloned_For_DeepClone_Too_In_Object()
    {
        IDoable? c1 = new DoableStruct1() as IDoable;
        Tuple<IDoable> t = new Tuple<IDoable>(c1);
        Assert.That(t.Item1.Do(), Is.EqualTo(1));
        Assert.That(t.Item1.Do(), Is.EqualTo(2));
        Tuple<IDoable> clone = t.DeepClone();
        Assert.That(t.Item1.Do(), Is.EqualTo(3));
        // deep clone copy object
        Assert.That(clone.Item1.Do(), Is.EqualTo(3));
    }

    [Test]
    public void Primitive_Should_Be_Cloned()
    {
        Assert.That(((object)null).ShallowClone(), Is.Null);
        Assert.That(3.ShallowClone(), Is.EqualTo(3));
    }

    [Test]
    public void Array_Should_Be_Cloned()
    {
        int[] a = [3, 4];
        int[] clone = a.ShallowClone();
        Assert.That(clone.Length, Is.EqualTo(2));
        Assert.That(clone[0], Is.EqualTo(3));
        Assert.That(clone[1], Is.EqualTo(4));
    }
}