using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace FastCloner.Tests;
public class CollectionTests
{
    [Test]
    public async Task PriorityQueue_Should_Be_Deep_Cloned_Correctly()
    {
        PriorityQueue<string, int> original = new PriorityQueue<string, int>();
        original.Enqueue("Low", 10);
        original.Enqueue("High", 1);
        original.Enqueue("Medium", 5);

        PriorityQueue<string, int> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(3);

        // Verify order
        await Assert.That(clone.Dequeue()).IsEqualTo("High");
        await Assert.That(clone.Dequeue()).IsEqualTo("Medium");
        await Assert.That(clone.Dequeue()).IsEqualTo("Low");

        // Original should remain untouched
        await Assert.That(original.Count).IsEqualTo(3);
        await Assert.That(original.Dequeue()).IsEqualTo("High");
    }

    [Test]
    public async Task Stack_Should_Be_Deep_Cloned_Correctly()
    {
        Stack<int> original = new Stack<int>();
        original.Push(1);
        original.Push(2);
        original.Push(3);

        Stack<int> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(3);

        // Verify order (LIFO)
        await Assert.That(clone.Pop()).IsEqualTo(3);
        await Assert.That(clone.Pop()).IsEqualTo(2);
        await Assert.That(clone.Pop()).IsEqualTo(1);

        // Original should remain untouched
        await Assert.That(original.Count).IsEqualTo(3);
        await Assert.That(original.Peek()).IsEqualTo(3);
    }

    [Test]
    public async Task Queue_Should_Be_Deep_Cloned_Correctly()
    {
        Queue<int> original = new Queue<int>();
        original.Enqueue(1);
        original.Enqueue(2);
        original.Enqueue(3);

        Queue<int> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(3);

        // Verify order (FIFO)
        await Assert.That(clone.Dequeue()).IsEqualTo(1);
        await Assert.That(clone.Dequeue()).IsEqualTo(2);
        await Assert.That(clone.Dequeue()).IsEqualTo(3);

        // Original should remain untouched
        await Assert.That(original.Count).IsEqualTo(3);
        await Assert.That(original.Peek()).IsEqualTo(1);
    }

    [Test]
    public async Task ConcurrentStack_Should_Be_Deep_Cloned_Correctly()
    {
        ConcurrentStack<int> original = new ConcurrentStack<int>();
        original.Push(1);
        original.Push(2);
        original.Push(3);

        ConcurrentStack<int> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(3);

        // Verify order (LIFO)
        int result;
        await Assert.That(clone.TryPop(out result)).IsTrue();
        await Assert.That(result).IsEqualTo(3);

        await Assert.That(clone.TryPop(out result)).IsTrue();
        await Assert.That(result).IsEqualTo(2);

        await Assert.That(clone.TryPop(out result)).IsTrue();
        await Assert.That(result).IsEqualTo(1);

        // Original should remain untouched
        await Assert.That(original.Count).IsEqualTo(3);
        await Assert.That(original.TryPeek(out result)).IsTrue();
        await Assert.That(result).IsEqualTo(3);
    }
    
    [Test]
    public async Task ConcurrentQueue_Should_Be_Deep_Cloned_Correctly()
    {
        ConcurrentQueue<int> original = new ConcurrentQueue<int>();
        original.Enqueue(1);
        original.Enqueue(2);
        original.Enqueue(3);

        ConcurrentQueue<int> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(3);

        // Verify order (FIFO)
        int result;
        await Assert.That(clone.TryDequeue(out result)).IsTrue();
        await Assert.That(result).IsEqualTo(1);

        await Assert.That(clone.TryDequeue(out result)).IsTrue();
        await Assert.That(result).IsEqualTo(2);

        await Assert.That(clone.TryDequeue(out result)).IsTrue();
        await Assert.That(result).IsEqualTo(3);

        // Original should remain untouched
        await Assert.That(original.Count).IsEqualTo(3);
        await Assert.That(original.TryPeek(out result)).IsTrue();
        await Assert.That(result).IsEqualTo(1);
    }

    [Test]
    public async Task BlockingCollection_Should_Be_Deep_Cloned_Correctly()
    {
        BlockingCollection<int> original = new BlockingCollection<int>();
        original.Add(1);
        original.Add(2);
        original.Add(3);

        BlockingCollection<int> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(3);

        // Verify order (FIFO by default)
        await Assert.That(clone.Take()).IsEqualTo(1);
        await Assert.That(clone.Take()).IsEqualTo(2);
        await Assert.That(clone.Take()).IsEqualTo(3);

        // Original should remain untouched
        await Assert.That(original.Count).IsEqualTo(3);
    }

    [Test]
    public async Task LinkedList_Should_Be_Deep_Cloned_Correctly()
    {
        LinkedList<int> original = new LinkedList<int>();
        original.AddLast(1);
        original.AddLast(2);
        original.AddLast(3);

        LinkedList<int> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(3);

        // Verify order
        await Assert.That(clone.First.Value).IsEqualTo(1);
        await Assert.That(clone.First.Next.Value).IsEqualTo(2);
        await Assert.That(clone.Last.Value).IsEqualTo(3);

        // Original should remain untouched
        await Assert.That(original.Count).IsEqualTo(3);
    }
}
