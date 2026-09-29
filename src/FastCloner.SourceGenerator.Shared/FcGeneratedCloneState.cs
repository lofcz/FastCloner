using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FastCloner.SourceGenerator.Shared;

/// <summary>
/// State for tracking circular references during cloning.
/// Used by source-generated clone methods to detect and handle cycles.
/// <br/><br/>
/// The instance is safe to use from several threads at once: there is no shared static state and no
/// locking, the lookup structure is concurrent, and a registration establishes its mapping atomically,
/// so concurrent registrations for one source object cannot both take effect. That is the same
/// per-method guarantee this type has always offered.
/// <br/><br/>
/// The <i>operation</i>-level semantics are narrower: a state belongs to <b>one</b> clone operation,
/// and each operation owns its own state. Sharing a single state between threads that are walking
/// different clone graphs is not a supported contract - a registration and its completion are not an
/// atomic pair, and the in-flight reading below is deliberately visible to every viewer of the same
/// state, so such a thread could close a cycle to a clone it does not own.
/// <br/><br/>
/// Execution state and policy are separate, and they share one lookup structure so that ordinary
/// reference tracking does not pay for the ability to opt out of identity preservation:
/// <list type="bullet">
/// <item><b>Execution state</b> is the set of objects whose clone is being constructed right now.
/// It belongs to the clone operation, not to a policy, and it is what lets a cycle - including one
/// that points back at an object several frames up, or at the container a value came from - resolve
/// to the clone under construction.</item>
/// <item><b>Policy</b> is whether completed clones are remembered, so repeated references to one
/// source object resolve to one clone instead of cloning independently.</item>
/// </list>
/// <see cref="SuppressIdentity"/> returns a view over an existing operation that keeps its execution
/// state but stops remembering completed clones, which is how a member marked
/// <c>[FastClonerPreserveIdentity(false)]</c> suppresses aliasing without losing the ability to close
/// a cycle that passes through it.
/// </summary>
public sealed class FcGeneratedCloneState
{
    /// <summary>
    /// One slot per source object, holding both readings of that object's clone. A single structure
    /// keeps ordinary tracking at the same cost as a plain source-to-clone map.
    /// </summary>
    private readonly ConcurrentDictionary<object, ReferenceSlot> _refs;

    private readonly bool _preserveIdentity;

    /// <summary>
    /// Creates a state that preserves identity: repeated references to one source object resolve to
    /// one clone for the whole operation.
    /// </summary>
    public FcGeneratedCloneState()
        : this(new ConcurrentDictionary<object, ReferenceSlot>(ReferenceEqualityComparer.Instance), preserveIdentity: true)
    {
    }

    /// <summary>
    /// Creates a view over an existing operation's slots that does not preserve identity.
    /// </summary>
    private FcGeneratedCloneState(ConcurrentDictionary<object, ReferenceSlot> refs, bool preserveIdentity)
    {
        _refs = refs;
        _preserveIdentity = preserveIdentity;
    }

    /// <summary>
    /// Whether the state remembers completed clones, i.e. whether it preserves identity.
    /// </summary>
    public bool PreservesIdentity => _preserveIdentity;

    /// <summary>
    /// Returns a state that does not preserve identity, for a member marked
    /// <c>[FastClonerPreserveIdentity(false)]</c>.
    /// <br/><br/>
    /// Passing <c>null</c> down cannot express this: <c>null</c> means "no state was supplied",
    /// which makes a type whose own default is to track references allocate tracking state and
    /// re-enable identity preservation for the member's subgraph.
    /// <br/><br/>
    /// The returned view shares the operation's execution state, so objects that are already being
    /// cloned further up stay visible and an active cycle through the opted-out edge still resolves
    /// to the clone under construction. Only completed aliases are dropped.
    /// <br/><br/>
    /// A state that already suppresses identity is returned unchanged, so a nested opt-out cannot
    /// re-widen it and the whole opted-out subgraph keeps one execution state. A <c>null</c> input
    /// starts an independent operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FcGeneratedCloneState SuppressIdentity(FcGeneratedCloneState? state)
    {
        if (state == null)
        {
            return new FcGeneratedCloneState(
                new ConcurrentDictionary<object, ReferenceSlot>(ReferenceEqualityComparer.Instance),
                preserveIdentity: false);
        }

        if (!state._preserveIdentity)
        {
            return state;
        }

        return new FcGeneratedCloneState(state._refs, preserveIdentity: false);
    }

