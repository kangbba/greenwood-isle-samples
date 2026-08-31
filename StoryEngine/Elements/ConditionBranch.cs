using System.Threading;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

/// <summary>
/// Runs one of two element sets depending on a predicate.
/// Mostly used for affinity-based branching through AffinityManager.
/// </summary>
public class ConditionBranch : Element
{
    private readonly Func<bool> _condition;
    private readonly List<Element> _trueElements;
    private readonly List<Element> _falseElements;

    private Element _runningElement;

    /// <summary>
    /// Single-element branches.
    /// </summary>
    public ConditionBranch(Func<bool> condition, Element trueElement, Element falseElement = null)
        : this(condition,
               new List<Element> { trueElement },
               falseElement != null ? new List<Element> { falseElement } : null)
    {
    }

    /// <summary>
    /// Multi-element branches.
    /// </summary>
    public ConditionBranch(Func<bool> condition, List<Element> trueElements, List<Element> falseElements = null)
    {
        _condition = condition;
        _trueElements = Compact(trueElements);
        _falseElements = Compact(falseElements);
    }

    private static List<Element> Compact(List<Element> elements)
    {
        var copy = elements != null ? new List<Element>(elements) : new List<Element>();
        copy.RemoveAll(element => element == null);
        return copy;
    }

    public override async UniTask ExecuteAsync(StoryContext ctx, CancellationToken token)
    {
        var elementsToExecute = _condition() ? _trueElements : _falseElements;

        foreach (var element in elementsToExecute)
        {
            _runningElement = element;
            await element.ExecuteAsync(ctx, token);
        }

        _runningElement = null;
    }

    public override void ExecuteInstantly(StoryContext ctx)
    {
        var elementsToExecute = _condition() ? _trueElements : _falseElements;

        foreach (var element in elementsToExecute)
        {
            element.ExecuteInstantly(ctx);
        }
    }

    /// <summary>
    /// Only what actually ran. Re-evaluating the condition here could pick the branch that
    /// never played, since the state it reads may have changed since.
    /// </summary>
    public override void ExitOnDestroy()
    {
        _runningElement?.ExitOnDestroy();
        _runningElement = null;
    }
}
