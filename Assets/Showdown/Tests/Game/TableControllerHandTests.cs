#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Showdown.Domain;
using Showdown.Game;

namespace Showdown.Tests.Game
{
    /// <summary>Whole hands played through the controller on a manual clock: phases, beats, cues, persistence.</summary>
    [TestFixture]
    public class TableControllerHandTests
    {
        private const double Tolerance = 1e-3;

        [Test]
        public void A_hand_walks_deal_kicker_climb_hold_reveal_settle_in_order()
        {
            var rig = TableRig.WithBankroll(500);

            rig.Table.StartHand(10);
            rig.DealToKickerPick();
            rig.LockKicker(2);
            rig.PlayOutHolding();

            var expected = new ShowdownPhase?[]
            {
                ShowdownPhase.Dealing, ShowdownPhase.Kicker, ShowdownPhase.Actions, ShowdownPhase.Reveal, ShowdownPhase.Settled,
            };
            CollectionAssert.AreEqual(expected, rig.PhaseSequence());
        }

        [Test]
        public void The_deal_beats_land_a_chip_then_five_cards_at_110ms_then_settle_at_910ms()
        {
            var rig = TableRig.WithBankroll(500);

            rig.Table.StartHand(10);
            rig.Advance(1f);

            CollectionAssert.AreEqual(
                new[] { Cue.Chip, Cue.Deal, Cue.Deal, Cue.Deal, Cue.Deal, Cue.Deal },
                rig.Feedback.Played.Take(6).ToArray());
            var dealTimes = rig.Feedback.TimesOf(Cue.Deal);
            for (var beat = 0; beat < 5; beat += 1)
            {
                Assert.AreEqual(0.11 * (beat + 1), dealTimes[beat], Tolerance);
            }

            var kickerChange = rig.Changes.First(change => change.Phase == ShowdownPhase.Kicker);
            Assert.AreEqual(0.91, kickerChange.Time, Tolerance);
        }

        [Test]
        public void Antes_leave_the_stack_at_once_and_bots_pick_kickers_when_the_deal_settles()
        {
            var rig = TableRig.WithBankroll(500);

            rig.Table.StartHand(10);
            Assert.AreEqual(ShowdownPhase.Dealing, rig.Round.Phase);
            Assert.AreEqual(480, rig.Table.Bankroll);
            Assert.AreEqual(500, rig.BankrollStore.Value, "the store only moves when a hand settles");

            rig.DealToKickerPick();

            Assert.IsNull(rig.Human.KickerIndex);
            Assert.IsTrue(rig.Round.Seats.Skip(1).All(seat => seat.KickerIndex.HasValue));
            Assert.IsFalse(rig.Table.KickerLocked);
        }

        [Test]
        public void Picking_and_locking_a_kicker_plays_tap_then_chip_and_the_first_card_turns_650ms_later()
        {
            var rig = TableRig.WithBankroll(500);
            rig.Table.StartHand(10);
            rig.DealToKickerPick();

            rig.Table.SelectKicker(3);
            rig.Table.SelectKicker(1);
            Assert.AreEqual(1, rig.Human.KickerIndex, "re-picking is fine until the climb starts");
            Assert.AreEqual(new[] { Cue.Tap, Cue.Tap }, rig.Feedback.Played.Skip(6).ToArray());

            var lockedAt = rig.Scheduler.Now;
            rig.Table.LockKicker();
            Assert.IsTrue(rig.Table.KickerLocked);
            Assert.AreEqual(Cue.Chip, rig.Feedback.Played.Last());

            rig.Table.SelectKicker(4);
            Assert.AreEqual(1, rig.Human.KickerIndex, "locked means locked");

            rig.RunToHumanTurn(1);
            var firstCard = rig.Changes.First(change => change.DealerCards == 1);
            Assert.AreEqual(lockedAt + 0.65, firstCard.Time, Tolerance);
            Assert.AreEqual(1, rig.Feedback.TimesOf(Cue.Flip).Count(time => System.Math.Abs(time - firstCard.Time) < Tolerance));
        }

