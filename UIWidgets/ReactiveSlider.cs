using System;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Slider that renders an injected ReactiveProperty&lt;float&gt; and writes back to it.
/// It has no idea which setting it represents, so the same widget works on the
/// settings screen, in other UI, and under test.
/// </summary>
public class ReactiveSlider : MonoBehaviour
{
    [SerializeField] private Slider _slider;

    private ReactiveProperty<float> _sourceProperty;
    private IDisposable _visualsSubscription;

    /// <summary>Reads through to the injected property.</summary>
    public IReadOnlyReactiveProperty<float> Value => _sourceProperty;

    private void Awake()
    {
        if (_slider == null)
        {
            Debug.LogError("[ReactiveSlider] Slider is not assigned.", gameObject);
            return;
        }

        _slider.onValueChanged.AddListener(UpdateSourceValue);
    }

    /// <summary>
    /// Injects the property this slider renders. Re-entrant: the previous
    /// subscription is dropped first, so a widget can be pointed at different
    /// state without being rebuilt.
    /// </summary>
    public void Init(ReactiveProperty<float> sourceProperty, float initialValue)
    {
        if (_slider == null) return;

        _sourceProperty = sourceProperty ?? throw new ArgumentNullException(nameof(sourceProperty));

        _visualsSubscription?.Dispose();

        _sourceProperty.Value = initialValue;

        // AddTo ties the subscription to this component's lifetime.
        _visualsSubscription = _sourceProperty
            .Subscribe(UpdateVisuals)
            .AddTo(this);

        UpdateVisuals(_sourceProperty.Value);
    }

    /// <summary>User input writes to the property, never to the visual directly.</summary>
    private void UpdateSourceValue(float sliderValue)
    {
        if (_sourceProperty == null
            || Mathf.Approximately(_sourceProperty.Value, sliderValue))
            return;

        _sourceProperty.Value = sliderValue;
    }

    /// <summary>
    /// The visual follows the property. Bailing out when the two already agree is
    /// what keeps the two-way binding from looping.
    /// </summary>
    private void UpdateVisuals(float sourceValue)
    {
        if (Mathf.Approximately(_slider.value, sourceValue)) return;

        _slider.value = sourceValue;
    }

    private void OnDestroy()
    {
        if (_slider != null)
            _slider.onValueChanged.RemoveAllListeners();
    }
}
