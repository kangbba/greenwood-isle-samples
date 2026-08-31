using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The state a branch is decided against. Bound once at the branch site, so the
/// evaluator never reaches into the scene and a rule can be checked without one.
/// </summary>
public readonly struct BranchFacts
{
    private readonly Func<string, int> _affinityOf;
    private readonly Func<string> _highestAffinityCharacterId;
    private readonly Func<string> _lowestAffinityCharacterId;
    private readonly Func<string, int> _selectedChoiceOf;
    private readonly Func<BeliefType, BeliefSide> _beliefOf;

    public BranchFacts(
        Func<string, int> affinityOf,
        Func<string> highestAffinityCharacterId,
        Func<string> lowestAffinityCharacterId,
        Func<string, int> selectedChoiceOf,
        Func<BeliefType, BeliefSide> beliefOf)
    {
        _affinityOf = affinityOf;
        _highestAffinityCharacterId = highestAffinityCharacterId;
        _lowestAffinityCharacterId = lowestAffinityCharacterId;
        _selectedChoiceOf = selectedChoiceOf;
        _beliefOf = beliefOf;
    }

    /// <summary>The one place in branching that knows the managers exist.</summary>
    public static BranchFacts FromManagers() => new BranchFacts(
        affinityOf: key => AffinityManager.Instance.GetAffinity(key),
        highestAffinityCharacterId: () => AffinityManager.Instance.GetHighestAffinityCharacterID_All(out _),
        lowestAffinityCharacterId: () => AffinityManager.Instance.GetLowestAffinityCharacterID_All(out _),
        selectedChoiceOf: key => ChoiceManager.Instance?.GetSelectedIndex(key) ?? -1,
        beliefOf: type => BeliefManager.Instance.GetBeliefSide(type));

    public int AffinityOf(string key) => _affinityOf?.Invoke(key) ?? 0;
    public string HighestAffinityCharacterId => _highestAffinityCharacterId?.Invoke();
    public string LowestAffinityCharacterId => _lowestAffinityCharacterId?.Invoke();
    public int SelectedChoiceOf(string key) => _selectedChoiceOf?.Invoke(key) ?? -1;
    public BeliefSide BeliefOf(BeliefType type) => _beliefOf != null ? _beliefOf(type) : default;
}

/// <summary>
/// Data-driven branch conditions. Pure over the condition, the save and the facts.
/// </summary>
public static class BranchEvaluator
{

    public static string Symbol(this CompareType t) => t switch
    {
        CompareType.GreaterThan    => ">",
        CompareType.GreaterOrEqual => "≥",
        CompareType.LessThan       => "<",
        CompareType.LessOrEqual    => "≤",
        CompareType.Equal          => "==",
        CompareType.NotEqual       => "!=",
        _                          => "?"
    };

    private static bool Compare(this int v, CompareType ct, int th) => ct switch
    {
        CompareType.GreaterThan    => v >  th,
        CompareType.GreaterOrEqual => v >= th,
        CompareType.LessThan       => v <  th,
        CompareType.LessOrEqual    => v <= th,
        CompareType.Equal          => v == th,
        CompareType.NotEqual       => v != th,
        _                          => false
    };

    private static bool Compare(this string v, CompareType ct, string th) => ct switch
    {
        CompareType.Equal    => v == th,
        CompareType.NotEqual => v != th,
        _                    => false
    };

    private static bool CompareBelief(
        BeliefSide current,
        CompareType ct,
        BeliefSide target)
    {
        return ct switch
        {
            CompareType.Equal    => current == target,
            CompareType.NotEqual => current != target,
            _                    => false
        };
    }

