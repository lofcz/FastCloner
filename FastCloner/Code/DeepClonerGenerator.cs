using System.Reflection;
using System.Runtime.CompilerServices;

namespace FastCloner.Helpers;

internal static class DeepClonerGenerator
{
    public static T? CloneObject<T>(T? obj)
    {
        switch (obj)
        {
            case ValueType:
            {
                Type type = obj.GetType();
                
                if (typeof(T) == type)
                {
                    return DeepClonerSafeTypes.CanReturnSameObject(type) ? obj : CloneStructInternal(obj, new DeepCloneState());
                }

                break;
            }
            case Delegate del:
            {
                Type? targetType = del.Target?.GetType();
            
                if (targetType?.GetCustomAttribute<CompilerGeneratedAttribute>() is not null)
                {
                    return (T?)CloneClassRoot(obj);
                }
            
                return obj;
            }
        }

        return (T?)CloneClassRoot(obj);
    }

    private static object? CloneClassRoot(object? obj)
    {
        if (obj == null)
            return null;

        Func<object, DeepCloneState, object>? cloner = (Func<object, DeepCloneState, object>)DeepClonerCache.GetOrAddClass(obj.GetType(), t => GenerateCloner(t, true));

        // null -> should return same type
        if (cloner == null)
            return obj;

        return cloner(obj, new DeepCloneState());
    }

    internal static object? CloneClassInternal(object? obj, DeepCloneState state)
    {
        if (obj == null)
            return null;

        Func<object, DeepCloneState, object>? cloner = (Func<object, DeepCloneState, object>)DeepClonerCache.GetOrAddClass(obj.GetType(), t => GenerateCloner(t, true));

        // safe object
        if (cloner == null)
            return obj;

        // loop
        object? knownRef = state.GetKnownRef(obj);
        if (knownRef != null)
            return knownRef;

        return cloner(obj, state);
    }

    internal static T CloneStructInternal<T>(T obj, DeepCloneState state) // where T : struct
    {
        // no loops, no nulls, no inheritance
        Func<T, DeepCloneState, T>? cloner = GetClonerForValueType<T>();

        // safe ojbect
        if (cloner == null)
            return obj;

        return cloner(obj, state);
    }

    // when we can't use code generation, we can use these methods
    internal static T[] Clone1DimArraySafeInternal<T>(T[] obj, DeepCloneState state)
    {
        int l = obj.Length;
        T[]? outArray = new T[l];
        state.AddKnownRef(obj, outArray);
        Array.Copy(obj, outArray, obj.Length);
        return outArray;
    }

    internal static T[]? Clone1DimArrayStructInternal<T>(T[]? obj, DeepCloneState state)
    {
        // not null from called method, but will check it anyway
        if (obj == null) return null;
        int l = obj.Length;
        T[]? outArray = new T[l];
        state.AddKnownRef(obj, outArray);
        Func<T, DeepCloneState, T>? cloner = GetClonerForValueType<T>();
        for (int i = 0; i < l; i++)
            outArray[i] = cloner(obj[i], state);

        return outArray;
    }

    internal static T[]? Clone1DimArrayClassInternal<T>(T[]? obj, DeepCloneState state)
    {
        // not null from called method, but will check it anyway
        if (obj == null) return null;
        int l = obj.Length;
        T[]? outArray = new T[l];
        state.AddKnownRef(obj, outArray);
        for (int i = 0; i < l; i++)
            outArray[i] = (T)CloneClassInternal(obj[i], state);

        return outArray;
    }

