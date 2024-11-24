using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Functions from Hurricane Plugin
/// </summary>
public static class Utilis
{
    public static float GetSign(this Axis axis)
    {
        return axis switch
        {
            Axis.X => 1f,
            Axis.Y => 1f,
            Axis.Z => 1f,
            Axis.NegX => -1f,
            Axis.NegY => -1f,
            Axis.NegZ => -1f,
            _ => throw new ArgumentOutOfRangeException($"Cannot get sign from axis, no case for axis={axis}")
        };
    }
    
    public static Vector3 GetVector(this Axis axis)
    {
        return axis switch
        {
            Axis.X => Vector3.right,
            Axis.Y => Vector3.up,
            Axis.Z => Vector3.forward,
            Axis.NegX => -Vector3.right,
            Axis.NegY => -Vector3.up,
            Axis.NegZ => -Vector3.forward,
            _ => throw new ArgumentOutOfRangeException($"Cannot get vector from axis - no case for axis={axis}")
        };
    }

    public static float GetVectorDimension(this Axis axis, Vector3 vector)
    {
        return axis switch
        {
            Axis.X => vector.x,
            Axis.Y => vector.y,
            Axis.Z => vector.z,
            Axis.NegX => -vector.x,
            Axis.NegY => -vector.y,
            Axis.NegZ => -vector.z,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
        };
    }
    
    public static Vector3 OrthogonalVector(this Vector3 v)
    {
        //////https://math.stackexchange.com/questions/137362/how-to-find-perpendicular-vector-to-another-vector
        v.Normalize();
        
        float x = v.x;
        float y = v.y;
        float z = v.z;
        
        Vector3 v1 = new(0f, z, -y);
        Vector3 v2 = new(-z, 0f, x);
        Vector3 v3 = new(-y, x, 0f);
        Vector3 largest = v1;

        if (v2.sqrMagnitude > largest.sqrMagnitude)
        {
            largest = v2;
        }

        if (v3.sqrMagnitude > largest.sqrMagnitude)
        {
            largest = v3;
        }
        
        return largest;
    }
    /// <summary>
    /// Returns the angle between two quaternions around a specified axis. Range is 0 to 360.
    /// </summary>
    /// <param name="qA"></param>
    /// <param name="qB"></param>
    /// <param name="axis"></param>
    /// <returns></returns>
    public static float GetAngleDifference(Quaternion qA, Quaternion qB, Vector3 axis)
    {
        Vector3 directionA = qA * axis.OrthogonalVector();
        Vector3 directionB = qB * axis.OrthogonalVector();

        Vector3 projectedA = Vector3.ProjectOnPlane(directionA, axis);
        Vector3 projectedB = Vector3.ProjectOnPlane(directionB, axis);

        float angle = Vector3.Angle(projectedA, projectedB);

        Vector3 crossProduct = Vector3.Cross(projectedA, projectedB);
        // convert -180 to 180 to 0 to 360
        if (Vector3.Dot(crossProduct, axis) < 0)
        {
            angle = 360 - angle;
        }

        return angle;
    }
    public static Vector3 FindNearestPointOnLine(Vector3 origin, Vector3 end, Vector3 point)
    {
        //Get heading
        Vector3 heading = (end - origin);
        float magnitudeMax = heading.magnitude;
        heading.Normalize();

        //Do projection from the point but clamp it
        Vector3 lhs = point - origin;
        float dotP = Vector3.Dot(lhs, heading);
        dotP = Mathf.Clamp(dotP, 0f, magnitudeMax);
        return origin + heading * dotP;
    }
    
    public static List<Collider> GetColliders(this GameObject go, bool includedTriggers = false)
    {
        return go.GetComponentsInChildren<Collider>().Where(e => !e.isTrigger || includedTriggers).ToList();
    }

    public static IEnumerable<Collider> GetColliders(this Rigidbody rigidbody, bool includeTriggers = false)
    {
        return GetColliders(rigidbody, rigidbody.transform, includeTriggers);
    }
    private static IEnumerable<Collider> GetColliders(this Rigidbody rigidbody, Transform transform, bool includeTriggers = false)
    {
        Rigidbody rb = transform.GetComponent<Rigidbody>();
        if (rb && rb != rigidbody)
            yield break;

        foreach (Collider c in transform.GetComponents<Collider>())
        {
            if (!c.enabled) continue;
            if (!c.isTrigger || (c.isTrigger && includeTriggers))
                yield return c;
        }

        foreach (Transform child in transform)
        {
            foreach (Collider c in GetColliders(rigidbody, child))
            {
                yield return c;
            }
        }
    }
    
