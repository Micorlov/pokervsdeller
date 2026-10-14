#nullable enable
using System;
using System.Collections.Generic;

namespace Showdown.Game
{
    /// <summary>
    /// A scheduler whose clock moves only when told to. Tests drive it directly; the production scheduler
    /// (<c>UnityScheduler</c>) feeds it the frame delta, so both share one set of firing rules.
    ///
    /// <see cref="Advance"/> runs due tasks in due-time order (ties in scheduling order) and moves the clock to
    /// each task's due time as it runs, so a task that schedules a follow-up lands it at the right moment even
    /// inside one large step.
    /// </summary>
    public sealed class ManualScheduler : IScheduler
    {
        private readonly List<Task> pending = new List<Task>();
        private readonly Action<Exception>? onError;
        private double now;
        private long nextSequence;

        /// <param name="onError">
        /// Receives an exception thrown by a task so the remaining tasks still run — one failing timer must not
        /// strand the rest of the table. Null rethrows, which is what tests want.
        /// </param>
        public ManualScheduler(Action<Exception>? onError = null)
        {
            this.onError = onError;
        }

        /// <summary>Seconds of scheduler time elapsed.</summary>
        public double Now => now;

        public int PendingCount => pending.Count;

        public IScheduledTask Schedule(float delaySeconds, Action action)
        {
            var task = new Task(this, now + Math.Max(0.0, delaySeconds), nextSequence, action);
            nextSequence += 1;
            pending.Add(task);
            return task;
        }

        public void CancelAll()
        {
            foreach (var task in pending)
            {
                task.MarkDone();
            }

            pending.Clear();
        }

        /// <summary>Moves the clock forward and runs everything that comes due on the way.</summary>
        public void Advance(float seconds)
        {
            var target = now + Math.Max(0.0, seconds);
            while (TryTakeNextDue(target, out var task))
            {
                now = Math.Max(now, task.DueAt);
                Run(task);
            }

            now = target;
        }

        private bool TryTakeNextDue(double target, out Task task)
        {
            task = null!;
            var best = -1;
            for (var index = 0; index < pending.Count; index += 1)
            {
                var candidate = pending[index];
                if (candidate.DueAt > target) continue;
                if (best < 0 || IsBefore(candidate, pending[best])) best = index;
            }

            if (best < 0) return false;
            task = pending[best];
            pending.RemoveAt(best);
            return true;
        }

        private static bool IsBefore(Task a, Task b) =>
            a.DueAt < b.DueAt || (a.DueAt == b.DueAt && a.Sequence < b.Sequence);

        private void Run(Task task)
        {
            task.MarkDone();
            try
            {
                task.Action();
            }
            catch (Exception error) when (onError != null)
            {
                onError(error);
            }
        }

        private void Remove(Task task)
        {
            pending.Remove(task);
        }

        private sealed class Task : IScheduledTask
        {
            private readonly ManualScheduler owner;

            public Task(ManualScheduler owner, double dueAt, long sequence, Action action)
            {
                this.owner = owner;
                DueAt = dueAt;
                Sequence = sequence;
                Action = action;
                IsPending = true;
            }

            public double DueAt { get; }

            public long Sequence { get; }

            public Action Action { get; }

            public bool IsPending { get; private set; }

            public void MarkDone()
            {
                IsPending = false;
            }

            public void Cancel()
            {
                if (!IsPending) return;
                IsPending = false;
                owner.Remove(this);
            }
        }
    }
}
