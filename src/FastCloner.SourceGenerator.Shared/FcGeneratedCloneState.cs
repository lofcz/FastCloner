using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FastCloner.SourceGenerator.Shared;

/// <summary>
/// State for tracking circular references during cloning.
/// Used by source-generated clone methods to detect and handle cycles.
/// Thread-safe for concurrent clone operations.
/// </summary>
public sealed class FcGeneratedCloneState
{
    private readonly ConcurrentDictionary<object, object> _knownRefs = new ConcurrentDictionary<object, object>(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Registers a known reference mapping from original to clone.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddKnownRef(object original, object clone)
    {
        if (original != null)
        {
            _knownRefs.TryAdd(original, clone);
        }
    }

    /// <summary>
    /// Gets the previously cloned object for the given original, if any.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? GetKnownRef(object original)
    {
        if (original == null) return null;
        return _knownRefs.TryGetValue(original, out var clone) ? clone : null;
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