        [Test]
        public void Locking_without_a_pick_and_picking_out_of_range_do_nothing()
        {
            var rig = TableRig.WithBankroll(500);
            rig.Table.StartHand(10);
            rig.DealToKickerPick();
            var before = rig.ChangedCount;

            rig.Table.LockKicker();
            rig.Table.SelectKicker(-1);
            rig.Table.SelectKicker(5);

            Assert.IsFalse(rig.Table.KickerLocked);
            Assert.IsNull(rig.Human.KickerIndex);
            Assert.AreEqual(before, rig.ChangedCount);
        }

        [Test]
        public void Settling_persists_the_bankroll_and_it_equals_the_human_stack()
        {
            var rig = TableRig.WithBankroll(500);

            rig.Table.StartHand(10);
            rig.PlayOutHolding();

            Assert.AreEqual(rig.Human.Stack, rig.Table.Bankroll);
            Assert.AreEqual(rig.Human.Stack, rig.BankrollStore.Value);
            Assert.AreEqual("{\"bankroll\":" + rig.Human.Stack + "}", rig.Storage.Get(StorageKeys.Bankroll));
            Assert.AreEqual(500 + rig.Human.Settlement!.Net + (rig.Table.StreakBonus?.Amount ?? 0), rig.Human.Stack);
        }

        [Test]
        public void Settling_counts_the_hand_once_and_adds_one_history_entry()
        {
            var rig = TableRig.WithBankroll(500);

            rig.Table.StartHand(10);
            rig.PlayOutHolding();
            rig.Advance(10f);

            var stats = rig.Table.Stats;
            Assert.AreEqual(1, stats.HandsPlayed);
            Assert.AreEqual(1, stats.History.Count);
            var entry = stats.History[0];
            Assert.AreEqual(TableRig.FixedEpochMs, entry.At);
            Assert.AreEqual(rig.Human.Settlement!.Net, entry.Net);
            Assert.AreEqual(HandRanks.EvaluateHand(rig.Human.Cards).Category, entry.Category);
            Assert.AreEqual(Showdown.Domain.Stats.OutcomeOf(rig.Human.Settlement), entry.Outcome);
            Assert.AreEqual(1, new StatsStore(rig.Storage).Current.HandsPlayed, "stats are persisted whole");
        }

        [Test]
        public void The_reveal_cue_comes_650ms_after_the_fifth_card_and_the_verdict_700ms_after_that()
        {
            var rig = TableRig.WithBankroll(500);
            rig.Table.StartHand(10);

            rig.PlayOutHolding();

            var revealPhase = rig.Changes.First(change => change.Phase == ShowdownPhase.Reveal);
            Assert.AreEqual(revealPhase.Time + 0.65, rig.Feedback.TimesOf(Cue.Reveal).Single(), Tolerance);
            var settledAt = rig.Changes.First(change => change.Phase == ShowdownPhase.Settled).Time;
            Assert.AreEqual(revealPhase.Time + 1.35, settledAt, Tolerance);
            var verdicts = new[] { Cue.Win, Cue.WinBig, Cue.Lose, Cue.Push };
            var verdictIndex = rig.Feedback.Played.FindIndex(cue => verdicts.Contains(cue));
            Assert.Greater(verdictIndex, rig.Feedback.Played.IndexOf(Cue.Reveal));
            Assert.AreEqual(settledAt, rig.Feedback.PlayedAt[verdictIndex], Tolerance);
            Assert.IsTrue(rig.Table.Revealed);
        }

