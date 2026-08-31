using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Collections.Generic;

public class Dialogue : Element
{

    private readonly string _characterID;
    private readonly List<string> _sentences;
    private readonly string _emotionID;
    private readonly string _poseID;
    private readonly bool _isUnknown;

    // Three states, not two: true always shows the profile, false changes the standing
    // character instead, and null decides from whether that character is already on screen.
    private readonly bool? _showProfile;

    public string CharacterID { get => _characterID;}
    public IReadOnlyList<string> Sentences => _sentences;
    public string EmotionID { get => _emotionID; }
    public string PoseID { get => _poseID;  }
    public bool IsUnknown { get => _isUnknown; }
    public bool? ShowProfile => _showProfile;

    public Dialogue(string characterID, List<string> sentences, string emotionID = null, string poseID = null, bool isUnknown = false, bool? showProfile = null)
    {
        _characterID = characterID;
        _sentences = sentences;
        _emotionID = emotionID;
        _poseID = poseID;
        _isUnknown = isUnknown;
        _showProfile = showProfile;
    }

    public Dialogue(string characterID, string sentence, string emotionID = null, string poseID = null, bool isUnknown = false, bool? showProfile = null)
        : this(characterID, new List<string> { sentence }, emotionID, poseID, isUnknown, showProfile) { }

    public override void ExecuteInstantly(StoryContext ctx)
    {
    }

    // Dialogue holds the content and sets the scene up; DialogueManager owns the
    // player instance and runs the sequence. The split keeps the element free of
    // presentation state, so it can be constructed in a story script as plain data.
    public override async UniTask ExecuteAsync(StoryContext ctx, CancellationToken token)
    {
        if (_sentences == null || _sentences.Count == 0)
        {
            Debug.LogWarning("Dialogue :: _sentences is null or empty");
            return;
        }

        var dialogueManager = ctx.Dialogues;
        var characterManager = ctx.Characters;
        var inGameManager = ctx.InGame;
        
        Transform parent = inGameManager.CurrentInterfaceCanvas.DialoguePlayerLayer;

        DialoguePlayer dialoguePlayer = dialogueManager.InstantiateDialoguePlayer(parent);
        if (dialoguePlayer == null)
        {
            Debug.LogError("[Dialogue] Could not instantiate a DialoguePlayer.");
            return;
        }

        var letterBox = inGameManager.CurrentInterfaceCanvas.LetterBox;
        try
        {
            
            var displayName = GetDisplayName(ctx);
            var displayColor = GetDisplayColor(ctx);
            
            dialoguePlayer.Init(displayName, displayColor);
            dialoguePlayer.IsShown.Value = true;

            bool hasActiveCharacter = characterManager.GetActiveCharacter(CharacterID) != null;

            if (_showProfile == true)
            {
                if(letterBox != null)
                {
                    letterBox.ShowCharacterDisplayer(characterID : CharacterID, emotionType : _emotionID, poseID : _poseID);
                }
            }
            else if (_showProfile == false)
            {
                
                if (_emotionID != null)
                {
                    await new EmotionChange(_characterID, _emotionID).ExecuteAsync(ctx, token);
                }
                if (_poseID != null)
                {
                    await new PoseChange(_characterID, _poseID).ExecuteAsync(ctx, token);
                }
            }
            else
            {
                if (!hasActiveCharacter && !_isUnknown)
                {
                    if(letterBox != null)
                    {
                        letterBox.ShowCharacterDisplayer(characterID : CharacterID, emotionType : _emotionID, poseID : _poseID);
                    }
                }
                else
                {
                    if (_emotionID != null) await new EmotionChange(_characterID, _emotionID).ExecuteAsync(ctx, token);
                    if (_poseID != null) await new PoseChange(_characterID, _poseID).ExecuteAsync(ctx, token);
                }
            }
            
            // The sequence itself takes no token, but it waits on the player object, which
            // the finally below destroys. Cancelling therefore ends it either way.
            await dialogueManager.ExecuteDialogueSequenceAsync(dialoguePlayer, _characterID, _sentences)
                .AttachExternalCancellation(token);
        }
        finally
        {
            // Runs on cancellation too, so a dialogue that is cut short leaves no box on
            // screen. Cancellation itself keeps travelling up to the story player.
            dialogueManager.DestroyDialoguePlayer();
            if(letterBox != null)
            {
                letterBox.HideCharacterDisplayer();
            }
        }
    }

    private string GetDisplayName(StoryContext ctx)
    {
        if (_characterID == CNames.Mono) return "";
        if (_isUnknown) return "??";

        var setting = ctx.CharacterAssets.GetCharacterSetting(_characterID);
        return setting?.DisplayName_KO ?? _characterID;
    }

    private Color GetDisplayColor(StoryContext ctx)
    {
        if (_characterID == CNames.Mono) return Color.clear;

        var setting = ctx.CharacterAssets.GetCharacterSetting(_characterID);
        if (_isUnknown) return setting?.CharacterColor ?? Color.grey;
        return setting?.CharacterColor ?? Color.clear;
    }

    public override void ExitOnDestroy()
    {
        
        if (DialogueManager.HasInstance)
        {
            
            DialogueManager.Instance.DestroyDialoguePlayer();
        }
    }
}
