using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;
using System.Threading.Tasks;

namespace FastCloner.Tests;
public class FailureHypothesisTests
{
    /// <summary>
    /// Demonstrates a weakness: <see cref="FastClonerSafeTypes"/> assumes any class that overrides
    /// <c>GetHashCode</c> has value-based hashing (<c>HasStableHashSemantics == true</c>). This drives
    /// hash-based collections through a memberwise (raw field) clone path that copies the internal
    /// <c>_slots</c>/<c>_buckets</c> arrays verbatim. When the override actually returns an identity-based
    /// hash (e.g. <c>RuntimeHelpers.GetHashCode(this)</c>), the cloned bucket entries store the *original*
    /// object's identity hash, but the elements inside are themselves deep-cloned and therefore have a
    /// brand-new identity hash. The cloned set/dictionary is structurally corrupt: lookups by the
    /// cloned key miss, even though the key is the very element stored in the clone.
    /// </summary>
    private sealed class IdentityHashedKey
    {
        public string Tag { get; set; } = "";
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
        public override bool Equals(object? obj) => ReferenceEquals(this, obj);
    }

    [Test]
    public async Task HashSet_With_IdentityBased_OverriddenGetHashCode_Should_Be_Lookupable_After_Clone()
    {
        IdentityHashedKey item = new IdentityHashedKey { Tag = "a" };
        HashSet<IdentityHashedKey> original = [item];

        HashSet<IdentityHashedKey> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(1);

        IdentityHashedKey cloneItem = clone.Single();
        await Assert.That(cloneItem).IsNotSameReferenceAs(item)
            .Because("Element is a reference type and should be deep-cloned");

        await Assert.That(clone.Contains(cloneItem)).IsTrue()
            .Because("Looking up the actual element of the cloned set must succeed; " +
                     "FastCloner copies the original identity-based hash into the cloned bucket, " +
                     "while the cloned element has a new identity hash, so lookup misses.");
    }

    [Test]
    public async Task Dictionary_With_IdentityBased_OverriddenGetHashCode_Key_Should_Be_Lookupable_After_Clone()
    {
        IdentityHashedKey key = new IdentityHashedKey { Tag = "k" };
        Dictionary<IdentityHashedKey, int> original = new Dictionary<IdentityHashedKey, int> { [key] = 42 };

        Dictionary<IdentityHashedKey, int> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(1);

        IdentityHashedKey cloneKey = clone.Keys.Single();
        await Assert.That(cloneKey).IsNotSameReferenceAs(key);

        await Assert.That(clone.TryGetValue(cloneKey, out int value)).IsTrue()
            .Because("The cloned dictionary must be able to find its own key. " +
                     "FastCloner stores stale identity hashes from the original key in the cloned bucket.");
        await Assert.That(value).IsEqualTo(42);
    }

