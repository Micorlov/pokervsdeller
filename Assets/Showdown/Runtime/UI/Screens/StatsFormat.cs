using System.Collections.Generic;
using Showdown.Domain;
using UnityEngine;

namespace Showdown.UI
{
    /// <summary>The words and colours of the record: how a settled hand reads in the history list.</summary>
    internal static class StatsFormat
    {
        /// <summary>Strongest first — the same reading order as the paytable.</summary>
        public static readonly IReadOnlyList<HandCategory> CategoriesStrongestFirst = new[]
        {
            HandCategory.RoyalFlush,
            HandCategory.StraightFlush,
            HandCategory.Quads,
            HandCategory.FullHouse,
            HandCategory.Flush,
            HandCategory.Straight,
            HandCategory.Trips,
            HandCategory.TwoPair,
            HandCategory.Pair,
            HandCategory.HighCard,
        };

        public static string CategoryLabel(HandCategory category)
        {
            return HandRanks.HandName(new HandRank { Category = category });
        }

        public static string Signed(int net)
        {
            return net > 0 ? "+" + net : net.ToString();
        }

        public static Color NetColor(int net)
        {
            if (net > 0) return Theme.Colors.Gold;
            return net < 0 ? Theme.Colors.Red : Theme.Colors.TextMuted;
        }

        public static string OutcomeLine(HandHistoryEntry entry)
        {
            string strike = entry.KickerStrike ? " · Strike ×2" : "";
            string hand = CategoryLabel(entry.Category);
            switch (entry.Outcome)
            {
                case ShowdownOutcome.Bail: return "Bailed";
                case ShowdownOutcome.Push: return "Push";
                case ShowdownOutcome.Sweep: return hand + " · Sweep!" + strike;
                case ShowdownOutcome.BeatBoth: return hand + " · Beat both" + strike;
                case ShowdownOutcome.BeatDealer: return hand + " · Beat the house" + strike;
                case ShowdownOutcome.BeatTable: return hand + " · Took the table";
                default: return hand + " · Lost";
            }
        }
    }
}
