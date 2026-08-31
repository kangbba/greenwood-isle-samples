using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ParallelElement : Element
{
    
    private readonly List<Element> _elements;

    public ParallelElement(params Element[] elements)
    {
        _elements = new List<Element>(elements ?? new Element[0]);
        _elements.RemoveAll(element => element == null);
    }

    public override void ExecuteInstantly(StoryContext ctx)
    {
        foreach (var element in _elements)
        {
            element.ExecuteInstantly(ctx);
        }
    }

    public override async UniTask ExecuteAsync(StoryContext ctx, CancellationToken token)
    {
        if (_elements.Count == 0)
        {
            Debug.LogWarning("[ParallelElement] Nothing to execute.");
            return;
        }

        List<UniTask> tasks = new List<UniTask>();
        foreach (var element in _elements)
        {
            tasks.Add(element.ExecuteAsync(ctx, token));
        }

        await UniTask.WhenAll(tasks);
    }
    /// <summary>
    /// Every child, because they all started together and any of them may still be playing.
    /// </summary>
    public override void ExitOnDestroy()
    {
        foreach (var element in _elements)
        {
            element.ExitOnDestroy();
        }
    }
}
