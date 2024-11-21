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
}