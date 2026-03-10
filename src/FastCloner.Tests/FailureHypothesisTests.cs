using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;
using System.Threading.Tasks;

namespace FastCloner.Tests;
public class FailureHypothesisTests
{
    [Test]
    public async Task BufferBlock_Should_Be_Deep_Cloned_Independently()
    {
        BufferBlock<int> original = new BufferBlock<int>();
        original.Post(1);
        
        // Deep clone
        BufferBlock<int> clone = original.DeepClone();
        
        await Assert.That(clone).IsNotSameReferenceAs(original);

        // Post to original
        original.Post(2);
        

        bool received = clone.TryReceive(out int item);
        await Assert.That(received).IsTrue().Because("Clone should contain initial data");
        await Assert.That(item).IsEqualTo(1);

        bool received2 = clone.TryReceive(out int item2);
        await Assert.That(received2).IsFalse().Because("Clone should not receive updates from original");

        bool receivedOrig1 = original.TryReceive(out int origItem1);
        await Assert.That(receivedOrig1).IsTrue().Because("Original should have data");

        await Assert.That(origItem1).IsEqualTo(1).Because("Original should still have initial data if clone is independent");

        bool receivedOrig2 = original.TryReceive(out int origItem2);
        await Assert.That(receivedOrig2).IsTrue();
        await Assert.That(origItem2).IsEqualTo(2);
    }
    
    [Test]
    public async Task SemaphoreSlim_Should_Be_Deep_Cloned_Independently()
    {
        using SemaphoreSlim original = new SemaphoreSlim(1, 1);
        SemaphoreSlim clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);

        original.Wait();
        await Assert.That(original.CurrentCount).IsEqualTo(0);

        await Assert.That(clone.CurrentCount).IsEqualTo(1).Because("Clone should have independent count");

        bool entered = clone.Wait(0);
        await Assert.That(entered).IsTrue().Because("Should be able to enter clone independently");
    }

    [Test]
    public async Task ConcurrentBag_Should_Be_Deep_Cloned_Correctly()
    {
        ConcurrentBag<int> original = [1, 2, 3];
        ConcurrentBag<int> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);

        // Verify items exist
        List<int> items = clone.ToList();
        await Assert.That(items).Count().IsEqualTo(3);
        await Assert.That(items).Contains(1);
        await Assert.That(items).Contains(2);
        await Assert.That(items).Contains(3);

        // Verify independence
        original.Add(4);
        await Assert.That(clone.Count).IsEqualTo(3);

        clone.Add(5);
        await Assert.That(original.Count).IsEqualTo(4);
        await Assert.That(clone.Count).IsEqualTo(4);

        // Verify functionality
        bool taken = clone.TryTake(out int _);
        await Assert.That(taken).IsTrue();
    }

    [Test]
    public async Task CancellationTokenSource_Should_Be_Reference_Copied()
    {
        using CancellationTokenSource original = new CancellationTokenSource();
        CancellationTokenSource clone = original.DeepClone();

        await Assert.That(clone).IsSameReferenceAs(original);
        await Assert.That(clone.IsCancellationRequested).IsFalse();

        // Cancel original
        original.Cancel();
        
        await Assert.That(original.IsCancellationRequested).IsTrue();
        // Since it is a reference copy, the clone (same object) MUST be cancelled too.
        await Assert.That(clone.IsCancellationRequested).IsTrue().Because("Clone is the same object, so it should be cancelled");

        // Cancel clone (safe to call again)
        clone.Cancel();
        await Assert.That(clone.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task ConcurrentQueue_Should_Be_Deep_Cloned_Correctly()
    {
        ConcurrentQueue<int> original = new ConcurrentQueue<int>();
        original.Enqueue(1);
        original.Enqueue(2);
        
        ConcurrentQueue<int> clone = original.DeepClone();
        
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(2);

        // Modify original
        original.Enqueue(3);
        await Assert.That(original.Count).IsEqualTo(3);
        await Assert.That(clone.Count).IsEqualTo(2).Because("Clone should be independent of original (enqueue)");

        // Modify clone
        clone.Enqueue(4);
        await Assert.That(original.Count).IsEqualTo(3);
        await Assert.That(clone.Count).IsEqualTo(3);

        // Dequeue
        int result;
        original.TryDequeue(out result);
        await Assert.That(result).IsEqualTo(1);

        clone.TryDequeue(out result);
        await Assert.That(result).IsEqualTo(1).Because("Clone should have same initial data");
    }

    [Test]
    public async Task ConcurrentStack_Should_Be_Deep_Cloned_Correctly()
    {
        ConcurrentStack<int> original = new ConcurrentStack<int>();
        original.Push(1);
        original.Push(2);
        
        ConcurrentStack<int> clone = original.DeepClone();
        
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(2);

        // Modify original
        original.Push(3);
        await Assert.That(original.Count).IsEqualTo(3);
        await Assert.That(clone.Count).IsEqualTo(2).Because("Clone should be independent of original (push)");

        // Modify clone
        clone.Push(4);
        await Assert.That(original.Count).IsEqualTo(3);
        await Assert.That(clone.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Channel_Should_Be_Deep_Cloned_Independently()
    {
        Channel<int> original = Channel.CreateUnbounded<int>();
        original.Writer.TryWrite(1);
        
        Channel<int> clone = original.DeepClone();
        
        await Assert.That(clone).IsNotSameReferenceAs(original);

        // Write to original
        original.Writer.TryWrite(2);
        
        bool readClone1 = clone.Reader.TryRead(out int item1);
        await Assert.That(readClone1).IsTrue().Because("Clone should contain initial data");
        await Assert.That(item1).IsEqualTo(1);

        bool readClone2 = clone.Reader.TryRead(out int item2);
        await Assert.That(readClone2).IsFalse().Because("Clone should not receive updates from original");

        bool readOriginal1 = original.Reader.TryRead(out int origItem1);
        await Assert.That(readOriginal1).IsTrue().Because("Original should still have data if clone is truly independent");
    }

    [Test]
    public async Task ManualResetEventSlim_Should_Be_Deep_Cloned_Independently()
    {
        using ManualResetEventSlim original = new ManualResetEventSlim(false);
        ManualResetEventSlim clone = original.DeepClone();
        
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.IsSet).IsFalse();

        // Set original
        original.Set();
        await Assert.That(original.IsSet).IsTrue();
        await Assert.That(clone.IsSet).IsFalse().Because("Clone should not be set when original is set");

        // Set clone
        clone.Set();
        await Assert.That(clone.IsSet).IsTrue();

        // Reset original
        original.Reset();
        await Assert.That(original.IsSet).IsFalse();
        await Assert.That(clone.IsSet).IsTrue().Because("Clone should remain set");
    }

    [Test]
    public async Task ReaderWriterLockSlim_Should_Be_Deep_Cloned_Independently()
    {
        using ReaderWriterLockSlim original = new ReaderWriterLockSlim();
        original.EnterReadLock();
        
        ReaderWriterLockSlim clone = original.DeepClone();
        
        await Assert.That(clone).IsNotSameReferenceAs(original);

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
    public async Task Task_Should_Not_Be_Deep_Cloned()
    {
        TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
        Task<int> original = tcs.Task;
        
        Task<int> clone = original.DeepClone();
        
        tcs.SetResult(42);
        
        await Assert.That(original.IsCompleted).IsTrue();
        await Assert.That(original.Result).IsEqualTo(42);

        await Assert.That(clone.IsCompleted).IsTrue().Because("Clone should be completed if it represents the same task");
        await Assert.That(clone.Result).IsEqualTo(42);
    }
}
