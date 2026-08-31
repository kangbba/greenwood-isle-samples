/// <summary>
/// Every setting the game exposes. The type each one carries is declared in
/// SettingManager._typeMap, and the PlayerPrefs key is derived from this value,
/// so a setting is never spelled out as a string at a call site.
/// </summary>
public enum SettingType
{
    MasterVolume,
    BGMVolume,
    SFXVolume,
    UIVolume,

    DialogueSpeed,
    FontSize,
    AutoModePagingSpeed,
    EffectSpeed,

    ResolutionIndex,
    IsFullScreen,

    Language,
}

/// <summary>
/// Persisted IDs. Existing numbers must not be changed: the stored value is this
/// explicit ID, not the declaration order, so inserting a language later is safe.
/// </summary>
public enum GameLanguage
{
    Korean = 0,
    English = 1,
    Japanese = 2,
}
