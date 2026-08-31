using System;
using UnityEngine;

/// <summary>
/// Creates Story instances by class name, so a card's string id can select
/// the compiled story class without a lookup table to keep in sync.
/// </summary>
public static class StoryFactory
{
    /// <summary>
    /// Instantiates the Story subclass whose type name matches storyID.
    /// </summary>
    /// <param name="storyID">Class name of the story, for example "Story_Chapter1".</param>
    /// <returns>The instance, or null if the type is missing or not a Story.</returns>
    public static Story GetStoryInstance(string storyID)
    {
        if (string.IsNullOrEmpty(storyID))
        {
            Debug.LogError("[StoryFactory] storyID is null or empty.");
            return null;
        }

        var t = Type.GetType(storyID);
        if (t == null || !typeof(Story).IsAssignableFrom(t))
        {
            Debug.LogError($"[StoryFactory] '{storyID}' was not found or does not derive from Story.");
            return null;
        }

        try
        {
            return Activator.CreateInstance(t) as Story;
        }
        catch (Exception e)
        {
            Debug.LogError($"[StoryFactory] Failed to instantiate '{storyID}': {e.Message}");
            return null;
        }
    }
}
