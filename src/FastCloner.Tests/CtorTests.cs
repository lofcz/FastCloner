using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace FastCloner.Tests;
public class CtorTests(int maxRecursionDepth) : BaseTestFixture(maxRecursionDepth)
{
    public class T1
    {
        private T1()
        {
        }

        public static T1 Create() => new T1();

        public int X { get; set; }
    }

    public class T2
    {
        public T2(int arg1, int arg2)
        {
        }

        public int X { get; set; }
    }

    public class ExClass
    {
        public ExClass() => throw new Exception();

        public ExClass(string x)
        {
            // does not throw here
        }

        public override bool Equals(object obj) => throw new Exception();

        public override int GetHashCode() => throw new Exception();

        public override string ToString() => throw new Exception();
    }

    [Test]
    public async Task GetOrAdd_ParallelAccess_ShouldBeThreadSafe()
    {
        // Arrange
        int iterations = 1000;
        List<Task> parallelTasks = [];
        ConcurrentDictionary<Type, string> typeCache = new ConcurrentDictionary<Type, string>();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            Task task = Task.Run(() =>
            {
                string value = typeCache.GetOrAdd(typeof(string), t =>
                {
                    Thread.Sleep(10);
                    return "computed value";
                });
            });
            parallelTasks.Add(task);
        }

        // Assert
        await Assert.That(async () => await Task.WhenAll(parallelTasks)).ThrowsNothing();
        await Assert.That(typeCache.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Object_With_Private_Constructor_Should_Be_Cloned()
    {
        T1 t1 = T1.Create();
        t1.X = 42;
        T1 cloned = t1.DeepClone();
        t1.X = 0;
        await Assert.That(cloned.X).IsEqualTo(42);
    }

    [Test]
    public async Task Object_With_Complex_Constructor_Should_Be_Cloned()
    {
        T2 t2 = new T2(1, 2)
        {
            X = 42
        };
        T2 cloned = t2.DeepClone();
        t2.X = 0;
        await Assert.That(cloned.X).IsEqualTo(42);
    }

    [Test]
    public async Task Anonymous_Object_Should_Be_Cloned()
    {
        var t2 = new { A = 1, B = "x" };
        var cloned = t2.DeepClone();
        await Assert.That(cloned.A).IsEqualTo(1);
        await Assert.That(cloned.B).IsEqualTo("x");
    }

    [Test]
    public async Task Cloner_Should_Not_Call_Any_Method_Of_Class_Be_Cloned()
    {
        await Assert.That(() => new ExClass("x").DeepClone()).ThrowsNothing();
        ExClass exClass = new ExClass("x");
        await Assert.That(() => new[] { exClass, exClass }.DeepClone()).ThrowsNothing();
    }
}