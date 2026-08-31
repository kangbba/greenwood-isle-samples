using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Character affinity. Holds no state of its own: every read and write goes straight to
/// the bound SaveData, so what the player sees, what branches evaluate, and what gets
/// written to disk are always the same numbers.
/// </summary>
public class AffinityManager : SaveSingletonMono<AffinityManager>
{
    protected override bool UseDontDestroyOnLoad => false;

    protected override void Release()
    {
        // Nothing cached — nothing to release.
    }

    public SaveData CurrentSave => CurrentSaveData;

    private SaveData GetActiveSaveOrNull()
    {
        if (CurrentSaveData == null)
        {
            Debug.LogWarning("[AffinityManager] SaveData not bound.");
            return null;
        }
        return CurrentSaveData;
    }

    public int GetAffinity(string characterId)
    {
        var saveData = GetActiveSaveOrNull();
        if (saveData?.affinityData != null && saveData.affinityData.TryGetValue(characterId, out var value))
            return value;

        return 0;
    }

    /// <summary>Highest-affinity character across everyone met so far.</summary>
    public string GetHighestAffinityCharacterID_All(out int highestValue)
    {
        highestValue = 0;
        var saveData = GetActiveSaveOrNull();

        if (saveData?.affinityData == null || saveData.affinityData.Count == 0)
            return null;

        var bestId = saveData.affinityData
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => kvp.Key)
            .FirstOrDefault();

        if (bestId == null) return null;
        highestValue = GetAffinity(bestId);
        return bestId;
    }

    /// <summary>Lowest-affinity character across everyone met so far.</summary>
    public string GetLowestAffinityCharacterID_All(out int lowestValue)
    {
        lowestValue = 0;
        var saveData = GetActiveSaveOrNull();

        if (saveData?.affinityData == null || saveData.affinityData.Count == 0)
            return null;

        var worstId = saveData.affinityData
            .OrderBy(kvp => kvp.Value)
            .Select(kvp => kvp.Key)
            .FirstOrDefault();

        if (worstId == null) return null;
        lowestValue = GetAffinity(worstId);
        return worstId;
    }

    /// <summary>Sorts the given candidates by affinity, highest first.</summary>
    public List<string> GetAffinityRanking(List<string> candidates)
    {
        var saveData = GetActiveSaveOrNull();

        if (saveData == null || candidates == null || candidates.Count == 0)
            return new List<string>();

        return candidates
            .OrderByDescending(id => GetAffinity(id))
            .ToList();
    }

    /// <summary>Highest-affinity character within the given candidates.</summary>
    public string GetHighestAffinityCharacterID_FromList(List<string> candidates, out int highestValue)
    {
        highestValue = 0;
        var saveData = GetActiveSaveOrNull();

        if (saveData == null || candidates == null || candidates.Count == 0)
            return null;

        var ranking = GetAffinityRanking(candidates);
        if (ranking.Count == 0) return null;

        var bestId = ranking.First();
        highestValue = GetAffinity(bestId);
        return bestId;
    }

    /// <summary>Adds to affinity; negative amounts are allowed.</summary>
    public void GainAffinity(string characterId, int amount)
    {
        var saveData = GetActiveSaveOrNull();
        if (saveData == null)
            return;

        if (saveData.affinityData == null)
            saveData.affinityData = new Dictionary<string, int>();

        if (!saveData.affinityData.ContainsKey(characterId))
            saveData.affinityData[characterId] = 0;

        saveData.affinityData[characterId] += amount;

        string sign = amount >= 0 ? "+" : "";
        Debug.Log($"[AffinityManager] {characterId} affinity {sign}{amount} -> {saveData.affinityData[characterId]}");
    }

    public bool HasAffinity(string characterId)
    {
        var saveData = GetActiveSaveOrNull();

        return saveData?.affinityData != null && saveData.affinityData.ContainsKey(characterId);
    }

    /// <summary>Deep copy, for story-log snapshots — the live dictionary stays private to the save.</summary>
    public Dictionary<string, int> ExportAffinityData()
    {
        var saveData = GetActiveSaveOrNull();

        if (saveData?.affinityData == null)
            return new Dictionary<string, int>();

        return new Dictionary<string, int>(saveData.affinityData);
    }
}
