#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Showdown.Domain
{
    /// <summary>Strength order: the numeric value of each member is its rank in the ladder.</summary>
    public enum HandCategory
    {
        HighCard = 0,
        Pair = 1,
        TwoPair = 2,
        Trips = 3,
        Straight = 4,
        Flush = 5,
        FullHouse = 6,
        Quads = 7,
        StraightFlush = 8,
        RoyalFlush = 9,
    }

    public sealed record HandRank
    {
        public HandCategory Category { get; init; }

        /// <summary>Descending significance — compared element by element.</summary>
        public IReadOnlyList<int> Tiebreak { get; init; } = Array.Empty<int>();
    }

    /// <summary>
    /// Five-card hand evaluation.
    ///
    /// A rank is a category plus a tiebreak list read left to right, so two hands
    /// of the same category are compared exactly the way a dealer would read them
    /// out: the trips rank first, then the kickers in descending order.
    /// (Named HandRanks because the file's main data type already owns HandRank.)
    /// </summary>
    public static class HandRanks
    {
        public const int HandSize = 5;

        /// <summary>The wheel A-2-3-4-5 plays as a five-high straight, the lowest one there is.</summary>
        private const int WheelHigh = 5;

        private static readonly string[] CategoryIds =
        {
            "highCard", "pair", "twoPair", "trips", "straight",
            "flush", "fullHouse", "quads", "straightFlush", "royalFlush",
        };

        private static readonly string[] CategoryNames =
        {
            "High Card", "Pair", "Two Pair", "Three of a Kind", "Straight",
            "Flush", "Full House", "Four of a Kind", "Straight Flush", "Royal Flush",
        };

        /// <summary>Strength order. Only the relative values matter.</summary>
        public static int CategoryOrder(HandCategory category) => (int)category;

        /// <summary>The persisted id of a category: "highCard", "fullHouse", "royalFlush", ...</summary>
        public static string CategoryId(HandCategory category) => CategoryIds[(int)category];

        public static bool TryParseCategoryId(string? id, out HandCategory category)
        {
            var index = id == null ? -1 : Array.IndexOf(CategoryIds, id);
            category = index < 0 ? HandCategory.HighCard : (HandCategory)index;
            return index >= 0;
        }

        private readonly struct RankGroup
        {
            public RankGroup(int rank, int count)
            {
                Rank = rank;
                Count = count;
            }

            public int Rank { get; }

            public int Count { get; }
        }

        /// <summary>Groups by rank, ordered by count first and rank second — the reading order.</summary>
        private static List<RankGroup> GroupByRank(IReadOnlyList<Card> cards) =>
            cards
                .GroupBy(card => card.Rank)
                .Select(group => new RankGroup(group.Key, group.Count()))
                .OrderByDescending(group => group.Count)
                .ThenByDescending(group => group.Rank)
                .ToList();

        /// <summary>Returns the straight's high card, or null. Handles the wheel.</summary>
        private static int? StraightHigh(IEnumerable<int> ranks)
        {
            var unique = ranks.Distinct().OrderByDescending(rank => rank).ToList();
            if (unique.Count != HandSize)
            {
                return null;
            }

            if (unique[0] - unique[4] == 4)
            {
                return unique[0];
            }

            var isWheel = unique[0] == Cards.Ace && unique[1] == 5 && unique[4] == 2;
            return isWheel ? WheelHigh : (int?)null;
        }

        private static HandRank Rank(HandCategory category, IEnumerable<int> tiebreak) =>
            new HandRank { Category = category, Tiebreak = tiebreak.ToArray() };

        /// <summary>
        /// Reads a five-card hand. Throws on any other length: a mis-sized hand is a
        /// bug in the dealer, not a weak hand, and must not be silently ranked.
        /// </summary>
        public static HandRank EvaluateHand(IReadOnlyList<Card> cards)
        {
            if (cards.Count != HandSize)
            {
                throw new ArgumentException($"evaluateHand expects {HandSize} cards, received {cards.Count}");
            }

            var groups = GroupByRank(cards);
            var isFlush = cards.All(card => card.Suit == cards[0].Suit);
            var high = StraightHigh(cards.Select(card => card.Rank));

            if (isFlush && high.HasValue)
            {
                return high.Value == Cards.Ace
                    ? Rank(HandCategory.RoyalFlush, new[] { Cards.Ace })
                    : Rank(HandCategory.StraightFlush, new[] { high.Value });
            }

            var shape = string.Concat(groups.Select(group => group.Count.ToString()));
            var ranks = groups.Select(group => group.Rank).ToList();

            if (shape == "41") return Rank(HandCategory.Quads, ranks);
            if (shape == "32") return Rank(HandCategory.FullHouse, ranks);
            if (isFlush)
            {
                return Rank(HandCategory.Flush, cards.Select(card => card.Rank).OrderByDescending(rank => rank));
            }

            if (high.HasValue) return Rank(HandCategory.Straight, new[] { high.Value });
            if (shape == "311") return Rank(HandCategory.Trips, ranks);
            if (shape == "221") return Rank(HandCategory.TwoPair, ranks);
            if (shape == "2111") return Rank(HandCategory.Pair, ranks);
            return Rank(HandCategory.HighCard, ranks);
        }

        /// <summary>Positive when <paramref name="a"/> beats <paramref name="b"/>, negative when it loses, zero on a true tie.</summary>
        public static int CompareHands(HandRank a, HandRank b)
        {
            var byCategory = CategoryOrder(a.Category) - CategoryOrder(b.Category);
            if (byCategory != 0)
            {
                return byCategory;
            }

            var length = Math.Max(a.Tiebreak.Count, b.Tiebreak.Count);
            for (var i = 0; i < length; i += 1)
            {
                var left = i < a.Tiebreak.Count ? a.Tiebreak[i] : 0;
                var right = i < b.Tiebreak.Count ? b.Tiebreak[i] : 0;
                var diff = left - right;
                if (diff != 0)
                {
                    return diff;
                }
            }

            return 0;
        }

        public static string HandName(HandRank rank) => CategoryNames[(int)rank.Category];
    }
}
