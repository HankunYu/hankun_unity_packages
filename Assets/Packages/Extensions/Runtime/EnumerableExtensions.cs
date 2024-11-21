using System;
using System.Collections.Generic;

public static class EnumerableExtensions
{
    public static IEnumerable<T> Flatten<T>(this T value, Func<T, IEnumerable<T>> inner) 
    {
        yield return value;
        foreach (T i in inner(value))
        {
            foreach (T j in Flatten(i, inner)) 
            {
                yield return j;
            }
        }
    }
    
    public static IEnumerable<T> Yield<T>(this T item)
    {
        yield return item;
    }

    public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> function)
    {
        foreach (T element in enumerable)
        {
            function(element);
        }
    }
}