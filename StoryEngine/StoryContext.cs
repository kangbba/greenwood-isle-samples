using UnityEngine;

/// <summary>
/// Resolved once when story playback starts, then handed down through every
/// element. Elements never touch globals themselves: whatever a presentation
/// needs to reach, it reaches through this.
/// </summary>
public sealed class StoryContext
{
    public SettingManager Settings { get; }
    public CharacterManager Characters { get; }
    public CharacterAssetManager CharacterAssets { get; }
    public PlaceManager Places { get; }
    public DialogueManager Dialogues { get; }
    public ChoiceManager Choices { get; }
    public ItemManager Items { get; }
    public ItemUIManager ItemUI { get; }
    public TutorialManager Tutorials { get; }
    public InteractionManager Interactions { get; }
    public InGameManager InGame { get; }
    public Transform CharacterLayer { get; }

    public static StoryContext FromManagers() =>
        new StoryContext(
            SettingManager.Instance,
            CharacterManager.Instance,
            CharacterAssetManager.Instance,
            PlaceManager.Instance,
            DialogueManager.Instance,
            ChoiceManager.Instance,
            ItemManager.Instance,
            ItemUIManager.Instance,
            TutorialManager.Instance,
            InteractionManager.Instance,
            InGameManager.Instance,
            InGameManager.Instance.CurrentScenarioCanvas.CharacterLayer);

    private StoryContext(
        SettingManager settings,
        CharacterManager characters,
        CharacterAssetManager characterAssets,
        PlaceManager places,
        DialogueManager dialogues,
        ChoiceManager choices,
        ItemManager items,
        ItemUIManager itemUI,
        TutorialManager tutorials,
        InteractionManager interactions,
        InGameManager inGame,
        Transform characterLayer)
    {
        Settings = settings;
        Characters = characters;
        CharacterAssets = characterAssets;
        Places = places;
        Dialogues = dialogues;
        Choices = choices;
        Items = items;
        ItemUI = itemUI;
        Tutorials = tutorials;
        Interactions = interactions;
        InGame = inGame;
        CharacterLayer = characterLayer;
    }
}
