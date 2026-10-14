#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Showdown.Domain
{
    /// <summary>
    /// A 52-card deck, described by rank and suit only — every card is drawn in
    /// code from these two values, so there are no image assets anywhere.
    /// </summary>
    public enum Suit
    {
        Spades,
        Hearts,
        Diamonds,
        Clubs,
    }

    public sealed record Card
    {
        /// <summary>2-10 are themselves; 11 = J, 12 = Q, 13 = K, 14 = A (always high).</summary>
        public int Rank { get; init; }

        public Suit Suit { get; init; }

        /// <summary>Stable identity, so entrance animations can key off a card. "14s", "7h".</summary>
        public string Id { get; init; } = "";
    }

    public static class Cards
    {
        public const int Ace = 14;
        public const int King = 13;
        public const int Queen = 12;
        public const int Jack = 11;

        public static readonly IReadOnlyList<Suit> Suits =
            Array.AsReadOnly(new[] { Suit.Spades, Suit.Hearts, Suit.Diamonds, Suit.Clubs });

        public static readonly IReadOnlyList<int> Ranks =
            Array.AsReadOnly(new[] { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 });

        private static string SuitLetter(Suit suit)
        {
            switch (suit)
            {
                case Suit.Spades: return "s";
                case Suit.Hearts: return "h";
                case Suit.Diamonds: return "d";
                default: return "c";
            }
        }

        private static string SuitWord(Suit suit)
        {
            switch (suit)
            {
                case Suit.Spades: return "spades";
                case Suit.Hearts: return "hearts";
                case Suit.Diamonds: return "diamonds";
                default: return "clubs";
            }
        }

        private static string FaceWord(int rank)
        {
            switch (rank)
            {
                case Jack: return "Jack";
                case Queen: return "Queen";
                case King: return "King";
                case Ace: return "Ace";
                default: return rank.ToString(CultureInfo.InvariantCulture);
            }
        }

        public static string CardId(int rank, Suit suit) =>
            rank.ToString(CultureInfo.InvariantCulture) + SuitLetter(suit);

        /// <summary>The 52 cards, suit by suit, each suit 2 up to ace.</summary>
        public static IReadOnlyList<Card> CreateDeck()
        {
            var deck = new List<Card>(Suits.Count * Ranks.Count);
            foreach (var suit in Suits)
            {
                foreach (var rank in Ranks)
                {
                    deck.Add(new Card { Rank = rank, Suit = suit, Id = CardId(rank, suit) });
                }
            }

            return deck;
        }

        /// <summary>The corner index: "A", "K", "10", "7".</summary>
        public static string RankLabel(int rank)
        {
            switch (rank)
            {
                case Jack: return "J";
                case Queen: return "Q";
                case King: return "K";
                case Ace: return "A";
                default: return rank.ToString(CultureInfo.InvariantCulture);
            }
        }

        public static string SuitSymbol(Suit suit)
        {
            switch (suit)
            {
                case Suit.Spades: return "♠";
                case Suit.Hearts: return "♥";
                case Suit.Diamonds: return "♦";
                default: return "♣";
            }
        }

        /// <summary>Hearts and diamonds print red; the card skin supplies the two inks.</summary>
        public static bool IsRedSuit(Suit suit) => suit == Suit.Hearts || suit == Suit.Diamonds;

        /// <summary>Spoken form for screen readers — "Ace of spades", "Seven of hearts".</summary>
        public static string CardName(Card card) => FaceWord(card.Rank) + " of " + SuitWord(card.Suit);
    }
}
