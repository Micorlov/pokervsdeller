#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Showdown.Domain
{
    /// <summary>
    /// The win-streak bonus: extra chips, on the house, when a streak lands
    /// exactly on 3, 5, 7 or 10 wins. Ante-scaled the same way the sweep bonus is
    /// (<c>SweepBonusUnits * ante</c> in ShowdownPayouts), and capped at 10 — the
    /// same ceiling the <c>unstoppable</c> achievement uses — so a streak can't be
    /// farmed for ever-larger payouts once it runs past the last rung.
    /// </summary>
    public static class StreakBonus
    {
        public static readonly IReadOnlyDictionary<int, int> StreakBonusUnitsTable =
            new ReadOnlyDictionary<int, int>(new Dictionary<int, int>
            {
                { 3, 2 },
                { 5, 4 },
                { 7, 6 },
                { 10, 10 },
            });

        /// <summary>Bonus units for a streak count, or zero off the ladder.</summary>
        public static int StreakBonusUnits(int streak) =>
            StreakBonusUnitsTable.TryGetValue(streak, out var units) ? units : 0;

        /// <summary>The chip amount a streak count pays at a given ante, or zero off the ladder.</summary>
        public static int StreakBonusFor(int streak, int ante) => StreakBonusUnits(streak) * ante;
    }
}
