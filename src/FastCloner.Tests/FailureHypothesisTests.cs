using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks.Dataflow;

namespace FastCloner.Tests;

[TestFixture]
public class FailureHypothesisTests
{
    [Test]
    public void BufferBlock_Should_Be_Deep_Cloned_Independently()
    {
        var original = new BufferBlock<int>();
        original.Post(1);
        
        // Deep clone
        var clone = original.DeepClone();
        
        Assert.That(clone, Is.Not.SameAs(original));
        
        // Post to original
        original.Post(2);
        

        bool received = clone.TryReceive(out int item);
        Assert.That(received, Is.True, "Clone should contain initial data");
        Assert.That(item, Is.EqualTo(1));
        
        bool received2 = clone.TryReceive(out int item2);
        Assert.That(received2, Is.False, "Clone should not receive updates from original");
        
        bool receivedOrig1 = original.TryReceive(out int origItem1);
        Assert.That(receivedOrig1, Is.True, "Original should have data");
        
        Assert.That(origItem1, Is.EqualTo(1), "Original should still have initial data if clone is independent");
        
        bool receivedOrig2 = original.TryReceive(out int origItem2);
        Assert.That(receivedOrig2, Is.True);
        Assert.That(origItem2, Is.EqualTo(2));
    }
    
    [Test]
    public void SemaphoreSlim_Should_Be_Deep_Cloned_Independently()
    {
        using var original = new SemaphoreSlim(1, 1);
        var clone = original.DeepClone();

        Assert.That(clone, Is.Not.SameAs(original));
        
        original.Wait();
        Assert.That(original.CurrentCount, Is.EqualTo(0));
        
        Assert.That(clone.CurrentCount, Is.EqualTo(1), "Clone should have independent count");

        bool entered = clone.Wait(0);
        Assert.That(entered, Is.True, "Should be able to enter clone independently");
    }

    [Test]
    public void ConcurrentBag_Should_Be_Deep_Cloned_Correctly()
    {
        var original = new ConcurrentBag<int> { 1, 2, 3 };
        var clone = original.DeepClone();

        Assert.That(clone, Is.Not.SameAs(original));
        
        // Verify items exist
        var items = clone.ToList();
        Assert.That(items, Has.Count.EqualTo(3));
        Assert.That(items, Contains.Item(1));
        Assert.That(items, Contains.Item(2));
        Assert.That(items, Contains.Item(3));
        
        // Verify independence
        original.Add(4);
        Assert.That(clone.Count, Is.EqualTo(3));
        
        clone.Add(5);
        Assert.That(original.Count, Is.EqualTo(4));
        Assert.That(clone.Count, Is.EqualTo(4));
        
        // Verify functionality
        bool taken = clone.TryTake(out int _);
        Assert.That(taken, Is.True);
    }

    [Test]
    public void CancellationTokenSource_Should_Be_Reference_Copied()
    {
        using var original = new CancellationTokenSource();
        var clone = original.DeepClone();

        Assert.That(clone, Is.SameAs(original));
        Assert.That(clone.IsCancellationRequested, Is.False);

        // Cancel original
        original.Cancel();
        
        Assert.That(original.IsCancellationRequested, Is.True);
        // Since it is a reference copy, the clone (same object) MUST be cancelled too.
        Assert.That(clone.IsCancellationRequested, Is.True, "Clone is the same object, so it should be cancelled");
        
        // Cancel clone (safe to call again)
        clone.Cancel();
        Assert.That(clone.IsCancellationRequested, Is.True);
    }

    [Test]
    public void ConcurrentQueue_Should_Be_Deep_Cloned_Correctly()
    {
        var original = new ConcurrentQueue<int>();
        original.Enqueue(1);
        original.Enqueue(2);
        
        var clone = original.DeepClone();
        
        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone.Count, Is.EqualTo(2));
        
