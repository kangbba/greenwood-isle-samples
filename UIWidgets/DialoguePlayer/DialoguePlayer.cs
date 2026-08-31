using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;
using System.Threading;
using System;
using DG.Tweening;

public enum LocationMode
{
    Down,
    Up
}

public class DialoguePlayer : MonoBehaviour
{
    private const float MinSpeed = 500f;
    private const float MaxSpeed = 2000f;
    private const int MinFont = 24;
    private const int MaxFont = 40;
    private const float MinSkipDelay = .05f;
    private const float MaxSkipDelay = 1.5f;
    
    // Stands in for instant in FastPlay, so the speed path stays one number.
    private const float FastPlaySpeed = 10000f;

    public Action OnSentenceStart;
    public Action OnSentenceEnd;
    public Action OnCancel;

    [Header("Binding")]
    [SerializeField] private GameObject _box;
    [SerializeField] private TapCatcher _tapLayer;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private GameObject _arrow;
    [SerializeField] private GameObject _downModeArrow;
    [SerializeField] private RevealingMultipleLines _revealer;
    [SerializeField] private Image _ownerBackground;
    [SerializeField] private TextMeshProUGUI _ownerText;

    [Header("Style")]
    [SerializeField] private string _highlightColor = "#FFD700";

    private readonly Subject<DialogueCmd> _cmdBus = new();
    private CancellationTokenSource _cts;
    private CompositeDisposable _disp = new();

    private List<string> _sentences = new();
    private int _idx;
    private DialogueState _state = DialogueState.Idle;

    private bool _isInteractable = true;
    
    // Auto and FastPlay collapsed into one advance-by-itself state, so the run
    // loop branches once instead of testing both modes everywhere.
    private IReadOnlyReactiveProperty<bool> _isEffectiveAuto;
    private float _autoSkipDelaySec = 1f;

    public IObservable<DialogueCmd> CmdStream => _cmdBus;
    public bool IsPlaying => _state == DialogueState.Playing;
    public bool IsCompleted => _state == DialogueState.Completed;

    public ReactiveProperty<bool> IsShown { get; private set; } = new ReactiveProperty<bool>(true);

    private Tweener _canvasFadeTween;
    private LocationMode _currentLocationMode = LocationMode.Down;

    private void Awake()
    {
        _revealer.Bind(CmdStream);

        SetLocationMode(LocationMode.Down);

        IsShown
            .DistinctUntilChanged()
            .Subscribe(isShow =>
            {
                if (isShow)
                    DoShow(0.2f);
                else
                    DoHide(0.2f);
            })
            .AddTo(this);

    }

    private void DoShow(float duration)
    {
        _canvasFadeTween?.Kill();
        _canvasFadeTween = _canvasGroup.DOFade(1f, duration)
            .SetEase(Ease.OutQuad)
            .OnKill(() => _canvasGroup.alpha = 1f);
    }

    private void DoHide(float duration)
    {
        _canvasFadeTween?.Kill();
        _canvasFadeTween = _canvasGroup.DOFade(0f, duration)
            .SetEase(Ease.OutQuad)
            .OnKill(() => _canvasGroup.alpha = 0f);
    }

    private void OnDestroy()
    {
        
        if (_canvasFadeTween != null && _canvasFadeTween.IsActive())
            _canvasFadeTween.Kill();
        _canvasFadeTween = null;

        // Cancelled, not disposed: the run loop reads its token as it unwinds and disposes
        // the source on its way out.
        _cts?.Cancel();
        _cts = null;

        if (_disp != null)
        {
            _disp.Dispose();
            _disp = null;
        }

        if (IsShown != null)
        {
            IsShown.Dispose();
            IsShown = null;
        }

        if (_cmdBus != null)
        {
            _cmdBus.OnCompleted();
            _cmdBus.Dispose();
        }
    }

    public void Init(string characterID, Color characterColor)
    {
        ListenSettings();
        SetArrow(false);
        SetOwner(characterID, characterColor);
    }

