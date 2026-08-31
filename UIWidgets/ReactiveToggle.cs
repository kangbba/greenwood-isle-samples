using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using UniRx;
using DG.Tweening;

/// <summary>
/// Toggle that renders an injected ReactiveProperty<bool> and writes back to it.
/// Holds no value of its own, so the settings object stays the single source of
/// truth and any other view of the same property updates with it.
/// </summary>
public class ReactiveToggle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    [SerializeField] private Button _button;
    [SerializeField] private Transform _onParent;
    [SerializeField] private Transform _offParent;

    [Header("Settings")]
    [SerializeField] private SayneToggleMode _mode = SayneToggleMode.OneTapToggle;

    [Header("Hover")]
    [SerializeField] private float _hoverScale = 1.03f;
    [SerializeField] private float _hoverDuration = 0.15f;

    [Header("Disabled appearance")]
    [SerializeField] private float _disabledAlpha = 0.5f;

    private ReactiveProperty<bool> _sourceProperty;
    private IDisposable _visualsSubscription;

    private CanvasGroup _canvasGroup;
    private Vector3 _originalScale;
    private Tween _hoverTween;

    /// <summary>
    /// Reads through to the injected property.
    /// </summary>
    public IReadOnlyReactiveProperty<bool> IsOn => _sourceProperty;
    
    public SayneToggleMode Mode => _mode;

    private void Awake()
    {
        if (_button == null)
            _button = GetComponent<Button>();

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _originalScale = transform.localScale;

        SetupInputMode();
    }
    
    /// <summary>
    /// Injects the property this toggle renders. Nothing works before this runs.
    /// </summary>
    /// <param name="sourceProperty">The property to render and write back to.</param>
    public void Init(ReactiveProperty<bool> sourceProperty)
    {
        
        _sourceProperty = sourceProperty ?? throw new ArgumentNullException(nameof(sourceProperty));
        
        // Init is re-entrant: the previous subscription is dropped first.
        _visualsSubscription?.Dispose();

        // Subscribing already pushes the current value, so the initial render happens here.
        _visualsSubscription = _sourceProperty
            .Subscribe(UpdateVisuals)
            .AddTo(this);
    }
    
    public void SetColor(Color c)
    {
        if (_onParent == null) return;

        foreach(var img in _onParent.GetComponentsInChildren<Image>())
        {
            img.color = c;
        }
    }

    public void SetInteractable(bool interactable)
    {
        if (_button != null)
            _button.interactable = interactable;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = interactable ? 1f : _disabledAlpha;
        }

        // Disabling cancels an in-flight hover tween, or the toggle stays scaled up.
        if (!interactable)
        {
            _hoverTween?.Kill();
            transform.localScale = _originalScale;
        }
    }

    /// <summary>
    /// Flips the injected property. The visual follows from the subscription, not
    /// from here, so the two cannot disagree.
    /// </summary>
    private void Toggle()
    {
        
        if (_sourceProperty == null) return;
        _sourceProperty.Value = !_sourceProperty.Value;
    }
    
    private void UpdateVisuals(bool isOn)
    {
        if (_onParent != null)
            _onParent.gameObject.SetActive(isOn);

        if (_offParent != null)
            _offParent.gameObject.SetActive(!isOn);
    }

    private void SetupInputMode()
    {
        if (_mode == SayneToggleMode.OneTapToggle)
        {
            _button.onClick.AddListener(Toggle);
        }
        else if (_mode == SayneToggleMode.PressHold)
        {
            Debug.LogWarning("[ReactiveToggle] PressHold mode is not implemented.");
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        
        if (_button == null || !_button.interactable) return;

        _hoverTween?.Kill();
        _hoverTween = transform.DOScale(_originalScale * _hoverScale, _hoverDuration)
            .SetUpdate(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        
        if (_button == null || !_button.interactable) return;

        _hoverTween?.Kill();
        _hoverTween = transform.DOScale(_originalScale, _hoverDuration)
            .SetUpdate(true);
    }

    private void OnDestroy()
    {
        _hoverTween?.Kill();
    }

    public enum SayneToggleMode
    {
        OneTapToggle,
        PressHold
    }
}