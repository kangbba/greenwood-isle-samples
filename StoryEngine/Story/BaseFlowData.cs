using UnityEngine;

/// <summary>
/// Base for every card node in the flow graph (Story, Branch, SubStory).
/// Holds the graph position and card id, and defines how the next card is resolved.
/// </summary>
public abstract class BaseFlowData : ScriptableObject
{
#if UNITY_EDITOR
    [HideInInspector] public Vector2 NodePosition;
#endif

    /// <summary>
    /// Unique id for this card. Each subclass decides where it comes from.
    /// </summary>
    public abstract string CardID { get; }

    /// <summary>
    /// Which kind of card this is.
    /// </summary>
    public abstract CardType CardType { get; }

    /// <summary>
    /// Resolves the next card id. Branch evaluates its conditions; Story and
    /// SubStory return their configured next id.
    /// branchResolver maps a card id to BranchData, for a branch chaining into another branch.
    /// </summary>
    public abstract string GetNextCardId(SaveData save, System.Func<string, BranchData> branchResolver = null);
}
