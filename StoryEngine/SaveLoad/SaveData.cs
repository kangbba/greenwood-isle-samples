using System;
using System.Globalization;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using UnityEngine;

[Serializable]
public class SaveData
{
    // =========================
    // ---- Metadata ----
    // =========================
    [JsonProperty("storyBuildVersion")]
    public int storyBuildVersion;

    [JsonProperty("saveTime")]
    public string saveTime;

    [JsonProperty("gameVersion")]
    public string gameVersion;

    // =========================
    // ---- Progress ----
    // =========================
    [JsonProperty("currentCardID")]
    public string currentCardID;

    [JsonProperty("currentElementIndex")]
    public int currentElementIndex;

    // =========================
    // ---- World state ----
    // =========================
    [JsonProperty("ownedItemIDs")]
    public List<string> ownedItemIDs;

    [JsonProperty("choiceHistory")]
    public Dictionary<string, int> choiceHistory;

    [JsonProperty("affinityData")]
    public Dictionary<string, int> affinityData;

    [JsonProperty("completedStoryIDs")]
    public List<string> completedStoryIDs;

    // =========================
    // ---- Beliefs ----
    // =========================
    [JsonProperty("beliefData")]
    public Dictionary<string, string> beliefData;

    public SaveData(
        int storyBuildVersion,
        string currentCardID = "",
        int currentElementIndex = 0,
        List<string> ownedItemIDs = null,
        Dictionary<string, int> choiceHistory = null,
        Dictionary<string, int> affinityData = null,
        List<string> completedStoryIDs = null,
        Dictionary<string, string> beliefData = null,
        string saveTime = null,
        string gameVersion = null
    )
    {
        this.storyBuildVersion = storyBuildVersion;
        this.currentCardID = currentCardID ?? "";
        this.currentElementIndex = currentElementIndex;

        this.ownedItemIDs = ownedItemIDs ?? new List<string>();
        this.choiceHistory = choiceHistory ?? new Dictionary<string, int>();
        this.affinityData = affinityData ?? new Dictionary<string, int>();
        this.completedStoryIDs = completedStoryIDs ?? new List<string>();

        this.beliefData = beliefData ?? new Dictionary<string, string>();
        EnsureBeliefDefaults();

        this.saveTime = string.IsNullOrEmpty(saveTime)
            ? DateTime.UtcNow.ToString("o")
            : saveTime;

        this.gameVersion = string.IsNullOrEmpty(gameVersion)
            ? Application.version
            : gameVersion;
    }

    // =========================
    // A deep copy. The story log keeps a snapshot per logged element, so handing
    // out the live instance would make every past entry show the present state.
    // =========================
    public SaveData GetCopiedSaveData(
        int? storyBuildVersion = null,
        string currentCardID = null,
        int? currentElementIndex = null,
        List<string> ownedItemIDs = null,
        Dictionary<string, int> choiceHistory = null,
        Dictionary<string, int> affinityData = null,
        List<string> completedStoryIDs = null,
        Dictionary<string, string> beliefData = null,
        string saveTime = null,
        string gameVersion = null
    )
    {
        return new SaveData(
            storyBuildVersion ?? this.storyBuildVersion,
            currentCardID ?? this.currentCardID,
            currentElementIndex ?? this.currentElementIndex,
            ownedItemIDs ?? new List<string>(this.ownedItemIDs),
            choiceHistory ?? new Dictionary<string, int>(this.choiceHistory),
            affinityData ?? new Dictionary<string, int>(this.affinityData),
            completedStoryIDs ?? new List<string>(this.completedStoryIDs),
            beliefData ?? new Dictionary<string, string>(this.beliefData),
            saveTime ?? DateTime.UtcNow.ToString("o"),
            gameVersion ?? this.gameVersion
        );
    }

    // =========================
    // Beliefs added after a save was written default rather than throwing.
    // =========================
    private void EnsureBeliefDefaults()
    {
        if (beliefData == null)
            beliefData = new Dictionary<string, string>();

        foreach (BeliefType type in Enum.GetValues(typeof(BeliefType)))
        {
            string key = type.SaveKey();
            if (!beliefData.ContainsKey(key))
                beliefData[key] = "";
        }
    }

    // =========================
    // Absorbs fields written by older builds.
    // =========================
    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        currentCardID ??= "";
        ownedItemIDs ??= new List<string>();
        choiceHistory ??= new Dictionary<string, int>();
        affinityData ??= new Dictionary<string, int>();
        completedStoryIDs ??= new List<string>();

        EnsureBeliefDefaults();
    }
    /// <summary>
    /// saveTime is written as a round-trip ("o") UTC string, so it is read back the same
    /// way: parsing it with the device's culture would depend on where the player is.
    /// Only the display string is localised.
    /// </summary>
    public string GetDisplayTime(string format = "yyyy-MM-dd HH:mm:ss")
    {
        if (!DateTime.TryParseExact(saveTime, "o", CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out DateTime utc))
        {
            return "Invalid Time";
        }

        return utc.ToLocalTime().ToString(format, CultureInfo.CurrentCulture);
    }

    public bool IsValidSave()
    {
        // The minimum needed to resume: somewhere to resume to.
        return !string.IsNullOrEmpty(currentCardID);
    }

}
