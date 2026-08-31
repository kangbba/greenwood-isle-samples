using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ChoiceWithAnswer : Element
{

    private readonly string _question;
    private readonly List<ChoiceOption> _options;
    private readonly int _correctIndex;
    private readonly Element _startElement;
    private readonly List<Element> _mismatchElements;

    public ChoiceWithAnswer(
        string question,
        List<ChoiceOption> options,
        int correctIndex,
        Element startElement,
        List<Element> mismatchElements)
    {
        _question = question;
        _options = options ?? new List<ChoiceOption>();
        _correctIndex = correctIndex;
        _startElement = startElement;
        _mismatchElements = mismatchElements ?? new List<Element>();
    }
    public override void ExecuteInstantly(StoryContext ctx)
    {
    }

    public override async UniTask ExecuteAsync(StoryContext ctx, CancellationToken token)
    {
        var choiceManager = ctx.Choices;

        while (true)
        {
            await _startElement.ExecuteAsync(ctx, token);

            int selectedIndex = await choiceManager.WaitForChoiceWindowResult(_question, _options, token);

            if (selectedIndex == _correctIndex)
            {
                
                choiceManager.CloseCurrentChoice(1f);
                await UniTask.WaitForSeconds(1f, cancellationToken: token);
                break;
            }
            else
            {
                
                choiceManager.CloseCurrentChoice(0.5f);
                await UniTask.WaitForSeconds(0.5f, cancellationToken: token);

                var mismatchSequence = new SequentialElement(_mismatchElements);
                await mismatchSequence.ExecuteAsync(ctx, token);
            }
        }
    }
    public override void ExitOnDestroy()
    {
        
        if (ChoiceManager.HasInstance)
        {
            
            ChoiceManager.Instance.CloseCurrentChoice(0f);
        }
    }
}
