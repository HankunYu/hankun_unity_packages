using UnityEngine;

public static class LayerMaskExtensions
{
    public static bool DoesMatch(this LayerMask layerMask, GameObject gameObject)
    {
        int gameObjectLayer = gameObject.layer;
        int gameObjectLayerMask = 1 << gameObjectLayer;
        bool passLayerMaskCheck = (layerMask & gameObjectLayerMask) != 0;
        return passLayerMaskCheck;
    }
}