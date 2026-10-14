#nullable enable
using System;
using System.Globalization;

namespace Showdown.Domain
{
    /// <summary>
    /// The daily bonus: a free stack of chips once per calendar day, the retention
    /// staple of every casino app. Eligibility compares local calendar days rather
    /// than 24-hour windows, so "come back tomorrow" means tomorrow morning, not
    /// the same hour tomorrow.
    /// </summary>
    public static class DailyBonus
    {
        public const int DailyBonusAmount = 250;

        /// <summary>
        /// A local calendar day as <c>YYYY-MM-DD</c> — the unit the bonus resets on.
        /// The date's own year/month/day are used as given; pass a local time.
        /// </summary>
        public static string DayKeyOf(DateTime date) =>
            date.Year.ToString(CultureInfo.InvariantCulture)
            + "-" + date.Month.ToString("00", CultureInfo.InvariantCulture)
            + "-" + date.Day.ToString("00", CultureInfo.InvariantCulture);

        /// <summary>Claimable when never claimed, or last claimed on an earlier calendar day.</summary>
        public static bool CanClaimDailyBonus(string? lastClaimDay, DateTime now) =>
            lastClaimDay == null || lastClaimDay != DayKeyOf(now);
    }
}
