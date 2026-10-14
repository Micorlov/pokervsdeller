#nullable enable
using System;

namespace Showdown.Game
{
    /// <summary>A scheduled callback that has not run yet and can be called off.</summary>
    public interface IScheduledTask
    {
        /// <summary>True until the task has run or been cancelled.</summary>
        bool IsPending { get; }

        void Cancel();
    }

    /// <summary>
    /// The game's clock: <c>setTimeout</c> and <c>clearTimeout</c>. Delays are unscaled seconds, so a hitching
    /// frame never stretches the beats between cards.
    /// </summary>
    public interface IScheduler
    {
        /// <summary>Runs <paramref name="action"/> once, <paramref name="delaySeconds"/> from now (zero or less means the next tick).</summary>
        IScheduledTask Schedule(float delaySeconds, Action action);

        /// <summary>Cancels everything still pending.</summary>
        void CancelAll();
    }
}
