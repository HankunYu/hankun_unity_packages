using System;
using System.Collections.Generic;

public static class DictionaryExtensions
{
    public static void AddMany<T, U>(this Dictionary<T, U> dictionary, IEnumerable<U> values, Func<U, T> keySelector)
    {
        foreach (U value in values)
        {
            T key = keySelector(value);
            dictionary.Add(key, value);
        }
    }
    
    public static void RemoveMany<T, U>(this Dictionary<T, U> dictionary, IEnumerable<U> values, Func<U, T> keySelector)
    {
        foreach (U value in values)
        {
            T key = keySelector(value);
            dictionary.Remove(key);
        }
    } 
}