    /// <summary>
    /// Records a source object's clone, if that object is not mapped yet: the <b>first</b> registration
    /// for a source object wins and any later call is a no-op while the state can still resolve it.
    /// <br/><br/>
    /// This is the original public entry point of this type, and it keeps the behaviour of the
    /// <c>TryAdd</c> it performed before the token-based path existed - including its atomicity, so
    /// concurrent registrations for one source object cannot both take effect. Callers compiled against
    /// an earlier version, including assemblies containing code generated against one, therefore
    /// observe no change: a repeated registration never displaces the clone that was recorded first.
    /// In particular a preserving state never displaces the mapping it already established, and a
    /// suppressing one never displaces the clone it is still building.
    /// <br/><br/>
    /// Generated code uses <see cref="RegisterKnownRef"/> instead. That method is deliberately able to
    /// update an in-flight registration, which is what makes a nested clone of the same source object
    /// visible as the object being built at the innermost frame; those semantics must not be emulated
    /// here.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddKnownRef(object original, object clone)
    {
        if (original == null)
        {
            return;
        }

        ReferenceSlot slot = _refs.GetOrAdd(original, static _ => new ReferenceSlot());

        if (_preserveIdentity)
        {
            // The completed reading is the one a preserving state resolves, so winning its
            // establishment is what makes this call the first writer.
            if (Interlocked.CompareExchange(ref slot.KnownClone, clone, null) == null)
            {
                Interlocked.CompareExchange(ref slot.ActiveClone, clone, null);
            }

            return;
        }

        Interlocked.CompareExchange(ref slot.ActiveClone, clone, null);
    }

    /// <summary>
    /// Records where a source object's clone is being built: the mapping always becomes the in-flight
    /// reading, and a state that preserves identity additionally records it as that object's
    /// completed clone. A state that suppresses identity never overwrites a completed clone another
    /// view of the same operation has already established for this source.
    /// <br/><br/>
    /// The completed reading is established atomically and keeps its first writer, which is what makes
    /// repeated references resolve to one clone. The in-flight reading is deliberately replaceable:
    /// a nested clone of the same source object has to become visible as the object being built at the
    /// innermost frame. Both are plain reference writes, so this costs no allocation and no lock.
    /// <br/><br/>
    /// Returns an opaque token describing that one registration, which
    /// <see cref="CompleteKnownRef"/> accepts so the completion does not have to look the source up
    /// again. The token is the slot the dictionary already holds, so returning it allocates nothing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? RegisterKnownRef(object original, object clone)
    {
        if (original == null)
        {
            return null;
        }

        ReferenceSlot slot = _refs.GetOrAdd(original, static _ => new ReferenceSlot());

        if (_preserveIdentity)
        {
            Interlocked.CompareExchange(ref slot.KnownClone, clone, null);
        }

        slot.ActiveClone = clone;

        return slot;
    }

    /// <summary>
    /// Gets the clone for the given original: the clone currently under construction if the object
    /// is part of the clone in progress, otherwise the completed clone when the state preserves
    /// identity. A suppressing view never sees another view's completed clone.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? GetKnownRef(object original)
    {
        if (original == null || !_refs.TryGetValue(original, out ReferenceSlot? slot))
        {
            return null;
        }

        object? active = slot.ActiveClone;
        if (active != null)
        {
            return active;
        }

        return _preserveIdentity ? slot.KnownClone : null;
    }

    /// <summary>
    /// Marks the registration returned by <see cref="RegisterKnownRef"/> as complete: the object
    /// leaves the operation's execution state, so a later, structurally identical reference under a
    /// suppressing view cannot be mistaken for a cycle. A state that preserves identity keeps the
    /// completed reading, which is what makes repeated references share one clone.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CompleteKnownRef(object? registration)
    {
        if (registration is ReferenceSlot slot)
        {
            slot.ActiveClone = null;
        }
    }

    /// <summary>
    /// Both readings of one source object's clone. <see cref="KnownClone"/> is established once and
    /// kept for the whole operation; <see cref="ActiveClone"/> exists only while that clone is being
    /// constructed.
    /// </summary>
    private sealed class ReferenceSlot
    {
        public object? KnownClone;

        public object? ActiveClone;
    }

    /// <summary>
    /// Reference equality comparer for proper circular reference detection.
    /// Uses object identity (ReferenceEquals) rather than value equality.
    /// </summary>
    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
        
        bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);
        int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}