using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;

[RequireComponent(typeof(RectMask2D))]
public class RevealingLine : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _textMesh;
    private RectMask2D _mask;
    private RectTransform _maskTransform;

    private float _playSpeed;
    private float _lastTextWidth;
    private float _cursorX;

    private List<char> _visibleChars = new();
    private List<float> _charXRight = new();

    private RevealState _state = RevealState.Idle;
    private CancellationTokenSource _cts;

    private enum RevealState
    {
        Idle,
        Playing,
        Paused,
        Completed
    }

    public RectTransform RectTransform => GetComponent<RectTransform>();
    public TextMeshProUGUI TextMesh => _textMesh;
    public float CursorX => _cursorX;
    public bool IsPlaying => _state == RevealState.Playing;
    public bool IsPaused => _state == RevealState.Paused;
    public bool IsCompleted => _state == RevealState.Completed;

    public char CurrentPlayingChar => GetCharAtCursor(true);
    public char RecentCompletedChar => GetCharAtCursor(false);

    private void Awake()
    {
        _mask = GetComponent<RectMask2D>();
        _maskTransform = GetComponent<RectTransform>();
    }

    // Cancels without disposing: a run owns its own source and disposes it on the way out.
    private void ResetInternalState()
    {
        _cts?.Cancel();
        _cts = null;

        _cursorX = 0f;
        _visibleChars.Clear();
        _charXRight.Clear();
        _state = RevealState.Idle;
        _textMesh.text = "";
    }

    public void Init(string text, float initialSpeed, int fontSize)
    {
        Clear();

        _playSpeed = Mathf.Max(1f, initialSpeed);
        _textMesh.fontSize = fontSize;
        _textMesh.text = text;
        _textMesh.ForceMeshUpdate();

        var info = _textMesh.textInfo;
        for (int i = 0; i < info.characterCount; i++)
        {
            var charInfo = info.characterInfo[i];
            if (!charInfo.isVisible) continue;

            _visibleChars.Add(charInfo.character);
            _charXRight.Add(charInfo.topRight.x);
        }

        _lastTextWidth = _textMesh.preferredWidth;
        _textMesh.rectTransform.sizeDelta = new Vector2(_lastTextWidth, _textMesh.rectTransform.sizeDelta.y);
        _maskTransform.sizeDelta = new Vector2(_lastTextWidth, _maskTransform.sizeDelta.y);

        HideAll();
    }

    /// <summary>Clamped like Init: a speed of zero or less would never finish the line.</summary>
    public void SetSpeed(float speed)
    {
        _playSpeed = Mathf.Max(1f, speed);
    }

    /// <summary>
    /// Reveals the line. The caller's token is linked in, so cancelling the dialogue that
    /// owns this line stops the reveal too rather than leaving it running on its own.
    /// </summary>
    public void Play(CancellationToken parentToken)
    {
        if (_charXRight.Count == 0)
        {
            Debug.LogError("[RevealingLine] Init() must be called first.");
            return;
        }

        _cts?.Cancel();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        PlayAsync(_cts).Forget();
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private async UniTaskVoid PlayAsync(CancellationTokenSource ownCts)
    {
        CancellationToken token = ownCts.Token;

        _state = RevealState.Playing;

        // Replaying a finished line starts from a hidden mask, not from wherever the last
        // run left it.
        _cursorX = 0f;
        HideAll();

        try
        {
            while (_cursorX < _lastTextWidth && !token.IsCancellationRequested)
            {
                if (_state == RevealState.Paused)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    continue;
                }

                _cursorX += _playSpeed * Time.deltaTime;
                _cursorX = Mathf.Min(_cursorX, _lastTextWidth);
                RevealTill(_cursorX);

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // Cancellation leaves the mask alone: whoever cancelled has already decided what
            // the line should look like, and a late RevealAll here would undo it.
            if (token.IsCancellationRequested)
                return;

            _cursorX = _lastTextWidth;
            RevealAll();
            _state = RevealState.Completed;
        }
        finally
        {
            ownCts.Dispose();
        }
    }

    public void Pause()
    {
        if (_state == RevealState.Playing)
            _state = RevealState.Paused;
    }

    public void Resume()
    {
        if (_state == RevealState.Paused)
            _state = RevealState.Playing;
    }

    public void Complete()
    {
        _cts?.Cancel();
        _cursorX = _lastTextWidth;
        RevealAll();
        _state = RevealState.Completed;
    }

    public void Clear()
    {
        ResetInternalState();
        HideAll();
    }

    private void RevealTill(float length)
    {
        _mask.padding = new Vector4(0, 0, _lastTextWidth - length, 0);
    }

    private void RevealAll()
    {
        _mask.padding = new Vector4(0, 0, 0, 0);
    }
    private void HideAll()
    {
        _mask.padding = new Vector4(0, 0, _lastTextWidth + 100, 0);
    }

    private char GetCharAtCursor(bool isCurrent)
    {
        for (int i = 0; i < _charXRight.Count; i++)
        {
            if (_cursorX < _charXRight[i])
                return (i > 0 && !isCurrent) ? _visibleChars[i - 1] : _visibleChars[i];
        }

        return _visibleChars.Count > 0 ? _visibleChars[^1] : ' ';
    }

    public List<float> GetCharXPositions(IEnumerable<char> targets)
    {
        HashSet<char> lookup = new(targets);
        List<float> result = new();

        for (int i = 0; i < _visibleChars.Count && i < _charXRight.Count; i++)
        {
            bool isLastChar = (i == _visibleChars.Count - 1);
            if (lookup.Contains(_visibleChars[i]) && !isLastChar)
            {
                result.Add(_charXRight[i]);
            }
        }

        return result;
    }
}
