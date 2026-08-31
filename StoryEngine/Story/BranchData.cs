using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewBranchData", menuName = "Story/BranchData")]
public class BranchData : BaseFlowData
{
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(name))
            Debug.LogError("[BranchData] Asset name is empty; CardID is derived from it.");
    }
#endif

[Header("Branch Identity")]
[Tooltip("Card ID of this branch, same as the asset name")]
public override string CardID => name;
public override CardType CardType => CardType.Branch;

    [System.Serializable]
    public class Group
    {
        public List<StoryBranchCondition> conditions = new();  // AND
        public string targetID = "";      // Where to go when the group passes.
    }

    [Tooltip("Condition groups, evaluated in order like if / else-if")]
    public List<Group> groups = new();   // if / else-if

    [Tooltip("Where to go when no group passes")]
    public string elseTargetID = "";     // else

    // A branch has several outputs, so it does not use a single NextFlowID;
    // groups[].targetID and elseTargetID carry them instead.

    public override string GetNextCardId(SaveData save, System.Func<string, BranchData> branchResolver = null)
    {
        if (groups == null) groups = new List<Group>();

        // Bound once, so every group is decided against the same state.
        var facts = BranchFacts.FromManagers();

        foreach (var grp in groups)
        {
            if (grp == null) continue;
            bool pass = true;
            foreach (var cond in grp.conditions)
            {
                if (!BranchEvaluator.EvaluateWithLog(cond, save, facts, out var log))
                {
                    Debug.Log($"[BranchData] FAIL {CardID} cond: {log}");
                    pass = false;
                    break;
                }
                Debug.Log($"[BranchData] OK   {CardID} cond: {log}");
            }

            if (pass)
            {
                var target = grp.targetID;
                if (branchResolver != null)
                {
                    var nextBranch = branchResolver(target);
                    if (nextBranch != null)
                        return nextBranch.GetNextCardId(save, branchResolver);
                }
                return target;
            }
        }

        if (!string.IsNullOrEmpty(elseTargetID))
        {
            if (branchResolver != null)
            {
                var elseBranch = branchResolver(elseTargetID);
                if (elseBranch != null)
                    return elseBranch.GetNextCardId(save, branchResolver);
            }
            return elseTargetID;
        }

        Debug.LogError($"[BranchData] No group passed and elseTargetID is empty: {CardID}");
        return string.Empty;
    }
}
