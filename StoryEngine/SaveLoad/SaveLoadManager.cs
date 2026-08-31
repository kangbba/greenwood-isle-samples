using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using UniRx;

/// <summary>
/// Why a slot is unusable, not just whether it is. A corrupt file and an empty
/// one look the same to the player otherwise, and only one of them is a bug.
/// </summary>
public enum SlotStatus
{
    Empty,          // No file.
    Valid,
    EmptyFile,      // File exists but has no contents.
    InvalidJson,    // Present but not parseable.
    InvalidData     // Parses, but fails its own validity check.
}

public class SlotStatusInfo
{
    public int SlotIndex;
    public SlotStatus Status;
    public string FilePath;
    public long FileSize;
    public string ErrorMessage;
    public SaveData Data;

    public string GetStatusText()
    {
        switch (Status)
        {
            case SlotStatus.Empty: return "Empty";
            case SlotStatus.Valid: return "OK";
            case SlotStatus.EmptyFile: return "Empty file";
            case SlotStatus.InvalidJson: return "Corrupt";
            case SlotStatus.InvalidData: return "Invalid";
            default: return "Unknown";
        }
    }
}

public class SaveLoadManager : SingletonMono<SaveLoadManager>
{
    protected override bool UseDontDestroyOnLoad => true;

    protected override void Release()
    {
    }

    private const int SlotCount = 8;
    private string SaveDirectory => Application.persistentDataPath;
    private const string DefaultStartStoryId = "Intro";

    /// <summary>True when at least one slot holds a save. Reactive, so the title
    /// screen's Continue button enables itself.</summary>
    private readonly ReactiveProperty<bool> _hasAnySaveData = new ReactiveProperty<bool>(false);
    public IReadOnlyReactiveProperty<bool> HasAnySaveData => _hasAnySaveData;

    protected override void Awake()
    {
        base.Awake();
        UpdateHasAnySaveData();
    }

