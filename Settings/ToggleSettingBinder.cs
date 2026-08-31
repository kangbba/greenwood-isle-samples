using System;
using UniRx;
using UnityEngine;

/// <summary>
/// The same flow as SliderSettingBinder with bool in place of float. Only the type
/// and the widget differ; the contract above is identical.
/// </summary>
public class ToggleSettingBinder : SettingBinderBase
{
    [SerializeField] private ReactiveToggle _reactiveToggle;

    private SettingValue<bool> _setting;
    private ReactiveProperty<bool> _pendingValue;
    private bool _initialValue;

    protected override void OnBind(
        SettingManager mgr,
        CompositeDisposable disp,
        Action onChanged)
    {
        _setting = mgr.GetSetting<bool>(Type);
        _initialValue = _setting.Value;

        _pendingValue = new ReactiveProperty<bool>(_setting.Value);

        _reactiveToggle.Init(_pendingValue);

        _pendingValue
            .Subscribe(_ => onChanged?.Invoke())
            .AddTo(disp);
    }

    public override void Apply()
    {
        _setting.Value = _pendingValue.Value;
        _initialValue = _pendingValue.Value;
    }

    public override void ResetInitial()
    {
        if (_pendingValue == null) return;
        _pendingValue.Value = _initialValue;
    }

    public override bool IsChanged()
    {
        if (_pendingValue == null) return false;
        return _pendingValue.Value != _initialValue;
    }

    public override void ResetToDefaultValue()
    {
        if (_pendingValue == null) return;
        _pendingValue.Value = _setting.DefaultValue;
    }
}