        [Test]
        public void Bots_think_one_after_another_and_the_human_can_act_throughout()
        {
            var rig = TableRig.WithBankroll(500);
            rig.Table.StartHand(10);
            rig.DealToKickerPick();
            rig.LockKicker();
            rig.RunUntil(() => rig.Table.Round!.DealerCards.Count == 1, "the first dealer card");
            var cardAt = rig.Changes.First(change => change.DealerCards == 1).Time;

            rig.Advance((float)(cardAt + 0.05 - rig.Scheduler.Now));
            Assert.AreEqual(1, rig.Table.ThinkingSeat);
            rig.Advance(0.55f);
            Assert.AreEqual(2, rig.Table.ThinkingSeat, "seat 1 acts at 530 ms and seat 2 starts thinking");
            rig.Advance(0.65f);
            Assert.IsNull(rig.Table.ThinkingSeat);
            Assert.IsTrue(rig.Round.Seats.Skip(1).All(seat => seat.LastMove != null));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void The_dealer_draws_exactly_one_card_per_round_whoever_acts_last(bool humanWaitsForBots)
        {
            var rig = TableRig.WithBankroll(500);
            rig.Table.StartHand(10);
            rig.DealToKickerPick();
            rig.LockKicker();

            rig.RunUntil(() =>
            {
                var cards = rig.Table.Round!.DealerCards.Count;
                var botsDone = rig.Round.Seats.Skip(1).All(seat => seat.Status == SeatStatus.Bailed || seat.LastMove != null);
                if (rig.HumanToMove(cards) && (!humanWaitsForBots || botsDone)) rig.Table.Hold();
                return rig.Table.Round!.Phase == ShowdownPhase.Settled;
            }, "the hand to settle");

            var counts = rig.Changes.Select(change => change.DealerCards).Where(count => count >= 0).ToList();
            var distinct = counts.Where((count, index) => index == 0 || count != counts[index - 1]).ToList();
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5 }, distinct);
            Assert.AreEqual(5, rig.Round.DealerCards.Count);
        }

        [Test]
        public void Changed_is_announced_for_every_observable_change_across_whole_hands()
        {
            foreach (var seed in new uint[] { 3, 17, 29 })
            {
                var rig = TableRig.WithBankroll(500, null, seed);
                rig.Table.StartHand(10);
                rig.AssertAnnounced();

                rig.PlayOutHolding();
                rig.AssertAnnounced();
                rig.Table.NextHand();
                rig.AssertAnnounced();
                rig.Advance(2f);
                rig.AssertAnnounced();
                Assert.Greater(rig.ChangedCount, 8);
            }
        }

        [Test]
        public void A_first_settled_hand_earns_the_first_deal_badge_until_dismissed()
        {
            var rig = TableRig.WithBankroll(500);

            rig.Table.StartHand(10);
            Assert.IsEmpty(rig.Table.NewAchievements);
            rig.PlayOutHolding();

            CollectionAssert.Contains(rig.Table.NewAchievements.Select(badge => badge.Id).ToArray(), AchievementIds.FirstHand);
            rig.Table.DismissAchievements();
            Assert.IsEmpty(rig.Table.NewAchievements);
            rig.AssertAnnounced();
        }

        [Test]
        public void NextHand_deals_again_from_the_settled_stack_and_clears_the_last_hands_presentation()
        {
            var rig = TableRig.WithBankroll(500);
            rig.Table.StartHand(10);
            rig.PlayOutHolding();
            var stackAfter = rig.Human.Stack;

            rig.Table.NextHand();

            Assert.AreEqual(ShowdownPhase.Dealing, rig.Round.Phase);
            Assert.AreEqual(2, rig.Round.HandNumber);
            Assert.AreEqual(stackAfter - 20, rig.Human.Stack);
            Assert.IsFalse(rig.Table.Revealed);
            Assert.IsFalse(rig.Table.KickerLocked);
            Assert.IsNull(rig.Table.StreakBonus);
            Assert.IsEmpty(rig.Table.NewAchievements);
        }

        [Test]
        public void NextHand_does_nothing_before_the_hand_has_settled()
        {
            var rig = TableRig.WithBankroll(500);
            rig.StartAndReachFirstDecision();
            var round = rig.Round;

            rig.Table.NextHand();

            Assert.AreSame(round, rig.Round);
        }

        [Test]
        public void StartHand_refuses_a_stack_that_cannot_cover_five_antes()
        {
            var rig = TableRig.WithBankroll(20);

            rig.Table.StartHand(5);

            Assert.IsNull(rig.Table.Round);
            Assert.IsEmpty(rig.Feedback.Played);
            Assert.AreEqual(0, rig.ChangedCount);
        }

        [Test]
        public void The_bankroll_shown_is_the_stored_one_away_from_the_table_and_the_stack_at_it()
        {
            var rig = TableRig.WithBankroll(740);
            Assert.IsTrue(rig.Table.BankrollLoaded);
            Assert.AreEqual(740, rig.Table.Bankroll);

            rig.Table.StartHand(25);

            Assert.AreEqual(690, rig.Table.Bankroll);
        }
    }
}
