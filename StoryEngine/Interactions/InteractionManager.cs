using System;
using System.Collections.Generic;
using UnityEngine;

public enum InteractionType
{
    Move,
    Photo,
    Research,
    Talk,
    Touch,
    Gun,
    Aim
}
[System.Serializable]
public class InteractionButtonEntry
{
    public InteractionType type;
    public InteractionButton prefab;
}

public class InteractionManager : SingletonMono<InteractionManager>
{
    protected override bool UseDontDestroyOnLoad => false;

    [SerializeField] private InteractionButtonEntry[] _buttonPrefabs;

    private readonly List<InteractionButton> _activeButtons = new();

    public InteractionButton GetInteractionButtonPrefab(InteractionType type)
    {
        foreach (var item in _buttonPrefabs)
        {
            if (item.type == type)
                return item.prefab;
        }
        return null;
    }

    /// <summary>
    /// Creates a hotspot button positioned in positionSpace, then hands it to the
    /// interface canvas.
    ///
    /// positionSpace is a coordinate frame, not an owner: hotspots are authored against
    /// the place they belong to, but they have to render on the interface canvas, above
    /// the scene. The button is instantiated in that frame so anchoredPosition resolves
    /// there, then reparented keeping its world position, which is what fixes it where
    /// the author put it. Ownership then belongs to this manager, which is why leaving
    /// the place does not take the buttons with it and ClearAllButtons exists.
    /// </summary>
    public InteractionButton MakeInteractionButton(
        InteractionType type,
        Transform positionSpace,
        Vector2 localPos,
        Action onClick)
    {
        var prefab = GetInteractionButtonPrefab(type);
        if (prefab == null) return null;

        var btn = Instantiate(prefab, positionSpace);
        var rect = btn.GetComponent<RectTransform>();
        rect.anchoredPosition = localPos;
        btn.Init(type, onClick);

        var buttonLayer = InGameManager.Instance.CurrentInterfaceCanvas.AwaitButtonLayer;
        btn.transform.SetParent(buttonLayer, worldPositionStays: true);

        _activeButtons.Add(btn);
        return btn;
    }

    public void DestroyButton(InteractionButton button)
    {
        if (button == null) return;
        _activeButtons.Remove(button);
        Destroy(button.gameObject);
    }

    public void ClearButtonsOfType(InteractionType type)
    {
        for (int i = _activeButtons.Count - 1; i >= 0; i--)
        {
            var btn = _activeButtons[i];
            if (btn == null)
            {
                // Destroyed elsewhere — drop the stale entry instead of carrying it forever.
                _activeButtons.RemoveAt(i);
                continue;
            }
            if (btn.Type == type)
                DestroyButton(btn);
        }
    }
    public void ClearAllButtons()
    {
        foreach (var btn in _activeButtons)
        {
            if (btn != null)
                Destroy(btn.gameObject);
        }
        _activeButtons.Clear();
    }

    protected override void Release()
    {
        ClearAllButtons();
    }
}