    private void SetOwner(string characterID, Color characterColor)
    {
        bool isMono = characterID == CNames.Mono || string.IsNullOrEmpty(characterID) || characterColor.a <= 0.001f;
        _ownerBackground.gameObject.SetActive(!isMono);
        _ownerText.text = characterID;
        _ownerText.color = Color.Lerp(characterColor, Color.white, .95f).ModifiedAlpha(1f);
        _ownerBackground.color = characterColor.ModifiedAlpha(1f);
        _ownerText.gameObject.SetActive(!isMono);
    }

    private void ListenSettings()
    {
        _disp.Clear();

        var setting = SettingManager.Instance;

        _isEffectiveAuto = Observable.CombineLatest(
            setting.IsAutoOn,
            setting.IsFastPlayOn,
            (isAuto, isFast) => isAuto || isFast
        ).ToReadOnlyReactiveProperty().AddTo(_disp);

        // Derived from the settings rather than copied, so changing dialogue speed
        // mid-sentence takes effect without the player leaving the menu.
        Observable.CombineLatest(
            setting.GetFloatSetting(SettingType.DialogueSpeed).ValueNotifier,
            setting.IsFastPlayOn,
            (speedSetting, isFastPlay) => isFastPlay ? FastPlaySpeed : Mathf.Lerp(MinSpeed, MaxSpeed, speedSetting)
        )
        .Subscribe(finalSpeed => _revealer.SetSpeed(finalSpeed))
        .AddTo(_disp);

        Observable.CombineLatest(
            setting.GetFloatSetting(SettingType.AutoModePagingSpeed).ValueNotifier,
            setting.IsFastPlayOn,
            (delaySetting, isFastPlay) => isFastPlay ? 0f : Mathf.Lerp(MaxSkipDelay, MinSkipDelay, delaySetting)
        )
        .Subscribe(finalDelay => _autoSkipDelaySec = finalDelay)
        .AddTo(_disp);

        setting.GetFloatSetting(SettingType.FontSize)
            .ValueNotifier
            .Subscribe(v =>
            {
                int size = Mathf.RoundToInt(Mathf.Lerp(MinFont, MaxFont, v));
                _revealer.SetFontSizeAndRestart(size);
            }).AddTo(_disp);

        PopupCanvas.Instance.PopupExistNotifier
            .Subscribe(isPopup =>
            {
                _isInteractable = !isPopup;
                if (isPopup) Pause();
                else Resume();
            }).AddTo(_disp);

        Observable.EveryUpdate()
            .Where(_ => _isInteractable && InputUtils.GetKeyDown(KeyCode.Space))
            .Subscribe(_ => CompleteCurrentLine())
            .AddTo(_disp);
    }

    public void Play(List<string> lines)
    {
        IsShown.Value = true;
        _sentences = lines;
        _idx = 0;

        // Not disposed here: the outgoing loop still reads its own token as it unwinds, and
        // it disposes the source itself once it is out.
        _cts?.Cancel();

        _cts = new CancellationTokenSource();
        RunLoop(_cts).Forget();
    }
    
    public void Pause() => _cmdBus.OnNext(new DialogueCmd(DialogueCmdType.Pause));
    
    public void Resume() => _cmdBus.OnNext(new DialogueCmd(DialogueCmdType.Resume));
    
    public void CompleteCurrentLine() => _cmdBus.OnNext(new DialogueCmd(DialogueCmdType.CompleteCurrentLine));
    
    public void Exit()
    {
        _cmdBus.OnNext(new DialogueCmd(DialogueCmdType.Exit));
        _cts?.Cancel();
    }

    public void Clear() => _cmdBus.OnNext(new DialogueCmd(DialogueCmdType.Clear));

