namespace FastCloner.Benchmark.Ideas.Clone1DimArrayClass;

public sealed class IdeaCloneState(bool trackReferences)
{
    public bool TrackReferences { get; } = trackReferences;

    private object? _knownFrom;
    private object? _knownTo;

    public void AddKnownRef(object from, object to)
    {
        if (!TrackReferences)
            return;

        _knownFrom = from;
        _knownTo = to;
    }
}

internal static class Clone1DimArrayClassIdeaMethods
{
    // Lifted from the original method shape:
    // always calls cloner with state and does not special-case null elements.
    internal static T?[]? Original<T>(
        T?[]? objFrom,
        T?[]? objTo,
        IdeaCloneState state,
        Func<object?, IdeaCloneState, object?> cloneTracking)
    {
        if (objFrom == null || objTo == null) return null;
        int l = Math.Min(objFrom.Length, objTo.Length);
        state.AddKnownRef(objFrom, objTo);
        for (int i = 0; i < l; i++)
            objTo[i] = (T?)cloneTracking(objFrom[i], state);

        return objTo;
    }

    // Optimization idea 1: null fast-path.
    internal static T?[]? NullFastPath<T>(
        T?[]? objFrom,
        T?[]? objTo,
        IdeaCloneState state,
        Func<object?, IdeaCloneState, object?> cloneTracking)
    {
        if (objFrom == null || objTo == null) return null;
        int l = Math.Min(objFrom.Length, objTo.Length);
        state.AddKnownRef(objFrom, objTo);
        for (int i = 0; i < l; i++)
        {
            object? item = objFrom[i];
            objTo[i] = item == null ? default : (T?)cloneTracking(item, state);
        }

        return objTo;
    }

    // Optimization idea 2: avoid tracking-aware cloner calls when tracking is disabled.
    internal static T?[]? NullFastPathAndNoTracking<T>(
        T?[]? objFrom,
        T?[]? objTo,
        IdeaCloneState state,
        Func<object?, IdeaCloneState, object?> cloneTracking,
        Func<object?, object?> cloneNoTracking)
    {
        if (objFrom == null || objTo == null) return null;
        int l = Math.Min(objFrom.Length, objTo.Length);
        state.AddKnownRef(objFrom, objTo);

        if (state.TrackReferences)
        {
            for (int i = 0; i < l; i++)
            {
                object? item = objFrom[i];
                objTo[i] = item == null ? default : (T?)cloneTracking(item, state);
            }
        }
        else
        {
            for (int i = 0; i < l; i++)
            {
                object? item = objFrom[i];
                objTo[i] = item == null ? default : (T?)cloneNoTracking(item);
            }
        }

        return objTo;
    }
}
