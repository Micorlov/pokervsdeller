#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Showdown.Domain;

namespace Showdown.Tests.Domain
{
    internal static class StatsFixtures
    {
        public static HandHistoryEntry Entry(
            HandCategory category = HandCategory.Pair,
            ShowdownOutcome outcome = ShowdownOutcome.BeatDealer,
            bool kickerStrike = false,
            bool pressured = false,
            int forges = 0,
            int net = 20,
            long at = 1_000) =>
            new HandHistoryEntry
            {
                Category = category,
                Outcome = outcome,
                KickerStrike = kickerStrike,
                Pressured = pressured,
                Forges = forges,
                Net = net,
                At = at,
            };

        public static ShowdownSettlement Settlement(
            VsDealer vsDealer = VsDealer.Win,
            bool wonTable = false,
            bool outright = false,
            bool sweep = false) =>
            new ShowdownSettlement
            {
                VsDealer = vsDealer,
                WonTable = wonTable,
                Outright = outright,
                KickerStrike = false,
                Sweep = sweep,
                DealerDelta = 10,
                TableDelta = -10,
                SweepBonus = 0,
                Net = 0,
            };
    }

    [TestFixture]
    public class OutcomeOfTests
    {
        [Test]
        public void ReadsEachSettlementAsItsScoreboardLine()
        {
            Assert.AreEqual(
                ShowdownOutcome.Sweep,
                Stats.OutcomeOf(StatsFixtures.Settlement(sweep: true, wonTable: true, outright: true)));
            Assert.AreEqual(
                ShowdownOutcome.BeatBoth,
                Stats.OutcomeOf(StatsFixtures.Settlement(wonTable: true, outright: true)));
            Assert.AreEqual(ShowdownOutcome.BeatDealer, Stats.OutcomeOf(StatsFixtures.Settlement()));
            Assert.AreEqual(
                ShowdownOutcome.BeatTable,
                Stats.OutcomeOf(StatsFixtures.Settlement(vsDealer: VsDealer.Lose, wonTable: true)));
            Assert.AreEqual(ShowdownOutcome.Push, Stats.OutcomeOf(StatsFixtures.Settlement(vsDealer: VsDealer.Push)));
            Assert.AreEqual(ShowdownOutcome.Lose, Stats.OutcomeOf(StatsFixtures.Settlement(vsDealer: VsDealer.Lose)));
            Assert.AreEqual(ShowdownOutcome.Bail, Stats.OutcomeOf(StatsFixtures.Settlement(vsDealer: VsDealer.Bailed)));
        }

        [Test]
        public void KeepsTheOriginalOutcomeIds()
        {
            var ids = new[] { "sweep", "beatBoth", "beatDealer", "beatTable", "push", "lose", "bail" };
            for (var index = 0; index < ids.Length; index += 1)
            {
                Assert.AreEqual(ids[index], Stats.OutcomeId((ShowdownOutcome)index));
                Assert.IsTrue(Stats.TryParseOutcomeId(ids[index], out var parsed));
                Assert.AreEqual((ShowdownOutcome)index, parsed);
            }

            Assert.IsFalse(Stats.TryParseOutcomeId("win", out _));
        }
    }

    [TestFixture]
    public class RecordHandResultTests
    {
        private static HandHistoryEntry Entry(
            HandCategory category = HandCategory.Pair,
            ShowdownOutcome outcome = ShowdownOutcome.BeatDealer,
            bool kickerStrike = false,
            bool pressured = false,
            int net = 20,
            long at = 1_000) =>
            StatsFixtures.Entry(category, outcome, kickerStrike, pressured, 0, net, at);

        [Test]
        public void CountsAWonHandAndStartsAStreak()
        {
            var next = Stats.RecordHandResult(Statistics.Default, Entry(net: 30));

            Assert.AreEqual(1, next.HandsPlayed);
            Assert.AreEqual(1, next.HandsWon);
            Assert.AreEqual(30, next.BiggestWin);
            Assert.AreEqual(1, next.CurrentStreak);
            Assert.AreEqual(1, next.BestStreak);
        }

