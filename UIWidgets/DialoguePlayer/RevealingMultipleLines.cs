using UnityEngine;
using TMPro;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;
using System.Threading;
using System;

[RequireComponent(typeof(RectTransform))]
public class RevealingMultipleLines : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform _container;
    [SerializeField] private RevealingLine _linePrefab;

    private readonly List<RevealingLine> _lines = new();
    private int   _curIdx = -1;
    private bool  _pause;

    private float _speed = 400f;
    private int   _font  = 36;

    private CompositeDisposable _subs = new();
    private CancellationTokenSource _localCts;
    private string _currentText = "";
    private TextMeshProUGUI _measurer;

    /* ---------- Command stream ---------- */
    public void Bind(IObservable<DialogueCmd> stream)
    {
        _subs.Dispose();
        _subs = new CompositeDisposable();

        stream.Subscribe(cmd =>
        {
            switch (cmd.Type)
            {
                case DialogueCmdType.Pause:
                    _pause = true;
                    CurrentLine?.Pause();
                    break;

                case DialogueCmdType.Resume:
                    _pause = false;
                    CurrentLine?.Resume();
                    break;

                case DialogueCmdType.CompleteCurrentLine:
                    CurrentLine?.Complete(); 
                    break;

                case DialogueCmdType.Exit:
                    Complete();              
                    break;

                case DialogueCmdType.Clear:
                    Clear();                 
                    break;
            }
        }).AddTo(_subs);
    }
    private void OnDestroy()
    {
        _localCts?.Cancel();
        _localCts = null;

        if (_measurer != null)
            Destroy(_measurer.gameObject);

        _subs?.Dispose();
        _subs = null;

        foreach (var l in _lines)
        {
            if (l != null && l.gameObject != null)
                Destroy(l.gameObject);
        }
        _lines.Clear();
    }

    /* ---------- Init / Settings ---------- */
    public void Init(string text)
    {
        Clear();
        _currentText = text;

        var split = BreakIntoLines(text, _container.rect.width);
        float y = 0f;
        foreach (var s in split)
        {
            var inst = Instantiate(_linePrefab, _container);
            inst.RectTransform.anchoredPosition = new Vector2(0, -y);
            inst.Init(s, _speed, _font);
            _lines.Add(inst);
            y += 60f;
        }
        _curIdx = 0;
    }

    public void SetSpeed(float s) { _speed = s; foreach (var l in _lines) l.SetSpeed(s); }
    /// <summary>
    /// Changing the size relays out every line, which invalidates the position the reveal
    /// had reached. Rather than trying to map a cursor onto new geometry, the current
    /// dialogue is shown in full and the next one starts normally.
    /// </summary>
    public void SetFontSizeAndRestart(int size)
    {
        _font = size;

        if (_lines.Count == 0) return;

        Init(_currentText);
        Complete();
    }

    /* ---------- Play ---------- */
    public async UniTask PlayAsync(IObservable<DialogueCmd> ctrl, CancellationToken ct)
    {
        if (_lines.Count == 0) return;

        _localCts?.Cancel();

        // Owned by this run and disposed by it: cancelling from outside must not pull the
        // source out from under a loop that is still reading its token.
        var runCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _localCts = runCts;

        CancellationToken token = runCts.Token;

        try
        {
            for (_curIdx = 0; _curIdx < _lines.Count && !token.IsCancellationRequested; _curIdx++)
            {
                var line = _lines[_curIdx];
                line.Play(token);

                while (!line.IsCompleted && !token.IsCancellationRequested)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
        }
        finally
        {
            if (_localCts == runCts)
                _localCts = null;

            runCts.Dispose();
        }
    }

    public RevealingLine CurrentLine =>
        _curIdx >= 0 && _curIdx < _lines.Count ? _lines[_curIdx] : null;

    public void Complete() { foreach (var l in _lines) l.Complete(); }

    public void Clear()
    {
        _localCts?.Cancel();
        foreach (var l in _lines) if (l) Destroy(l.gameObject);
        _lines.Clear();
        _curIdx = -1;
    }

    /// <summary>
    /// One off-screen text object, reused for every measurement. Creating and destroying one
    /// per dialogue meant DestroyImmediate in runtime code, since a deferred Destroy would
    /// let it render for a frame.
    /// </summary>
    private TextMeshProUGUI GetMeasurer(float max)
    {
        if (_measurer == null)
        {
            var go = new GameObject("LineMeasurer");
            go.transform.SetParent(transform, worldPositionStays: false);

            _measurer = go.AddComponent<TextMeshProUGUI>();
            _measurer.font = _linePrefab.TextMesh.font;
            _measurer.richText = true;
            _measurer.textWrappingMode = TextWrappingModes.NoWrap;
            _measurer.overflowMode = TextOverflowModes.Overflow;
        }

        _measurer.rectTransform.sizeDelta = new Vector2(max, 1000);
        _measurer.fontSize = _font;

        return _measurer;
    }

    private List<string> BreakIntoLines(string text, float max)
    {
        var tmp = GetMeasurer(max);

        var lines = new List<string>();
        string[] words = text.Split(' ');
        string curr = "";

        foreach (var w in words)
        {
            string test = curr.Length == 0 ? w : $"{curr} {w}";

            // GetPreferredValues is measurably cheaper here than ForceMeshUpdate.
            Vector2 preferredSize = tmp.GetPreferredValues(test, max, 0);

            if (preferredSize.x > max && curr.Length > 0)
            {
                lines.Add(curr);
                curr = w;
            }
            else curr = test;
        }
        if (!string.IsNullOrWhiteSpace(curr)) lines.Add(curr);

        // Left empty so the reused object draws nothing between dialogues.
        tmp.text = "";

        return lines;
    }
}
