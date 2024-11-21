using UnityEngine;

public static class FloatExtensions
{
    public static bool Approximately(this float f, float other)
    {
        return Mathf.Approximately(f, other);
    }
}