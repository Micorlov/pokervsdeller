#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Showdown.Domain
{
    public enum ShowdownMoveKind
    {
        Hold,
        Forge,
        Bail,
    }

    public enum VsDealer
    {
        Win,
        Lose,
        Push,
        Bailed,
    }

    public sealed record ShowdownMove
    {
        public ShowdownMoveKind Kind { get; init; }

        /// <summary>Which card leaves the hand. Required for a forge, meaningless otherwise.</summary>
        public int? ForgeDiscardIndex { get; init; }

        /// <summary>Doubles the dealer stake. Legal only in the pressure round, never on a bail.</summary>
        public bool Pressure { get; init; }
    }

    public sealed record ShowdownSettlement
    {
        public VsDealer VsDealer { get; init; }

        /// <summary>Took a share of the table pot.</summary>
        public bool WonTable { get; init; }

        /// <summary>Took the table pot alone, no split.</summary>
        public bool Outright { get; init; }

        public bool KickerStrike { get; init; }

        public bool Sweep { get; init; }

        /// <summary>Chips won or lost against the house on the dealer stake.</summary>
        public int DealerDelta { get; init; }

        /// <summary>Chips won or lost on the table pot, contributions netted out.</summary>
        public int TableDelta { get; init; }

        public int SweepBonus { get; init; }

        /// <summary>
        /// The seat's total change for the hand. For a bailed seat this already
        /// includes the half-back refund it was handed on the way out.
        /// </summary>
        public int Net { get; init; }
    }

    public sealed record SettleInput
    {
        /// <summary>The five-card hand a survivor shows down. Null once bailed.</summary>
        public HandRank? Rank { get; init; }

        public int KickerRank { get; init; }

        public int DealerStake { get; init; }

        public int TableContrib { get; init; }

        public bool Pressured { get; init; }

        public bool Bailed { get; init; }

        /// <summary>The half-back a bailed seat already received. Zero for survivors.</summary>
        public int Refund { get; init; }
    }

    public sealed record SettleSeatsResult
    {
        public IReadOnlyList<ShowdownSettlement> Settlements { get; init; } = Array.Empty<ShowdownSettlement>();

        public int HouseDelta { get; init; }
    }

    /// <summary>
    /// Settlement for Showdown's two-front battle.
    ///
    /// Every seat fights the house over its dealer stake and the other players over
    /// the table pot, and the two fights pay independently. Keeping the deltas
    /// apart is what lets the table say "+30 vs the house, +50 off the table"
    /// instead of one opaque number — and what makes conservation checkable.
    /// </summary>
    public static class ShowdownPayouts
    {
        /// <summary>A hand allows this many card trades, total.</summary>
        public const int ForgeLimit = 2;

        /// <summary>Players act after dealer cards 1-4; the fifth goes straight to showdown.</summary>
        public const int ActionRounds = 4;

        /// <summary>The one action round where the dealer stake can be doubled.</summary>
        public const int PressureRound = 3;

        /// <summary>Odds paid on a pressured stake that beats the dealer.</summary>
        public const int PressureOdds = 3;

        /// <summary>Antes awarded, on the house, for beating dealer and table in one hand.</summary>
        public const int SweepBonusUnits = 5;

        /// <summary>Half of everything committed comes back, rounded the house's way.</summary>
        public static int BailRefund(int committed) => committed / 2;

        private static ShowdownSettlement SettledBail(SettleInput seat) =>
            new ShowdownSettlement
            {
                VsDealer = VsDealer.Bailed,
                WonTable = false,
                Outright = false,
                KickerStrike = false,
                Sweep = false,
                DealerDelta = -seat.DealerStake,
                TableDelta = -seat.TableContrib,
                SweepBonus = 0,
                Net = seat.Refund - seat.DealerStake - seat.TableContrib,
            };

        /// <summary>Splits the pot evenly, handing the remainder out one chip per winner in seat order.</summary>
        private static Dictionary<int, int> TableShares(IReadOnlyList<int> winners, int tablePot)
        {
            var shares = new Dictionary<int, int>();
            if (winners.Count == 0)
            {
                return shares;
            }

            var baseShare = tablePot / winners.Count;
            var remainder = tablePot - baseShare * winners.Count;
            for (var position = 0; position < winners.Count; position += 1)
            {
                shares[winners[position]] = baseShare + (position < remainder ? 1 : 0);
            }

            return shares;
        }

        /// <summary>
        /// Settles the whole table in one pass.
        ///
        /// The table pot is everything the losers left behind: every ante and forge
        /// fee, the stakes bailed seats abandoned mid-hand, and — added here — the
        /// stakes of survivors the dealer just beat. The best surviving hand takes
        /// all of it. The house pays dealer-beat winnings and sweep bonuses from a
        /// bottomless bank; <c>HouseDelta</c> is its take from this step alone — bail
        /// refunds were paid mid-hand and are already on the ledger.
        /// </summary>
        public static SettleSeatsResult SettleSeats(
            IReadOnlyList<SettleInput> seats,
            HandRank dealerRank,
            Card dealerFinalCard,
            int tablePot,
            int ante)
        {
            var verdicts = new int?[seats.Count];
            for (var index = 0; index < seats.Count; index += 1)
            {
                var seat = seats[index];
                if (seat.Bailed)
                {
                    continue;
                }

                if (seat.Rank == null)
                {
                    throw new InvalidOperationException("A surviving seat reached the settle without a hand");
                }

                verdicts[index] = HandRanks.CompareHands(seat.Rank, dealerRank);
            }

            // Beaten survivors' stakes pour into the pot before it is awarded.
            var effectivePot = tablePot;
            for (var index = 0; index < seats.Count; index += 1)
            {
                effectivePot += (verdicts[index] ?? 0) < 0 ? seats[index].DealerStake : 0;
            }

            var survivors = Enumerable.Range(0, seats.Count).Where(index => !seats[index].Bailed).ToList();

            HandRank? best = null;
            foreach (var index in survivors)
            {
                var rank = seats[index].Rank!;
                if (best == null || HandRanks.CompareHands(rank, best) > 0)
                {
                    best = rank;
                }
            }

            var winners = best == null
                ? new List<int>()
                : survivors.Where(index => HandRanks.CompareHands(seats[index].Rank!, best) == 0).ToList();
            var shares = TableShares(winners, effectivePot);
            var outright = winners.Count == 1;

            var settlements = new ShowdownSettlement[seats.Count];
            for (var index = 0; index < seats.Count; index += 1)
            {
                var seat = seats[index];
                if (seat.Bailed)
                {
                    settlements[index] = SettledBail(seat);
                    continue;
                }

                var verdict = verdicts[index]!.Value;
                var vsDealer = verdict > 0 ? VsDealer.Win : verdict < 0 ? VsDealer.Lose : VsDealer.Push;
                var kickerStrike = vsDealer == VsDealer.Win && seat.KickerRank == dealerFinalCard.Rank;
                var dealerDelta =
                    vsDealer == VsDealer.Win
                        ? seat.DealerStake * (seat.Pressured ? PressureOdds : 1) * (kickerStrike ? 2 : 1)
                        : vsDealer == VsDealer.Lose
                            ? -seat.DealerStake
                            : 0;

                var share = shares.TryGetValue(index, out var found) ? found : 0;
                var wonTable = share > 0;
                var sweep = vsDealer == VsDealer.Win && wonTable && outright;
                var sweepBonus = sweep ? SweepBonusUnits * ante : 0;
                var tableDelta = share - seat.TableContrib;

                settlements[index] = new ShowdownSettlement
                {
                    VsDealer = vsDealer,
                    WonTable = wonTable,
                    Outright = wonTable && outright,
                    KickerStrike = kickerStrike,
                    Sweep = sweep,
                    DealerDelta = dealerDelta,
                    TableDelta = tableDelta,
                    SweepBonus = sweepBonus,
                    Net = dealerDelta + tableDelta + sweepBonus,
                };
            }

            // The house only pays winners; every lost stake is already in the pot.
            var winningsPaid = settlements
                .Where(settlement => settlement.VsDealer == VsDealer.Win)
                .Sum(settlement => settlement.DealerDelta + settlement.SweepBonus);
            var unclaimedPot = winners.Count == 0 ? effectivePot : 0;

            return new SettleSeatsResult { Settlements = settlements, HouseDelta = -winningsPaid + unclaimedPot };
        }
    }
}
