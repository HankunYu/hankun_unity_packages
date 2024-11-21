using UnityEngine.UIElements;

public static class VisualElementExtensions
{
    public static void SetAllPadding(this VisualElement visualElement, float padding)
    {
        visualElement.style.paddingBottom = padding;
        visualElement.style.paddingTop = padding;
        visualElement.style.paddingLeft = padding;
        visualElement.style.paddingRight = padding;
    }
    
    public static void SetAllPadding(this VisualElement visualElement, float vertical, float horizontal)
    {
        visualElement.style.paddingBottom = vertical;
        visualElement.style.paddingTop = vertical;
        visualElement.style.paddingLeft = horizontal;
        visualElement.style.paddingRight = horizontal;
    }
    
    public static void SetAllMargins(this VisualElement visualElement, float margin)
    {
        visualElement.style.marginBottom = margin;
        visualElement.style.marginTop = margin;
        visualElement.style.marginLeft = margin;
        visualElement.style.marginRight = margin;
    }
    
    public static void SetAllMargins(this VisualElement visualElement, float vertical, float horizontal)
    {
        visualElement.style.marginBottom = vertical;
        visualElement.style.marginTop = vertical;
        visualElement.style.marginLeft = horizontal;
        visualElement.style.marginRight = horizontal;
    }
    
    public static void SetAllBorderWidths(this VisualElement visualElement, float borderWidth)
    {
        visualElement.style.borderBottomWidth = borderWidth;
        visualElement.style.borderTopWidth = borderWidth;
        visualElement.style.borderLeftWidth = borderWidth;
        visualElement.style.borderRightWidth = borderWidth;
    }
    
    public static void SetAllBorderWidths(this VisualElement visualElement, float vertical, float horizontal)
    {
        visualElement.style.borderBottomWidth = vertical;
        visualElement.style.borderTopWidth = vertical;
        visualElement.style.borderLeftWidth = horizontal;
        visualElement.style.borderRightWidth = horizontal;
    }

    public static void SetAllBorderColours(this VisualElement visualElement, StyleColor borderColour)
    {
        visualElement.style.borderBottomColor = borderColour;
        visualElement.style.borderLeftColor = borderColour;
        visualElement.style.borderRightColor = borderColour;
        visualElement.style.borderTopColor = borderColour;
    }

    public static void SetElementVisible(this VisualElement visualElement, bool visible)
    {
        visualElement.style.display = new StyleEnum<DisplayStyle>(visible ? DisplayStyle.Flex : DisplayStyle.None);
    }
}