    // relatively frequent case. specially handled
    internal static T[,]? Clone2DimArrayInternal<T>(T[,]? obj, DeepCloneState state)
    {
        // not null from called method, but will check it anyway
        if (obj == null) return null;

        // we cannot determine by type multidim arrays (one dimension is possible)
        // so, will check for index here
        int lb1 = obj.GetLowerBound(0);
        int lb2 = obj.GetLowerBound(1);
        if (lb1 != 0 || lb2 != 0)
            return (T[,]) CloneAbstractArrayInternal(obj, state);

        int l1 = obj.GetLength(0);
        int l2 = obj.GetLength(1);
        T[,]? outArray = new T[l1, l2];
        state.AddKnownRef(obj, outArray);
        if (DeepClonerSafeTypes.CanReturnSameObject(typeof(T)))
        {
            Array.Copy(obj, outArray, obj.Length);
            return outArray;
        }

        if (typeof(T).IsValueType())
        {
            Func<T, DeepCloneState, T>? cloner = GetClonerForValueType<T>();
            for (int i = 0; i < l1; i++)
                for (int k = 0; k < l2; k++)
                    outArray[i, k] = cloner(obj[i, k], state);
        }
        else
        {
            for (int i = 0; i < l1; i++)
                for (int k = 0; k < l2; k++)
                    outArray[i, k] = (T)CloneClassInternal(obj[i, k], state);
        }

        return outArray;
    }

    // rare cases, very slow cloning. currently it's ok
    internal static Array? CloneAbstractArrayInternal(Array? obj, DeepCloneState state)
    {
        // not null from called method, but will check it anyway
        if (obj == null) return null;
        int rank = obj.Rank;

        int[]? lengths = Enumerable.Range(0, rank).Select(obj.GetLength).ToArray();

        int[]? lowerBounds = Enumerable.Range(0, rank).Select(obj.GetLowerBound).ToArray();
        int[]? idxes = Enumerable.Range(0, rank).Select(obj.GetLowerBound).ToArray();

        Type? elementType = obj.GetType().GetElementType();
        Array? outArray = Array.CreateInstance(elementType, lengths, lowerBounds);

        state.AddKnownRef(obj, outArray);

        // we're unable to set any value to this array, so, just return it
        if (lengths.Any(x => x == 0))
            return outArray;

        if (DeepClonerSafeTypes.CanReturnSameObject(elementType))
        {
            Array.Copy(obj, outArray, obj.Length);
            return outArray;
        }

        int ofs = rank - 1;
        while (true)
        {
            outArray.SetValue(CloneClassInternal(obj.GetValue(idxes), state), idxes);
            idxes[ofs]++;

            if (idxes[ofs] >= lowerBounds[ofs] + lengths[ofs])
            {
                do
                {
                    if (ofs == 0) return outArray;
                    idxes[ofs] = lowerBounds[ofs];
                    ofs--;
                    idxes[ofs]++;
                } while (idxes[ofs] >= lowerBounds[ofs] + lengths[ofs]);

                ofs = rank - 1;
            }
        }
    }

    internal static Func<T, DeepCloneState, T> GetClonerForValueType<T>() => (Func<T, DeepCloneState, T>)DeepClonerCache.GetOrAddStructAsObject(typeof(T), t => GenerateCloner(t, false));

    private static object? GenerateCloner(Type t, bool asObject)
    {
        if (DeepClonerSafeTypes.CanReturnSameObject(t) && (asObject && !t.IsValueType()))
            return null;

        return DeepClonerExprGenerator.GenerateClonerInternal(t, asObject);
    }

    public static object? CloneObjectTo(object? objFrom, object? objTo, bool isDeep)
    {
        if (objTo == null) return null;

        if (objFrom == null)
            throw new ArgumentNullException(nameof(objFrom), "Cannot copy null object to another");
        Type? type = objFrom.GetType();
        if (!type.IsInstanceOfType(objTo))
            throw new InvalidOperationException("From object should be derived from From object, but From object has type " + objFrom.GetType().FullName + " and to " + objTo.GetType().FullName);
        if (objFrom is string)
            throw new InvalidOperationException("It is forbidden to clone strings");
        Func<object, object, DeepCloneState, object>? cloner = (Func<object, object, DeepCloneState, object>)(isDeep
            ? DeepClonerCache.GetOrAddDeepClassTo(type, t => ClonerToExprGenerator.GenerateClonerInternal(t, true))
            : DeepClonerCache.GetOrAddShallowClassTo(type, t => ClonerToExprGenerator.GenerateClonerInternal(t, false)));
        if (cloner == null) return objTo;
        return cloner(objFrom, objTo, new DeepCloneState());
    }
}