    /// <summary>
    /// Type whose override would normally throw on a default-state probe instance (Tag is null, ToUpper NREs).
    /// Without an opt-in, the probe catches the throw and conservatively rebuilds the collection. With
    /// <see cref="FastClonerStableHashAttribute"/> the type author asserts the override is value-based, so
    /// FastCloner skips the probe and uses the fast memberwise path. Lookups in the cloned set must still work.
    /// </summary>
    [FastClonerStableHash]
    private sealed class ProbeUnfriendlyButStableKey
    {
        public string Tag { get; set; } = "";
        public override int GetHashCode() => Tag.ToUpperInvariant().GetHashCode();
        public override bool Equals(object? obj)
            => obj is ProbeUnfriendlyButStableKey other
               && string.Equals(Tag, other.Tag, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Same hash semantics as <see cref="ProbeUnfriendlyButStableKey"/> but without the attribute. Used to
    /// assert that the attribute really is what changes the verdict (not some unrelated probe success).
    /// </summary>
    private sealed class ProbeUnfriendlyKeyNoAttribute
    {
        public string Tag { get; set; } = "";
        public override int GetHashCode() => Tag.ToUpperInvariant().GetHashCode();
        public override bool Equals(object? obj)
            => obj is ProbeUnfriendlyKeyNoAttribute other
               && string.Equals(Tag, other.Tag, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task FastClonerStableHashAttribute_Marks_Type_As_Stable()
    {
        await Assert.That(Code.FastClonerSafeTypes.HasStableHashSemantics(typeof(ProbeUnfriendlyButStableKey)))
            .IsTrue()
            .Because("[FastClonerStableHash] must short-circuit the probe and declare stable semantics, " +
                     "even when GetHashCode would throw on default-state instances.");
        
        await Assert.That(Code.FastClonerSafeTypes.HasStableHashSemantics(typeof(ProbeUnfriendlyKeyNoAttribute)))
            .IsTrue()
            .Because("The smarter probe substitutes string.Empty for stable-hash reference fields, " +
                     "so an override of Tag.ToUpperInvariant().GetHashCode() no longer NREs and is " +
                     "correctly classified as stable even without the [FastClonerStableHash] opt-in.");
    }

    [Test]
    public async Task FastClonerStableHashAttribute_Allows_FastPath_With_Correct_Lookup()
    {
        ProbeUnfriendlyButStableKey key = new ProbeUnfriendlyButStableKey { Tag = "Alpha" };
        HashSet<ProbeUnfriendlyButStableKey> original = [key];

        HashSet<ProbeUnfriendlyButStableKey> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(1);

        ProbeUnfriendlyButStableKey cloneKey = clone.Single();
        await Assert.That(cloneKey).IsNotSameReferenceAs(key);
        await Assert.That(clone.Contains(cloneKey)).IsTrue();

        // Equality is case-insensitive, so a fresh key with different casing must also resolve.
        await Assert.That(clone.Contains(new ProbeUnfriendlyButStableKey { Tag = "alpha" })).IsTrue()
            .Because("Hash is value-based on Tag (case-insensitive) and survives the clone unchanged.");
    }
    
    private sealed class IdentityNode
    {
        public string Tag { get; set; } = "";
    }

    private struct IdentityDelegatingStruct : IEquatable<IdentityDelegatingStruct>
    {
        public IdentityNode? Node;

        public override int GetHashCode() => Node is null ? 0 : Node.GetHashCode();

        public bool Equals(IdentityDelegatingStruct other) => ReferenceEquals(Node, other.Node);

        public override bool Equals(object? obj)
            => obj is IdentityDelegatingStruct other && Equals(other);
    }

    [Test]
    public async Task HashSet_Of_Struct_With_IdentityHashed_Reference_Field_Should_Be_Lookupable_After_Clone()
    {
        IdentityNode node = new IdentityNode { Tag = "n" };
        IdentityDelegatingStruct entry = new IdentityDelegatingStruct { Node = node };

        HashSet<IdentityDelegatingStruct> original = [entry];
        HashSet<IdentityDelegatingStruct> clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Count).IsEqualTo(1);

        IdentityDelegatingStruct cloneEntry = clone.Single();
        await Assert.That(cloneEntry.Node).IsNotSameReferenceAs(node)
            .Because("Reference field inside the struct must be deep-cloned to a fresh instance.");

        await Assert.That(clone.Contains(cloneEntry)).IsTrue()
            .Because("FastCloner treats every value type as having stable hash semantics, but this struct's " +
                     "hash delegates to an identity-hashed reference field that gets a new identity on clone, " +
                     "so the bucket-stored hash no longer matches the element's actual hash and lookup misses.");
    }

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
    public async Task FrozenSet_Should_Be_Cloned_Without_StackOverflow()
    {
        FrozenSet<string> original = new[] { "alpha", "beta", "gamma" }.ToFrozenSet();

        FrozenSet<string> clone = original.DeepClone();

        await Assert.That(clone.Count).IsEqualTo(3)
            .Because("All elements must survive the clone.");
        await Assert.That(clone.Contains("alpha")).IsTrue();
        await Assert.That(clone.Contains("beta")).IsTrue();
        await Assert.That(clone.Contains("gamma")).IsTrue();
        await Assert.That(clone.Contains("delta")).IsFalse();
    }

    private sealed class StructWrappedSelfReferenceContainer
    {
        public List<int> Payload { get; set; } = [1, 2, 3];
        public StructBackRef BackRef;
    }

    private struct StructBackRef
    {
        public StructWrappedSelfReferenceContainer? Owner;
    }

    [Test]
    public async Task Container_With_StructMediated_SelfReference_Should_Clone_Without_Overflow()
    {
        StructWrappedSelfReferenceContainer original = new StructWrappedSelfReferenceContainer();
        original.BackRef = new StructBackRef { Owner = original };
        await Assert.That(original.BackRef.Owner).IsSameReferenceAs(original);

        StructWrappedSelfReferenceContainer clone = original.DeepClone();

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Payload).IsEquivalentTo(new[] { 1, 2, 3 });
        await Assert.That(clone.BackRef.Owner).IsSameReferenceAs(clone)
            .Because("The cycle must be rebound to the clone, not left dangling at the original.");
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
