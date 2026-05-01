namespace FastCloner.Code;

internal static class Extensions
{
    #if MODERN
    
    #else 
    public static bool Contains(this string source, string value, StringComparison comparisonType)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));
        if (value is null)
            throw new ArgumentNullException(nameof(value));

        return source.IndexOf(value, comparisonType) >= 0;
    }

    public static TValue? GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue? defaultValue = default)
    {
        return dictionary.TryGetValue(key, out TValue value) ? value : defaultValue;
    }
    
    extension<T>(T[] array)
    {
        public void Fill(T value)
        {
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = value;
            }
        }

        public void Fill(T value, int startIndex, int count)
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
    }

    extension(Array array)
    {
        public void Fill(object value)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            
            for (int i = 0; i < array.Length; i++)
            {
                array.SetValue(value, i);
            }
        }

        public void Fill(object value, int startIndex, int count)
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
    }

#endif
}