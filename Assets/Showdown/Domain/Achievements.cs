#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Showdown.Domain
{
    /// <summary>The persisted badge ids. Byte-identical to the original game's.</summary>
    public static class AchievementIds
    {
        public const string FirstHand = "firstHand";
        public const string FirstWin = "firstWin";
        public const string Sweep = "sweep";
        public const string SweepMaster = "sweepMaster";
        public const string HighRoller = "highRoller";
        public const string HotHand = "hotHand";
        public const string Unstoppable = "unstoppable";
        public const string FullHouseOrBetter = "fullHouseOrBetter";
        public const string Royalty = "royalty";
        public const string KickerKing = "kickerKing";
        public const string IronNerve = "ironNerve";
        public const string Regular = "regular";
    }

    public sealed record Achievement
    {
        /// <summary>One of <see cref="AchievementIds"/>.</summary>
        public string Id { get; init; } = "";

        public string Title { get; init; } = "";

        public string Description { get; init; } = "";

        public Func<Statistics, bool> IsUnlocked { get; init; } = _ => false;
    }

    /// <summary>
    /// Badges are read straight off the lifetime record — no separate storage, no
    /// separate reducer. A badge is unlocked exactly when its check passes against
    /// the current <see cref="Statistics"/>, so the book can never drift out of sync with the
    /// numbers it's celebrating.
    /// </summary>
    public static class Achievements
    {
        private static Achievement Badge(string id, string title, string description, Func<Statistics, bool> isUnlocked) =>
            new Achievement { Id = id, Title = title, Description = description, IsUnlocked = isUnlocked };

        /// <summary>Every badge the book can award, in the order the Achievements panel shows them.</summary>
        public static readonly IReadOnlyList<Achievement> All = Array.AsReadOnly(new[]
        {
            Badge(AchievementIds.FirstHand, "First Deal", "Play your first hand.", stats => stats.HandsPlayed >= 1),
            Badge(AchievementIds.FirstWin, "On the Board", "Win your first hand.", stats => stats.HandsWon >= 1),
            Badge(
                AchievementIds.Sweep,
                "Clean Sweep",
                "Beat the dealer and the table in the same hand.",
                stats => stats.Sweeps >= 1),
            Badge(AchievementIds.SweepMaster, "Sweep Master", "Sweep the table 10 times.", stats => stats.Sweeps >= 10),
            Badge(
                AchievementIds.HighRoller,
                "High Roller",
                "Win 500 chips in a single hand.",
                stats => stats.BiggestWin >= 500),
            Badge(AchievementIds.HotHand, "Hot Hand", "Win 5 hands in a row.", stats => stats.BestStreak >= 5),
            Badge(AchievementIds.Unstoppable, "Unstoppable", "Win 10 hands in a row.", stats => stats.BestStreak >= 10),
            Badge(
                AchievementIds.FullHouseOrBetter,
                "Full Boat",
                "Make a full house or better.",
                stats => stats.BestHandCategory.HasValue
                    && HandRanks.CategoryOrder(stats.BestHandCategory.Value)
                        >= HandRanks.CategoryOrder(HandCategory.FullHouse)),
            Badge(
                AchievementIds.Royalty,
                "Royalty",
                "Make a royal flush.",
                stats => stats.BestHandCategory == HandCategory.RoyalFlush),
            Badge(AchievementIds.KickerKing, "Kicker King", "Land 5 kicker strikes.", stats => stats.KickerStrikes >= 5),
            Badge(AchievementIds.IronNerve, "Iron Nerve", "Press your bet 10 times.", stats => stats.Pressures >= 10),
            Badge(AchievementIds.Regular, "Regular", "Play 100 hands.", stats => stats.HandsPlayed >= 100),
        });

        /// <summary>Every badge the current record has earned, in book order.</summary>
        public static IReadOnlyList<Achievement> UnlockedAchievements(Statistics stats) =>
            All.Where(achievement => achievement.IsUnlocked(stats)).ToArray();

        /// <summary>Badges <paramref name="next"/> earns that <paramref name="prev"/> had not — the moment a settled hand should celebrate.</summary>
        public static IReadOnlyList<Achievement> NewlyUnlockedAchievements(Statistics prev, Statistics next) =>
            All.Where(achievement => !achievement.IsUnlocked(prev) && achievement.IsUnlocked(next)).ToArray();
    }
}
