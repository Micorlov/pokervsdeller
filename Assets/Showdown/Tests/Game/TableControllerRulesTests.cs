#nullable enable
using System.Linq;
using NUnit.Framework;
using Showdown.Domain;
using DomainBankroll = Showdown.Domain.Bankroll;
using Showdown.Game;

namespace Showdown.Tests.Game
{
    /// <summary>The player's decisions — forge, pressure, bail — and what each costs, cues and persists.</summary>
    [TestFixture]
    public class TableControllerRulesTests
    {
        private const int Ante = 10;
        private const double Tolerance = 1e-3;

        [Test]
        public void Arming_a_forge_only_arms_it_and_cancelling_disarms()
        {
            var rig = TableRig.WithBankroll(500);
            rig.StartAndReachFirstDecision();

            rig.Table.BeginForge();
            Assert.IsTrue(rig.Table.ForgeArming);
            rig.Table.CancelForge();
            Assert.IsFalse(rig.Table.ForgeArming);
            Assert.AreEqual(0, rig.Human.ForgesUsed);
            rig.AssertAnnounced();
        }

        [Test]
        public void A_forge_swaps_the_card_rings_the_slot_for_1800ms_then_clears_it()
        {
            var rig = TableRig.WithBankroll(500);
            rig.StartAndReachFirstDecision();
            var stackBefore = rig.Human.Stack;
            var discard = rig.Human.KickerIndex == 0 ? 1 : 0;
            var oldCard = rig.Human.Cards[discard];
            rig.Table.BeginForge();

            rig.Table.Forge(discard);

            Assert.AreEqual(discard, rig.Table.FreshCardIndex);
            Assert.IsFalse(rig.Table.ForgeArming, "submitting a move disarms the forge");
            Assert.AreEqual(1, rig.Human.ForgesUsed);
            Assert.AreNotEqual(oldCard, rig.Human.Cards[discard]);
            Assert.AreEqual(stackBefore - Ante, rig.Human.Stack);
            Assert.AreEqual(Cue.Flip, rig.Feedback.Played.Last());

            rig.Advance(1.79f);
            Assert.AreEqual(discard, rig.Table.FreshCardIndex);
            rig.Advance(0.03f);
            Assert.IsNull(rig.Table.FreshCardIndex);
            rig.AssertAnnounced();
        }

        [Test]
        public void The_kicker_cannot_be_forged_away()
        {
            var rig = TableRig.WithBankroll(500);
            rig.StartAndReachFirstDecision();
            var before = rig.Round;

            rig.Table.Forge(rig.Human.KickerIndex!.Value);

            Assert.AreSame(before, rig.Round);
            Assert.IsNull(rig.Table.FreshCardIndex);
        }

        [Test]
        public void A_forge_outside_the_hand_is_ignored_and_never_rings_a_card()
        {
            var rig = TableRig.WithBankroll(500);
            rig.StartAndReachFirstDecision();
            rig.Table.Hold();
            var before = rig.Round;

            rig.Table.Forge(4);
            rig.Table.Forge(-1);
            rig.Table.Forge(9);

            Assert.AreSame(before, rig.Round, "already moved this round");
            Assert.IsNull(rig.Table.FreshCardIndex);
        }

        [Test]
        public void Only_two_forges_are_allowed_per_hand()
        {
            var rig = TableRig.WithBankroll(500);
            rig.StartAndReachFirstDecision();
            var spare = Enumerable.Range(0, 5).Where(index => index != rig.Human.KickerIndex).ToArray();

            rig.Table.Forge(spare[0]);
            rig.RunToHumanTurn(2);
            rig.Table.Forge(spare[1]);
            rig.RunToHumanTurn(3);
            var before = rig.Round;
            rig.Table.Forge(spare[2]);

            Assert.AreEqual(2, rig.Human.ForgesUsed);
            Assert.AreSame(before, rig.Round);
        }

        [Test]
        public void Pressure_armed_before_round_three_is_not_applied()
        {
            var rig = TableRig.WithBankroll(500);
            rig.StartAndReachFirstDecision();
            rig.Table.SetPressureArmed(true);
            Assert.IsTrue(rig.Table.PressureArmed);

            rig.Table.Hold();

            Assert.IsFalse(rig.Human.Pressured);
            Assert.AreEqual(Ante, rig.Human.DealerStake);
            Assert.IsFalse(rig.Table.PressureArmed, "the toggle is spent by the move");
            Assert.AreEqual(0, rig.CountCues(Cue.Raise));
            Assert.AreEqual(Cue.Tap, rig.Feedback.Played.Last());
        }

