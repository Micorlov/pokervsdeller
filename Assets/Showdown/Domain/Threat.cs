#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Showdown.Domain
{
    /// <summary>
    /// How scary a hand looks before it is finished.
    ///
    /// The dealer's hand grows one face-up card at a time, so the table spends most
    /// of the round staring at 1-4 cards that <c>EvaluateHand</c> refuses to rank. This
    /// module scores that partial hand on a 0..1 scale — made components plus
    /// drawing potential — so bots can weigh their moves and the threat meter has
    /// something honest to show. At five cards it defers to the real evaluator.
    /// </summary>
    public static class Threat
    {
        /// <summary>Weight of the strongest made component among the visible cards.</summary>
        private const double MadeQuads = 0.85;
        private const double MadeTrips = 0.6;
        private const double MadeTwoPair = 0.45;
        public const double MadePair = 0.3;

        /// <summary>
        /// A finished hand's base weight by category. The visible climb weights made
        /// pairs, two pair, trips and quads above, and these bases continue that same
        /// scale, so a pair reads the same on the table whether its last card has
        /// landed or not. Each base sits far enough above the one below that the lead
        /// tiebreak (at most LeadWeight) can never reorder two categories.
        /// </summary>
        private static double CategoryBase(HandCategory category)
        {
            switch (category)
            {
                case HandCategory.HighCard: return 0;
                case HandCategory.Pair: return MadePair;
                case HandCategory.TwoPair: return MadeTwoPair;
                case HandCategory.Trips: return MadeTrips;
                case HandCategory.Straight: return 0.7;
                case HandCategory.Flush: return 0.75;
                case HandCategory.FullHouse: return 0.8;
                case HandCategory.Quads: return MadeQuads;
                case HandCategory.StraightFlush: return 0.92;
                default: return 0.97;
            }
        }

        /// <summary>How far the lead rank can lift a finished hand within its category.</summary>
        private const double LeadWeight = 0.04;

        /// <summary>High-card menace: an ace showing is worth this much on its own.</summary>
        private const double HighCardWeight = 0.15;

        /// <summary>Per-card weight of an unbroken flush draw among the visible cards.</summary>
        private const double FlushDrawWeight = 0.06;

        /// <summary>Per-card weight of ranks tight enough to still make a straight.</summary>
        private const double StraightDrawWeight = 0.05;

        /// <summary>Weight of the average rank — a table of paint threatens more than rags.</summary>
        private const double RankDensityWeight = 0.1;

        private static double Clamp01(double value) => Math.Min(1, Math.Max(0, value));

        private static List<int> RankCounts(IReadOnlyList<Card> cards) =>
            cards
                .GroupBy(card => card.Rank)
                .Select(group => group.Count())
                .OrderByDescending(count => count)
                .ToList();

        private static double MadeComponent(IReadOnlyList<Card> cards)
        {
            var counts = RankCounts(cards);
            if (counts.Count == 0) return 0;
            if (counts[0] == 4) return MadeQuads;
            if (counts[0] == 3) return MadeTrips;
            if (counts[0] == 2) return counts.Count > 1 && counts[1] == 2 ? MadeTwoPair : MadePair;
            return 0;
        }

        /// <summary>Draw weight for cards that could still become a flush or straight.</summary>
        private static double DrawComponent(IReadOnlyList<Card> cards)
        {
            if (cards.Count < 2) return 0;

            var allSuited = cards.All(card => card.Suit == cards[0].Suit);
            var flush = allSuited ? FlushDrawWeight * cards.Count : 0;

            var ranks = cards.Select(card => card.Rank).Distinct().OrderByDescending(rank => rank).ToList();
            var allUnique = ranks.Count == cards.Count;
            var tightSpan = allUnique && ranks[0] - ranks[ranks.Count - 1] <= HandRanks.HandSize - 1;
            var straight = tightSpan ? StraightDrawWeight * cards.Count : 0;

            return flush + straight;
        }

        /// <summary>
        /// A finished hand's strength on the same 0..1 scale, monotone with
        /// <c>CompareHands</c> across categories and with the leading tiebreak inside one.
        /// </summary>
        public static double HandStrengthScore(HandRank rank)
        {
            var leadRank = rank.Tiebreak.Count > 0 ? rank.Tiebreak[0] : 0;
            var lead = (double)leadRank / Cards.Ace;
            return Clamp01(CategoryBase(rank.Category) + lead * LeadWeight);
        }

        /// <summary>
        /// Scores a partial hand of 0-5 cards.
        ///
        /// Made pairs and trips carry the score; unfinished flush and straight draws
        /// and general rank density add potential, scaled down as the remaining draws
        /// run out. An empty hand is no threat at all, and a complete hand is scored by
        /// the real evaluator so the meter never disagrees with the showdown.
        /// </summary>
        public static double PartialThreat(IReadOnlyList<Card> cards)
        {
            if (cards.Count == 0) return 0;
            if (cards.Count == HandRanks.HandSize) return HandStrengthScore(HandRanks.EvaluateHand(cards));

            var highCard = ((double)cards.Max(card => card.Rank) / Cards.Ace) * HighCardWeight;
            var averageRank = (double)cards.Sum(card => card.Rank) / cards.Count;
            var density = (averageRank / Cards.Ace) * RankDensityWeight;

            var drawsLeft = (double)(HandRanks.HandSize - cards.Count) / (HandRanks.HandSize - 1);
            var potential = (DrawComponent(cards) + density) * drawsLeft;

            return Clamp01(MadeComponent(cards) + highCard + potential);
        }
    }
}
