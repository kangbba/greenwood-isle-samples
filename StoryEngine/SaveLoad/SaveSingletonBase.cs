using UnityEngine;

/// <summary>
/// Non-generic marker for anything that can have a SaveData bound to it.
/// InGameManager collects these on scene entry and binds the loaded save to all
/// of them in one pass — see SaveDataBinding.cs.
/// </summary>
public abstract class SaveSingletonBase : MonoBehaviour
{
    public abstract void BindSaveData(SaveData data);
    public abstract void UnbindSaveData();
}
