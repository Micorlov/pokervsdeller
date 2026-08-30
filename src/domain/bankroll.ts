/**
 * The player's chips, and the one rule that keeps the game playable: you can
 * always re-up for free. This is a game against a house edge, not a wallet.
 */

export const ANTE_OPTIONS = [5, 10, 25] as const;

export type Ante = (typeof ANTE_OPTIONS)[number];

export const STARTING_BANKROLL = 500;

export const RE_UP_AMOUNT = 500;

/**
 * A full hand costs five antes in the worst case — two in, two forges, one
 * pressure — so a stack that cannot cover five is not short: it is out, and
 * re-upping is the only honest offer.
 */
export const MIN_BANKROLL_MULTIPLE = 5;

export const minimumToPlay = (ante: Ante): number => ante * MIN_BANKROLL_MULTIPLE;

export const canPlay = (bankroll: number, ante: Ante): boolean => bankroll >= minimumToPlay(ante);

export const isBust = (bankroll: number, ante: Ante): boolean => !canPlay(bankroll, ante);

/**
 * Tops the stack up. Deliberately a no-op while the player can still cover a
 * hand — the button is offered at bust, and nowhere else.
 */
export const reUp = (bankroll: number, ante: Ante): number =>
  canPlay(bankroll, ante) ? bankroll : bankroll + RE_UP_AMOUNT;

/** The largest ante this stack can still sit down for, or null when bust at every level. */
export const largestAffordableAnte = (bankroll: number): Ante | null => {
  const affordable = [...ANTE_OPTIONS].reverse().find((ante) => canPlay(bankroll, ante));
  return affordable ?? null;
};
