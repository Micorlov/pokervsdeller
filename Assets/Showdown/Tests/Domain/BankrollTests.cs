#nullable enable
using NUnit.Framework;
using Showdown.Domain;

namespace Showdown.Tests.Domain
{
    [TestFixture]
    public class CanPlayTests
    {
        [Test]
        public void NeedsExactlyFiveAntesAndTurnsOverAtThatBoundary()
        {
            foreach (var ante in Bankroll.AnteOptions)
            {
                var floor = ante * Bankroll.MinBankrollMultiple;
                Assert.AreEqual(floor, Bankroll.MinimumToPlay(ante));
                Assert.IsTrue(Bankroll.CanPlay(floor, ante));
                Assert.IsFalse(Bankroll.CanPlay(floor - 1, ante));
                Assert.IsTrue(Bankroll.IsBust(floor - 1, ante));
            }
        }

        [Test]
        public void SeatsAFreshPlayerAtEveryAnte()
        {
            foreach (var ante in Bankroll.AnteOptions)
            {
                Assert.IsTrue(Bankroll.CanPlay(Bankroll.StartingBankroll, ante));
            }
        }
    }

    [TestFixture]
    public class ReUpTests
    {
        [Test]
        public void DoesNothingWhileThePlayerCanStillCoverAHand()
        {
            Assert.AreEqual(60, Bankroll.ReUp(60, 10));
            Assert.AreEqual(Bankroll.StartingBankroll, Bankroll.ReUp(Bankroll.StartingBankroll, 25));
        }

        [Test]
        public void TopsTheStackUpOnlyAtBustKeepingWhateverWasLeft()
        {
            Assert.AreEqual(49 + Bankroll.ReUpAmount, Bankroll.ReUp(49, 10));
            Assert.AreEqual(Bankroll.ReUpAmount, Bankroll.ReUp(0, 5));
        }
    }

    [TestFixture]
    public class LargestAffordableAnteTests
    {
        [Test]
        public void PicksTheBiggestStakeTheStackCanStillSitDownFor()
        {
            Assert.AreEqual(25, Bankroll.LargestAffordableAnte(130));
            Assert.AreEqual(10, Bankroll.LargestAffordableAnte(124));
            Assert.AreEqual(5, Bankroll.LargestAffordableAnte(49));
            Assert.AreEqual(5, Bankroll.LargestAffordableAnte(30));
        }

        [Test]
        public void IsNullWhenEvenTheSmallestAnteIsOutOfReach()
        {
            Assert.IsNull(Bankroll.LargestAffordableAnte(24));
        }
    }
}
