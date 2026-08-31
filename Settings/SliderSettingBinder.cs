using System;
using TMPro;
using UniRx;
using UnityEngine;

/// <summary>
/// Connects a float setting to a ReactiveSlider. The type difference between the
/// setting and the widget is absorbed here and nowhere above.
/// </summary>
public class SliderSettingBinder : SettingBinderBase
{
    [SerializeField] private ReactiveSlider _reactiveSlider;
    [SerializeField] private TextMeshProUGUI _valueLabel;

    private SettingValue<float> _setting;

    /// <summary>
    /// The value being edited before it is committed. The real setting is left
    /// untouched until Apply, which is what makes cancelling free.
    /// </summary>
    private ReactiveProperty<float> _pendingValue;

    private float _initialValue;

    protected override void OnBind(
        SettingManager mgr,
        CompositeDisposable disp,
        Action onChanged)
    {
        _setting = mgr.GetSetting<float>(Type);
        _initialValue = _setting.Value;

        _pendingValue = new ReactiveProperty<float>(_setting.Value);

        _reactiveSlider.Init(_pendingValue, _setting.Value);

        _pendingValue
            .Subscribe(newValue =>
            {
                if (_valueLabel != null)
                    _valueLabel.text = (newValue * 100).ToString("F0") + "%";

                onChanged?.Invoke();
            })
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
        return !Mathf.Approximately(_pendingValue.Value, _initialValue);
    }

    public override void ResetToDefaultValue()
    {
        if (_pendingValue == null) return;
        _pendingValue.Value = _setting.DefaultValue;
    }
}
