using System;
using UnityEngine;

/// <summary>
/// Base for managers that live off the bound SaveData instead of keeping their own state.
/// - External code calls BindSaveData(save); the base stores it and calls OnSaveDataBound(save).
///   Derived classes never call the base hooks themselves.
/// - An unbound CurrentSaveData logs a warning and returns null rather than throwing;
///   callers guard with HasSaveData.
/// Mirrors the SingletonMono lifecycle instead of inheriting it: this class must derive
/// from the non-generic SaveSingletonBase so InGameManager can bind saves in one pass,
/// and C# single inheritance forces the copy. The two are kept in sync by hand.
/// </summary>
public abstract class SaveSingletonMono<T> : SaveSingletonBase where T : MonoBehaviour
{
    // ======= Singleton =======
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

    // ======= SaveData binding =======
    private SaveData _currentSaveData;

    /// <summary>
    /// The bound SaveData. Warns and returns null when unbound — guard with HasSaveData.
    /// </summary>
    protected SaveData CurrentSaveData
    {
        get
        {
            if (_currentSaveData == null)
                Debug.LogWarning($"[SaveSingletonMono<{typeof(T).Name}>] SaveData not bound. Call BindSaveData() first.");
            return _currentSaveData;
        }
    }

    public bool HasSaveData => _currentSaveData != null;

    /// <summary>Init hook, called by the base right after binding.</summary>
    protected virtual void OnSaveDataBound(SaveData data) { }

    /// <summary>Cleanup hook, called by the base right before unbinding.</summary>
    protected virtual void OnSaveDataUnbound(SaveData data) { }

    // ======= Binding API =======

    public override void BindSaveData(SaveData data)
    {
        if (data == null)
        {
            Debug.LogError($"[SaveSingletonMono<{typeof(T).Name}>] BindSaveData(null) ignored.");
            return;
        }

        if (_currentSaveData != null && !ReferenceEquals(_currentSaveData, data))
        {
            try { OnSaveDataUnbound(_currentSaveData); }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSingletonMono<{typeof(T).Name}>] OnSaveDataUnbound exception: {ex}");
            }
        }

        _currentSaveData = data;

        // One faulty manager must not break binding for the rest of the pass.
        try { OnSaveDataBound(data); }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSingletonMono<{typeof(T).Name}>] OnSaveDataBound exception: {ex}");
        }
    }

    public override void UnbindSaveData()
    {
        if (_currentSaveData == null) return;

        try { OnSaveDataUnbound(_currentSaveData); }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSingletonMono<{typeof(T).Name}>] OnSaveDataUnbound exception: {ex}");
        }

        _currentSaveData = null;
    }

    // ======= Unity lifecycle =======

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
                Debug.LogError($"[SaveSingletonMono<{typeof(T).Name}>] '{gameObject.name}' wants DontDestroyOnLoad but has a parent. Move it to the scene root.");
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
