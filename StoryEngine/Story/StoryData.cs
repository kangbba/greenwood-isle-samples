using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "NewStoryData", menuName = "Story/StoryData")]
public class StoryData : BaseFlowData
{
    /*──────── Story Identity ────────*/
    [Header("Story Identity")]
    [Tooltip("Card id, same as the asset name")]
    public string storyID = "";

    // Identity comes from the asset name, so renaming the asset renames the card
    // and the graph editor does not need a separate id field to keep in sync.
    public string StoryID => string.IsNullOrEmpty(storyID) ? name : storyID;

    /*──────── Clone System ────────*/
    [Header("Clone System")]
    [Tooltip("Whether this card is a clone of another story")]
    public bool IsClone = false;

    [Tooltip("Story a clone actually plays")]
    public string OriginalStoryID = "";

    [Tooltip("Suffix appended to the original id, for example '_BadEnding'")]
    public string CloneSuffix = "";
    
    public override string CardID => StoryID;
    public override CardType CardType => CardType.Story;

    // A clone is a second place in the graph that plays the same content: its own
    // card id and its own outgoing connection, but the original's script. That is
    // how one scene can be reached from two branches and continue differently.
    public string StoryIDToPlay =>
        IsClone && !string.IsNullOrEmpty(OriginalStoryID) ? OriginalStoryID : StoryID;
    
    public string DisplayName
    {
        get
        {
            if (IsClone)
                return StoryID + " [Clone]";
            return string.IsNullOrEmpty(StoryName_KO) ? StoryID : StoryName_KO;
        }
    }
    
    [Header("Story Info")]
    [SerializeField, Tooltip("Build version")]
    private int _storyBuildVersion = 0;

    [SerializeField, Tooltip("Story name, Korean")]
    private string _storyName_KO = "";

    [SerializeField, Tooltip("Thumbnail")]
    private Sprite _storyThumbnail;

    [SerializeField, Tooltip("Thumbnail tint")]
    private Color _storyColor = Color.white;

    // On a clone these read through to the original, so shared content has exactly
    // one definition.
    public int StoryBuildVersion
    {
        get
        {
            if (IsClone && !string.IsNullOrEmpty(OriginalStoryID))
            {
                var original = GetOriginalStoryData();
                if (original != null)
                    return original.StoryBuildVersion;
            }
            return _storyBuildVersion;
        }
#if UNITY_EDITOR
        set => _storyBuildVersion = value;
#endif
    }

    public string StoryName_KO
    {
        get
        {
            if (IsClone && !string.IsNullOrEmpty(OriginalStoryID))
            {
                var original = GetOriginalStoryData();
                if (original != null)
                    return original.StoryName_KO;
            }
            return _storyName_KO;
        }
#if UNITY_EDITOR
        set => _storyName_KO = value;
#endif
    }

    public Sprite StoryThumbnail
    {
        get
        {
            if (IsClone && !string.IsNullOrEmpty(OriginalStoryID))
            {
                var original = GetOriginalStoryData();
                if (original != null)
                    return original.StoryThumbnail;
            }
            return _storyThumbnail;
        }
#if UNITY_EDITOR
        set => _storyThumbnail = value;
#endif
    }

    public Color StoryColor
    {
        get
        {
            if (IsClone && !string.IsNullOrEmpty(OriginalStoryID))
            {
                var original = GetOriginalStoryData();
                if (original != null)
                    return original.StoryColor;
            }
            return _storyColor;
        }
#if UNITY_EDITOR
        set => _storyColor = value;
#endif
    }

    /// <summary>
    /// Resolves a clone's original, in the editor and at runtime.
    /// </summary>
    private StoryData GetOriginalStoryData()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            var originalPath = $"Assets/Resources/Stories/StoryDatas/{OriginalStoryID}.asset";
            return AssetDatabase.LoadAssetAtPath<StoryData>(originalPath);
        }
#endif
        return StoryAssetManager.Instance?.GetStoryData(OriginalStoryID);
    }
    
    [Header("Card Connection")]
    [Tooltip("Id of the next card, which may be a Story, Branch or SubStory")]
    public string NextCardID = "";

    [Tooltip("Ends the run and plays the dead-end sequence")]
    public bool IsDeadEnd = false;

#if UNITY_EDITOR
    private void OnEnable() => SyncStoryIdWithName();

    private void SyncStoryIdWithName()
    {
        if (storyID == name) return;
        storyID = name;
        EditorUtility.SetDirty(this);
    }
#endif

    public override string GetNextCardId(SaveData save, System.Func<string, BranchData> branchResolver = null)
    {
        if (string.IsNullOrEmpty(NextCardID))
        {
            Debug.LogError($"[StoryData] NextCardID is empty, cannot resolve the next card: {name}");
            return string.Empty;
        }
        return NextCardID;
    }

    /*──────── Editor Only ────────*/
    [Header("Editor Settings")]
    [Tooltip("Show the summary panel")]
    public bool ShowSummary = false;

    [Tooltip("Editor-only note")]
    [TextArea(3, 10)]
    public string Memo = "";

    [Tooltip("Show the memo panel")]
    public bool UseMemo = false;
}
