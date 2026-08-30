import { Card, Rank } from './cards';
import { HandRank, compareHands } from './handRank';

/**
 * Settlement for Showdown's two-front battle.
 *
 * Every seat fights the house over its dealer stake and the other players over
 * the table pot, and the two fights pay independently. Keeping the deltas
 * apart is what lets the table say "+30 vs the house, +50 off the table"
 * instead of one opaque number — and what makes conservation checkable.
 */

/** A hand allows this many card trades, total. */
export const FORGE_LIMIT = 2;
/** Players act after dealer cards 1–4; the fifth goes straight to showdown. */
export const ACTION_ROUNDS = 4;
/** The one action round where the dealer stake can be doubled. */
export const PRESSURE_ROUND = 3;
/** Odds paid on a pressured stake that beats the dealer. */
export const PRESSURE_ODDS = 3;
/** Antes awarded, on the house, for beating dealer and table in one hand. */
export const SWEEP_BONUS_UNITS = 5;

export type ShowdownMoveKind = 'hold' | 'forge' | 'bail';

export interface ShowdownMove {
  readonly kind: ShowdownMoveKind;
  /** Which card leaves the hand. Required for a forge, meaningless otherwise. */
  readonly forgeDiscardIndex?: number;
  /** Doubles the dealer stake. Legal only in the pressure round, never on a bail. */
  readonly pressure: boolean;
}

export type VsDealer = 'win' | 'lose' | 'push' | 'bailed';

export interface ShowdownSettlement {
  readonly vsDealer: VsDealer;
  /** Took a share of the table pot. */
  readonly wonTable: boolean;
  /** Took the table pot alone, no split. */
  readonly outright: boolean;
  readonly kickerStrike: boolean;
  readonly sweep: boolean;
  /** Chips won or lost against the house on the dealer stake. */
  readonly dealerDelta: number;
  /** Chips won or lost on the table pot, contributions netted out. */
  readonly tableDelta: number;
  readonly sweepBonus: number;
  /**
   * The seat's total change for the hand. For a bailed seat this already
   * includes the half-back refund it was handed on the way out.
   */
  readonly net: number;
}

export interface SettleInput {
  /** The five-card hand a survivor shows down. Null once bailed. */
  readonly rank: HandRank | null;
  readonly kickerRank: Rank;
  readonly dealerStake: number;
  readonly tableContrib: number;
  readonly pressured: boolean;
  readonly bailed: boolean;
  /** The half-back a bailed seat already received. Zero for survivors. */
  readonly refund: number;
}

/** Half of everything committed comes back, rounded the house's way. */
export const bailRefund = (committed: number): number => Math.floor(committed / 2);

const settledBail = (seat: SettleInput): ShowdownSettlement => ({
  vsDealer: 'bailed',
  wonTable: false,
  outright: false,
  kickerStrike: false,
  sweep: false,
  dealerDelta: -seat.dealerStake,
  tableDelta: -seat.tableContrib,
  sweepBonus: 0,
  net: seat.refund - seat.dealerStake - seat.tableContrib,
});

/** Splits the pot evenly, handing the remainder out one chip per winner in seat order. */
const tableShares = (winners: readonly number[], tablePot: number): Map<number, number> => {
  if (winners.length === 0) return new Map();
  const base = Math.floor(tablePot / winners.length);
  const remainder = tablePot - base * winners.length;
  return new Map(winners.map((seatIndex, position) => [seatIndex, base + (position < remainder ? 1 : 0)]));
};

/**
 * Settles the whole table in one pass.
 *
 * The table pot is everything the losers left behind: every ante and forge
 * fee, the stakes bailed seats abandoned mid-hand, and — added here — the
 * stakes of survivors the dealer just beat. The best surviving hand takes
 * all of it. The house pays dealer-beat winnings and sweep bonuses from a
 * bottomless bank; `houseDelta` is its take from this step alone — bail
 * refunds were paid mid-hand and are already on the ledger.
 */
export const settleSeats = (
  seats: readonly SettleInput[],
  dealerRank: HandRank,
  dealerFinalCard: Card,
  tablePot: number,
  ante: number,
): { settlements: readonly ShowdownSettlement[]; houseDelta: number } => {
  const verdicts = seats.map((seat) => {
    if (seat.bailed) return null;
    if (!seat.rank) throw new Error('A surviving seat reached the settle without a hand');
    return compareHands(seat.rank, dealerRank);
  });

  // Beaten survivors' stakes pour into the pot before it is awarded.
  const effectivePot =
    tablePot +
    seats.reduce(
      (total, seat, index) => total + ((verdicts[index] ?? 0) < 0 ? seat.dealerStake : 0),
      0,
    );

  const survivors = seats
    .map((seat, index) => ({ seat, index }))
    .filter(({ seat }) => !seat.bailed);

  const best = survivors.reduce<HandRank | null>((strongest, { seat }) => {
    const rank = seat.rank as HandRank;
    return strongest === null || compareHands(rank, strongest) > 0 ? rank : strongest;
  }, null);

  const winners = best
    ? survivors
        .filter(({ seat }) => compareHands(seat.rank as HandRank, best) === 0)
        .map(({ index }) => index)
    : [];
  const shares = tableShares(winners, effectivePot);
  const outright = winners.length === 1;

  const settlements = seats.map((seat, index): ShowdownSettlement => {
    if (seat.bailed) return settledBail(seat);

    const verdict = verdicts[index] as number;
    const vsDealer: VsDealer = verdict > 0 ? 'win' : verdict < 0 ? 'lose' : 'push';
    const kickerStrike = vsDealer === 'win' && seat.kickerRank === dealerFinalCard.rank;
    const dealerDelta =
      vsDealer === 'win'
        ? seat.dealerStake * (seat.pressured ? PRESSURE_ODDS : 1) * (kickerStrike ? 2 : 1)
        : vsDealer === 'lose'
          ? -seat.dealerStake
          : 0;

    const share = shares.get(index) ?? 0;
    const wonTable = share > 0;
    const sweep = vsDealer === 'win' && wonTable && outright;
    const sweepBonus = sweep ? SWEEP_BONUS_UNITS * ante : 0;
    const tableDelta = share - seat.tableContrib;

    return {
      vsDealer,
      wonTable,
      outright: wonTable && outright,
      kickerStrike,
      sweep,
      dealerDelta,
      tableDelta,
      sweepBonus,
      net: dealerDelta + tableDelta + sweepBonus,
    };
  });

  // The house only pays winners; every lost stake is already in the pot.
  const winningsPaid = settlements
    .filter((settlement) => settlement.vsDealer === 'win')
    .reduce((total, settlement) => total + settlement.dealerDelta + settlement.sweepBonus, 0);
  const unclaimedPot = winners.length === 0 ? effectivePot : 0;

  return { settlements, houseDelta: -winningsPaid + unclaimedPot };
};
