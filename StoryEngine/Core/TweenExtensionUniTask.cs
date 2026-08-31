using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;

namespace Cysharp.Threading.Tasks.DOTween
{
    /// <summary>
    /// Bridges DOTween into the story engine's async discipline: any tween becomes
    /// awaitable with the story's own token, so presentation joins the same
    /// cancellation flow as everything else.
    /// </summary>
    public static class TweenExtensionsUniTask
    {
        public static UniTask ToUniTask(this Tween tween, CancellationToken cancellationToken = default)
        {
            var promise = new UniTaskCompletionSource();

            tween.OnComplete(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                    promise.TrySetResult();
            });

            tween.OnKill(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                    promise.TrySetResult(); // A manually killed tween also counts as done.
            });

            cancellationToken.Register(() =>
            {
                if (tween.IsActive() && tween.IsPlaying())
                    tween.Kill();
                promise.TrySetCanceled();
            });

            return promise.Task;
        }
    }
}
