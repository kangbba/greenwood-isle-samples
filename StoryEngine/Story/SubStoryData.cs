using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewSubStoryData", menuName = "Story/SubStoryData")]
public class SubStoryData : BaseFlowData
{
    [Header("SubStory Identity")]
    [Tooltip("Card ID of this sub-story, entered manually")]
    public string cardID = "";

    [Tooltip("Card ID of this sub-story")]
    public override string CardID
    {
        get
        {
            if (string.IsNullOrEmpty(cardID))
            {
                Debug.LogError($"[SubStoryData] cardID is empty, cannot play: {name}");
                return string.Empty;
            }
            return cardID;
        }
    }

    public string flowID
    {
        get => cardID;
        set => cardID = value;
    }

    [Header("Candidate Stories")]
    [Tooltip("Candidate sub-story ids, entered manually")]
    public List<string> candidateStoryIds = new List<string>();

    [Header("Card Connection")]
    [Tooltip("Id of the next card, which may be a Story, Branch or SubStory")]
    public string NextCardID = "";

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (BuildPipeline.isBuildingPlayer) return;
        if (string.IsNullOrEmpty(cardID))
        {
            Debug.LogError($"[SubStoryData] cardID is empty. Set it explicitly rather than relying on the asset name. ({name})");
        }
        if (string.IsNullOrEmpty(NextCardID))
        {
            Debug.LogError($"[SubStoryData] NextCardID is empty: {name}");
        }
    }
#endif

    public string NextFlowID
    {
        get => NextCardID;
        set => NextCardID = value;
    }

    public string nextStoryID
    {
        get => NextCardID;
        set => NextCardID = value;
    }

    public override string GetNextCardId(SaveData save, System.Func<string, BranchData> branchResolver = null)
    {
        if (string.IsNullOrEmpty(NextCardID))
        {
            Debug.LogError($"[SubStoryData] NextCardID is empty, cannot resolve the next card: {name}");
            return string.Empty;
        }
        return NextCardID;
    }

    public override CardType CardType => CardType.SubStory;
}