    public static Bounds GetColliderBounds(this Rigidbody rb)
    {
        var bounds = new Bounds();
        var first = true;
        foreach (var collider in rb.GetColliders())
        {
            if (first)
            {
                first = false;
                bounds = collider.bounds;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return bounds;
    }

    public static Bounds GetColliderBounds(this Transform transform)
    {
        Collider[] colliders = transform.GetComponents<Collider>();
        return colliders.GetColliderBounds();
    }

    public static Bounds GetColliderBounds(this GameObject go)
    {
        Collider[] colliders = go.GetComponents<Collider>();
        return colliders.GetColliderBounds();
    }

    public static Bounds GetColliderBounds(this List<Collider> colliders)
    {
        Bounds bounds = new Bounds();
        for (int i = 0; i < colliders.Count; i++)
        {
            Collider collider = colliders[i];
            if (i == 0)
            {
                bounds = collider.bounds;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return bounds;
    }

    public static Bounds GetColliderBounds(this Collider[] colliders)
    {
        Bounds bounds = new Bounds();
        bool first = true;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];

            if (!collider.enabled) continue;

            if (first)
            {
                first = false;
                bounds = collider.bounds;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return bounds;
    }
    
    public static void DrawBounds(this Bounds bounds)
    {
        Vector3 v3Center = bounds.center;
        Vector3 v3Extents = bounds.extents;

        var v3FrontTopLeft = new Vector3(v3Center.x - v3Extents.x, v3Center.y + v3Extents.y, v3Center.z - v3Extents.z); // Front top left corner
        var v3FrontTopRight = new Vector3(v3Center.x + v3Extents.x, v3Center.y + v3Extents.y, v3Center.z - v3Extents.z); // Front top right corner
        var v3FrontBottomLeft = new Vector3(v3Center.x - v3Extents.x, v3Center.y - v3Extents.y, v3Center.z - v3Extents.z); // Front bottom left corner
        var v3FrontBottomRight = new Vector3(v3Center.x + v3Extents.x, v3Center.y - v3Extents.y, v3Center.z - v3Extents.z); // Front bottom right corner
        var v3BackTopLeft = new Vector3(v3Center.x - v3Extents.x, v3Center.y + v3Extents.y, v3Center.z + v3Extents.z); // Back top left corner
        var v3BackTopRight = new Vector3(v3Center.x + v3Extents.x, v3Center.y + v3Extents.y, v3Center.z + v3Extents.z); // Back top right corner
        var v3BackBottomLeft = new Vector3(v3Center.x - v3Extents.x, v3Center.y - v3Extents.y, v3Center.z + v3Extents.z); // Back bottom left corner
        var v3BackBottomRight = new Vector3(v3Center.x + v3Extents.x, v3Center.y - v3Extents.y, v3Center.z + v3Extents.z); // Back bottom right corner


        var color = Color.magenta;
        Debug.DrawLine(v3FrontTopLeft, v3FrontTopRight, color);
        Debug.DrawLine(v3FrontTopRight, v3FrontBottomRight, color);
        Debug.DrawLine(v3FrontBottomRight, v3FrontBottomLeft, color);
        Debug.DrawLine(v3FrontBottomLeft, v3FrontTopLeft, color);

        Debug.DrawLine(v3BackTopLeft, v3BackTopRight, color);
        Debug.DrawLine(v3BackTopRight, v3BackBottomRight, color);
        Debug.DrawLine(v3BackBottomRight, v3BackBottomLeft, color);
        Debug.DrawLine(v3BackBottomLeft, v3BackTopLeft, color);

        Debug.DrawLine(v3FrontTopLeft, v3BackTopLeft, color);
        Debug.DrawLine(v3FrontTopRight, v3BackTopRight, color);
        Debug.DrawLine(v3FrontBottomRight, v3BackBottomRight, color);
        Debug.DrawLine(v3FrontBottomLeft, v3BackBottomLeft, color);
    }
}

public enum Axis
{
    X,
    Y,
    Z,
    NegX,
    NegY,
    NegZ
}