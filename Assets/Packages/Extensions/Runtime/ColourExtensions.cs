using UnityEngine;

public static class ColourExtensions
{
    public static Color ToUnityColour(this System.Drawing.Color systemColour)
    {
        return new Color(systemColour.R / 255f, systemColour.G / 255f, systemColour.B / 255f, systemColour.A / 255f);
    }

    public static Color ModifyAlpha(this Color colour, float modifiedAlpha)
    {
        colour.a = modifiedAlpha;
        return colour;
    }
}