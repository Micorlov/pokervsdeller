#nullable enable
using System.Linq;
using NUnit.Framework;
using Showdown.Domain;
using Showdown.Game;

namespace Showdown.Tests.Game
{
    [TestFixture]
    public class HandLedgerTests
    {
        private const int Ante = 10;
        private const long At = 1_760_000_000_000L;

        private static ShowdownRoundState Settled(ShowdownSettlement? settlement, bool pressured = false, int forges = 0)
        {
            var human = new ShowdownSeatState
            {
                Id = "you",
                Name = "You",
                IsHuman = true,
                Cards = Cards.CreateDeck().Take(HandRanks.HandSize).ToArray(),
                KickerIndex = 0,
                Pressured = pressured,
                ForgesUsed = forges,
                Settlement = settlement,
            };
            return new ShowdownRoundState { Phase = ShowdownPhase.Settled, Ante = Ante, Seats = new[] { human } };
        }

        private static ShowdownSettlement Win(int net) =>
            new ShowdownSettlement { VsDealer = VsDealer.Win, WonTable = false, Net = net };

        private static Statistics WithStreak(int streak) =>
            Statistics.Default with { CurrentStreak = streak, BestStreak = streak, HandsPlayed = streak, HandsWon = streak };

        [Test]
        public void Books_the_entry_from_the_settled_human_seat()
        {
            var tally = HandLedger.Tally(Statistics.Default, Settled(Win(20), pressured: true, forges: 2), At)!;

            Assert.AreEqual(HandRanks.EvaluateHand(Cards.CreateDeck().Take(5).ToArray()).Category, tally.Entry.Category);
            Assert.AreEqual(ShowdownOutcome.BeatDealer, tally.Entry.Outcome);
            Assert.IsTrue(tally.Entry.Pressured);
            Assert.AreEqual(2, tally.Entry.Forges);
            Assert.AreEqual(20, tally.Entry.Net);
            Assert.AreEqual(At, tally.Entry.At);
            Assert.AreEqual(1, tally.NextStats.HandsPlayed);
            Assert.AreSame(tally.Entry, tally.NextStats.History[0]);
        }

        [Test]
        public void Reports_the_badges_the_hand_newly_earned()
        {
            var tally = HandLedger.Tally(Statistics.Default, Settled(Win(20)), At)!;

            var ids = tally.Unlocked.Select(badge => badge.Id).ToArray();
            CollectionAssert.IsSubsetOf(new[] { AchievementIds.FirstHand, AchievementIds.FirstWin }, ids);
            CollectionAssert.DoesNotContain(ids, AchievementIds.Sweep);
        }

        [TestCase(2, 3, 20)]
        [TestCase(4, 5, 40)]
        [TestCase(6, 7, 60)]
        [TestCase(9, 10, 100)]
        public void A_win_that_lands_on_a_rung_pays_that_rungs_units_of_ante(int before, int streak, int amount)
        {
            var tally = HandLedger.Tally(WithStreak(before), Settled(Win(20)), At)!;

            Assert.AreEqual(new StreakPayout(streak, amount), tally.Bonus);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(7)]
        [TestCase(10)]
        public void A_win_between_rungs_pays_nothing(int before)
        {
            Assert.IsNull(HandLedger.Tally(WithStreak(before), Settled(Win(20)), At)!.Bonus);
        }

        [Test]
        public void A_push_holding_a_rung_count_does_not_pay_it_again()
        {
            var push = new ShowdownSettlement { VsDealer = VsDealer.Push, Net = 0 };

            var tally = HandLedger.Tally(WithStreak(3), Settled(push), At)!;

            Assert.AreEqual(3, tally.NextStats.CurrentStreak);
            Assert.IsNull(tally.Bonus);
        }

        [Test]
        public void A_hand_without_a_settlement_books_nothing()
        {
            Assert.IsNull(HandLedger.Tally(Statistics.Default, Settled(null), At));
        }
    }
}
