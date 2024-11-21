using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class StringExtensions
{
    /// <summary>
    /// Combines a collection of strings into a single string, using <paramref name="separator"/> between string elements
    /// </summary>
    public static string JoinString(this IEnumerable<string> parts, string separator = ", ")
    {
        StringBuilder sb = new();
        bool isFirst = true;

        foreach (string part in parts)
        {
            if (!isFirst)
            {
                sb.Append(separator);
            }
            
            sb.Append(part);
            isFirst = false;
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Adds rich-text colour tags to a string. This means it will display in the specified colour in UI elements that support colour tags, such as TextMeshPro
    /// </summary>
    public static string AddColourTags(this string str, Color colour)
    {
        string colourHex = ColorUtility.ToHtmlStringRGBA(colour);
        return $"<color=#{colourHex}>{str}</color>";
    }
}