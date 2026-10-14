#nullable enable
using System;
using System.Collections.Generic;

namespace Showdown.Domain
{
    /// <summary>
    /// The player's chips, and the one rule that keeps the game playable: you can
    /// always re-up for free. This is a game against a house edge, not a wallet.
    /// An ante is a plain int; the legal values are <see cref="AnteOptions"/>.
    /// </summary>
    public static class Bankroll
    {
        public static readonly IReadOnlyList<int> AnteOptions = Array.AsReadOnly(new[] { 5, 10, 25 });

        public const int StartingBankroll = 500;

        public const int ReUpAmount = 500;

        /// <summary>
        /// A full hand costs five antes in the worst case — two in, two forges, one
        /// pressure — so a stack that cannot cover five is not short: it is out, and
        /// re-upping is the only honest offer.
        /// </summary>
        public const int MinBankrollMultiple = 5;

        public static int MinimumToPlay(int ante) => ante * MinBankrollMultiple;

        public static bool CanPlay(int bankroll, int ante) => bankroll >= MinimumToPlay(ante);

        public static bool IsBust(int bankroll, int ante) => !CanPlay(bankroll, ante);

        /// <summary>
        /// Tops the stack up. Deliberately a no-op while the player can still cover a
        /// hand — the button is offered at bust, and nowhere else.
        /// </summary>
        public static int ReUp(int bankroll, int ante) =>
            CanPlay(bankroll, ante) ? bankroll : bankroll + ReUpAmount;

        /// <summary>The largest ante this stack can still sit down for, or null when bust at every level.</summary>
        public static int? LargestAffordableAnte(int bankroll)
        {
            for (var i = AnteOptions.Count - 1; i >= 0; i -= 1)
            {
                if (CanPlay(bankroll, AnteOptions[i]))
                {
                    return AnteOptions[i];
                }
            }

            return null;
        }
    }
}
