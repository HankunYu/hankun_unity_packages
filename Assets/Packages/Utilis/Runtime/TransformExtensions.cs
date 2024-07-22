using System.Collections.Generic;
using UnityEngine;

public static class TransformExtensions
{
    /// <summary>
    /// Get all children of a transform
    /// </summary>
    /// <param name="transform"></param>
    /// <returns>List of all children transform</returns>
    public static List<Transform> GetAllChildren(this Transform transform)
    {
        var children = new List<Transform>();
        GetAllChildrenRecursive(transform, children);
        return children;
    }

    private static void GetAllChildrenRecursive(Transform transform, List<Transform> children)
    {
        foreach (Transform child in transform)
        {
            children.Add(child);
            GetAllChildrenRecursive(child, children);
        }
    }
}