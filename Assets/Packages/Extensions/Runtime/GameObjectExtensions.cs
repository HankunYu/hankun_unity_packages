using System.Collections;
using UnityEngine;

public static class GameObjectExtensions
{
    public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
    {
        T existing = gameObject.GetComponent<T>();
        if (existing)
        {
            return existing;
        }

        return gameObject.AddComponent<T>();
    }
    
    public static void RestartCoroutine(this MonoBehaviour monoBehaviour, ref Coroutine coroutine, IEnumerator routine)
    {
        if (coroutine != null)
        {
            monoBehaviour.StopCoroutine(coroutine);
        }
        coroutine = monoBehaviour.StartCoroutine(routine);
    }

    /// <summary>
    /// Gets a descendant GameObject with a specific name
    /// </summary>
    /// <param name="go">The parent object that is searched for a named child.</param>
    /// <param name="name">Name of child to be found.</param>
    /// <returns>The returned child GameObject or null if no child is found.</returns>
    // From Unity.XR.CoreUtils
    public static GameObject GetNamedChild(this GameObject go, string name)
    {
        k_Transforms.Clear();
        go.GetComponentsInChildren(k_Transforms);
        var foundObject = k_Transforms.Find(currentTransform => currentTransform.name == name);
        k_Transforms.Clear();

        if (foundObject != null)
            return foundObject.gameObject;

        return null;
    }
}
