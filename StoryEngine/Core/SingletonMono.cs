using System;
using UnityEngine;

/// <summary>
/// Fail-fast singleton base for scene managers.
/// - Instance throws instead of lazily creating one: an access before Awake or after
///   destruction is an initialization-order bug, and hiding it behind auto-creation
///   only moves the crash somewhere less readable. HasInstance is the safe probe.
/// - Every subclass must declare its scene lifetime (UseDontDestroyOnLoad) and its
///   teardown (Release); neither can be forgotten silently.
/// Save-bound managers use SaveSingletonMono instead, which mirrors this lifecycle —
/// it must derive from the non-generic SaveSingletonBase for one-pass save binding,
/// and C# single inheritance forces the copy. The two are kept in sync by hand.
/// </summary>
public abstract class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    public static T Instance
    {
        get
        {
            // Two distinct failures. ReferenceEquals sees the true C# null (never
            // initialized); Unity's == overload reports a destroyed instance as null
            // while the reference itself remains, so that case is caught separately
            // through the implicit bool conversion.
            if (ReferenceEquals(_instance, null))
                throw new Exception($"[{typeof(T)}] instance is null (not initialized)");
            if (!_instance)
                throw new Exception($"[{typeof(T)}] has been destroyed (Unity null)");
            return _instance;
        }
    }

    public static bool HasInstance => _instance != null;

    /// <summary>Whether this manager survives scene changes.</summary>
    protected abstract bool UseDontDestroyOnLoad { get; }

    /// <summary>Teardown for subscriptions/resources, called once on destroy.</summary>
    protected abstract void Release();

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this as T;

        if (UseDontDestroyOnLoad)
        {
            if (transform.parent != null)
            {
                // DontDestroyOnLoad is silently ignored on non-root objects — fail loudly instead.
                Debug.LogError($"[SingletonMono<{typeof(T).Name}>] '{gameObject.name}' wants DontDestroyOnLoad but has a parent. Move it to the scene root.");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
                throw new InvalidOperationException($"{gameObject.name} cannot use DontDestroyOnLoad with a parent.");
            }
            DontDestroyOnLoad(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            try
            {
                Release();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{typeof(T)}] exception during Release(): {ex}");
            }

            _instance = null;
        }
    }
}
