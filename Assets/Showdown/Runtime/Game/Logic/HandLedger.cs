#nullable enable
using System.Collections.Generic;
using Showdown.Domain;

namespace Showdown.Game
{
    /// <summary>Everything a settled hand changes in the player's lifetime record.</summary>
    public sealed record HandTally(
        HandHistoryEntry Entry,
        Statistics NextStats,
        IReadOnlyList<Achievement> Unlocked,
        StreakPayout? Bonus);

    /// <summary>The bookkeeping done when a hand settles, as a pure function so it can be tested without a clock.</summary>
    public static class HandLedger
    {
        /// <summary>
        /// Folds the human's settled hand into <paramref name="before"/>. Null when the human seat has no
        /// settlement (the hand was not settled).
        /// </summary>
        public static HandTally? Tally(Statistics before, ShowdownRoundState settled, long atEpochMs)
        {
            var seat = ShowdownRound.HumanSeat(settled);
            var settlement = seat.Settlement;
            if (settlement == null) return null;

            var entry = new HandHistoryEntry
            {
                Category = HandRanks.EvaluateHand(seat.Cards).Category,
                Outcome = Stats.OutcomeOf(settlement),
                KickerStrike = settlement.KickerStrike,
                Pressured = seat.Pressured,
                Forges = seat.ForgesUsed,
                Net = settlement.Net,
                At = atEpochMs,
            };

            var next = Stats.RecordHandResult(before, entry);
            return new HandTally(
                entry,
                next,
                Achievements.NewlyUnlockedAchievements(before, next),
                StreakPayoutFor(entry, next, settled.Ante));
        }

        /// <summary>
        /// Exact-match, not a threshold: the current streak only ever changes by +1 on a win, so gating on a
        /// winning hand means this streak value is freshly reached — a later push holding the same count is
        /// excluded by the same guard, so this can't double-pay.
        /// </summary>
        private static StreakPayout? StreakPayoutFor(HandHistoryEntry entry, Statistics next, int ante)
        {
            if (entry.Net <= 0) return null;
            var amount = StreakBonus.StreakBonusFor(next.CurrentStreak, ante);
            return amount > 0 ? new StreakPayout(next.CurrentStreak, amount) : null;
        }
    }
}
