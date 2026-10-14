#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Showdown.Domain
{
    public enum BotAvatarKind
    {
        Initial,
        Emoji,
    }

    public sealed record BotAvatar
    {
        public BotAvatarKind Kind { get; init; }

        public string Value { get; init; } = "";

        /// <summary>Optional two-colour gradient (start, end). Null when the avatar has none.</summary>
        public IReadOnlyList<string>? Gradient { get; init; }
    }

    public sealed record BotPersona
    {
        public string Id { get; init; } = "";

        public string Name { get; init; } = "";

        public BotAvatar Avatar { get; init; } = new BotAvatar();

        /// <summary>0 = calls anything, 1 = folds all but the certainties.</summary>
        public double Tightness { get; init; }

        /// <summary>The one-line read shown under the seat on the rules screen.</summary>
        public string Style { get; init; } = "";
    }

    /// <summary>
    /// The part of a seat a bot looks at when it decides. Both the lightweight
    /// <see cref="ShowdownBotSeat"/> and a full table seat satisfy it.
    /// </summary>
    public interface IShowdownBotSeat
    {
        IReadOnlyList<Card> Cards { get; }

        int? KickerIndex { get; }

        int ForgesUsed { get; }

        bool Pressured { get; }

        int Stack { get; }
    }

    public sealed record ShowdownBotSeat : IShowdownBotSeat
    {
        public IReadOnlyList<Card> Cards { get; init; } = Array.Empty<Card>();

        public int? KickerIndex { get; init; }

        public int ForgesUsed { get; init; }

        public bool Pressured { get; init; }

        public int Stack { get; init; }
    }

    /// <summary>
    /// The two regulars at the table.
    ///
    /// Each one weighs its hand against the dealer's visible climb and then
    /// deviates by <c>Tightness</c>: a rock bails early and pressures only certainties,
    /// a gambler rides bad hands and doubles light. The deviation is what makes
    /// the seats readable as people rather than copies of the same solver.
    /// </summary>
    public static class Bots
    {
        /// <summary>
        /// Two regulars, spread tight to loose. The table seats one of each, so the
        /// spread matters more than the headcount.
        /// </summary>
        public static readonly IReadOnlyList<BotPersona> BotPersonas = Array.AsReadOnly(new[]
        {
            new BotPersona
            {
                Id = "duke",
                Name = "Duke",
                Avatar = new BotAvatar { Kind = BotAvatarKind.Emoji, Value = "\U0001F3A9" },
                Tightness = 0.88,
                Style = "Waits all night for a pair.",
            },
            new BotPersona
            {
                Id = "gus",
                Name = "Gus",
                Avatar = new BotAvatar { Kind = BotAvatarKind.Emoji, Value = "\U0001F3B2" },
                Tightness = 0.12,
                Style = "Here for the action.",
            },
        });

        /// <summary>How far a persona's tightness drifts hand to hand, so nobody is a metronome.</summary>
        public const double TightnessJitter = 0.18;

        /// <summary>How far behind a seat lets itself fall before bailing, at tightness 0.</summary>
        private const double BailMarginBase = 0.32;

        /// <summary>Tight players bail that much earlier when behind.</summary>
        private const double BailMarginTightness = 0.22;

        /// <summary>Bail probability once behind: base plus tightness.</summary>
        private const double BailRollBase = 0.35;
        private const double BailRollTightness = 0.5;

        /// <summary>How often a drawy hand trades a card in: base plus looseness.</summary>
        private const double ForgeRollBase = 0.2;
        private const double ForgeRollLooseness = 0.45;

        /// <summary>Margin needed to double the dealer stake, at tightness 0.</summary>
        private const double PressureMarginBase = 0.05;

        /// <summary>Tight players want that much more edge before pressuring.</summary>
        private const double PressureMarginTightness = 0.25;

        /// <summary>A low card at or under this is worth trading away from an unmade hand.</summary>
        private const int DeadCardRank = 6;

        private static readonly ShowdownMove Hold = new ShowdownMove { Kind = ShowdownMoveKind.Hold, Pressure = false };

        private static double Clamp01(double value) => Math.Min(1, Math.Max(0, value));

        private static Dictionary<int, int> CountsByRank(IReadOnlyList<Card> cards)
        {
            var counts = new Dictionary<int, int>();
            foreach (var card in cards)
            {
                counts[card.Rank] = (counts.TryGetValue(card.Rank, out var count) ? count : 0) + 1;
            }

            return counts;
        }

        private static Dictionary<Suit, int> CountsBySuit(IReadOnlyList<Card> cards)
        {
            var counts = new Dictionary<Suit, int>();
            foreach (var card in cards)
            {
                counts[card.Suit] = (counts.TryGetValue(card.Suit, out var count) ? count : 0) + 1;
            }

            return counts;
        }

        /// <summary>
        /// Which card a bot marks as its kicker. Deterministic, zero rng draws: the
        /// highest card outside the made core, because the pairs and trips are doing
        /// their own work and the spare card is the one free bet on the dealer's
        /// final rank. A pat hand just nominates its highest card.
        /// </summary>
        public static int ChooseBotKicker(IReadOnlyList<Card> cards)
        {
            var counts = CountsByRank(cards);
            var spare = Enumerable.Range(0, cards.Count).Where(index => counts[cards[index].Rank] == 1).ToList();
            var pool = spare.Count > 0 ? spare : Enumerable.Range(0, cards.Count).ToList();

            // First of the equally high cards wins, as in the original reduce.
            var best = pool[0];
            foreach (var index in pool)
            {
                if (cards[index].Rank > cards[best].Rank)
                {
                    best = index;
                }
            }

            return best;
        }

        /// <summary>
        /// Which card leaves the hand on a forge. Deterministic, zero rng draws: the
        /// least useful card that is not the kicker — unpaired before paired, off the
        /// dominant suit before on it, low before high.
        /// </summary>
        public static int ChooseForgeDiscard(IReadOnlyList<Card> cards, int kickerIndex)
        {
            var counts = CountsByRank(cards);
            var suitCounts = CountsBySuit(cards);
            var dominantCount = suitCounts.Values.Max();

            int KeepScore(Card card) =>
                card.Rank
                + (counts[card.Rank] > 1 ? 100 : 0)
                + (dominantCount >= 3 && suitCounts[card.Suit] == dominantCount ? 50 : 0);

            var candidates = Enumerable.Range(0, cards.Count).Where(index => index != kickerIndex).ToList();

            // First of the equally useless cards wins, as in the original reduce.
            var worst = candidates[0];
            foreach (var index in candidates)
            {
                if (KeepScore(cards[index]) < KeepScore(cards[worst]))
                {
                    worst = index;
                }
            }

            return worst;
        }

        /// <summary>A hand worth trading into: unmade, with a live draw or a dead low card.</summary>
        private static bool IsDrawyHand(IReadOnlyList<Card> cards)
        {
            var category = HandRanks.EvaluateHand(cards).Category;
            if (HandRanks.CategoryOrder(category) >= HandRanks.CategoryOrder(HandCategory.TwoPair)) return false;

            if (CountsBySuit(cards).Values.Max() >= 4) return true;

            var unique = cards.Select(card => card.Rank).Distinct().OrderByDescending(rank => rank).ToList();
            for (var i = 0; i + 3 < unique.Count; i += 1)
            {
                if (unique[i] - unique[i + 3] <= 4) return true;
            }

            return cards.Min(card => card.Rank) <= DeadCardRank;
        }

        /// <summary>
        /// A seat's move for one action round of the climb.
        ///
        /// Draws exactly twice from <paramref name="rng"/> — tightness drift, then the action
        /// roll — on every branch, so a hand replays identically from its seed. The margin
        /// between the seat's made strength and the dealer's visible threat drives
        /// everything: far enough behind an unmade hand bails, a drawy hand trades a
        /// card, and in the pressure round a comfortable lead doubles the stake.
        /// Made hands of two pair or better never bail — there is no version of this
        /// game where that is right either.
        /// </summary>
        public static ShowdownMove DecideShowdownMove(
            IShowdownBotSeat seat,
            IReadOnlyList<Card> dealerCards,
            int round,
            int ante,
            BotPersona persona,
            Func<double> rng)
        {
            var tightness = Clamp01(persona.Tightness + (rng() - 0.5) * TightnessJitter);
            var roll = rng();

            if (seat.KickerIndex == null)
            {
                throw new InvalidOperationException("A bot cannot act before its kicker is set");
            }

            var rank = HandRanks.EvaluateHand(seat.Cards);
            var margin = Threat.HandStrengthScore(rank) - Threat.PartialThreat(dealerCards);
            var madeHand = HandRanks.CategoryOrder(rank.Category) >= HandRanks.CategoryOrder(HandCategory.TwoPair);

            var bailMargin = -(BailMarginBase - BailMarginTightness * tightness);
            if (!madeHand && margin < bailMargin && roll < BailRollBase + BailRollTightness * tightness)
            {
                return new ShowdownMove { Kind = ShowdownMoveKind.Bail, Pressure = false };
            }

            var wantsPressure =
                round == ShowdownPayouts.PressureRound
                && !seat.Pressured
                && margin > PressureMarginBase + PressureMarginTightness * tightness;

            var forgeRate = ForgeRollBase + ForgeRollLooseness * (1 - tightness);
            var canForge =
                seat.ForgesUsed < ShowdownPayouts.ForgeLimit && IsDrawyHand(seat.Cards) && seat.Cards.Count > 1;
            if (canForge && roll < forgeRate)
            {
                var pressure = wantsPressure && seat.Stack >= ante * 2;
                if (seat.Stack >= ante * (pressure ? 2 : 1))
                {
                    return new ShowdownMove
                    {
                        Kind = ShowdownMoveKind.Forge,
                        ForgeDiscardIndex = ChooseForgeDiscard(seat.Cards, seat.KickerIndex.Value),
                        Pressure = pressure,
                    };
                }
            }

            return wantsPressure && seat.Stack >= ante
                ? new ShowdownMove { Kind = ShowdownMoveKind.Hold, Pressure = true }
                : Hold;
        }
    }
}