        [Test]
        public void Pressure_armed_in_round_three_doubles_the_dealer_stake_and_raises()
        {
            var rig = TableRig.WithBankroll(500);
            rig.StartAndReachFirstDecision();
            rig.Table.Hold();
            rig.RunToHumanTurn(2);
            rig.Table.Hold();
            rig.RunToHumanTurn(3);
            var stackBefore = rig.Human.Stack;

            rig.Table.SetPressureArmed(true);
            rig.Table.Hold();

            Assert.IsTrue(rig.Human.Pressured);
            Assert.AreEqual(2 * Ante, rig.Human.DealerStake);
            Assert.AreEqual(stackBefore - Ante, rig.Human.Stack);
            Assert.AreEqual(1, rig.CountCues(Cue.Raise));
            Assert.IsFalse(rig.Table.PressureArmed);
        }

        [Test]
        public void Pressure_is_counted_in_the_stats_when_the_hand_settles()
        {
            var rig = TableRig.WithBankroll(500);
            rig.StartAndReachFirstDecision();
            rig.Table.Hold();
            rig.RunToHumanTurn(2);
            rig.Table.Hold();
            rig.RunToHumanTurn(3);
            rig.Table.SetPressureArmed(true);
            rig.Table.Hold();

            rig.PlayOutHolding();

            Assert.AreEqual(1, rig.Table.Stats.Pressures);
            Assert.IsTrue(rig.Table.Stats.History[0].Pressured);
        }

        [Test]
        public void A_forge_in_round_three_can_carry_the_pressure_for_two_antes()
        {
            var rig = TableRig.WithBankroll(500);
            rig.StartAndReachFirstDecision();
            rig.Table.Hold();
            rig.RunToHumanTurn(2);
            rig.Table.Hold();
            rig.RunToHumanTurn(3);
            var stackBefore = rig.Human.Stack;
            var discard = rig.Human.KickerIndex == 0 ? 1 : 0;

            rig.Table.SetPressureArmed(true);
            rig.Table.Forge(discard);

            Assert.IsTrue(rig.Human.Pressured);
            Assert.AreEqual(1, rig.Human.ForgesUsed);
            Assert.AreEqual(stackBefore - 2 * Ante, rig.Human.Stack);
            Assert.AreEqual(Cue.Raise, rig.Feedback.Played.Last());
        }

        [Test]
        public void Bailing_takes_half_back_folds_and_the_climb_carries_on_without_the_human()
        {
            var rig = TableRig.WithBankroll(500);
            rig.StartAndReachFirstDecision();

            rig.Table.SetPressureArmed(true);
            rig.Table.Bail();

            Assert.AreEqual(SeatStatus.Bailed, rig.Human.Status);
            Assert.AreEqual(Ante, rig.Human.Refund);
            Assert.AreEqual(500 - 2 * Ante + Ante, rig.Human.Stack);
            Assert.AreEqual(Cue.Fold, rig.Feedback.Played.Last());
            Assert.IsFalse(rig.Human.Pressured, "a bail never carries pressure");

            rig.RunUntil(() => rig.Table.Round!.Phase == ShowdownPhase.Settled, "the hand to settle without the human");

            Assert.AreEqual(5, rig.Round.DealerCards.Count);
            Assert.AreEqual(ShowdownOutcome.Bail, rig.Table.Stats.History[0].Outcome);
            Assert.AreEqual(1, rig.Table.Stats.Bails);
            Assert.AreEqual(-Ante, rig.Table.Stats.History[0].Net);
            Assert.AreEqual(rig.Human.Stack, rig.BankrollStore.Value);
            Assert.AreEqual(490, rig.Table.Bankroll);
        }

        [Test]
        public void A_round_that_changes_again_while_the_next_card_waits_still_schedules_one_draw()
        {
            var rig = TableRig.WithBankroll(500);
            rig.StartAndReachFirstDecision();
            rig.Table.Hold();
            rig.RunUntil(() => ShowdownRound.AllActed(rig.Round), "every seat to have acted");
            Assert.AreEqual(1, rig.Scheduler.PendingCount, "just the dealer's draw");

            rig.Table.ClaimDailyBonus();
            rig.Table.ClaimDailyBonus();

            Assert.AreEqual(1, rig.Scheduler.PendingCount, "re-rendering the same round must not stack a second draw");
            rig.Advance(0.7f);
            Assert.AreEqual(2, rig.Round.DealerCards.Count);
        }

