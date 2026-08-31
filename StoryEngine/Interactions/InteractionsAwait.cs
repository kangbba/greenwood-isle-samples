using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UniRx;

/// <summary>
/// An escape-room investigation as a single element: the story waits until every
/// hotspot has been found and its payload has finished. Being an Element is what keeps
/// interactive segments out of the story loop as a special case, and each hotspot's
/// payload is itself an Element, so it can grant an item or nest a whole sub-sequence.
/// </summary>
public class InteractionsAwait : Element
{
    private const float ProximityThreshold = 100f;

    private readonly List<InteractionInfo> _interactionInfos;
    private readonly Dictionary<int, bool> _buttonClickStates = new();
    private readonly List<InteractionButton> _createdButtons = new();
    // Disposed once, in ExitOnDestroy. Safe because an element instance runs a single
    // time: a story is rebuilt from its script rather than replayed on the same objects.
    private readonly CompositeDisposable _proximityDisposables = new();

    private bool _isPlayingInteraction;

    private bool IsAllCompleted
    {
        get
        {
            foreach (bool clicked in _buttonClickStates.Values)
            {
                if (!clicked)
                    return false;
            }
            return true;
        }
    }

    // Both conditions matter: the last hotspot's payload has to finish, or the
    // story would advance over the top of it.
    private bool CanExit => IsAllCompleted && !_isPlayingInteraction;

    public InteractionsAwait(params InteractionInfo[] interactionInfos)
    {
        _interactionInfos = new List<InteractionInfo>(interactionInfos ?? Array.Empty<InteractionInfo>());
    }

    public override async UniTask ExecuteAsync(StoryContext ctx, CancellationToken token)
    {
        if (_interactionInfos.Count == 0)
        {
            Debug.LogWarning("[InteractionsAwait] No hotspots to create.");
            return;
        }

        _buttonClickStates.Clear();
        for (int i = 0; i < _interactionInfos.Count; i++)
        {
            _buttonClickStates[i] = false;
        }

        var tutorialManager = ctx.Tutorials;

        await tutorialManager.CreateAndShowSearchTutorialsAsync().AttachExternalCancellation(token);
        tutorialManager.UpdateSearchProgressText(_interactionInfos.Count, 0);

        CreateButtons(ctx, token);

        // Created hidden and revealed a beat later, so the hotspots do not pop in
        // on the same frame as the place transition.
        SetAllButtonsHidden();
        await UniTask.Delay(TimeSpan.FromSeconds(.5f), cancellationToken: token);
        SetAllButtonsIdle();

        Refresh(ctx);
        StartProximityDetection();

        await UniTask.WaitUntil(() => CanExit, cancellationToken: token);

        await tutorialManager.HideSearchTutorialsAsync().AttachExternalCancellation(token);
        ctx.Interactions.ClearAllButtons();
    }

    /// <summary>
    /// Investigation has no instant form: on save restore the player is placed at the
    /// start of the element and searches again. Its hotspot payloads still restore
    /// through their own ExecuteInstantly, so items already taken are not re-granted.
    /// </summary>
    public override void ExecuteInstantly(StoryContext ctx)
    {
    }

    public override void ExitOnDestroy()
    {
        _proximityDisposables.Dispose();
        InteractionManager.Instance.ClearAllButtons();
        _createdButtons.Clear();
        _buttonClickStates.Clear();
        TutorialManager.Instance.DestroyAllTutorialTexts();
    }

    private void CreateButtons(StoryContext ctx, CancellationToken token)
    {
        // Hotspots are authored in the current place's coordinates; InteractionManager
        // positions them there and then moves them onto the interface canvas.
        var currentPlace = ctx.Places != null ? ctx.Places.CurrentPlace : null;
        if (currentPlace == null)
        {
            Debug.LogError("[InteractionsAwait] Cannot create hotspots: no current place.");
            return;
        }

        Transform parent = currentPlace.transform;
        var interactionManager = ctx.Interactions;

        _createdButtons.Clear();
        for (int i = 0; i < _interactionInfos.Count; i++)
        {
            var info = _interactionInfos[i];
            int buttonId = i;

            var button = interactionManager.MakeInteractionButton(
                info.Type,
                parent,
                info.AnchoredPos,
                () => OnButtonClicked(buttonId, info, ctx, token).Forget());

            _createdButtons.Add(button);
        }
    }

    private async UniTaskVoid OnButtonClicked(int buttonId, InteractionInfo info, StoryContext ctx, CancellationToken token)
    {
        _isPlayingInteraction = true;

        try
        {
            // Hidden for the duration, so the player cannot start a second hotspot
            // while this one is still playing.
            SetAllButtonsHidden();

            if (info.UseFocus)
            {
                await new PlaceZoomEnter(info.AnchoredPos).ExecuteAsync(ctx, token);
            }

            await info.Elements.ExecuteAsync(ctx, token);

            _buttonClickStates[buttonId] = true;

            if (info.UseFocus)
            {
                await new PlaceZoomClear().ExecuteAsync(ctx, token);
            }

            Refresh(ctx);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Nothing awaits this method, so a failed payload would otherwise be invisible
            // here: the hotspots would stay hidden and the element would wait forever for a
            // completion that cannot arrive. Put the scene back and let the player retry.
            Debug.LogError($"[InteractionsAwait] Hotspot payload failed: {e}");

            if (info.UseFocus)
            {
                new PlaceZoomClear().ExecuteInstantly(ctx);
            }

            Refresh(ctx);
        }
        finally
        {
            // Cleared in a finally so a cancelled payload cannot leave the element
            // permanently unable to exit.
            _isPlayingInteraction = false;
        }
    }

    private void SetAllButtonsHidden() => SetAllButtons(InteractionButtonStatus.Hidden);

    private void SetAllButtonsIdle() => SetAllButtons(InteractionButtonStatus.Idle);

    private void SetAllButtons(InteractionButtonStatus status)
    {
        foreach (var button in _createdButtons)
        {
            if (button != null)
                button.SetStatus(status);
        }
    }

    private void Refresh(StoryContext ctx)
    {
        for (int i = 0; i < _createdButtons.Count; i++)
        {
            if (_createdButtons[i] == null) continue;

            bool done = _buttonClickStates.TryGetValue(i, out bool clicked) && clicked;
            _createdButtons[i].SetStatus(done
                ? InteractionButtonStatus.Completed
                : InteractionButtonStatus.Idle);
        }

        UpdateProgressText(ctx);
    }

    private void UpdateProgressText(StoryContext ctx)
    {
        int completed = 0;
        foreach (bool clicked in _buttonClickStates.Values)
        {
            if (clicked) completed++;
        }

        ctx.Tutorials.UpdateSearchProgressText(_buttonClickStates.Count, completed);
    }

    /// <summary>
    /// Highlights a hotspot as the cursor nears it, so searching is guided rather
    /// than pixel hunting. Subscriptions go into the composite and are disposed in
    /// ExitOnDestroy, since EveryUpdate outlives the element otherwise.
    /// </summary>
    private void StartProximityDetection()
    {
        _proximityDisposables.Clear();

        foreach (var button in _createdButtons)
        {
            if (button == null) continue;

            var rectTransform = button.GetComponent<RectTransform>();
            if (rectTransform == null) continue;

            Observable.EveryUpdate()
                .Where(_ => button != null && rectTransform != null)
                .Subscribe(_ =>
                {
                    bool isNear = MouseUtils.IsMouseWithinRadius(rectTransform.position, ProximityThreshold);
                    button.SetPlayerProximity(isNear);
                })
                .AddTo(_proximityDisposables);
        }
    }
}
