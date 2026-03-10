using System.Threading.Tasks;

namespace FastCloner.Tests;
public class GenericTests(int maxRecursionDepth) : BaseTestFixture(maxRecursionDepth)
{
    [Test]
    public async Task Tuple_Should_Be_Cloned()
    {
        Tuple<int, int> c = new Tuple<int, int>(1, 2).DeepClone();
        await Assert.That(c.Item1).IsEqualTo(1);
        await Assert.That(c.Item2).IsEqualTo(2);

        c = new Tuple<int, int>(1, 2).ShallowClone();
        await Assert.That(c.Item1).IsEqualTo(1);
        await Assert.That(c.Item2).IsEqualTo(2);

        Tuple<int, int, int, int, int, int, int> cc = new Tuple<int, int, int, int, int, int, int>(1, 2, 3, 4, 5, 6, 7).DeepClone();
        await Assert.That(cc.Item7).IsEqualTo(7);

        Tuple<int, Generic<object>> tuple = new Tuple<int, Generic<object>>(1, new Generic<object>());
        tuple.Item2.Value = tuple;
        Tuple<int, Generic<object>> ccc = tuple.DeepClone();
        await Assert.That(ReferenceEquals(ccc, ccc.Item2.Value)).IsTrue();
    }

    [Test]
    public async Task Generic_Should_Be_Cloned()
    {
        Generic<int> c = new Generic<int>
        {
            Value = 12
        };
        await Assert.That(c.DeepClone().Value).IsEqualTo(12);

        Generic<object> c2 = new Generic<object>
        {
            Value = 12
        };
        await Assert.That(c2.DeepClone().Value).IsEqualTo(12);
    }

    public class C1
    {
        public int X { get; set; }
    }

    public class C2 : C1
    {
        public int Y { get; set; }
    }

    public class Generic<T>
    {
        public T Value { get; set; } = default!;
    }

    [Test]
    public async Task Tuple_Should_Be_Cloned_With_Inheritance_And_Same_Object()
    {
        C2 c2 = new C2 { X = 1, Y = 2 };
        Tuple<C1, C2> c = new Tuple<C1, C2>(c2, c2).DeepClone();
        Tuple<C1, C2> cs = new Tuple<C1, C2>(c2, c2).ShallowClone();
        c2.X = 42;
        c2.Y = 42;
        await Assert.That(c.Item1.X).IsEqualTo(1);
        await Assert.That(c.Item2.Y).IsEqualTo(2);
        await Assert.That(ReferenceEquals(c.Item2, c.Item1)).IsTrue();

        await Assert.That(cs.Item1.X).IsEqualTo(42);
        await Assert.That(cs.Item2.Y).IsEqualTo(42);
        await Assert.That(ReferenceEquals(cs.Item2, cs.Item1)).IsTrue();
    }
}