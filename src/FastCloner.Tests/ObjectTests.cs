using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FastCloner.Tests.Objects;
using System.Threading.Tasks;

namespace FastCloner.Tests;
public class ObjectTests(int maxRecursionDepth) : BaseTestFixture(maxRecursionDepth)
{
    [Test]
    public async Task SimpleObject_Should_Be_Cloned()
    {
        TestObject1 obj = new TestObject1 { Int = 42, Byte = 42, Short = 42, Long = 42, DateTime = new DateTime(2001, 01, 01), Char = 'X', Decimal = 1.2m, Double = 1.3, Float = 1.4f, String = "test1", UInt = 42, ULong = 42, UShort = 42, Bool = true, IntPtr = new IntPtr(42), UIntPtr = new UIntPtr(42), Enum = AttributeTargets.Delegate };

        TestObject1 cloned = obj.DeepClone();
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

    public struct S1
    {
        public int A;
    }

    public struct S2
    {
        public S3 S;
    }

    public struct S3
    {
        public bool B;
    }

    [Test]
    [Property("Description", "We have an special logic for simple structs, so, this test checks that this logic works correctly")]
    public async Task SimpleStruct_Should_Be_Cloned()
    {
        S1 s1 = new S1 { A = 1 };
        S1 cloned = s1.DeepClone();
        await Assert.That(cloned.A).IsEqualTo(1);
    }

    [Test]
    [Property("Description", "We have an special logic for simple structs, so, this test checks that this logic works correctly")]
    public async Task Simple_Struct_With_Child_Should_Be_Cloned()
    {
        S2 s1 = new S2 { S = new S3 { B = true } };
        S2 cloned = s1.DeepClone();
        await Assert.That(cloned.S.B).IsEqualTo(true);
    }

    public class ClassWithNullable
    {
        public int? A { get; set; }

        public long? B { get; set; }
    }

    [Test]
    public async Task Nullable_Shoild_Be_Cloned()
    {
        ClassWithNullable c = new ClassWithNullable { B = 42 };
        ClassWithNullable cloned = c.DeepClone();
        await Assert.That(cloned.A).IsNull();
        await Assert.That(cloned.B).IsEqualTo(42);
    }

    public class C1
    {
        public C2 C { get; set; }
    }

    public class C2
    {
    }

    public class C3
    {
        public string X { get; set; }
    }

    [Test]
    public async Task Class_Should_Be_Cloned()
    {
        C1 c1 = new C1
        {
            C = new C2()
        };
        C1 cloned = c1.DeepClone();
        await Assert.That(cloned.C).IsNotNull();
        await Assert.That(cloned.C).IsNotEqualTo(c1.C);
    }

    public struct S4
    {
        public C2 C;

        public int F;
    }

    [Test]
    public async Task StructWithClass_Should_Be_Cloned()
    {
        S4 c1 = new S4
        {
            F = 1,
            C = new C2()
        };
        S4 cloned = c1.DeepClone();
        c1.F = 2;
        await Assert.That(cloned.C).IsNotNull();
        await Assert.That(cloned.F).IsEqualTo(1);
    }

    [Test]
    public async Task Privitive_Should_Be_Cloned()
    {
        await Assert.That(3.DeepClone()).IsEqualTo(3);
        await Assert.That('x'.DeepClone()).IsEqualTo('x');
        await Assert.That("xxxxxxxxxx yyyyyyyyyyyyyy".DeepClone()).IsEqualTo("xxxxxxxxxx yyyyyyyyyyyyyy");
        await Assert.That(string.Empty.DeepClone()).IsEqualTo(string.Empty);
        await Assert.That(ReferenceEquals("y".DeepClone(), "y")).IsTrue();
        await Assert.That(DateTime.MinValue.DeepClone()).IsEqualTo(DateTime.MinValue);
        await Assert.That(AttributeTargets.Delegate.DeepClone()).IsEqualTo(AttributeTargets.Delegate);
        await Assert.That(((object)null).DeepClone()).IsNull();
        object obj = new object();
        await Assert.That(obj.DeepClone()).IsNotNull();
        await Assert.That(true.DeepClone()).IsTrue();
        await Assert.That((bool)((object)true).DeepClone()).IsTrue();
        await Assert.That(obj.DeepClone().GetType()).IsEqualTo(typeof(object));
        await Assert.That(obj.DeepClone()).IsNotEqualTo(obj);
    }

    [Test]
    public async Task Guid_Should_Be_Cloned()
    {
        Guid g = Guid.NewGuid();
        await Assert.That(g.DeepClone()).IsEqualTo(g);
    }

    private class UnsafeObject
    {
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Reviewed. Suppression is OK here.")]
        public unsafe void* Void;

        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Reviewed. Suppression is OK here.")]
        public unsafe int* Int;
    }

    [Test]
    public async Task Unsafe_Should_Be_Cloned()
    {
        UnsafeObject u = new UnsafeObject();
        int i = 1;
        int j = 2;
        unsafe
        {
            u.Int = &i;
            u.Void = &i;
        }

        UnsafeObject cloned = u.DeepClone();
        bool clonedIntMatches;
        bool clonedVoidMatches;
        unsafe
        {
            u.Int = &j;
            clonedIntMatches = cloned.Int == &i;
            clonedVoidMatches = cloned.Void == &i;
        }

        await Assert.That(clonedIntMatches).IsTrue();
        await Assert.That(clonedVoidMatches).IsTrue();
    }

    [Test]
    public async Task String_In_Class_Should_Not_Be_Cloned()
    {
        C3 c = new C3 { X = "aaa" };
        C3 cloned = c.DeepClone();
        await Assert.That(cloned.X).IsEqualTo(c.X);
        await Assert.That(ReferenceEquals(cloned.X, c.X)).IsTrue();
    }

    public sealed class C6
    {
        public readonly int X = 1;

        private readonly object y = new object();

        // it is struct - and it can't be null, but it's readonly and should be copied
        // also it private to ensure it copied correctly
        #pragma warning disable 169
        private readonly StructWithObject z;
        #pragma warning restore 169

        public object GetY() => y;
    }

    public struct StructWithObject
    {
        public readonly object Z;
    }

    [Test]
    public async Task Object_With_Readonly_Fields_Should_Be_Cloned()
    {
        C6 c = new C6();
        C6 clone = c.DeepClone();
        await Assert.That(clone).IsNotEqualTo(c);
        await Assert.That(clone.X).IsEqualTo(1);
        await Assert.That(clone.GetY()).IsNotNull();
        await Assert.That(clone.GetY()).IsNotEqualTo(c.GetY());
        await Assert.That(clone.GetY()).IsNotEqualTo(c.GetY());
    }

    public class VirtualClass1
    {
        public virtual int A { get; set; }

        public virtual int B { get; set; }

        // not safe
        public object X { get; set; }
    }

    public class VirtualClass2 : VirtualClass1
    {
        public override int B { get; set; }
    }

    [Test]
    [Property("Description", "Nothings special, just for checking")]
    public async Task Class_With_Virtual_Methods_Should_Be_Cloned()
    {
        VirtualClass2 v2 = new VirtualClass2
        {
            A = 1,
            B = 2
        };
        VirtualClass1 v1 = v2;
        v1.A = 3;
        VirtualClass2? clone = v1.DeepClone() as VirtualClass2;
        v2.B = 0;
        v2.A = 0;
        await Assert.That(clone.B).IsEqualTo(2);
        await Assert.That(clone.A).IsEqualTo(3);
    }

    [Test]
    [Property("Description", "DBNull is compared by value, so, we don't need to clone it")]
    public async Task DbNull_Should_Not_Be_Cloned()
    {
        DBNull v = DBNull.Value;
        await Assert.That(v == v.DeepClone()).IsTrue();
        await Assert.That(v == v.ShallowClone()).IsTrue();
    }

    public class EmptyClass {}

    [Test]
    public async Task Empty_Should_Be_Cloned()
    {
        EmptyClass v = new EmptyClass();
        await Assert.That(ReferenceEquals(v, v.DeepClone())).IsFalse();
        await Assert.That(ReferenceEquals(v, v.ShallowClone())).IsFalse();
    }

    [Test]
    [Property("Description", "Reflection classes should not be cloned")]
    public async Task MethodInfo_Should_Not_Be_Cloned()
    {
        MethodInfo? v = GetType().GetMethod("MethodInfo_Should_Not_Be_Cloned");

        await Assert.That(ReferenceEquals(v, v.DeepClone())).IsTrue();
        await Assert.That(ReferenceEquals(v, v.ShallowClone())).IsTrue();
    }

    public class Readonly1
    {
        public readonly object X;

        public object Z = new object();

        public Readonly1(string x) => X = x;
    }

    [Test]
    public async Task Readonly_Field_Should_Remain_ReadOnly()
    {
        Readonly1 c = new Readonly1("Z").DeepClone();
        await Assert.That(c.X).IsEqualTo("Z");
        await Assert.That(typeof(Readonly1).GetField("X").IsInitOnly).IsTrue();
    }

    [Test]
    public async Task System_Type_Should_Not_Be_Cloned()
    {
        // it used for dictionaries as key. there are no sense to copy it
        Type t = GetType(); // RuntimeType
        Type clone = t.DeepClone();
        await Assert.That(ReferenceEquals(t, clone)).IsTrue();
    }
}