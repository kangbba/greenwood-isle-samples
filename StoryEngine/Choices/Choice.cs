using System.Threading;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class Choice : Element
{

    private readonly string _choiceId;
    private readonly string _question;
    private readonly List<ChoiceOption> _choiceContents;

    public Choice(string choiceId, string question, List<ChoiceOption> choices)
    {
        _choiceId = choiceId;
        _question = question;
        _choiceContents = choices ?? new List<ChoiceOption>();
    }

    /// <summary>
    /// Nothing to apply: a choice is the player's input, and a restore replays the branch
    /// that input already led to.
    /// </summary>
    public override void ExecuteInstantly(StoryContext ctx)
    {
    }

   public override async UniTask ExecuteAsync(StoryContext ctx, CancellationToken token)
    {

        var choiceManager = ctx.Choices;

        SFXEnter.PlayOneShot(Sfxs.LifeObjects_WhooshingSmall);
        int selectedChoiceIndex = await choiceManager.WaitForChoiceWindowResult(this, token);

        if (selectedChoiceIndex >= 0 && selectedChoiceIndex < _choiceContents.Count)
        {

            // Recorded in the save, so a later branch can ask what the player chose here.
            choiceManager.SaveChoice(_choiceId, selectedChoiceIndex);

            choiceManager.CloseCurrentChoice(1f);
            await UniTask.WaitForSeconds(1f, cancellationToken: token);

            // Deliberately unguarded: cancellation has to keep travelling up to the
            // player, and a branch that throws has left the story in a state no later
            // element can assume.
            await _choiceContents[selectedChoiceIndex].ExecuteAsync(ctx, token);
        }
        else
        {
            Debug.LogWarning("[Choice] Selection index out of range.");
        }
    }

    public string Question => _question;
    public IReadOnlyList<ChoiceOption> Choices => _choiceContents;

    public string ChoiceId { get => _choiceId; }

    public override void ExitOnDestroy()
    {
        
        if (ChoiceManager.HasInstance)
        {
            
            ChoiceManager.Instance.CloseCurrentChoice(0f);
        }
    }
}
