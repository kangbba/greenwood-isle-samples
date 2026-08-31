using System.Threading;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

public class ChoiceOption
{
    private string _title;
    private List<Element> _elements;

    public string Title => _title;
    public List<Element> Elements => _elements;

    public ChoiceOption(string title, List<Element> elements)
    {
        _title = title;
        _elements = elements;
    }

    public async UniTask ExecuteAsync(StoryContext ctx, CancellationToken token)
    {
        foreach (var element in _elements)
        {
            await element.ExecuteAsync(ctx, token);
        }
    }
}
