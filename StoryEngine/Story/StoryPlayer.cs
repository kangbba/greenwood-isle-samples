using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Runs one story's element list, then dies with it: a fresh player per story, and
/// destroying the object is the only way to stop one.
///
/// Init replays everything before the resume index through ExecuteInstantly, so a
/// save is restored without animating hours of content. PlayElements runs the rest.
/// </summary>
public class StoryPlayer : MonoBehaviour
{
    private static readonly Type[] ElementsToHideLetterbox =
    {
        typeof(VideoEnter),
        typeof(VideoInteractionAwait),
        typeof(VideoPlayThenClear),
        typeof(NarrationEnter),
    };

    private static readonly HashSet<Type> StoryLoggableTypes = new()
    {
        typeof(Dialogue),
        typeof(PlaceEnter),
        typeof(ImaginationEnter),
    };

    private Story _currentStoryInstance;
    private Element _currentExecutingElement;
    private StoryContext _ctx;

    public Story CurrentStory => _currentStoryInstance;

    // The destroy token unwinds whatever is being awaited; the element still needs
    // teardown for subscriptions and tweens the token does not reach.
    private void OnDestroy()
    {
        _currentExecutingElement?.ExitOnDestroy();
        _currentExecutingElement = null;
    }

    /// <summary>Rebuilds state up to initialElementIndex without animating. Call before PlayElements.</summary>
    public void Init(Story storyInstance, SaveData saveData, int initialElementIndex)
    {
        if (storyInstance == null)
        {
            Debug.LogError("[StoryPlayer] Story instance is null.");
            return;
        }

        if (saveData == null)
        {
            Debug.LogError("[StoryPlayer] SaveData is null.");
            return;
        }

        if (_currentStoryInstance != null)
        {
            Debug.LogError(
                $"[StoryPlayer] A story is already set. Current: {_currentStoryInstance.GetType().Name}, " +
                $"new: {storyInstance.GetType().Name}. One player runs one story.");
            return;
        }

        _currentStoryInstance = storyInstance;

        // Resolved once per playback; elements only ever see this.
        _ctx = StoryContext.FromManagers();

        StoryLogManager.Instance.ClearElements();
        _currentStoryInstance.PreloadResources();

        var elements = storyInstance.UpdateElements;

        // A save written before a content update can point past the end of a story that
        // has since been shortened. Resume from where the story now ends rather than
        // throwing on the player's only save.
        if (initialElementIndex < 0 || initialElementIndex > elements.Count)
        {
            Debug.LogWarning(
                $"[StoryPlayer] Save resumes at element {initialElementIndex}, but the story " +
                $"has {elements.Count}. Clamping.");

            initialElementIndex = Mathf.Clamp(initialElementIndex, 0, elements.Count);
        }

        // The replay walks the index forward as it goes, so the log records each element
        // against the state it was reached with.
        for (int i = 0; i < initialElementIndex; i++)
        {
            saveData.currentElementIndex = i;
            ProcessElementActions(elements[i], saveData);
            elements[i].ExecuteInstantly(_ctx);
        }

        // Left where the story resumes, not where the replay stopped.
        saveData.currentElementIndex = initialElementIndex;
    }

    /// <summary>
    /// Plays from initialElementIndex to the end.
    /// </summary>
    public async UniTask PlayElements(SaveData saveData, int initialElementIndex)
    {
        if (_currentStoryInstance == null)
        {
            Debug.LogError("[StoryPlayer] PlayElements called before Init.");
            return;
        }

        CancellationToken token = this.GetCancellationTokenOnDestroy();

        var elements = _currentStoryInstance.UpdateElements;

        for (int i = initialElementIndex; i < elements.Count; i++)
        {
            saveData.currentElementIndex = i;
            ProcessElementActions(elements[i], saveData);

            // Tracked so OnDestroy can tear down whichever element is mid-flight.
            _currentExecutingElement = elements[i];
            await elements[i].ExecuteAsync(_ctx, token);
            _currentExecutingElement = null;
        }

        await WaitUntilPopupCleared(token);
    }

    /// <summary>Holds at the last element until popups close, so the next story does not start under one.</summary>
    private static async UniTask WaitUntilPopupCleared(CancellationToken token)
    {
        await UniTask.WaitUntil(
            () => PopupCanvas.Instance == null || !PopupCanvas.Instance.PopupExistNotifier.Value,
            cancellationToken: token);
    }

    /// <summary>What the player owns rather than the element: the story log, and letterbox visibility.</summary>
    private void ProcessElementActions(Element element, SaveData saveData)
    {
        if (element == null) return;

        if (IsStoryLoggable(element))
        {
            StoryLogManager.Instance.AddElement(saveData.GetCopiedSaveData(), element);
        }

        var letterbox = InGameManager.Instance.CurrentInterfaceCanvas.LetterBox;

        if (ShouldOfferQuickButtons(element, IsAtDeadEnd(saveData)))
        {
            letterbox.ShowAndActiveQuickButtons();
        }
        else
        {
            letterbox.HideAndDeactiveQuickButtons();
        }
    }

    /// <summary>
    /// Whether to offer the buttons that advance the story. Decided on what is passed in
    /// rather than on the scene, so the rule reads on its own: an element that takes the
    /// screen has nothing to advance past yet, and a dead end has no continuation at all,
    /// which makes offering the buttons there a lie.
    /// </summary>
    internal static bool ShouldOfferQuickButtons(Element element, bool isAtDeadEnd)
    {
        if (isAtDeadEnd) return false;

        return !IsAnyOf(ElementsToHideLetterbox, element);
    }

    /// <summary>The one read behind that decision, kept apart from it.</summary>
    private static bool IsAtDeadEnd(SaveData saveData)
    {
        if (string.IsNullOrEmpty(saveData.currentCardID)) return false;

        var storyData = StoryAssetManager.Instance?.GetStoryData(saveData.currentCardID);
        return storyData != null && storyData.IsDeadEnd;
    }

    private static bool IsStoryLoggable(Element element) => IsAnyOf(StoryLoggableTypes, element);

    /// <summary>
    /// Assignability rather than an exact match, so a subclass is classified with its
    /// parent instead of having to be listed again.
    /// </summary>
    private static bool IsAnyOf(IEnumerable<Type> types, Element element)
    {
        var elementType = element.GetType();

        foreach (var type in types)
        {
            if (type.IsAssignableFrom(elementType))
                return true;
        }

        return false;
    }
}
