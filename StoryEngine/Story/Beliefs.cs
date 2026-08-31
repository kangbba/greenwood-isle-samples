using System;

/// <summary>
/// The axes the player's outlook is tracked on. Branch conditions read them, and the
/// save stores one side per axis.
/// </summary>
public enum BeliefType
{
    Time = 0,
    Faith = 1,
    BigPicture = 2
}

/// <summary>
/// Which way an axis has been leaned. Left is always the "values it / puts it first"
/// side and Right the opposite, so a condition reads the same for every axis.
/// </summary>
public enum BeliefSide
{
    None = 0,
    Left = 1,
    Right = 2
}

public static class BeliefKeys
{
    /// <summary>
    /// The key a belief is stored under in a save. Written out rather than taken from the
    /// member name, so renaming BeliefType.Faith stays a rename instead of silently becoming
    /// a new belief with the old one's value stranded beside it.
    ///
    /// Unmapped members throw: a belief nobody can save is a mistake worth hearing about at
    /// the first save rather than finding in a bug report.
    /// </summary>
    public static string SaveKey(this BeliefType type) => type switch
    {
        BeliefType.Time => "time",
        BeliefType.Faith => "faith",
        BeliefType.BigPicture => "bigPicture",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No save key for this belief.")
    };
}