        // Modify original
        original.Enqueue(3);
        Assert.That(original.Count, Is.EqualTo(3));
        Assert.That(clone.Count, Is.EqualTo(2), "Clone should be independent of original (enqueue)");
        
        // Modify clone
        clone.Enqueue(4);
        Assert.That(original.Count, Is.EqualTo(3));
        Assert.That(clone.Count, Is.EqualTo(3));
        
        // Dequeue
        int result;
        original.TryDequeue(out result);
        Assert.That(result, Is.EqualTo(1));
        
        clone.TryDequeue(out result);
        Assert.That(result, Is.EqualTo(1), "Clone should have same initial data");
    }

    [Test]
    public void ConcurrentStack_Should_Be_Deep_Cloned_Correctly()
    {
        var original = new ConcurrentStack<int>();
        original.Push(1);
        original.Push(2);
        
        var clone = original.DeepClone();
        
        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone.Count, Is.EqualTo(2));
        
        // Modify original
        original.Push(3);
        Assert.That(original.Count, Is.EqualTo(3));
        Assert.That(clone.Count, Is.EqualTo(2), "Clone should be independent of original (push)");
        
        // Modify clone
        clone.Push(4);
        Assert.That(original.Count, Is.EqualTo(3));
        Assert.That(clone.Count, Is.EqualTo(3));
    }

    [Test]
    public void Channel_Should_Be_Deep_Cloned_Independently()
    {
        var original = System.Threading.Channels.Channel.CreateUnbounded<int>();
        original.Writer.TryWrite(1);
        
        var clone = original.DeepClone();
        
        Assert.That(clone, Is.Not.SameAs(original));
        
        // Write to original
        original.Writer.TryWrite(2);
        
        bool readClone1 = clone.Reader.TryRead(out int item1);
        Assert.That(readClone1, Is.True, "Clone should contain initial data");
        Assert.That(item1, Is.EqualTo(1));
        
        bool readClone2 = clone.Reader.TryRead(out int item2);
        Assert.That(readClone2, Is.False, "Clone should not receive updates from original");
        
        bool readOriginal1 = original.Reader.TryRead(out int origItem1);
        Assert.That(readOriginal1, Is.True, "Original should still have data if clone is truly independent");
    }

    [Test]
    public void ManualResetEventSlim_Should_Be_Deep_Cloned_Independently()
    {
        using var original = new ManualResetEventSlim(false);
        var clone = original.DeepClone();
        
        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone.IsSet, Is.False);
        
        // Set original
        original.Set();
        Assert.That(original.IsSet, Is.True);
        Assert.That(clone.IsSet, Is.False, "Clone should not be set when original is set");
        
        // Set clone
        clone.Set();
        Assert.That(clone.IsSet, Is.True);
        
        // Reset original
        original.Reset();
        Assert.That(original.IsSet, Is.False);
        Assert.That(clone.IsSet, Is.True, "Clone should remain set");
    }

    [Test]
    public void ReaderWriterLockSlim_Should_Be_Deep_Cloned_Independently()
    {
        using var original = new ReaderWriterLockSlim();
        original.EnterReadLock();
        
        var clone = original.DeepClone();
        
        Assert.That(clone, Is.Not.SameAs(original));

        try
        {
            if (original.IsReadLockHeld) original.ExitReadLock();
            if (clone.IsReadLockHeld) clone.ExitReadLock();
        }
        catch (Exception)
        {
            // Ignore cleanup errors
        }
    }

    [Test]
    public void Task_Should_Not_Be_Deep_Cloned()
    {
        var tcs = new TaskCompletionSource<int>();
        Task<int> original = tcs.Task;
        
        var clone = original.DeepClone();
        
        tcs.SetResult(42);
        
        Assert.That(original.IsCompleted, Is.True);
        Assert.That(original.Result, Is.EqualTo(42));
        
        Assert.That(clone.IsCompleted, Is.True, "Clone should be completed if it represents the same task");
        Assert.That(clone.Result, Is.EqualTo(42));
    }
}

