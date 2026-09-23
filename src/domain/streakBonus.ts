/**
 * The win-streak bonus: extra chips, on the house, when a streak lands
 * exactly on 3, 5, 7 or 10 wins. Ante-scaled the same way the sweep bonus is
 * (`SWEEP_BONUS_UNITS * ante` in showdownPayouts.ts), and capped at 10 — the
 * same ceiling the `unstoppable` achievement uses — so a streak can't be
 * farmed for ever-larger payouts once it runs past the last rung.
 */

export const STREAK_BONUS_UNITS: Record<number, number> = {
  3: 2,
  5: 4,
  7: 6,
  10: 10,
};

/** Bonus units for a streak count, or zero off the ladder. */
export const streakBonusUnits = (streak: number): number => STREAK_BONUS_UNITS[streak] ?? 0;

/** The chip amount a streak count pays at a given ante, or zero off the ladder. */
export const streakBonusFor = (streak: number, ante: number): number =>
  streakBonusUnits(streak) * ante;
