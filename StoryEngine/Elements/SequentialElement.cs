using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Runs its child elements one after another. The composite counterpart to
/// ParallelElement, so staging can be assembled from either.
/// </summary>
public class SequentialElement : Element
{
    
    private readonly List<Element> _elements;

    public SequentialElement(List<Element> elements)
    {
        // Copied and compacted once, so the three paths below can assume a clean list and
        // a story script cannot change the sequence after handing it over.
        _elements = elements != null ? new List<Element>(elements) : new List<Element>();
        _elements.RemoveAll(element => element == null);
    }

    public IReadOnlyList<Element> Elements => _elements;

    public override void ExecuteInstantly(StoryContext ctx)
    {
        foreach (var element in _elements)
        {
            element.ExecuteInstantly(ctx);
        }
    }

    // The child currently being awaited, if any. StoryPlayer only knows about the
    // outermost element, so teardown has to find its way down from here.
    private Element _runningElement;

    public override async UniTask ExecuteAsync(StoryContext ctx, CancellationToken token)
    {
        foreach (var element in _elements)
        {
            _runningElement = element;
            await element.ExecuteAsync(ctx, token);
        }

        _runningElement = null;
    }

    /// <summary>
    /// Only the child that was still playing: the ones before it finished on their own,
    /// and the ones after it never started.
    /// </summary>
    public override void ExitOnDestroy()
    {
        _runningElement?.ExitOnDestroy();
        _runningElement = null;
    }
}
