using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ChoiceManager : SaveSingletonMono<ChoiceManager>
{
    protected override bool UseDontDestroyOnLoad => false;

    [SerializeField] private ChoiceWindowMultiple _choiceWindowMultiplePrefab;
    private ChoiceWindowMultiple _currentChoiceWindowMultiple;

    protected override void Release()
    {
        if (_currentChoiceWindowMultiple != null)
        {
            _currentChoiceWindowMultiple.FadeAndDestroy(0f);
            _currentChoiceWindowMultiple = null;
        }
    }

    public UniTask<int> WaitForChoiceWindowResult(Choice choice, CancellationToken token)
        => WaitForChoiceWindowResult(choice.Question, choice.Choices, token);

    /// <summary>
    /// Opens the choice window and resolves with the selected index.
    /// Cancelling closes the window, so a story torn down while the player is
    /// deciding does not leave the popup on screen waiting for an answer.
    /// </summary>
    public async UniTask<int> WaitForChoiceWindowResult(
        string question, IReadOnlyList<ChoiceOption> choices, CancellationToken token)
    {
        CloseCurrentChoice(0f);

        using (token.Register(() => CloseCurrentChoice(0f)))
        {
            var go = await PopupCanvas.Instance.RequestPopup(PopupType.Choice).AttachExternalCancellation(token);

            // Refused: another popup is mid-request, or the type is not registered.
            if (go == null)
            {
                Debug.LogError("[ChoiceManager] PopupCanvas did not return a choice window.");
                return -1;
            }

            _currentChoiceWindowMultiple = go.GetComponent<ChoiceWindowMultiple>();

            // RequestPopup does not take the token, so it finishes building the window even
            // if cancellation lands mid-request. When that happens the registered callback
            // above has already run against a null window, so close it here instead.
            if (token.IsCancellationRequested)
            {
                CloseCurrentChoice(0f);
                token.ThrowIfCancellationRequested();
            }

            _currentChoiceWindowMultiple.Init(question);

            var options = choices as List<ChoiceOption> ?? new List<ChoiceOption>(choices);
            return await _currentChoiceWindowMultiple.ShowChoices(options).AttachExternalCancellation(token);
        }
    }

    public void CloseCurrentChoice(float duration)
    {
        _currentChoiceWindowMultiple?.FadeAndDestroy(duration);
        _currentChoiceWindowMultiple = null;
    }

    /// <summary>
    /// Records a choice in the bound save.
    /// </summary>
    public void SaveChoice(string choiceId, int index)
    {
        if (string.IsNullOrEmpty(choiceId)) return;

        var save = CurrentSaveData;
        if (save == null) return;

        save.choiceHistory ??= new Dictionary<string, int>();
        save.choiceHistory[choiceId] = index;
    }

    /// <summary>
    /// Reads back a previous choice.
    /// </summary>
    public bool TryGetChoice(string choiceId, out int index)
    {
        index = -1;
        if (string.IsNullOrEmpty(choiceId)) return false;

        var save = CurrentSaveData;
        if (save == null) return false;

        save.choiceHistory ??= new Dictionary<string, int>();
        return save.choiceHistory.TryGetValue(choiceId, out index);
    }

    public int GetSelectedIndex(string choiceId)
    {
        if (string.IsNullOrEmpty(choiceId)) return -1;

        if (!HasSaveData)
            return -1;

        return TryGetChoice(choiceId, out var index) ? index : -1;
    }
}