    /* One condition. */
    public static bool Evaluate(StoryBranchCondition c, SaveData s, in BranchFacts facts) => c.kind switch
    {
        ConditionKind.HasItem =>
            s?.ownedItemIDs?.Contains(c.key) ?? false,

        ConditionKind.Affinity =>
            facts.AffinityOf(c.key)
                 .Compare(c.compareType, c.threshold),

        ConditionKind.IsHighestAffinityCharacter =>
            c.key == facts.HighestAffinityCharacterId,

        ConditionKind.IsLowestAffinityCharacter =>
            c.key == facts.LowestAffinityCharacterId,

        ConditionKind.SelectedChoice =>
            facts.SelectedChoiceOf(c.key)
                 .Compare(c.compareType, c.threshold),

        ConditionKind.BeliefValue =>
            CompareBelief(
                facts.BeliefOf(c.beliefType),
                c.compareType,
                c.beliefSide
            ),

        _ => false
    };

    /* Same verdict, plus a trace for the editor. The logged value is the one that
       decided the condition, so the log cannot disagree with the game. */
    public static bool EvaluateWithLog(
        StoryBranchCondition c,
        SaveData s,
        in BranchFacts facts,
        out string log)
    {
        bool ok;

        switch (c.kind)
        {
            case ConditionKind.HasItem:
                ok  = s?.ownedItemIDs?.Contains(c.key) ?? false;
                log = $"HasItem({c.key}) => {(ok ? "yes" : "no")}";
                break;

            case ConditionKind.Affinity:
            {
                int v = facts.AffinityOf(c.key);
                ok = v.Compare(c.compareType, c.threshold);
                log = $"Affinity({c.key}={v}) {c.compareType.Symbol()} {c.threshold} => {(ok ? "OK" : "NG")}";
                break;
            }

            case ConditionKind.IsHighestAffinityCharacter:
            {
                string top = facts.HighestAffinityCharacterId;
                ok = c.key == top;
                log = $"IsTop({c.key}) => {(ok ? "OK" : $"NG(top={top})")}";
                break;
            }

            case ConditionKind.IsLowestAffinityCharacter:
            {
                string low = facts.LowestAffinityCharacterId;
                ok = c.key == low;
                log = $"IsLow({c.key}) => {(ok ? "OK" : $"NG(low={low})")}";
                break;
            }

            case ConditionKind.SelectedChoice:
            {
                int sel = facts.SelectedChoiceOf(c.key);
                ok = sel.Compare(c.compareType, c.threshold);
                log = $"SelectedChoice({c.key}={sel}) {c.compareType.Symbol()} {c.threshold} => {(ok ? "OK" : "NG")}";
                break;
            }

            case ConditionKind.BeliefValue:
            {
                BeliefSide current = facts.BeliefOf(c.beliefType);
                ok = CompareBelief(current, c.compareType, c.beliefSide);
                log =
                    $"Belief({c.beliefType}={current}) " +
                    $"{c.compareType.Symbol()} {c.beliefSide} => {(ok ? "OK" : "NG")}";
                break;
            }

            default:
                ok  = false;
                log = "UnknownCondition";
                break;
        }

        return ok;
    }

    /* A group passes only if every condition in it does. */
    public static bool EvaluateGroup(
        IEnumerable<StoryBranchCondition> group,
        SaveData s,
        in BranchFacts facts)
    {
        // Not All(...): a lambda cannot capture the by-ref facts.
        foreach (var c in group)
        {
            if (!Evaluate(c, s, facts))
                return false;
        }

        return true;
    }

    public static string ToLabelString(StoryBranchCondition c) => c.kind switch
    {
        ConditionKind.Affinity =>
            $"{c.key}{c.compareType.Symbol()}{c.threshold}",

        ConditionKind.HasItem =>
            $"Has({c.key})",

        ConditionKind.IsHighestAffinityCharacter =>
            $"IsTop({c.key})",

        ConditionKind.IsLowestAffinityCharacter =>
            $"IsLow({c.key})",

        ConditionKind.SelectedChoice =>
            $"Choice[{c.key}]{c.compareType.Symbol()}{c.threshold}",

        ConditionKind.BeliefValue =>
            $"Belief[{c.beliefType}]{c.compareType.Symbol()}{c.beliefSide}",

        _ => "?"
    };
}
