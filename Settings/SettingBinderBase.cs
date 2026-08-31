using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The contract between a setting and the widget that presents it. The panel above
/// only ever sees this type, so it never learns whether a setting is a float or a
/// bool, or whether it is drawn as a slider or a toggle.
/// </summary>
public abstract class SettingBinderBase : MonoBehaviour
{
    [SerializeField] private SettingType _type;
    [SerializeField] protected CanvasGroup _canvasGroup;
    [SerializeField] protected TextMeshProUGUI _text;
    [SerializeField] private Image _selectBorder;

    public SettingType Type => _type;

    /// <summary>Nothing here branches on the value type.</summary>
    public void Bind(
        SettingManager mgr,
        CompositeDisposable disp,
        Action onChanged)
    {
        OnBind(mgr, disp, onChanged);
    }

    protected abstract void OnBind(
        SettingManager mgr,
        CompositeDisposable disp,
        Action onChanged);

    /// <summary>Writes the pending value into the real setting.</summary>
    public abstract void Apply();

    /// <summary>Drops the pending value back to the current baseline.</summary>
    public abstract void ResetInitial();

    /// <summary>True while the pending value differs from the baseline.</summary>
    public abstract bool IsChanged();

    public abstract void ResetToDefaultValue();

    public void SetText(string s)
    {
        if (_text != null) _text.SetText(s);
    }

    public void SetSelected(bool isSelected)
    {
        if (_selectBorder != null) _selectBorder.gameObject.SetActive(isSelected);
    }

    public virtual void SetInteractable(bool interactable)
    {
        if (_canvasGroup == null) return;

        _canvasGroup.interactable = interactable;
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.alpha = interactable ? 1f : 0.5f;
    }
}
