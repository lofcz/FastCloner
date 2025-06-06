namespace FastCloner.Code;

internal static class Extensions
{
    #if MODERN
    
    #else 
    public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
    {
        return dictionary.TryGetValue(key, out TValue value) ? value : defaultValue;
    }
    
    public static void Fill<T>(this T[] array, T value)
    {
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = value;
        }
    }
    
    public static void Fill<T>(this T[] array, T value, int startIndex, int count)
    {
        if (array == null)
            throw new ArgumentNullException(nameof(array));
        if (startIndex < 0 || startIndex >= array.Length)
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        if (count < 0 || startIndex + count > array.Length)
            throw new ArgumentOutOfRangeException(nameof(count));
            
        for (int i = startIndex; i < startIndex + count; i++)
        {
            array[i] = value;
        }
    }
    
    public static void Fill(this Array array, object value)
    {
        if (array == null)
            throw new ArgumentNullException(nameof(array));
            
        for (int i = 0; i < array.Length; i++)
        {
            array.SetValue(value, i);
        }
    }
    
    public static void Fill(this Array array, object value, int startIndex, int count)
    {
        if (array == null)
            throw new ArgumentNullException(nameof(array));
        if (startIndex < 0 || startIndex >= array.Length)
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        if (count < 0 || startIndex + count > array.Length)
            throw new ArgumentOutOfRangeException(nameof(count));
            
        for (int i = startIndex; i < startIndex + count; i++)
        {
            array.SetValue(value, i);
        }
    }
    #endif
}