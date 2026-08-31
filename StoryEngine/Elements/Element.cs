using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// One unit of story presentation. A scene is a list of these, and the composites
/// (SequentialElement, ParallelElement, ConditionBranch) are elements too, so
/// staging nests.
///
/// Two paths, because the same content plays forwards, skipped, and restored from a
/// save. ExecuteAsync animates; ExecuteInstantly applies only the end state, which is
/// what save restore replays.
///
/// An element is authored as plain data in a story script (new Dialogue("aria", "..."))
/// and is handed a StoryContext when it runs, resolved once by the player when playback
/// starts, so an element never touches globals: whatever a presentation needs to reach,
/// it reaches through the context it was given. Anything that <i>decides</i> still
/// takes the state it decides on as a parameter of its own. Branching is the case that
/// matters, and it is bound once at the branch site (BranchFacts) so a rule can be
/// checked without a scene.
/// </summary>
public abstract class Element
{
    /// <summary>Animated path. The token is the story's, so nothing outlives its scene.</summary>
    public abstract UniTask ExecuteAsync(StoryContext ctx, CancellationToken token);

    /// <summary>Final state only, synchronously.</summary>
    public abstract void ExecuteInstantly(StoryContext ctx);

    /// <summary>
    /// Fire-and-forget, for callers that do not await: a one-shot sound alongside
    /// dialogue. Not virtual, so it cannot diverge from ExecuteAsync.
    /// </summary>
    public void Execute(StoryContext ctx, CancellationToken token) => ExecuteAsync(ctx, token).Forget();

    /// <summary>Forced teardown: subscriptions and running presentation.</summary>
    public abstract void ExitOnDestroy();
}
