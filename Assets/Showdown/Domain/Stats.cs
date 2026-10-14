#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Showdown.Domain
{
    /// <summary>Best-first: a sweep took both battles, a bail took the half-back and left.</summary>
    public enum ShowdownOutcome
    {
        Sweep,
        BeatBoth,
        BeatDealer,
        BeatTable,
        Push,
        Lose,
        Bail,
    }

    public sealed record HandHistoryEntry
    {
        /// <summary>The hand the player held at showdown (or when bailing).</summary>
        public HandCategory Category { get; init; }

        public ShowdownOutcome Outcome { get; init; }

        public bool KickerStrike { get; init; }

        public bool Pressured { get; init; }

        public int Forges { get; init; }

        /// <summary>Chips won or lost over the whole hand, both battles together.</summary>
        public int Net { get; init; }

        /// <summary>Epoch milliseconds when the hand settled.</summary>
        public long At { get; init; }
    }

    public sealed record Statistics
    {
        /// <summary>The record of a player who has not played a hand yet.</summary>
        public static readonly Statistics Default = new Statistics();

        public int HandsPlayed { get; init; }

        public int HandsWon { get; init; }

        public int BiggestWin { get; init; }

        public int ReUps { get; init; }

        /// <summary>Consecutive winning hands right now. Pushes leave it untouched.</summary>
        public int CurrentStreak { get; init; }

        public int BestStreak { get; init; }

        /// <summary>The strongest hand ever made, or null before the first deal settles.</summary>
        public HandCategory? BestHandCategory { get; init; }

        /// <summary>How many times each hand has been made. Absent categories are zero.</summary>
        public IReadOnlyDictionary<HandCategory, int> CategoryCounts { get; init; } = Stats.EmptyCategoryCounts();

        public int Sweeps { get; init; }

        public int KickerStrikes { get; init; }

        public int Bails { get; init; }

        public int Pressures { get; init; }

        /// <summary>Newest first, at most <see cref="Stats.HistoryLimit"/> entries.</summary>
        public IReadOnlyList<HandHistoryEntry> History { get; init; } = Array.Empty<HandHistoryEntry>();
    }

    /// <summary>
    /// The player's lifetime record, held as one immutable value and advanced by a
    /// pure reducer. The store persists it whole; the reducer is the only place a
    /// settled hand touches it, which keeps every derived number consistent.
    /// </summary>
    public static class Stats
    {
        /// <summary>How many settled hands the history keeps, newest first.</summary>
        public const int HistoryLimit = 20;

        private static readonly string[] OutcomeIds =
        {
            "sweep", "beatBoth", "beatDealer", "beatTable", "push", "lose", "bail",
        };

        /// <summary>How the old game's outcomes read in the new one's terms.</summary>
        private static readonly Dictionary<string, ShowdownOutcome> LegacyOutcome =
            new Dictionary<string, ShowdownOutcome>
            {
                { "fold", ShowdownOutcome.Bail },
                { "noQualify", ShowdownOutcome.BeatDealer },
                { "win", ShowdownOutcome.BeatDealer },
                { "lose", ShowdownOutcome.Lose },
                { "push", ShowdownOutcome.Push },
            };

        internal static IReadOnlyDictionary<HandCategory, int> EmptyCategoryCounts() =>
            new ReadOnlyDictionary<HandCategory, int>(new Dictionary<HandCategory, int>());

        /// <summary>The persisted id of an outcome: "sweep", "beatBoth", "bail", ...</summary>
        public static string OutcomeId(ShowdownOutcome outcome) => OutcomeIds[(int)outcome];

        public static bool TryParseOutcomeId(string? id, out ShowdownOutcome outcome)
        {
            var index = id == null ? -1 : Array.IndexOf(OutcomeIds, id);
            outcome = index < 0 ? ShowdownOutcome.Sweep : (ShowdownOutcome)index;
            return index >= 0;
        }

        /// <summary>Reads one settlement as the single line the scoreboard shows.</summary>
        public static ShowdownOutcome OutcomeOf(ShowdownSettlement settlement)
        {
            if (settlement.VsDealer == VsDealer.Bailed) return ShowdownOutcome.Bail;
            if (settlement.Sweep) return ShowdownOutcome.Sweep;
            if (settlement.VsDealer == VsDealer.Win)
            {
                return settlement.WonTable ? ShowdownOutcome.BeatBoth : ShowdownOutcome.BeatDealer;
            }

            if (settlement.WonTable) return ShowdownOutcome.BeatTable;
            return settlement.VsDealer == VsDealer.Push ? ShowdownOutcome.Push : ShowdownOutcome.Lose;
        }

        private static HandCategory StrongerCategory(HandCategory? a, HandCategory b) =>
            a.HasValue && HandRanks.CategoryOrder(a.Value) >= HandRanks.CategoryOrder(b) ? a.Value : b;

        /// <summary>
        /// Folds one settled hand into the record.
        ///
        /// A win extends the streak, a loss ends it, and a push leaves it be — the
        /// convention every card room scoreboard uses. The hand always counts toward
        /// the made-hands table, bailed or not: the player still held it.
        /// </summary>
        public static Statistics RecordHandResult(Statistics prev, HandHistoryEntry entry)
        {
            var isWin = entry.Net > 0;
            var isLoss = entry.Net < 0;
            var currentStreak = isWin ? prev.CurrentStreak + 1 : isLoss ? 0 : prev.CurrentStreak;

            var categoryCounts = new Dictionary<HandCategory, int>();
            foreach (var pair in prev.CategoryCounts)
            {
                categoryCounts[pair.Key] = pair.Value;
            }

            categoryCounts[entry.Category] =
                (prev.CategoryCounts.TryGetValue(entry.Category, out var made) ? made : 0) + 1;

            var history = new[] { entry }.Concat(prev.History).Take(HistoryLimit).ToArray();

            return prev with
            {
                HandsPlayed = prev.HandsPlayed + 1,
                HandsWon = prev.HandsWon + (isWin ? 1 : 0),
                BiggestWin = Math.Max(prev.BiggestWin, entry.Net),
                CurrentStreak = currentStreak,
                BestStreak = Math.Max(prev.BestStreak, currentStreak),
                BestHandCategory = StrongerCategory(prev.BestHandCategory, entry.Category),
                CategoryCounts = new ReadOnlyDictionary<HandCategory, int>(categoryCounts),
                Sweeps = prev.Sweeps + (entry.Outcome == ShowdownOutcome.Sweep ? 1 : 0),
                KickerStrikes = prev.KickerStrikes + (entry.KickerStrike ? 1 : 0),
                Bails = prev.Bails + (entry.Outcome == ShowdownOutcome.Bail ? 1 : 0),
                Pressures = prev.Pressures + (entry.Pressured ? 1 : 0),
                History = history,
            };
        }

        /// <summary>Whole-percent win rate, or null before any hand has been played.</summary>
        public static int? WinRatePercent(Statistics stats)
        {
            if (stats.HandsPlayed == 0)
            {
                return null;
            }

            // Math.round semantics: halves round up, not to even.
            var percent = (double)stats.HandsWon / stats.HandsPlayed * 100;
            var floor = Math.Floor(percent);
            return (int)(percent - floor >= 0.5 ? floor + 1 : floor);
        }

        // ---- Migration -------------------------------------------------------------
        //
        // The loaded value is whatever a JSON parser produced: dictionaries keyed by
        // string, sequences, strings, booleans and numbers (any CLR numeric type).
        // No parser lives in this assembly; the serialization layer hands the tree in.

        private static bool TryGetField(object? raw, string key, out object? value)
        {
            value = null;
            switch (raw)
            {
                case IDictionary dictionary when dictionary.Contains(key):
                    value = dictionary[key];
                    return true;
                case IReadOnlyDictionary<string, object?> readOnly when readOnly.ContainsKey(key):
                    value = readOnly[key];
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsObject(object? raw) => raw is IDictionary || raw is IReadOnlyDictionary<string, object?>;

        private static bool IsArray(object? raw) => raw is IEnumerable && !(raw is string) && !IsObject(raw);

        private static double? AsNumber(object? raw)
        {
            switch (raw)
            {
                case sbyte _:
                case byte _:
                case short _:
                case ushort _:
                case int _:
                case uint _:
                case long _:
                case ulong _:
                case float _:
                case double _:
                case decimal _:
                    return Convert.ToDouble(raw, System.Globalization.CultureInfo.InvariantCulture);
                default:
                    return null;
            }
        }

        private static double? FieldNumber(object? raw, string key) =>
            TryGetField(raw, key, out var value) ? AsNumber(value) : null;

        private static HandHistoryEntry? MigrateEntry(object? raw)
        {
            if (!IsObject(raw)) return null;

            TryGetField(raw, "category", out var categoryRaw);
            if (!(categoryRaw is string categoryId) || !HandRanks.TryParseCategoryId(categoryId, out var category)) return null;

            var net = FieldNumber(raw, "net");
            var at = FieldNumber(raw, "at");
            if (net == null || at == null) return null;

            TryGetField(raw, "outcome", out var outcomeRaw);
            if (!(outcomeRaw is string outcomeId)) return null;
            if (!TryParseOutcomeId(outcomeId, out var outcome) && !LegacyOutcome.TryGetValue(outcomeId, out outcome))
            {
                return null;
            }

            TryGetField(raw, "kickerStrike", out var kickerStrike);
            TryGetField(raw, "pressured", out var pressured);

            return new HandHistoryEntry
            {
                Category = category,
                Outcome = outcome,
                KickerStrike = kickerStrike is bool strike && strike,
                Pressured = pressured is bool pressed && pressed,
                Forges = Count(raw, "forges", 0),
                Net = (int)net.Value,
                At = (long)at.Value,
            };
        }

        /// <summary>
        /// A stored counter: only a whole number in 0..int.MaxValue is believed. Anything else (negative, fractional,
        /// 1e20 from a corrupt or hand-edited save) falls back, rather than wrapping into a nonsense total.
        /// </summary>
        private static int Count(object? source, string name, int fallback)
        {
            double? value = FieldNumber(source, name);
            if (value is null) return fallback;
            double number = value.Value;
            return number >= 0 && number <= int.MaxValue && Math.Floor(number) == number ? (int)number : fallback;
        }

        private static IReadOnlyList<HandHistoryEntry> MigrateHistory(object? raw)
        {
            if (!TryGetField(raw, "history", out var history) || !IsArray(history))
            {
                return Array.Empty<HandHistoryEntry>();
            }

            return ((IEnumerable)history!)
                .Cast<object?>()
                .Select(MigrateEntry)
                .Where(entry => entry != null)
                .Select(entry => entry!)
                .Take(HistoryLimit)
                .ToArray();
        }

        private static IReadOnlyDictionary<HandCategory, int> MigrateCategoryCounts(object? raw)
        {
            var counts = new Dictionary<HandCategory, int>();
            if (!TryGetField(raw, "categoryCounts", out var source) || !IsObject(source))
            {
                return EmptyCategoryCounts();
            }

            for (var index = 0; index < HandRanks.CategoryOrder(HandCategory.RoyalFlush) + 1; index += 1)
            {
                var category = (HandCategory)index;
                var made = FieldNumber(source, HandRanks.CategoryId(category));
                if (made != null)
                {
                    counts[category] = (int)made.Value;
                }
            }

            return new ReadOnlyDictionary<HandCategory, int>(counts);
        }

        /// <summary>
        /// Brings whatever the store holds up to the current shape.
        ///
        /// The lifetime aggregates carry straight over — the player keeps their
        /// record from the old game. History entries written by the old game are
        /// translated (its fold is a bail, its wins are dealer-beats), anything
        /// unreadable is dropped, and already-current data passes through untouched.
        /// Missing or wrongly typed aggregate fields fall back to their defaults.
        /// </summary>
        public static Statistics MigrateStatistics(object? loaded)
        {
            if (loaded is Statistics current)
            {
                return current with { History = current.History.Take(HistoryLimit).ToArray() };
            }

            if (!IsObject(loaded)) return Statistics.Default;

            var fallback = Statistics.Default;
            TryGetField(loaded, "bestHandCategory", out var bestRaw);
            var best = bestRaw is string bestId && HandRanks.TryParseCategoryId(bestId, out var parsed)
                ? parsed
                : fallback.BestHandCategory;

            return new Statistics
            {
                HandsPlayed = Count(loaded, "handsPlayed", fallback.HandsPlayed),
                HandsWon = Count(loaded, "handsWon", fallback.HandsWon),
                BiggestWin = Count(loaded, "biggestWin", fallback.BiggestWin),
                ReUps = Count(loaded, "reUps", fallback.ReUps),
                CurrentStreak = Count(loaded, "currentStreak", fallback.CurrentStreak),
                BestStreak = Count(loaded, "bestStreak", fallback.BestStreak),
                BestHandCategory = best,
                CategoryCounts = MigrateCategoryCounts(loaded),
                Sweeps = Count(loaded, "sweeps", fallback.Sweeps),
                KickerStrikes = Count(loaded, "kickerStrikes", fallback.KickerStrikes),
                Bails = Count(loaded, "bails", fallback.Bails),
                Pressures = Count(loaded, "pressures", fallback.Pressures),
                History = MigrateHistory(loaded),
            };
        }
    }
}
