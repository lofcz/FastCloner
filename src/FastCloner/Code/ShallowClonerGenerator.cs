namespace FastCloner.Code;

internal static class ShallowClonerGenerator
{
    public static T? CloneObject<T>(T obj)
    {
        if (typeof(T).IsValueType)
            return obj;

        if (obj is null)
            return default;

        Type runtimeType = obj.GetType();
        
        if (runtimeType.IsValueType)
            return (T)ShallowObjectCloner.DirectCloneObject(obj);

        if (FastClonerSafeTypes.CanReturnSameObject(runtimeType))
            return obj;

        return (T)ShallowObjectCloner.DirectCloneObject(obj);
    }
}