    private async UniTaskVoid RunLoop(CancellationTokenSource ownCts)
    {
        CancellationToken ct = ownCts.Token;

        _state = DialogueState.Playing;
        try
        {
            while (_idx < _sentences.Count && !ct.IsCancellationRequested)
            {
                string raw = _sentences[_idx];
                string processed = raw
                    .Replace("*", "☆")
                    .Replace("{", $"<color={_highlightColor}>")
                    .Replace("}", "</color>");

                OnSentenceStart?.Invoke();
                _revealer.Init(processed);
                SetArrow(false);
                await _revealer.PlayAsync(_cmdBus, ct);

                if (ct.IsCancellationRequested) break;

                _state = DialogueState.WaitInput;
                OnSentenceEnd?.Invoke();
                SetArrow(true, false);

                if (!_isEffectiveAuto.Value)
                {
                    // Manual: whichever comes first, an input or the player switching to auto.
                    // Merged into one stream rather than awaited as three tasks, so the two
                    // that lose are unsubscribed by First instead of living until the next
                    // cancellation.
                    try
                    {
                        var advance = Observable.Merge(
                            _isEffectiveAuto.Where(on => on).AsUnitObservable(),
                            _tapLayer.OnTap.Where(_ => _isInteractable).AsUnitObservable(),
                            Observable.EveryUpdate()
                                .Where(_ => _isInteractable && InputUtils.GetKeyDown(KeyCode.Space))
                                .AsUnitObservable());

                        await advance.First().ToUniTask(cancellationToken: ct);
                    }
                    // Cancellation is deliberately not caught: it belongs to whoever owns
                    // this player. Anything else means an input stream broke.
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        Debug.LogError($"[DialoguePlayer] Advance input failed: {e}");
                    }
                }
                else
                {
                    // Auto: the delay is precomputed by the settings subscription, and is zero
                    // under FastPlay.
                    await UniTask.WaitForSeconds(_autoSkipDelaySec, cancellationToken: ct);
                }

                if (ct.IsCancellationRequested) break;

                UISoundManager.Instance.PlaySound(UISounds.Crispy_SFX_UI_Click_Organic_Crispy_Plastic_Thin_Negative_1);
                _idx++;
                _revealer.Clear();
            }
        }
        finally
        {
            // A restart cancels this loop and starts another one immediately, so this can
            // run after the new loop is already playing. Only the current loop is allowed
            // to report the state it left the box in.
            if (_cts == ownCts)
            {
                _state = DialogueState.Completed;
                SetArrow(false);
            }

            ownCts.Dispose();
        }
    }

    private void SetArrow(bool on, bool cont = false)
    {
        if (_arrow == null) return;
        _arrow.gameObject.SetActive(on);
        if (on)
            _arrow.transform.localRotation = Quaternion.Euler(Vector3.forward * (cont ? 0 : -90));
    }

    /// <summary>
    /// Moves the dialogue box between the bottom and top of the screen, so it can
    /// step out of the way of whatever the scene needs to show.
    /// </summary>
    
    public void SetLocationMode(LocationMode mode)
    {
        if (_box == null)
        {
            
            return;
        }

        _currentLocationMode = mode;
        RectTransform rectTransform = _box.GetComponent<RectTransform>();

        if (rectTransform == null)
        {
            
            return;
        }

        bool isUpMode = (mode == LocationMode.Up);

        if (isUpMode)
        {
            
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
        }
        else
        {
            
            rectTransform.anchorMin = new Vector2(0.5f, 0f);
            rectTransform.anchorMax = new Vector2(0.5f, 0f);
        }

        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        // Position Y
        rectTransform.anchoredPosition = new Vector2(
            rectTransform.anchoredPosition.x,
            isUpMode ? -220f : 215.3814f
        );

        // Scale
        float scale = isUpMode ? 0.95f : 1f;
        rectTransform.localScale = new Vector3(scale, scale, scale);

        if (_downModeArrow != null)
            _downModeArrow.SetActive(isUpMode);

    }

    public LocationMode GetCurrentLocationMode() => _currentLocationMode;

    private enum DialogueState { Idle, Playing, WaitInput, Completed }

}
