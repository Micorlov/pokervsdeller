/**
 * The daily bonus: a free stack of chips once per calendar day, the retention
 * staple of every casino app. Eligibility compares local calendar days rather
 * than 24-hour windows, so "come back tomorrow" means tomorrow morning, not
 * the same hour tomorrow.
 */

export const DAILY_BONUS_AMOUNT = 250;

/** A local calendar day as `YYYY-MM-DD` — the unit the bonus resets on. */
export const dayKeyOf = (date: Date): string => {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
};

/** Claimable when never claimed, or last claimed on an earlier calendar day. */
export const canClaimDailyBonus = (lastClaimDay: string | null, now: Date): boolean =>
  lastClaimDay === null || lastClaimDay !== dayKeyOf(now);