        [Test]
        public void ALossEndsTheStreakButBestStreakSurvives()
        {
            var afterTwoWins = Stats.RecordHandResult(Stats.RecordHandResult(Statistics.Default, Entry()), Entry());

            var next = Stats.RecordHandResult(afterTwoWins, Entry(outcome: ShowdownOutcome.Lose, net: -30));

            Assert.AreEqual(0, next.CurrentStreak);
            Assert.AreEqual(2, next.BestStreak);
            Assert.AreEqual(2, next.HandsWon);
        }

        [Test]
        public void APushLeavesTheStreakUntouched()
        {
            var afterWin = Stats.RecordHandResult(Statistics.Default, Entry());

            var next = Stats.RecordHandResult(afterWin, Entry(outcome: ShowdownOutcome.Push, net: 0));

            Assert.AreEqual(1, next.CurrentStreak);
            Assert.AreEqual(2, next.HandsPlayed);
            Assert.AreEqual(1, next.HandsWon);
        }

        [Test]
        public void TalliesSweepsStrikesBailsAndPressures()
        {
            var one = Stats.RecordHandResult(
                Statistics.Default,
                Entry(outcome: ShowdownOutcome.Sweep, kickerStrike: true, pressured: true, net: 120));
            var two = Stats.RecordHandResult(one, Entry(outcome: ShowdownOutcome.Bail, net: -10));

            Assert.AreEqual(1, two.Sweeps);
            Assert.AreEqual(1, two.KickerStrikes);
            Assert.AreEqual(1, two.Pressures);
            Assert.AreEqual(1, two.Bails);
        }

        [Test]
        public void RemembersTheStrongestHandEverMadeEvenWhenItLost()
        {
            var afterPair = Stats.RecordHandResult(Statistics.Default, Entry());

            var next = Stats.RecordHandResult(
                afterPair,
                Entry(category: HandCategory.Flush, outcome: ShowdownOutcome.Lose, net: -30));

            Assert.AreEqual(HandCategory.Flush, next.BestHandCategory);

            var later = Stats.RecordHandResult(next, Entry(category: HandCategory.TwoPair));
            Assert.AreEqual(HandCategory.Flush, later.BestHandCategory);
        }

        [Test]
        public void TalliesMadeHandsPerCategoryBailsIncluded()
        {
            var one = Stats.RecordHandResult(Statistics.Default, Entry());
            var two = Stats.RecordHandResult(one, Entry(outcome: ShowdownOutcome.Bail, net: -10));

            Assert.AreEqual(2, two.CategoryCounts[HandCategory.Pair]);
            Assert.IsFalse(two.CategoryCounts.ContainsKey(HandCategory.Flush));
        }

        [Test]
        public void KeepsHistoryNewestFirstAndCapped()
        {
            var filled = Enumerable.Range(0, Stats.HistoryLimit + 3)
                .Aggregate(Statistics.Default, (stats, i) => Stats.RecordHandResult(stats, Entry(at: i)));

            Assert.AreEqual(Stats.HistoryLimit, filled.History.Count);
            Assert.AreEqual(Stats.HistoryLimit + 2, filled.History[0].At);
        }

        [Test]
        public void DoesNotMutateThePreviousStatistics()
        {
            var prev = Statistics.Default;
            var snapshot = new Statistics();

            Stats.RecordHandResult(prev, Entry());

            TestSupport.AssertDeepEqual(snapshot, prev);
        }
    }

    [TestFixture]
    public class WinRatePercentTests
    {
        [Test]
        public void ReturnsNullBeforeAnyHand()
        {
            Assert.IsNull(Stats.WinRatePercent(Statistics.Default));
        }

        [Test]
        public void RoundsToAWholePercent()
        {
            var one = Stats.RecordHandResult(Statistics.Default, StatsFixtures.Entry());
            var two = Stats.RecordHandResult(one, StatsFixtures.Entry(outcome: ShowdownOutcome.Lose, net: -30));
            var three = Stats.RecordHandResult(two, StatsFixtures.Entry(outcome: ShowdownOutcome.Lose, net: -30));

            Assert.AreEqual(33, Stats.WinRatePercent(three));
        }

        [Test]
        public void RoundsHalvesUpLikeMathRound()
        {
            // 1 win in 8 hands = 12.5 percent, which Math.Round alone would take down to 12.
            var stats = new Statistics { HandsPlayed = 8, HandsWon = 1 };

            Assert.AreEqual(13, Stats.WinRatePercent(stats));
        }
    }

