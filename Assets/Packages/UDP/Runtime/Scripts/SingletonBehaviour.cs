using UnityEngine;

namespace Mnemosyne.Networking
{
    /// <summary>
    /// Generic persistent singleton base for networking-related behaviours.
    /// </summary>
    /// <typeparam name="T">The concrete component type.</typeparam>
    public abstract class SingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static readonly object LockObject = new object();

        /// <summary>
        /// Current active instance (if any).
        /// </summary>
        public static T Instance { get; private set; }

        /// <summary>
        /// Ensures uniqueness and marks the GameObject as persistent.
        /// </summary>
        protected virtual void Awake()
        {
            lock (LockObject)
            {
                if (Instance != null && Instance != this)
                {
                    Debug.LogWarning($"{typeof(T).Name} singleton already exists. Destroying duplicate on {name}.");
                    Destroy(gameObject);
                    return;
                }

                Instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
        }

        /// <summary>
        /// Clears static reference when the instance is destroyed.
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