    /// <summary>
    /// Counts slots the game can actually resume from. A zero-length or corrupt file is
    /// not one, and offering Continue for it would fail after the player pressed it.
    /// </summary>
    private void UpdateHasAnySaveData()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (AnalyzeSlot(i).Status == SlotStatus.Valid)
            {
                _hasAnySaveData.Value = true;
                return;
            }
        }

        _hasAnySaveData.Value = false;
    }

    private bool IsValidSlotIndex(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < SlotCount)
            return true;

        Debug.LogError($"[SaveLoadManager] Slot {slotIndex} is out of range (0-{SlotCount - 1}).");
        return false;
    }

    public int GetSlotCount() => SlotCount;

    public bool ExistsSlotData(int slotIndex)
    {
        string path = GetSlotPath(slotIndex);
        return File.Exists(path);
    }

    /// <summary>Writes the given data. Needs no session, unlike SaveWithCurrentData.</summary>
    public void SaveWithData(SaveData saveData, int slotIndex)
    {
        Save(saveData, slotIndex);
    }
    public void SaveWithCurrentData(int slotIndex)
    {
        if (!InGameManager.HasInstance)
        {
            Debug.LogError("[SaveLoadManager] No InGameManager; save aborted.");
            return;
        }

        var current = InGameManager.Instance.TmpSaveData;
        if (current == null)
        {
            Debug.LogError("[SaveLoadManager] No session data; save aborted.");
            return;
        }

        Save(current, slotIndex);
    }

    /// <summary>
    /// Writes a slot through a temporary file, so a crash or a full disk mid-write costs
    /// the new save rather than the one already there.
    /// </summary>
    public void Save(SaveData saveData, int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex)) return;

        if (saveData == null)
        {
            Debug.LogError($"[SaveLoadManager] Slot {slotIndex}: nothing to save.");
            return;
        }

        string path = GetSlotPath(slotIndex);
        string tempPath = path + ".tmp";

        try
        {
            // Newtonsoft rather than JsonUtility: the save carries dictionaries and
            // nullable fields that JsonUtility silently drops.
            string json = JsonConvert.SerializeObject(saveData);
            File.WriteAllText(tempPath, json);

            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveLoadManager] Slot {slotIndex} could not be written: {e.Message}");

            // The previous save is still intact; drop the partial one.
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            return;
        }

        Debug.Log($"[SaveLoadManager] Saved slot {slotIndex}.");

        UpdateHasAnySaveData();
    }
    /// <summary>
    /// True when no slot holds a save.
    /// </summary>
    public bool AreAllSlotsEmpty()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (File.Exists(GetSlotPath(i)))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Loads a slot, or null unless it is Valid. Anything else is a slot the game cannot
    /// resume from, which is the same line CleanInvalidSlots draws: parsing is not the
    /// bar, being resumable is. Reading goes through AnalyzeSlot so a corrupt file is
    /// classified in one place instead of throwing here and being handled there.
    /// </summary>
    public SaveData GetSaveDataFromSlot(int slotIndex)
    {
        var info = AnalyzeSlot(slotIndex);

        if (info.Status != SlotStatus.Valid)
        {
            Debug.LogWarning($"[SaveLoadManager] Slot {slotIndex} is {info.Status}. {info.ErrorMessage}");
            return null;
        }

        return info.Data;
    }

    /// <summary>Every slot, for the save/load panel. A slot that is empty or does not
    /// parse is null at its index, so the panel can draw the gap in place.</summary>
    public SaveData[] GetAllSlotData()
    {
        SaveData[] all = new SaveData[SlotCount];

        for (int i = 0; i < SlotCount; i++)
        {
            all[i] = AnalyzeSlot(i).Data;
        }

        return all;
    }

    private string GetSlotPath(int slotIndex)
    {
        return Path.Combine(SaveDirectory, $"save_{slotIndex}.json");
    }

    public void DeleteSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex)) return;

        string path = GetSlotPath(slotIndex);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"[SaveLoadManager] Cleared slot {slotIndex}.");

            UpdateHasAnySaveData();
        }
        else
        {
            Debug.LogWarning($"[SaveLoadManager] Cannot clear slot {slotIndex}: no file.");
        }
    }

    public void DeleteAllSlots()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            string path = GetSlotPath(i);
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log($"[SaveLoadManager] Cleared slot {i}.");
            }
        }

        UpdateHasAnySaveData();
    }
    
    /// <summary>
    /// The save a new game starts from. The editor's Play button uses this too,
    /// so there is only one definition of a fresh game.
    /// </summary>
    public SaveData CreateNewSaveData()
    {
        
        var storyData = StoryAssetManager.Instance.GetStoryData(DefaultStartStoryId);
        if (storyData == null)
        {
            Debug.LogError($"[SaveLoadManager] Default start story '{DefaultStartStoryId}' not found.");
            return null;
        }

        var saveData = new SaveData(
            storyBuildVersion: storyData.StoryBuildVersion,
            currentCardID: storyData.CardID,
            currentElementIndex: 0,
            ownedItemIDs: new List<string> { ItemNames.FilmCamera },
            choiceHistory: new Dictionary<string, int>()
        );

        return saveData;
    }

    /// <summary>
    /// Takes slots the build can no longer resume out of rotation, which is what an update
    /// that removed or renamed content leaves behind. Run once at startup.
    ///
    /// Set aside rather than deleted: this may be hours of someone's playthrough, and the
    /// judgement that it is unusable comes from a version of the game that could itself be
    /// wrong. The slot frees up either way, and the file is still there to recover from.
    /// </summary>
    public void CleanInvalidSlots()
    {
        bool changed = false;

        for (int i = 0; i < SlotCount; i++)
        {
            var info = AnalyzeSlot(i);

            // Empty slots are not a problem; anything else that is not Valid is a file the
            // game cannot resume from, including one this pass could not read.
            if (info.Status == SlotStatus.Empty || info.Status == SlotStatus.Valid)
                continue;

            string setAsidePath = info.FilePath + ".unreadable";

            try
            {
                if (File.Exists(setAsidePath))
                    File.Delete(setAsidePath);

                File.Move(info.FilePath, setAsidePath);
                changed = true;

                Debug.LogWarning(
                    $"[SaveLoadManager] Slot {i} was {info.Status} and has been set aside as " +
                    $"{Path.GetFileName(setAsidePath)}. {info.ErrorMessage}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveLoadManager] Slot {i} could not be set aside: {e.Message}");
            }
        }

        if (changed)
        {
            UpdateHasAnySaveData();
        }
    }

    /// <summary>
    /// Classifies a slot, distinguishing absent from empty from corrupt.
    /// </summary>
    public SlotStatusInfo AnalyzeSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
        {
            return new SlotStatusInfo
            {
                SlotIndex = slotIndex,
                Status = SlotStatus.Empty,
                ErrorMessage = "Slot index out of range."
            };
        }

        var info = new SlotStatusInfo
        {
            SlotIndex = slotIndex,
            FilePath = GetSlotPath(slotIndex)
        };

        if (!File.Exists(info.FilePath))
        {
            info.Status = SlotStatus.Empty;
            info.FileSize = 0;
            return info;
        }

        FileInfo fileInfo = new FileInfo(info.FilePath);
        info.FileSize = fileInfo.Length;

        if (info.FileSize == 0)
        {
            info.Status = SlotStatus.EmptyFile;
            info.ErrorMessage = "File is zero length.";
            return info;
        }

        try
        {
            
            string json = File.ReadAllText(info.FilePath);

            if (string.IsNullOrWhiteSpace(json))
            {
                info.Status = SlotStatus.EmptyFile;
                info.ErrorMessage = "File contains no data.";
                return info;
            }

            var data = JsonConvert.DeserializeObject<SaveData>(json);

            if (data == null)
            {
                info.Status = SlotStatus.InvalidJson;
                info.ErrorMessage = "JSON parsed to null.";
                return info;
            }

            if (!data.IsValidSave())
            {
                info.Status = SlotStatus.InvalidData;
                info.ErrorMessage = "Save data failed validation.";
                info.Data = data;
                return info;
            }

            info.Status = SlotStatus.Valid;
            info.Data = data;
            return info;
        }
        catch (JsonException ex)
        {
            info.Status = SlotStatus.InvalidJson;
            info.ErrorMessage = $"JSON error: {ex.Message}";
            return info;
        }
        catch (Exception ex)
        {
            info.Status = SlotStatus.InvalidJson;
            info.ErrorMessage = $"Unexpected error: {ex.Message}";
            return info;
        }
    }

    public SlotStatusInfo[] AnalyzeAllSlots()
    {
        var results = new SlotStatusInfo[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            results[i] = AnalyzeSlot(i);
        }
        return results;
    }
}
