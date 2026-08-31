using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Brings a place on screen and takes the previous one off. The two overlap: the
/// incoming place fades up while the outgoing one fades down, and the outgoing object
/// is only destroyed once it is fully covered.
///
/// The duration is a budget rather than a constant. Characters standing in the old
/// place have to leave before the place under them changes, so when any are on screen
/// their exit is paid for out of the same budget and the transition keeps the length
/// the story script asked for.
/// </summary>
public class PlaceEnter : Element
{
    /// <summary>Share of the budget spent clearing characters before the place changes.</summary>
    private const float CharacterClearShare = 0.2f;

    private readonly string _placeID;
    private readonly Color _color;
    private readonly bool _useRainEffect;
    private readonly bool _useFog;
    private readonly float _initialScaleFactor;
    private readonly float _targetScaleFactor;

    private float _duration;

    public string PlaceID => _placeID;
    public Color Color => _color;

    public PlaceEnter(
        string placeID,
        float duration = 2f,
        Color color = default,
        bool useRainEffect = false,
        bool useFog = false,
        float initialScaleFactor = 1f,
        float targetScaleFactor = 1.1f)
    {
        _placeID = placeID;
        _duration = duration;
        _color = color == default ? Color.white : color;
        _useRainEffect = useRainEffect;
        _useFog = useFog;
        _initialScaleFactor = initialScaleFactor;
        _targetScaleFactor = targetScaleFactor;
    }

    public override async UniTask ExecuteAsync(StoryContext ctx, CancellationToken token)
    {
        // The player's effect-speed setting scales every timed element, so it is applied
        // here rather than at each tween below.
        _duration = ctx.Settings.GetDurationByEffectSpeedSetting(_duration);

        await PlaceTransition(ctx, token);
    }

    private async UniTask PlaceTransition(StoryContext ctx, CancellationToken token)
    {
        var placeManager = ctx.Places;

        AnimationImage place = placeManager.InstantiatePlaceWithTransition(_placeID, out AnimationImage prevPlace);

        if (place == null)
        {
            Debug.LogWarning($"[PlaceEnter] Could not instantiate place '{_placeID}'.");
            return;
        }

        // Staged before the first frame it is visible on, so it never appears untinted
        // at full size for a frame.
        place.SetColorGroupInstant(_color);
        place.SetScale(_initialScaleFactor, 0f);
        place.Fade(0f, 0f);

        if (_useRainEffect)
        {
            new PlaceRainEnter().Execute(ctx, token);
        }

        if (_useFog)
        {
            new PlaceFogEnter().Execute(ctx, token);
        }

        float remainingDuration = _duration;

        // Characters belong to the place they are standing in, so they leave first.
        // Awaited, not fired off, or they would still be fading while the ground under
        // them is replaced.
        bool isExistCharacter = ctx.Characters.ActiveCharacters.Count > 0;
        if (isExistCharacter)
        {
            float csAllClearDuration = _duration * CharacterClearShare;
            remainingDuration = _duration * (1f - CharacterClearShare);

            await new CsAllClear(csAllClearDuration).ExecuteAsync(ctx, token);
        }

        if (prevPlace != null)
        {
            prevPlace.Fade(0f, remainingDuration);
        }

        place.Fade(1f, remainingDuration);
        place.SetScale(_targetScaleFactor, remainingDuration);

        await UniTask.WaitForSeconds(remainingDuration, cancellationToken: token);

        // Destroyed only after the incoming place has fully covered it, so the gap
        // between the two is never visible.
        if (prevPlace != null)
        {
            placeManager.DestroyPlace(prevPlace);
        }
    }

    /// <summary>
    /// The same transition at zero length. Save restore replays it this way, so a
    /// restored save lands with the place, its tint and its effects already staged
    /// rather than with an empty screen.
    /// </summary>
    public override void ExecuteInstantly(StoryContext ctx)
    {
        _duration = 0f;
        Execute(ctx, CancellationToken.None);
    }

    /// <summary>
    /// Nothing of its own: places are children of the scenario canvas and go with it,
    /// and DestroyPlace is null-safe for the one case that outlives this element.
    /// </summary>
    public override void ExitOnDestroy()
    {
    }
}
