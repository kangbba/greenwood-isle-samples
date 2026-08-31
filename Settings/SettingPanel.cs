using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

/// <summary>
/// The settings screen. It holds one list of SettingBinderBase and never asks what
/// a given entry actually is: whether a setting is a float or a bool, and whether
/// it is drawn as a slider or a toggle, stays inside the binder.
///
/// Everything below this class was designed to keep this one simple.
/// </summary>
public class SettingPanel : AnimationImage
{
    [SerializeField] private GreenwoodButton _applyButton;
    [SerializeField] private GreenwoodButton _resetButton;
    [SerializeField] private GreenwoodButton _closeButton;

    [SerializeField] private Transform _soundContainer;
    [SerializeField] private Transform _systemContainer;
    [SerializeField] private Transform _gameplayContainer;

    private readonly List<SettingBinderBase> _allBinders = new();
    private CompositeDisposable _disp;
    private Action _onClose;

    public void Init(Action onClose)
    {
        _onClose = onClose;
        _disp = new CompositeDisposable();

        CollectBinders();
        ConfigureBindersAndBind();
        SetupButtons();
        UpdateApply();
    }

    /// <summary>
    /// The binders already exist in the scene, laid out per category. They are
    /// collected as the base type, which is all this class ever needs.
    /// </summary>
    private void CollectBinders()
    {
        _allBinders.Clear();

        foreach (var root in new[] { _soundContainer, _systemContainer, _gameplayContainer })
        {
            if (root == null) continue;
            _allBinders.AddRange(root.GetComponentsInChildren<SettingBinderBase>(true));
        }
    }

    private void ConfigureBindersAndBind()
    {
        foreach (var binder in _allBinders)
        {
            binder.Bind(
                SettingManager.Instance,
                _disp,
                onChanged: UpdateApply);

            binder.ResetInitial();
        }
    }

    private void OnApply()
    {
        foreach (var binder in _allBinders)
        {
            if (binder.IsChanged())
            {
                binder.Apply();
                binder.ResetInitial();
            }
        }

        SettingManager.Instance.Save();
        UpdateApply();
    }

    private void OnReset()
    {
        foreach (var binder in _allBinders)
            binder.ResetToDefaultValue();

        UpdateApply();
    }

    /// <summary>
    /// Nothing has to be restored from storage: a deferred setting never touched
    /// the real value, so returning each binder to its baseline is enough.
    /// </summary>
    private void OnCloseInternal()
    {
        foreach (var binder in _allBinders)
        {
            if (binder.IsChanged())
                binder.ResetInitial();
        }

        Release();
        _onClose?.Invoke();
    }

    private void UpdateApply()
    {
        bool shouldEnable =
            _allBinders.Any(binder => binder.IsChanged());

        if (_applyButton != null)
            _applyButton.SetInteractable(shouldEnable);
    }

    private void SetupButtons()
    {
        if (_applyButton != null) _applyButton.AddListener(OnApply);
        if (_resetButton != null) _resetButton.AddListener(OnReset);
        if (_closeButton != null) _closeButton.AddListener(OnCloseInternal);
    }

    private void Release()
    {
        _disp?.Dispose();
        _disp = null;

        _applyButton?.RemoveAllListeners();
        _resetButton?.RemoveAllListeners();
        _closeButton?.RemoveAllListeners();

        _allBinders.Clear();
    }
}
