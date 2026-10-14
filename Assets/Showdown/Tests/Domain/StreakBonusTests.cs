#nullable enable
using NUnit.Framework;
using Showdown.Domain;

namespace Showdown.Tests.Domain
{
    [TestFixture]
    public class StreakBonusUnitsTests
    {
        [Test]
        public void ZeroBelowTheFirstThreshold()
        {
            Assert.AreEqual(0, StreakBonus.StreakBonusUnits(0));
            Assert.AreEqual(0, StreakBonus.StreakBonusUnits(1));
            Assert.AreEqual(0, StreakBonus.StreakBonusUnits(2));
        }

        [Test]
        public void PaysAtEachThresholdExactly()
        {
            Assert.AreEqual(2, StreakBonus.StreakBonusUnits(3));
            Assert.AreEqual(4, StreakBonus.StreakBonusUnits(5));
            Assert.AreEqual(6, StreakBonus.StreakBonusUnits(7));
            Assert.AreEqual(10, StreakBonus.StreakBonusUnits(10));
        }

        [Test]
        public void ZeroBetweenThresholds()
        {
            Assert.AreEqual(0, StreakBonus.StreakBonusUnits(4));
            Assert.AreEqual(0, StreakBonus.StreakBonusUnits(6));
            Assert.AreEqual(0, StreakBonus.StreakBonusUnits(8));
            Assert.AreEqual(0, StreakBonus.StreakBonusUnits(9));
        }

        [Test]
        public void ZeroPastTheLastThresholdSoAnEndlessStreakCannotBeFarmed()
        {
            Assert.AreEqual(0, StreakBonus.StreakBonusUnits(11));
            Assert.AreEqual(0, StreakBonus.StreakBonusUnits(25));
        }
    }

    [TestFixture]
    public class StreakBonusForTests
    {
        [Test]
        public void ScalesTheUnitPayoutByTheAnte()
        {
            Assert.AreEqual(20, StreakBonus.StreakBonusFor(3, 10));
            Assert.AreEqual(40, StreakBonus.StreakBonusFor(5, 10));
            Assert.AreEqual(150, StreakBonus.StreakBonusFor(7, 25));
            Assert.AreEqual(50, StreakBonus.StreakBonusFor(10, 5));
        }

        [Test]
        public void ZeroChipsOffTheAnteForANonThresholdStreak()
        {
            Assert.AreEqual(0, StreakBonus.StreakBonusFor(4, 25));
            Assert.AreEqual(0, StreakBonus.StreakBonusFor(0, 25));
        }
    }
}
