#nullable enable
using System.Collections.Generic;
using Showdown.Domain;

namespace Showdown.Game
{
    /// <summary>
    /// The lifetime record in the exact JSON shape src/domain/stats.ts persisted (same keys, same order, category
    /// and outcome ids as strings), so a blob the TypeScript app wrote reads back and vice versa. Reading goes
    /// through <see cref="Stats.MigrateStatistics"/>, which also upgrades the previous game's shapes.
    /// </summary>
    public static class StatisticsCodec
    {
        public static Statistics Decode(object? tree) => Stats.MigrateStatistics(tree);

        public static object Encode(Statistics stats)
        {
            var history = new List<object?>();
            foreach (var entry in stats.History)
            {
                history.Add(EncodeEntry(entry));
            }

            return new Dictionary<string, object?>
            {
                { "handsPlayed", stats.HandsPlayed },
                { "handsWon", stats.HandsWon },
                { "biggestWin", stats.BiggestWin },
                { "reUps", stats.ReUps },
                { "currentStreak", stats.CurrentStreak },
                { "bestStreak", stats.BestStreak },
                { "bestHandCategory", stats.BestHandCategory.HasValue ? HandRanks.CategoryId(stats.BestHandCategory.Value) : null },
                { "categoryCounts", EncodeCategoryCounts(stats.CategoryCounts) },
                { "sweeps", stats.Sweeps },
                { "kickerStrikes", stats.KickerStrikes },
                { "bails", stats.Bails },
                { "pressures", stats.Pressures },
                { "history", history },
            };
        }

        private static Dictionary<string, object?> EncodeCategoryCounts(IReadOnlyDictionary<HandCategory, int> counts)
        {
            var encoded = new Dictionary<string, object?>();
            for (var order = 0; order <= HandRanks.CategoryOrder(HandCategory.RoyalFlush); order += 1)
            {
                var category = (HandCategory)order;
                if (counts.TryGetValue(category, out var made))
                {
                    encoded[HandRanks.CategoryId(category)] = made;
                }
            }

            return encoded;
        }

        private static Dictionary<string, object?> EncodeEntry(HandHistoryEntry entry) =>
            new Dictionary<string, object?>
            {
                { "category", HandRanks.CategoryId(entry.Category) },
                { "outcome", Stats.OutcomeId(entry.Outcome) },
                { "kickerStrike", entry.KickerStrike },
                { "pressured", entry.Pressured },
                { "forges", entry.Forges },
                { "net", entry.Net },
                { "at", entry.At },
            };
    }
}
