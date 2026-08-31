using System;

public enum CompareType
{
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual,
    Equal,
    NotEqual
}

public enum ConditionKind
{
    Affinity,
    HasItem,
    IsHighestAffinityCharacter,
    IsLowestAffinityCharacter,
    SelectedChoice,

    // Belief, expressed as an enum rather than a string key.
    BeliefValue
}

[Serializable]
public class StoryBranchCondition
{
    public ConditionKind kind;

    // Shared key, used by affinity, item and choice conditions.
    public string key;

    // Numeric comparison.
    public CompareType compareType;
    public int threshold;

    // Belief only.
    public BeliefType beliefType;
    public BeliefSide beliefSide;
}

public static class ConditionRules
{
    public static CompareType[] AllowedCompareTypes(ConditionKind kind) => kind switch
    {
        ConditionKind.Affinity =>
            new[]
            {
                CompareType.GreaterThan,
                CompareType.GreaterOrEqual,
                CompareType.LessThan,
                CompareType.LessOrEqual,
                CompareType.Equal,
                CompareType.NotEqual
            },

        ConditionKind.SelectedChoice =>
            new[] { CompareType.Equal, CompareType.NotEqual },

        ConditionKind.BeliefValue =>
            new[] { CompareType.Equal, CompareType.NotEqual },

        ConditionKind.HasItem
        or ConditionKind.IsHighestAffinityCharacter
        or ConditionKind.IsLowestAffinityCharacter
            => Array.Empty<CompareType>(),

        _ => Array.Empty<CompareType>()
    };

    public static bool NeedsNumericValue(ConditionKind kind) =>
        kind == ConditionKind.Affinity
        || kind == ConditionKind.SelectedChoice;

    public static bool NeedsKey(ConditionKind kind) => kind switch
    {
        ConditionKind.IsHighestAffinityCharacter => true,
        ConditionKind.IsLowestAffinityCharacter  => true,
        ConditionKind.HasItem                    => true,
        ConditionKind.Affinity                   => true,
        ConditionKind.SelectedChoice             => true,
        ConditionKind.BeliefValue                => false, // uses the enum field instead
        _                                        => true
    };

    public static bool NeedsBeliefEnum(ConditionKind kind) =>
        kind == ConditionKind.BeliefValue;

    public static CompareType Normalize(ConditionKind kind, CompareType current)
    {
        var allowed = AllowedCompareTypes(kind);
        if (allowed.Length == 0) return CompareType.Equal;

        foreach (var a in allowed)
            if (a == current) return current;

        return allowed[0];
    }
}