        [Test]
        public void A_second_move_in_the_same_round_is_ignored()
        {
            var rig = TableRig.WithBankroll(500);
            rig.StartAndReachFirstDecision();

            rig.Table.Hold();
            var round = rig.Round;
            var cues = rig.Feedback.Played.Count;
            rig.Table.Bail();
            rig.Table.Hold();

            Assert.AreSame(round, rig.Round);
            Assert.AreEqual(cues, rig.Feedback.Played.Count);
        }

        [Test]
        public void A_win_streak_of_three_pays_two_antes_once_and_persists_it()
        {
            var streakTwo = Statistics.Default with { CurrentStreak = 2, BestStreak = 2, HandsPlayed = 2, HandsWon = 2 };
            var seed = TableRig.FindSeed(500, Ante, streakTwo, rig => rig.Human.Settlement!.Net > 0);

            var played = TableRig.PlayHand(seed, 500, Ante, streakTwo);

            var bonus = played.Table.StreakBonus!;
            Assert.AreEqual(3, bonus.Streak);
            Assert.AreEqual(2 * Ante, bonus.Amount);
            Assert.AreEqual(3, played.Table.Stats.CurrentStreak);
            var expectedStack = 500 + played.Human.Settlement!.Net + 2 * Ante;
            Assert.AreEqual(expectedStack, played.Human.Stack, "the bonus lands on the human stack after the settle");
            Assert.AreEqual(expectedStack, played.Table.Bankroll);
            Assert.AreEqual(expectedStack, played.BankrollStore.Value);
            Assert.AreEqual("{\"bankroll\":" + expectedStack + "}", played.Storage.Get(StorageKeys.Bankroll));

            played.Advance(30f);
            played.Table.DismissStreakBonus();
            Assert.IsNull(played.Table.StreakBonus);
            Assert.AreEqual(expectedStack, played.Table.Bankroll, "paid exactly once");
            Assert.AreEqual(3, new StatsStore(played.Storage).Current.CurrentStreak);
            played.AssertAnnounced();
        }

        [Test]
        public void A_win_that_does_not_land_on_the_streak_ladder_pays_nothing()
        {
            var seed = TableRig.FindSeed(500, Ante, Statistics.Default, rig => rig.Human.Settlement!.Net > 0);

            var played = TableRig.PlayHand(seed, 500, Ante);

            Assert.AreEqual(1, played.Table.Stats.CurrentStreak);
            Assert.IsNull(played.Table.StreakBonus);
            Assert.AreEqual(500 + played.Human.Settlement!.Net, played.Table.Bankroll);
        }

        [Test]
        public void A_loss_breaks_the_streak_and_pays_no_bonus()
        {
            var streakTwo = Statistics.Default with { CurrentStreak = 2, BestStreak = 2 };
            var seed = TableRig.FindSeed(500, Ante, streakTwo, rig => rig.Human.Settlement!.Net < 0);

            var played = TableRig.PlayHand(seed, 500, Ante, streakTwo);

            Assert.AreEqual(0, played.Table.Stats.CurrentStreak);
            Assert.IsNull(played.Table.StreakBonus);
            Assert.AreEqual(2, played.Table.Stats.BestStreak);
        }

