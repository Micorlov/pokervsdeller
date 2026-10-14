#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Showdown.Domain;

namespace Showdown.Tests.Domain
{
    [TestFixture]
    public class AchievementsTests
    {
        private static IReadOnlyList<string> Ids(IEnumerable<Achievement> achievements) =>
            achievements.Select(a => a.Id).ToList();

        [Test]
        public void EveryIdIsUnique()
        {
            var ids = Ids(Achievements.All);
            Assert.AreEqual(ids.Count, ids.Distinct().Count());
        }

        [Test]
        public void KeepsTheOriginalIdsInBookOrder()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "firstHand", "firstWin", "sweep", "sweepMaster", "highRoller", "hotHand",
                    "unstoppable", "fullHouseOrBetter", "royalty", "kickerKing", "ironNerve", "regular",
                },
                Ids(Achievements.All));
        }
    }

    [TestFixture]
    public class UnlockedAchievementsTests
    {
        private static IReadOnlyList<string> UnlockedIds(Statistics stats) =>
            Achievements.UnlockedAchievements(stats).Select(a => a.Id).ToList();

        [Test]
        public void NothingIsUnlockedFromAFreshRecord()
        {
            Assert.AreEqual(0, Achievements.UnlockedAchievements(Statistics.Default).Count);
        }

        [Test]
        public void FirstHandAndFirstWinUnlockTogetherAfterOneWin()
        {
            var ids = UnlockedIds(new Statistics { HandsPlayed = 1, HandsWon = 1 });
            CollectionAssert.Contains(ids, "firstHand");
            CollectionAssert.Contains(ids, "firstWin");
        }

        [Test]
        public void FirstHandUnlocksWithoutAWin()
        {
            var ids = UnlockedIds(new Statistics { HandsPlayed = 1, HandsWon = 0 });
            CollectionAssert.Contains(ids, "firstHand");
            CollectionAssert.DoesNotContain(ids, "firstWin");
        }

        [Test]
        public void SweepMasterRequiresTenSweepsNotJustOne()
        {
            CollectionAssert.DoesNotContain(UnlockedIds(new Statistics { Sweeps = 1 }), "sweepMaster");
            CollectionAssert.Contains(UnlockedIds(new Statistics { Sweeps = 10 }), "sweepMaster");
        }

        [Test]
        public void HighRollerUnlocksAt500BiggestWin()
        {
            CollectionAssert.DoesNotContain(UnlockedIds(new Statistics { BiggestWin = 499 }), "highRoller");
            CollectionAssert.Contains(UnlockedIds(new Statistics { BiggestWin = 500 }), "highRoller");
        }

        [Test]
        public void HotHandAndUnstoppableReadOffBestStreakNotCurrentStreak()
        {
            var ids = UnlockedIds(new Statistics { CurrentStreak = 0, BestStreak = 5 });
            CollectionAssert.Contains(ids, "hotHand");
            CollectionAssert.DoesNotContain(ids, "unstoppable");
        }

        [Test]
        public void FullHouseOrBetterUnlocksForFullHouseAndEveryStrongerCategoryNotWeakerOnes()
        {
            CollectionAssert.DoesNotContain(
                UnlockedIds(new Statistics { BestHandCategory = HandCategory.Flush }), "fullHouseOrBetter");
            CollectionAssert.Contains(
                UnlockedIds(new Statistics { BestHandCategory = HandCategory.FullHouse }), "fullHouseOrBetter");
            CollectionAssert.Contains(
                UnlockedIds(new Statistics { BestHandCategory = HandCategory.RoyalFlush }), "fullHouseOrBetter");
        }

        [Test]
        public void RoyaltyRequiresTheRoyalFlushSpecifically()
        {
            CollectionAssert.DoesNotContain(
                UnlockedIds(new Statistics { BestHandCategory = HandCategory.StraightFlush }), "royalty");
            CollectionAssert.Contains(
                UnlockedIds(new Statistics { BestHandCategory = HandCategory.RoyalFlush }), "royalty");
        }

        [Test]
        public void KickerKingAndIronNerveReadTheirOwnCounters()
        {
            CollectionAssert.Contains(UnlockedIds(new Statistics { KickerStrikes = 5 }), "kickerKing");
            CollectionAssert.Contains(UnlockedIds(new Statistics { Pressures = 10 }), "ironNerve");
        }

        [Test]
        public void RegularUnlocksAt100HandsPlayed()
        {
            CollectionAssert.DoesNotContain(UnlockedIds(new Statistics { HandsPlayed = 99 }), "regular");
            CollectionAssert.Contains(UnlockedIds(new Statistics { HandsPlayed = 100 }), "regular");
        }

        [Test]
        public void UnlockedBadgesComeBackInBookOrderNotUnlockOrder()
        {
            var ids = UnlockedIds(new Statistics { HandsPlayed = 100, HandsWon = 1, Sweeps = 1 });
            CollectionAssert.AreEqual(new[] { "firstHand", "firstWin", "sweep", "regular" }, ids);
        }
    }

    [TestFixture]
    public class NewlyUnlockedAchievementsTests
    {
        private static IReadOnlyList<string> NewIds(Statistics prev, Statistics next) =>
            Achievements.NewlyUnlockedAchievements(prev, next).Select(a => a.Id).ToList();

        [Test]
        public void EmptyWhenNothingCrossedAThreshold()
        {
            var prev = new Statistics { HandsPlayed = 5, HandsWon = 2 };
            var next = new Statistics { HandsPlayed = 6, HandsWon = 2 };
            Assert.AreEqual(0, Achievements.NewlyUnlockedAchievements(prev, next).Count);
        }

        [Test]
        public void ReportsOnlyTheBadgesTheNextRecordNewlyQualifiesFor()
        {
            var prev = new Statistics { HandsPlayed = 0, HandsWon = 0 };
            var next = new Statistics { HandsPlayed = 1, HandsWon = 1 };

            CollectionAssert.AreEqual(new[] { "firstHand", "firstWin" }, NewIds(prev, next));
        }

        [Test]
        public void NeverReReportsABadgeAlreadyUnlockedInPrev()
        {
            var prev = new Statistics { HandsPlayed = 1, HandsWon = 1 };
            var next = new Statistics { HandsPlayed = 2, HandsWon = 2 };
            Assert.AreEqual(0, Achievements.NewlyUnlockedAchievements(prev, next).Count);
        }

        [Test]
        public void ASingleHandCanCrossMoreThanOneThresholdAtOnce()
        {
            var prev = new Statistics { Sweeps = 9, BiggestWin = 400 };
            var next = new Statistics { Sweeps = 10, BiggestWin = 600 };

            var ids = NewIds(prev, next);
            CollectionAssert.IsSubsetOf(new[] { "sweepMaster", "highRoller" }, ids);
        }
    }
}
