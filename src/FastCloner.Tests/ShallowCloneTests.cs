using FastCloner.Tests.Objects;
using System.Threading.Tasks;

namespace FastCloner.Tests;
public class ShallowCloneTests(int maxRecursionDepth) : BaseTestFixture(maxRecursionDepth)
{
    [Test]
    public async Task SimpleObject_Should_Be_Cloned()
    {
        TestObject1 obj = new TestObject1 { Int = 42, Byte = 42, Short = 42, Long = 42, DateTime = new DateTime(2001, 01, 01), Char = 'X', Decimal = 1.2m, Double = 1.3, Float = 1.4f, String = "test1", UInt = 42, ULong = 42, UShort = 42, Bool = true, IntPtr = new IntPtr(42), UIntPtr = new UIntPtr(42), Enum = AttributeTargets.Delegate };

        TestObject1 cloned = obj.ShallowClone();
        await Assert.That(cloned.Byte).IsEqualTo((byte)42);
        await Assert.That(cloned.Short).IsEqualTo((short)42);
        await Assert.That(cloned.UShort).IsEqualTo((ushort)42);
        await Assert.That(cloned.Int).IsEqualTo(42);
        await Assert.That(cloned.UInt).IsEqualTo(42u);
        await Assert.That(cloned.Long).IsEqualTo(42);
        await Assert.That(cloned.ULong).IsEqualTo(42ul);
        await Assert.That(cloned.Decimal).IsEqualTo(1.2m);
        await Assert.That(cloned.Double).IsEqualTo(1.3);
        await Assert.That(cloned.Float).IsEqualTo(1.4f);
        await Assert.That(cloned.Char).IsEqualTo('X');
        await Assert.That(cloned.String).IsEqualTo("test1");
        await Assert.That(cloned.DateTime).IsEqualTo(new DateTime(2001, 1, 1));
        await Assert.That(cloned.Bool).IsEqualTo(true);
        await Assert.That(cloned.IntPtr).IsEqualTo(new IntPtr(42));
        await Assert.That(cloned.UIntPtr).IsEqualTo(new UIntPtr(42));
        await Assert.That(cloned.Enum).IsEqualTo(AttributeTargets.Delegate);
    }

    private class C1
    {
        public object X { get; set; }
    }

    [Test]
    public async Task Reference_Should_Not_Be_Copied()
    {
        C1 c1 = new C1
        {
            X = new object()
        };
        C1 clone = c1.ShallowClone();
        await Assert.That(clone.X).IsEqualTo(c1.X);
    }

    private struct S1 : IDisposable
    {
        public int X;

        public void Dispose()
        {
        }
    }

    [Test]
    public async Task Struct_Should_Be_Cloned()
    {
        S1 c1 = new S1();
        c1.X = 1;
        S1 clone = c1.ShallowClone();
        c1.X = 2;
        await Assert.That(clone.X).IsEqualTo(1);
    }

    [Test]
    public async Task Struct_As_Object_Should_Be_Cloned()
    {
        S1 c1 = new S1();
        c1.X = 1;
        S1 clone = (S1)((IDisposable)c1).ShallowClone();
        c1.X = 2;
        await Assert.That(clone.X).IsEqualTo(1);
    }

    [Test]
    public async Task Struct_As_Interface_Should_Be_Cloned()
    {
        IDoable? c1 = new DoableStruct1() as IDoable;
        await Assert.That(c1.Do()).IsEqualTo(1);
        await Assert.That(c1.Do()).IsEqualTo(2);
        IDoable clone = c1.ShallowClone();
        await Assert.That(c1.Do()).IsEqualTo(3);
        await Assert.That(clone.Do()).IsEqualTo(3);
    }

    [Test]
    public async Task Struct_As_Interface_Should_Be_Cloned_For_DeepClone_Too()
    {
        IDoable? c1 = new DoableStruct1() as IDoable;
        await Assert.That(c1.Do()).IsEqualTo(1);
        await Assert.That(c1.Do()).IsEqualTo(2);
        IDoable clone = c1.DeepClone();
        await Assert.That(c1.Do()).IsEqualTo(3);
        await Assert.That(clone.Do()).IsEqualTo(3);
    }

    [Test]
    public async Task Struct_As_Interface_Should_Be_Cloned_In_Object()
    {
        IDoable? c1 = new DoableStruct1() as IDoable;
        Tuple<IDoable> t = new Tuple<IDoable>(c1);
        await Assert.That(t.Item1.Do()).IsEqualTo(1);
        await Assert.That(t.Item1.Do()).IsEqualTo(2);
        Tuple<IDoable> clone = t.ShallowClone();
        await Assert.That(t.Item1.Do()).IsEqualTo(3);
        // shallow clone do not copy object
        await Assert.That(clone.Item1.Do()).IsEqualTo(4);
    }

    [Test]
    public async Task Struct_As_Interface_Should_Be_Cloned_For_DeepClone_Too_In_Object()
    {
        IDoable? c1 = new DoableStruct1() as IDoable;
        Tuple<IDoable> t = new Tuple<IDoable>(c1);
        await Assert.That(t.Item1.Do()).IsEqualTo(1);
        await Assert.That(t.Item1.Do()).IsEqualTo(2);
        Tuple<IDoable> clone = t.DeepClone();
        await Assert.That(t.Item1.Do()).IsEqualTo(3);
        // deep clone copy object
        await Assert.That(clone.Item1.Do()).IsEqualTo(3);
    }

    [Test]
    public async Task Primitive_Should_Be_Cloned()
    {
        await Assert.That(((object)null).ShallowClone()).IsNull();
        await Assert.That(3.ShallowClone()).IsEqualTo(3);
    }

    [Test]
    public async Task Array_Should_Be_Cloned()
    {
        int[] a = [3, 4];
        int[] clone = a.ShallowClone();
        await Assert.That(clone.Length).IsEqualTo(2);
        await Assert.That(clone[0]).IsEqualTo(3);
        await Assert.That(clone[1]).IsEqualTo(4);
    }
}