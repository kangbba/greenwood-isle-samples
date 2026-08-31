// Excerpted from InGameManager — the binding side of SaveSingletonMono.
//
// One SaveData object is the whole game state. Managers that need it derive from
// SaveSingletonMono<T> and keep no local copies; on scene entry InGameManager collects
// every SaveSingletonBase in the scene and binds the loaded save to all of them in one
// pass. There is no per-manager sync code, so nothing can drift from the save.

using UnityEngine;

public class InGameManagerBindingExcerpt : MonoBehaviour
{
    private SaveSingletonBase[] _saveManagers;
    private SaveData _tmpSaveData; // the live save this session mutates; the loaded original stays untouched

    private void CollectAndBindSaveManagers()
    {
        _saveManagers = FindObjectsByType<SaveSingletonBase>(FindObjectsSortMode.None);
        BindSaveDataToAllManagers(_tmpSaveData);
    }

    private void BindSaveDataToAllManagers(SaveData saveData)
    {
        if (_saveManagers == null || _saveManagers.Length == 0) return;
        foreach (var m in _saveManagers)
            m?.BindSaveData(saveData);
    }

    private void UnbindAllSaveManagers()
    {
        if (_saveManagers == null || _saveManagers.Length == 0) return;
        foreach (var m in _saveManagers)
            m?.UnbindSaveData();
    }
}
