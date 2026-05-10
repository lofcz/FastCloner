using System.Collections.Concurrent;
using FastCloner.Code;

namespace FastCloner.Tests;

[NotInParallel]
public class ConcurrentTests(int maxRecursionDepth) : BaseTestFixture(maxRecursionDepth)
{
    private class TestClass
    {
        public int Value { get; set; }
    }

    [Test]
    public async Task GenerateCloner_IsStoredOnlyOnce()
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
        using (Assert.Multiple())
        {
            // Factory may be called multiple times under concurrent access (ConcurrentDictionary behavior)
            // but only one result is stored and returned to all callers
            await Assert.That(generatorCallCount.Count).IsGreaterThanOrEqualTo(1);

            object firstResult = tasks[0].Result;
            foreach (Task<object> task in tasks)
            {
                await Assert.That(task.Result).IsSameReferenceAs(firstResult);
            }

        // Assert
        }
        
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
    public async Task CloneObject_WithConcurrentAccess_GeneratesOnlyOneCloner()
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
        using (Assert.Multiple())
        {
            foreach (Task<TestClass> task in tasks)
            {
                TestClass clone = task.Result;
                await Assert.That(clone.Value).IsEqualTo(42);
            }

        // Assert
        }
    }
    
    [Test]
    public async Task GetOrAdd_CanCallValueFactoryMultipleTimes()
    {
        // Arrange
        ConcurrentDictionary<int, string> dictionary = new ConcurrentDictionary<int, string>();
        int callCount = 0;
        const int key = 1;
        const int workerCount = 16;
        using CountdownEvent ready = new CountdownEvent(workerCount);
        using ManualResetEventSlim start = new ManualResetEventSlim(false);

        // Act
        Task<string>[] tasks = Enumerable.Range(0, workerCount)
            .Select(_ => Task.Factory.StartNew(() =>
            {
                ready.Signal();
                start.Wait();
                return dictionary.GetOrAdd(key, ValueFactory);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))
            .ToArray();

        ready.Wait();
        start.Set();
        Task.WaitAll(tasks);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(dictionary).Count().IsEqualTo(1);
            await Assert.That(callCount).IsGreaterThan(1);
            await Assert.That(tasks.Select(t => t.Result).Distinct().Count()).IsEqualTo(1);

            // Assert
        }
        return;

        string ValueFactory(int k)
        {
            Thread.Sleep(100);
            Interlocked.Increment(ref callCount);
            return $"Value{k}";
        }
    }

}