        [Test]
        public void Busting_cues_the_bust_800ms_after_the_settle_and_the_free_top_up_refills()
        {
            const int minimumBankroll = 25;
            const int smallAnte = 5;
            var seed = TableRig.FindSeed(minimumBankroll, smallAnte, null, rig => rig.Human.Settlement!.Net < 0);
            var rig = TableRig.WithBankroll(minimumBankroll, null, seed);
            rig.Table.StartHand(smallAnte);

            rig.PlayOutHolding();
            var settledAt = rig.Changes.First(change => change.Phase == ShowdownPhase.Settled).Time;
            Assert.IsTrue(DomainBankroll.IsBust(rig.Table.Bankroll, smallAnte));
            Assert.AreEqual(0, rig.CountCues(Cue.Bust));

            rig.Advance(0.7f);
            Assert.AreEqual(0, rig.CountCues(Cue.Bust));
            rig.Advance(0.15f);
            Assert.AreEqual(1, rig.CountCues(Cue.Bust));
            Assert.AreEqual(settledAt + 0.8, rig.Feedback.TimesOf(Cue.Bust).Single(), 0.02);

            rig.Table.NextHand();
            Assert.AreEqual(ShowdownPhase.Settled, rig.Round.Phase, "no seat for a short stack");

            var short_ = rig.Table.Bankroll;
            rig.Table.ReUpNow(smallAnte);
            Assert.AreEqual(short_ + 500, rig.Table.Bankroll);
            Assert.AreEqual(short_ + 500, rig.BankrollStore.Value);
            Assert.AreEqual(1, rig.Table.Stats.ReUps);
            Assert.AreEqual(Cue.Chip, rig.Feedback.Played.Last());

            rig.Table.ReUpNow(smallAnte);
            Assert.AreEqual(1, rig.Table.Stats.ReUps, "re-up is a no-op once the stack can cover a hand");
            rig.Table.NextHand();
            Assert.AreEqual(ShowdownPhase.Dealing, rig.Round.Phase);
        }

        [Test]
        public void Re_up_away_from_the_table_tops_up_the_stored_bankroll_only_when_short()
        {
            var rig = TableRig.WithBankroll(10);

            rig.Table.ReUpNow(5);

            Assert.AreEqual(510, rig.Table.Bankroll);
            Assert.AreEqual("{\"bankroll\":510}", rig.Storage.Get(StorageKeys.Bankroll));
            Assert.AreEqual(1, rig.Table.Stats.ReUps);

            var healthy = TableRig.WithBankroll(500);
            healthy.Table.ReUpNow(10);
            Assert.AreEqual(500, healthy.Table.Bankroll);
            Assert.AreEqual(0, healthy.ChangedCount);
            Assert.IsEmpty(healthy.Feedback.Played);
        }

        [Test]
        public void The_daily_bonus_adds_250_on_or_off_the_table_and_persists()
        {
            var home = TableRig.WithBankroll(300);
            home.Table.ClaimDailyBonus();
            Assert.AreEqual(550, home.Table.Bankroll);
            Assert.AreEqual("{\"bankroll\":550}", home.Storage.Get(StorageKeys.Bankroll));
            Assert.AreEqual(Cue.Chip, home.Feedback.Played.Last());
            home.AssertAnnounced();

            var seated = TableRig.WithBankroll(300);
            seated.Table.StartHand(Ante);
            seated.Table.ClaimDailyBonus();
            Assert.AreEqual(300 - 2 * Ante + 250, seated.Human.Stack);
            Assert.AreEqual(300 - 2 * Ante + 250, seated.BankrollStore.Value);
            seated.AssertAnnounced();
        }

        [Test]
        public void Resetting_the_bankroll_restores_500_and_announces_it()
        {
            var rig = TableRig.WithBankroll(1234);

            rig.Table.ResetBankroll();

            Assert.AreEqual(500, rig.Table.Bankroll);
            Assert.AreEqual("{\"bankroll\":500}", rig.Storage.Get(StorageKeys.Bankroll));
            Assert.AreEqual(1, rig.ChangedCount);
        }

        [Test]
        public void Leaving_the_table_clears_every_timer_and_all_presentation_state()
        {
            var rig = TableRig.WithBankroll(500);
            rig.StartAndReachFirstDecision();
            rig.Table.BeginForge();
            rig.Table.SetPressureArmed(true);
            rig.Table.Forge(rig.Human.KickerIndex == 0 ? 1 : 0);
            Assert.Greater(rig.Scheduler.PendingCount, 0);

            rig.Table.LeaveTable();

            Assert.IsNull(rig.Table.Round);
            Assert.IsFalse(rig.Table.KickerLocked);
            Assert.IsFalse(rig.Table.ForgeArming);
            Assert.IsFalse(rig.Table.PressureArmed);
            Assert.IsNull(rig.Table.FreshCardIndex);
            Assert.IsNull(rig.Table.ThinkingSeat);
            Assert.IsFalse(rig.Table.Revealed);
            Assert.AreEqual(0, rig.Scheduler.PendingCount);

            var cues = rig.Feedback.Played.Count;
            var changes = rig.ChangedCount;
            rig.Advance(30f);
            Assert.AreEqual(cues, rig.Feedback.Played.Count, "no cue fires after leaving");
            Assert.AreEqual(changes, rig.ChangedCount, "nothing is announced after leaving");
            Assert.AreEqual(500, rig.BankrollStore.Value, "nothing was settled, so the chips are untouched");
            Assert.AreEqual(0, rig.Table.Stats.HandsPlayed);
        }

