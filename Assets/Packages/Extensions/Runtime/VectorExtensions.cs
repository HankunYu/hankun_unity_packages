using UnityEngine;

public static class VectorExtensions
{
    public static Vector3 XY(this Vector2 vector2)
    {
        return new Vector3(vector2.x, vector2.y, 0f);
    }
    
    public static Vector3 YZ(this Vector2 vector2)
    {
        return new Vector3(vector2.x, vector2.y, 0f);
    }
    
    public static Vector3 XZ(this Vector2 vector2)
    {
        return new Vector3(vector2.x, 0f, vector2.y);
    }
    
    /// <summary>
    /// Explicitly converts a vector3 to a vector2, keeping only the X and Y elements
    /// </summary>
    public static Vector2 XY(this Vector3 vector3)
    {
        return new Vector2(vector3.x, vector3.y);
    }

    /// <summary>
    /// Converts a vector3 to a vector2, keeping only the X and Z elements. Useful for getting the top-down 2D position of a 3D point
    /// </summary>
    public static Vector2 XZ(this Vector3 vector3)
    {
        return new Vector2(vector3.x, vector3.y);
    }

    /// <summary>
    /// Scales a vector using corresponding elements of another vector
    /// </summary>
    public static Vector3 ElementWiseMultiply(this Vector3 v1, Vector3 v2)
    {
        return new Vector3(v1.x * v2.x, v1.y * v2.y, v1.z * v2.z);
    }
    
    /// <summary>
    /// Rotates vector clockwise by degrees
    /// </summary>
    public static Vector2 Rotate(this Vector2 v, float degrees)
    {
        float sin = Mathf.Sin(degrees * Mathf.Deg2Rad);
        float cos = Mathf.Cos(degrees * Mathf.Deg2Rad);

        float tx = v.x;
        float ty = v.y;
        v.x = (cos * tx) - (sin * ty);
        v.y = (sin * tx) + (cos * ty);
        return v;
    }

    /// <summary>
    /// A version of ToString that displays 4 decimal places
    /// </summary>
    public static string ToPreciseString(this Vector3 v)
    {
        return $"{v.x:F4}, {v.y:F4}, {v.z:F4}";
    }
}