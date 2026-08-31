using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Standing positions a character can occupy, left to right.</summary>
public enum CLocation
{
    Left2, Left1, Center, Right1, Right2
}

/// <summary>
/// Puts a character on stage: created hidden, positioned and tinted while invisible,
/// then revealed. Staging before the reveal is what keeps a character from appearing
/// for a frame in the wrong place or the wrong colour.
///
/// With no colour given, the character is tinted toward the current place instead of
/// being drawn at full white, so a character standing in a night scene belongs to it.
/// </summary>
public class CEnter : Element
{
    private const float DefaultDuration = .3f;

    /// <summary>How far a character is pulled toward the place's colour when tinting.</summary>
    private const float PlaceTintStrength = 0.25f;

    private readonly string _characterID;
    private readonly string _emotionID;
    private readonly string _poseID;
    private readonly CLocation _location;
    private readonly float? _zoomScaleFactor;
    private readonly float? _zoomOffsetY;

    private Color _color;
    private bool _usePlaceColor;
    private float _duration;

    public string CharacterID => _characterID;
    public string EmotionID => _emotionID;

    public CEnter(
        string characterID,
        CLocation location = CLocation.Center,
        string emotionID = "Normal",
        string poseID = "Normal",
        float duration = DefaultDuration,
        Color color = default,
        float? zoomScaleFactor = null,
        float? zoomOffsetY = null)
    {
        _characterID = characterID;
        _emotionID = emotionID;
        _poseID = poseID;
        _location = location;
        _duration = duration;

        // No colour given means "take it from the place", which cannot be read until
        // the element runs. White stands in until then.
        _usePlaceColor = color == default;
        _color = _usePlaceColor ? Color.white : color;

        _zoomScaleFactor = zoomScaleFactor;
        _zoomOffsetY = zoomOffsetY;
    }

    public override async UniTask ExecuteAsync(StoryContext ctx, CancellationToken token)
    {
        _duration = ctx.Settings.GetDurationByEffectSpeedSetting(_duration);

        // The story is told from Ryan's eyes, so he is never on stage himself.
        if (_characterID == CNames.Ryan)
        {
            Debug.LogError($"[CEnter] {CNames.Ryan} is the viewpoint character and cannot enter.");
            return;
        }

        CharacterSetting characterSetting = ctx.CharacterAssets.GetCharacterSetting(_characterID);
        if (characterSetting == null)
        {
            Debug.LogError($"[CEnter] No character setting for '{_characterID}'.");
            return;
        }

        Character character = ctx.Characters.CreateCharacter(
            _characterID,
            ctx.CharacterLayer);

        if (character == null)
        {
            Debug.LogError($"[CEnter] Could not create '{_characterID}'.");
            return;
        }

        if (_usePlaceColor)
        {
            _color = Color.Lerp(Color.white, ReadPlaceColor(ctx), PlaceTintStrength);
            _usePlaceColor = false; // Resolved once: a re-entry keeps the colour it entered with.
        }

        character.Init(_emotionID, _poseID, _color);

        var shower = character.Shower;

        // Positioned and hidden before the reveal below. The character's own local x stays
        // at zero; the shower is what holds the standing position.
        shower?.MoveToLocationX(_location, 0f);
        shower?.Hide(0f);

        // Applied instantly rather than animated: the zoom is part of how the character
        // enters, not a move performed after arriving.
        if (shower != null && (_zoomScaleFactor.HasValue || _zoomOffsetY.HasValue))
        {
            new CZoom(_characterID, _zoomScaleFactor, _zoomOffsetY, _duration).ExecuteInstantly(ctx);
        }

        if (shower != null)
        {
            await shower.ShowAsync(_duration);
        }
        else if (_duration > 0f)
        {
            // No shower to reveal, but the story still paid for the time.
            await UniTask.Delay(TimeSpan.FromSeconds(_duration), cancellationToken: token);
        }
    }

    /// <summary>Falls back to white when there is no place to read, which is a scene
    /// that opens on a character before its background.</summary>
    private static Color ReadPlaceColor(StoryContext ctx)
    {
        var placeManager = ctx.Places;
        if (placeManager == null || placeManager.CurrentPlace == null)
        {
            Debug.LogWarning("[CEnter] No current place to tint from; using white.");
            return Color.white;
        }

        var image = placeManager.CurrentPlace.GetComponent<Image>();
        if (image == null)
        {
            Debug.LogWarning("[CEnter] Current place has no Image to tint from; using white.");
            return Color.white;
        }

        return image.color;
    }

    /// <summary>
    /// The same entrance at zero length, so a restored save has its characters already
    /// standing where the story left them.
    /// </summary>
    public override void ExecuteInstantly(StoryContext ctx)
    {
        _duration = 0f;
        Execute(ctx, CancellationToken.None);
    }

    /// <summary>Nothing of its own: characters belong to the character layer and are
    /// cleared with it, or by CsAllClear when a place changes.</summary>
    public override void ExitOnDestroy()
    {
    }
}
