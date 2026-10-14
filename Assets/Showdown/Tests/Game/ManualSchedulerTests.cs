#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Showdown.Game;

namespace Showdown.Tests.Game
{
    [TestFixture]
    public class ManualSchedulerTests
    {
        [Test]
        public void Runs_a_task_only_once_its_delay_has_elapsed()
        {
            var scheduler = new ManualScheduler();
            var ran = 0;
            scheduler.Schedule(1f, () => ran += 1);

            scheduler.Advance(0.99f);
            Assert.AreEqual(0, ran);
            scheduler.Advance(0.02f);
            Assert.AreEqual(1, ran);
            scheduler.Advance(5f);
            Assert.AreEqual(1, ran);
        }

        [Test]
        public void Runs_tasks_in_due_order_with_ties_in_scheduling_order()
        {
            var scheduler = new ManualScheduler();
            var order = new List<string>();
            scheduler.Schedule(2f, () => order.Add("late"));
            scheduler.Schedule(1f, () => order.Add("first-tie"));
            scheduler.Schedule(1f, () => order.Add("second-tie"));
            scheduler.Schedule(0.5f, () => order.Add("early"));

            scheduler.Advance(10f);

            CollectionAssert.AreEqual(new[] { "early", "first-tie", "second-tie", "late" }, order);
        }

        [Test]
        public void A_zero_delay_task_runs_on_the_next_advance()
        {
            var scheduler = new ManualScheduler();
            var ran = false;
            scheduler.Schedule(0f, () => ran = true);
            Assert.IsFalse(ran);

            scheduler.Advance(0f);

            Assert.IsTrue(ran);
        }

        [Test]
        public void A_task_scheduled_from_a_task_lands_at_the_right_moment_inside_one_big_step()
        {
            var scheduler = new ManualScheduler();
            var firedAt = new List<double>();
            scheduler.Schedule(1f, () =>
            {
                firedAt.Add(scheduler.Now);
                scheduler.Schedule(1f, () => firedAt.Add(scheduler.Now));
            });

            scheduler.Advance(5f);

            CollectionAssert.AreEqual(new[] { 1.0, 2.0 }, firedAt);
            Assert.AreEqual(5.0, scheduler.Now, 1e-9);
        }

        [Test]
        public void A_cancelled_task_never_runs_and_reports_not_pending()
        {
            var scheduler = new ManualScheduler();
            var ran = false;
            var task = scheduler.Schedule(1f, () => ran = true);
            Assert.IsTrue(task.IsPending);

            task.Cancel();
            scheduler.Advance(2f);

            Assert.IsFalse(ran);
            Assert.IsFalse(task.IsPending);
            Assert.AreEqual(0, scheduler.PendingCount);
        }

        [Test]
        public void A_task_is_no_longer_pending_once_it_has_run()
        {
            var scheduler = new ManualScheduler();
            var task = scheduler.Schedule(1f, () => { });

            scheduler.Advance(1f);

            Assert.IsFalse(task.IsPending);
            task.Cancel();
        }

        [Test]
        public void CancelAll_clears_everything_pending()
        {
            var scheduler = new ManualScheduler();
            var ran = 0;
            var first = scheduler.Schedule(1f, () => ran += 1);
            scheduler.Schedule(2f, () => ran += 1);

            scheduler.CancelAll();
            scheduler.Advance(10f);

            Assert.AreEqual(0, ran);
            Assert.IsFalse(first.IsPending);
        }

        [Test]
        public void A_task_can_cancel_the_rest_while_the_clock_is_advancing()
        {
            var scheduler = new ManualScheduler();
            var ran = new List<string>();
            scheduler.Schedule(1f, () =>
            {
                ran.Add("a");
                scheduler.CancelAll();
            });
            scheduler.Schedule(2f, () => ran.Add("b"));

            scheduler.Advance(10f);

            CollectionAssert.AreEqual(new[] { "a" }, ran);
        }

        [Test]
        public void An_error_handler_keeps_the_remaining_tasks_running()
        {
            var errors = new List<Exception>();
            var scheduler = new ManualScheduler(errors.Add);
            var ran = false;
            scheduler.Schedule(1f, () => throw new InvalidOperationException("boom"));
            scheduler.Schedule(2f, () => ran = true);

            scheduler.Advance(5f);

            Assert.AreEqual(1, errors.Count);
            Assert.IsTrue(ran);
        }

        [Test]
        public void Without_a_handler_a_task_error_propagates_to_the_caller()
        {
            var scheduler = new ManualScheduler();
            scheduler.Schedule(1f, () => throw new InvalidOperationException("boom"));

            Assert.Throws<InvalidOperationException>(() => scheduler.Advance(2f));
        }
    }
}