        [Test]
        public void Leaving_during_the_deal_stops_the_deal_beats()
        {
            var rig = TableRig.WithBankroll(500);
            rig.Table.StartHand(Ante);
            rig.Advance(0.25f);

            rig.Table.LeaveTable();
            rig.Advance(10f);

            Assert.AreEqual(2, rig.CountCues(Cue.Deal));
            Assert.IsNull(rig.Table.Round);
            Assert.AreEqual(500, rig.Table.Bankroll);
        }

        [Test]
        public void Disposing_the_controller_cancels_everything_pending()
        {
            var rig = TableRig.WithBankroll(500);
            rig.Table.StartHand(Ante);

            rig.Table.Dispose();

            Assert.AreEqual(0, rig.Scheduler.PendingCount);
        }

        [Test]
        public void A_pinned_seed_makes_two_controllers_deal_identical_hands()
        {
            var first = TableRig.WithBankroll(500, null, 4242);
            var second = TableRig.WithBankroll(500, null, 4242);

            first.Table.StartHand(Ante);
            second.Table.StartHand(Ante);

            for (var seat = 0; seat < first.Round.Seats.Count; seat += 1)
            {
                CollectionAssert.AreEqual(first.Round.Seats[seat].Cards, second.Round.Seats[seat].Cards);
            }

            CollectionAssert.AreEqual(first.Round.Stock, second.Round.Stock);
        }

        [Test]
        public void A_pinned_seed_replays_the_whole_hand_including_the_bots()
        {
            var first = TableRig.PlayHand(77, 500, Ante);
            var second = TableRig.PlayHand(77, 500, Ante);

            Assert.AreEqual(first.Human.Stack, second.Human.Stack);
            CollectionAssert.AreEqual(first.Round.DealerCards, second.Round.DealerCards);
            CollectionAssert.AreEqual(
                first.Round.Seats.Select(seat => seat.Status).ToArray(),
                second.Round.Seats.Select(seat => seat.Status).ToArray());
        }

        [Test]
        public void A_pinned_seed_is_used_once_then_the_seed_source_takes_over()
        {
            var sourced = TableRig.WithBankroll(500);
            sourced.Table.PinNextSeed(5);
            sourced.Table.StartHand(Ante);
            sourced.PlayOutHolding();
            sourced.Table.NextHand();

            var control = TableRig.WithBankroll(500, null, 1000);
            control.Table.StartHand(Ante);

            for (var seat = 0; seat < control.Round.Seats.Count; seat += 1)
            {
                CollectionAssert.AreEqual(control.Round.Seats[seat].Cards, sourced.Round.Seats[seat].Cards);
            }
        }

        [Test]
        public void Different_seeds_deal_different_hands()
        {
            var first = TableRig.WithBankroll(500, null, 1);
            var second = TableRig.WithBankroll(500, null, 2);

            first.Table.StartHand(Ante);
            second.Table.StartHand(Ante);

            CollectionAssert.AreNotEqual(first.Human.Cards, second.Human.Cards);
        }

        [Test]
        public void A_failing_beat_does_not_strand_the_rest_of_the_clock()
        {
            var rig = TableRig.WithBankroll(500);
            var errors = new System.Collections.Generic.List<System.Exception>();
            var scheduler = new ManualScheduler(errors.Add);
            var table = new TableController(rig.BankrollStore, rig.StatsStore, new ThrowingOnceFeedback(), scheduler, () => 1u);

            table.StartHand(Ante);
            scheduler.Advance(5f);

            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual(ShowdownPhase.Kicker, table.Round!.Phase, "the later beats still ran");
        }

        private sealed class ThrowingOnceFeedback : IFeedback
        {
            private bool thrown;

            public void Play(Cue cue)
            {
                if (cue != Cue.Deal || thrown) return;
                thrown = true;
                throw new System.InvalidOperationException("audio device vanished");
            }
        }
    }
}
