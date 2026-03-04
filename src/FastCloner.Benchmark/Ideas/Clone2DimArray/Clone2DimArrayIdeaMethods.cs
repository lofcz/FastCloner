namespace FastCloner.Benchmark.Ideas.Clone2DimArray;

internal static class Clone2DimArrayIdeaMethods
{
    // Lifted from the original Clone2DimArrayInternal shallow-copy flow.
    internal static T[,] OriginalShallow<T>(T[,] objFrom, T[,] objTo)
    {
        if (objFrom.GetLowerBound(0) != 0 || objFrom.GetLowerBound(1) != 0
                                          || objTo.GetLowerBound(0) != 0 || objTo.GetLowerBound(1) != 0)
            return (T[,])CloneAbstractArrayShallow(objFrom, objTo)!;

        int l1 = Math.Min(objFrom.GetLength(0), objTo.GetLength(0));
        int l2 = Math.Min(objFrom.GetLength(1), objTo.GetLength(1));

        if (objFrom.GetLength(0) == objTo.GetLength(0)
            && objFrom.GetLength(1) == objTo.GetLength(1))
        {
            Array.Copy(objFrom, objTo, objFrom.Length);
            return objTo;
        }

        for (int i = 0; i < l1; i++)
            for (int k = 0; k < l2; k++)
                objTo[i, k] = objFrom[i, k];

        return objTo;
    }

    // Lifted from the edited Clone2DimArrayInternal shallow-copy flow.
    internal static T[,] EditedShallow<T>(T[,] objFrom, T[,] objTo)
    {
        int fromLower0 = objFrom.GetLowerBound(0);
        int fromLower1 = objFrom.GetLowerBound(1);
        int toLower0 = objTo.GetLowerBound(0);
        int toLower1 = objTo.GetLowerBound(1);
        if (fromLower0 != 0 || fromLower1 != 0 || toLower0 != 0 || toLower1 != 0)
            return (T[,])CloneAbstractArrayShallow(objFrom, objTo)!;

        int fromLength0 = objFrom.GetLength(0);
        int fromLength1 = objFrom.GetLength(1);
        int toLength0 = objTo.GetLength(0);
        int toLength1 = objTo.GetLength(1);

        int l1 = Math.Min(fromLength0, toLength0);

        // Row-major locality optimization: when row width matches, copy contiguous
        // row prefix in one block.
        if (fromLength1 == toLength1)
        {
            Array.Copy(objFrom, objTo, l1 * fromLength1);
            return objTo;
        }

        int l2 = Math.Min(fromLength1, toLength1);
        for (int i = 0; i < l1; i++)
            for (int k = 0; k < l2; k++)
                objTo[i, k] = objFrom[i, k];

        return objTo;
    }

    private static Array? CloneAbstractArrayShallow(Array? objFrom, Array? objTo)
    {
        if (objFrom == null || objTo == null) return null;
        int rank = objFrom.Rank;

        int[] lowerBoundsFrom = new int[rank];
        int[] lowerBoundsTo = new int[rank];
        int[] lengths = new int[rank];
        int[] idxesFrom = new int[rank];
        int[] idxesTo = new int[rank];
        bool hasZeroLength = false;
        for (int i = 0; i < rank; i++)
        {
            int lowerBoundFrom = objFrom.GetLowerBound(i);
            int lowerBoundTo = objTo.GetLowerBound(i);
            int length = Math.Min(objFrom.GetLength(i), objTo.GetLength(i));

            lowerBoundsFrom[i] = lowerBoundFrom;
            lowerBoundsTo[i] = lowerBoundTo;
            lengths[i] = length;
            idxesFrom[i] = lowerBoundFrom;
            idxesTo[i] = lowerBoundTo;
            hasZeroLength |= length == 0;
        }

        if (hasZeroLength)
            return objTo;

        while (true)
        {
            objTo.SetValue(objFrom.GetValue(idxesFrom), idxesTo);
            int ofs = rank - 1;
            while (true)
            {
                idxesFrom[ofs]++;
                idxesTo[ofs]++;
                if (idxesFrom[ofs] >= lowerBoundsFrom[ofs] + lengths[ofs])
                {
                    idxesFrom[ofs] = lowerBoundsFrom[ofs];
                    idxesTo[ofs] = lowerBoundsTo[ofs];
                    ofs--;
                    if (ofs < 0) return objTo;
                }
                else
                    break;
            }
        }
    }
}