    [TestFixture]
    public class MigrateStatisticsTests
    {
        private static Dictionary<string, object> Obj(params (string key, object value)[] fields) =>
            fields.ToDictionary(field => field.key, field => field.value);

        /// <summary>What the previous game actually wrote to storage, as a JSON parser would hand it over.</summary>
        private static Dictionary<string, object> Legacy() =>
            Obj(
                ("handsPlayed", 40L),
                ("handsWon", 17L),
                ("biggestWin", 250L),
                ("reUps", 2L),
                ("currentStreak", 1L),
                ("bestStreak", 5L),
                ("bestHandCategory", "flush"),
                ("categoryCounts", Obj(("pair", 20L), ("flush", 1L))),
                ("history", new List<object>
                {
                    Obj(("category", "pair"), ("decision", "raise"), ("outcome", "win"), ("net", 20L), ("at", 1_000L)),
                    Obj(("category", "highCard"), ("decision", "fold"), ("outcome", "fold"), ("net", -10L), ("at", 900L)),
                    Obj(("category", "pair"), ("decision", "raise"), ("outcome", "noQualify"), ("net", 10L), ("at", 800L)),
                    Obj(("category", "twoPair"), ("decision", "raise"), ("outcome", "push"), ("net", 0L), ("at", 700L)),
                }));

        [Test]
        public void KeepsTheLifetimeRecordAndTranslatesOldHistory()
        {
            var migrated = Stats.MigrateStatistics(Legacy());

            Assert.AreEqual(40, migrated.HandsPlayed);
            Assert.AreEqual(5, migrated.BestStreak);
            Assert.AreEqual(HandCategory.Flush, migrated.BestHandCategory);
            Assert.AreEqual(0, migrated.Sweeps);
            Assert.AreEqual(20, migrated.CategoryCounts[HandCategory.Pair]);
            CollectionAssert.AreEqual(
                new[] { ShowdownOutcome.BeatDealer, ShowdownOutcome.Bail, ShowdownOutcome.BeatDealer, ShowdownOutcome.Push },
                migrated.History.Select(h => h.Outcome).ToArray());
            foreach (var h in migrated.History)
            {
                Assert.IsFalse(h.KickerStrike);
                Assert.AreEqual(0, h.Forges);
            }
        }

        [Test]
        public void IsIdempotentOnAlreadyMigratedData()
        {
            var once = Stats.MigrateStatistics(Legacy());
            TestSupport.AssertDeepEqual(once, Stats.MigrateStatistics(once));
        }

        [Test]
        public void FallsBackToDefaultsOnGarbage()
        {
            TestSupport.AssertDeepEqual(Statistics.Default, Stats.MigrateStatistics(null));
            TestSupport.AssertDeepEqual(Statistics.Default, Stats.MigrateStatistics("corrupt"));
            TestSupport.AssertDeepEqual(Statistics.Default, Stats.MigrateStatistics(7));
        }

        [Test]
        public void DropsUnreadableHistoryEntriesAndKeepsTheRest()
        {
            var migrated = Stats.MigrateStatistics(
                Obj(
                    ("history", new List<object>
                    {
                        Obj(("category", "notAHand"), ("outcome", "win"), ("net", 1L), ("at", 1L)),
                        Obj(("category", "pair"), ("outcome", "mystery"), ("net", 1L), ("at", 1L)),
                        Obj(("category", "pair"), ("outcome", "win"), ("net", "lots"), ("at", 1L)),
                        Obj(
                            ("category", "trips"),
                            ("outcome", "beatBoth"),
                            ("kickerStrike", true),
                            ("pressured", true),
                            ("forges", 1L),
                            ("net", 90L),
                            ("at", 2L)),
                    })));

            Assert.AreEqual(1, migrated.History.Count);
            Assert.AreEqual(ShowdownOutcome.BeatBoth, migrated.History[0].Outcome);
            Assert.IsTrue(migrated.History[0].KickerStrike);
            Assert.IsTrue(migrated.History[0].Pressured);
            Assert.AreEqual(1, migrated.History[0].Forges);
        }
    }
}
