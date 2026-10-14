using System;
using Showdown.UI;
using UnityEngine;

namespace Showdown.Game
{
    /// <summary>
    /// The production <see cref="IScheduler"/>: a <see cref="ManualScheduler"/> whose clock is fed the unscaled frame
    /// delta from <see cref="UiHost.Frame"/>, so the beats between cards neither stretch with a hitch nor depend
    /// on <c>Time.timeScale</c>. A task that throws is logged and the rest of the queue still runs.
    /// </summary>
    public sealed class UnityScheduler : IScheduler, IDisposable
    {
        private readonly ManualScheduler inner = new ManualScheduler(Debug.LogException);

        public UnityScheduler()
        {
            UiHost.Frame += inner.Advance;
        }

        public IScheduledTask Schedule(float delaySeconds, Action action) => inner.Schedule(delaySeconds, action);

        public void CancelAll() => inner.CancelAll();

        public void Dispose()
        {
            UiHost.Frame -= inner.Advance;
            inner.CancelAll();
        }
    }
}
