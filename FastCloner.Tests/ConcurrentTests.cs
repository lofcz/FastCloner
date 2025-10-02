using System.Collections.Concurrent;
using FastCloner.Code;

namespace FastCloner.Tests;

[TestFixture(Low)]
[TestFixture(High)]
public class ConcurrentTests(int maxRecursionDepth) : BaseTestFixture(maxRecursionDepth)
{
    private class TestClass
    {
        public int Value { get; set; }
    }

    [Test]
    public void GenerateCloner_IsCalledOnlyOnce()
    {
        // Arrange
        // clear cache between fixtures
        FastClonerCache.ClearCache();
        
        CountHolder generatorCallCount = new CountHolder();
        Type testType = typeof(TestClassForSingleCallTest);

        // Act
        Task<object>[] tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => 
                FastClonerCache.GetOrAddClass(testType, ValueFactory)))
            .ToArray();

        Task.WaitAll(tasks);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(generatorCallCount.Count, Is.EqualTo(1));
    
            object firstResult = tasks[0].Result;
            foreach (Task<object> task in tasks)
            {
                Assert.That(task.Result, Is.SameAs(firstResult));
            }
        });
        
        return;

        object ValueFactory(Type t)
        {
            Thread.Sleep(100);
            generatorCallCount.Increment();
            return new Func<object, FastCloneState, object>((obj, state) => obj);
        }
    }

    private class TestClassForSingleCallTest
    {
        public int Value { get; set; }
    }
    
    private class CountHolder
    {
        private int count;
        public int Count => count;
    
        public void Increment()
        {
            Interlocked.Increment(ref count);
        }
    }
    
    [Test]
    public void CloneObject_WithConcurrentAccess_GeneratesOnlyOneCloner()
    {
        // Arrange
        TestClass obj = new TestClass { Value = 42 };
        
        // Act
        Task<TestClass>[] tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            return FastClonerGenerator.CloneObject(obj);
        })).ToArray();

        Task.WaitAll(tasks);

        // Assert
        Assert.Multiple(() =>
        {
            foreach (Task<TestClass> task in tasks)
            {
                TestClass clone = task.Result;
                Assert.That(clone.Value, Is.EqualTo(42));
            }
        });
    }
    
    [Test]
    public void GetOrAdd_CanCallValueFactoryMultipleTimes()
    {
        // Arrange
        ConcurrentDictionary<int, string> dictionary = new ConcurrentDictionary<int, string>();
        int callCount = 0;
        const int key = 1;

        // Act
        Task<string>[] tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => dictionary.GetOrAdd(key, ValueFactory)))
            .ToArray();
        
        Task.WaitAll(tasks);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(dictionary, Has.Count.EqualTo(1));
            Assert.That(callCount, Is.GreaterThan(1));
            Assert.That(tasks.Select(t => t.Result).Distinct().Count(), Is.EqualTo(1));
        });
        return;

        string ValueFactory(int k)
        {
            Thread.Sleep(100);
            Interlocked.Increment(ref callCount);
            return $"Value{k}";
        }
    }

}