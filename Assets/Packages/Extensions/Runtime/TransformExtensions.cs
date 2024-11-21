using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class TransformExtensions
{
    public static Pose GetWorldSpacePose(this Transform transform)
    {
        return new Pose(transform.position, transform.rotation);
    }

    public static void SetWorldSpacePose(this Transform transform, Pose pose)
    {
        transform.position = pose.position;
        transform.rotation = pose.rotation;
    }
    
    public static void MatchPoseToOtherTransform(this Transform transform, Transform other)
    {
        transform.position = other.position;
        transform.rotation = other.rotation;
    }
    
    /// <summary>
    /// Iterates through a transform's child transforms
    /// If you plan on destroying returned transforms while iterating then use reverseOrder to iterate backwards
    /// </summary>
    public static IEnumerable<Transform> GetChildren(this Transform parent, bool reverseOrder = false)
    {
        int initialChildCount = parent.childCount;

        for (int i = 0; i < initialChildCount; i++)
        {
            int childIndex = reverseOrder ? initialChildCount - i - 1 : i;
            Transform child = parent.GetChild(childIndex);

            yield return child;
        }
    }

    /// <summary>
    /// Iterates through all transforms below original transform in a depth-first manner
    /// </summary>
    public static IEnumerable<Transform> GetAllChildren(this Transform transform)
    {
        return transform.Flatten(t => t.GetChildren()).Where(p => p != transform);
    }
    
    /// <summary>
    /// Gets transform's path, through transform hierarchy
    /// The separator is the string that's placed between parent/child nodes to delimit the path
    /// The formatter returns the transform name from the transform, exposed to allow for extra info or rich text tags
    /// As default (if null) this just returns the transform's name
    /// </summary>
    /// <param name="transform"></param>
    /// <param name="separator"></param>
    /// <param name="formatter"></param>
    /// <returns></returns>
    public static string GetFullPath(this Transform transform, string separator = "/", Func<Transform, bool ,string> formatter = null)
    {
        formatter ??= (t, isFirst) => t.name;
        return GetFullPathInternal(transform, separator, formatter, true);
    }

    private static string GetFullPathInternal(Transform transform, string separator, Func<Transform, bool ,string> formatter, bool isFirst)
    {
        if (!transform)
        {
            return "";
        }

        if (!transform.parent)
        {
            return formatter(transform, isFirst);
        }
            
        return $"{GetFullPathInternal(transform.parent, separator, formatter, false)}{separator}{formatter(transform, isFirst)}";
    }

    public static Ray GetRayFromTransform(this Transform transform)
    {
        return new Ray(transform.position, transform.forward);
    }
}