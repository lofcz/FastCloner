using System.Collections.Concurrent;

namespace FastCloner.Tests;

[TestFixture]
public class CollectionTests
{
    [Test]
    public void PriorityQueue_Should_Be_Deep_Cloned_Correctly()
    {
        PriorityQueue<string, int> original = new PriorityQueue<string, int>();
        original.Enqueue("Low", 10);
        original.Enqueue("High", 1);
        original.Enqueue("Medium", 5);

        PriorityQueue<string, int> clone = original.DeepClone();

        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone.Count, Is.EqualTo(3));

        // Verify order
        Assert.That(clone.Dequeue(), Is.EqualTo("High"));
        Assert.That(clone.Dequeue(), Is.EqualTo("Medium"));
        Assert.That(clone.Dequeue(), Is.EqualTo("Low"));

        // Original should remain untouched
        Assert.That(original.Count, Is.EqualTo(3));
        Assert.That(original.Dequeue(), Is.EqualTo("High"));
    }

    [Test]
    public void Stack_Should_Be_Deep_Cloned_Correctly()
    {
        Stack<int> original = new Stack<int>();
        original.Push(1);
        original.Push(2);
        original.Push(3);

        Stack<int> clone = original.DeepClone();

        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone.Count, Is.EqualTo(3));

        // Verify order (LIFO)
        Assert.That(clone.Pop(), Is.EqualTo(3));
        Assert.That(clone.Pop(), Is.EqualTo(2));
        Assert.That(clone.Pop(), Is.EqualTo(1));

        // Original should remain untouched
        Assert.That(original.Count, Is.EqualTo(3));
        Assert.That(original.Peek(), Is.EqualTo(3));
    }

    [Test]
    public void Queue_Should_Be_Deep_Cloned_Correctly()
    {
        Queue<int> original = new Queue<int>();
        original.Enqueue(1);
        original.Enqueue(2);
        original.Enqueue(3);

        Queue<int> clone = original.DeepClone();

        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone.Count, Is.EqualTo(3));

        // Verify order (FIFO)
        Assert.That(clone.Dequeue(), Is.EqualTo(1));
        Assert.That(clone.Dequeue(), Is.EqualTo(2));
        Assert.That(clone.Dequeue(), Is.EqualTo(3));

        // Original should remain untouched
        Assert.That(original.Count, Is.EqualTo(3));
        Assert.That(original.Peek(), Is.EqualTo(1));
    }

    [Test]
    public void ConcurrentStack_Should_Be_Deep_Cloned_Correctly()
    {
        ConcurrentStack<int> original = new ConcurrentStack<int>();
        original.Push(1);
        original.Push(2);
        original.Push(3);

        ConcurrentStack<int> clone = original.DeepClone();

        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone.Count, Is.EqualTo(3));

        // Verify order (LIFO)
        int result;
        Assert.That(clone.TryPop(out result), Is.True);
        Assert.That(result, Is.EqualTo(3));
        
        Assert.That(clone.TryPop(out result), Is.True);
        Assert.That(result, Is.EqualTo(2));
        
        Assert.That(clone.TryPop(out result), Is.True);
        Assert.That(result, Is.EqualTo(1));

        // Original should remain untouched
        Assert.That(original.Count, Is.EqualTo(3));
        Assert.That(original.TryPeek(out result), Is.True);
        Assert.That(result, Is.EqualTo(3));
    }
    
    [Test]
    public void ConcurrentQueue_Should_Be_Deep_Cloned_Correctly()
    {
        ConcurrentQueue<int> original = new ConcurrentQueue<int>();
        original.Enqueue(1);
        original.Enqueue(2);
        original.Enqueue(3);

        ConcurrentQueue<int> clone = original.DeepClone();

        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone.Count, Is.EqualTo(3));

        // Verify order (FIFO)
        int result;
        Assert.That(clone.TryDequeue(out result), Is.True);
        Assert.That(result, Is.EqualTo(1));

        Assert.That(clone.TryDequeue(out result), Is.True);
        Assert.That(result, Is.EqualTo(2));

        Assert.That(clone.TryDequeue(out result), Is.True);
        Assert.That(result, Is.EqualTo(3));

        // Original should remain untouched
        Assert.That(original.Count, Is.EqualTo(3));
        Assert.That(original.TryPeek(out result), Is.True);
        Assert.That(result, Is.EqualTo(1));
    }

    [Test]
    public void BlockingCollection_Should_Be_Deep_Cloned_Correctly()
    {
        BlockingCollection<int> original = new BlockingCollection<int>();
        original.Add(1);
        original.Add(2);
        original.Add(3);

        BlockingCollection<int> clone = original.DeepClone();

        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone.Count, Is.EqualTo(3));
        
        // Verify order (FIFO by default)
        Assert.That(clone.Take(), Is.EqualTo(1));
        Assert.That(clone.Take(), Is.EqualTo(2));
        Assert.That(clone.Take(), Is.EqualTo(3));
        
        // Original should remain untouched
        Assert.That(original.Count, Is.EqualTo(3));
    }

    [Test]
    public void LinkedList_Should_Be_Deep_Cloned_Correctly()
    {
        LinkedList<int> original = new LinkedList<int>();
        original.AddLast(1);
        original.AddLast(2);
        original.AddLast(3);

        LinkedList<int> clone = original.DeepClone();

        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone.Count, Is.EqualTo(3));

        // Verify order
        Assert.That(clone.First.Value, Is.EqualTo(1));
        Assert.That(clone.First.Next.Value, Is.EqualTo(2));
        Assert.That(clone.Last.Value, Is.EqualTo(3));
        
        // Original should remain untouched
        Assert.That(original.Count, Is.EqualTo(3));
    }
}

