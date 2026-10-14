#nullable enable
using System;
using NUnit.Framework;
using Showdown.Domain;

namespace Showdown.Tests.Domain
{
    [TestFixture]
    public class DayKeyOfTests
    {
        [Test]
        public void FormatsALocalCalendarDayWithZeroPadding()
        {
            Assert.AreEqual("2026-01-05", DailyBonus.DayKeyOf(new DateTime(2026, 1, 5, 23, 59, 0)));
        }
    }

    [TestFixture]
    public class CanClaimDailyBonusTests
    {
        // The TypeScript tests count months from zero (7 = August); these are the same dates, month numbers from one.
        [Test]
        public void ClaimableWhenNeverClaimed()
        {
            Assert.IsTrue(DailyBonus.CanClaimDailyBonus(null, new DateTime(2026, 8, 29)));
        }

        [Test]
        public void NotClaimableTwiceOnTheSameDay()
        {
            var morning = new DateTime(2026, 8, 29, 8, 0, 0);
            var evening = new DateTime(2026, 8, 29, 22, 0, 0);

            Assert.IsFalse(DailyBonus.CanClaimDailyBonus(DailyBonus.DayKeyOf(morning), evening));
        }

        [Test]
        public void ClaimableAgainTheNextCalendarDayEvenMinutesLater()
        {
            var lateNight = new DateTime(2026, 8, 29, 23, 55, 0);
            var justPastMidnight = new DateTime(2026, 8, 30, 0, 5, 0);

            Assert.IsTrue(DailyBonus.CanClaimDailyBonus(DailyBonus.DayKeyOf(lateNight), justPastMidnight));
        }
    }
}
