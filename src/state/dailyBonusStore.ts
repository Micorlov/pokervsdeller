import { useCallback, useEffect, useState } from 'react';
import { canClaimDailyBonus, dayKeyOf } from '../domain/dailyBonus';
import { readJson, writeJson } from '../storage/persistence';

const DAILY_BONUS_KEY = 'dailyBonus';

interface StoredDailyBonus {
  readonly lastClaimDay: string | null;
}

/**
 * Remembers the calendar day of the last claim. Eligibility is derived at
 * render time so a session left open overnight becomes claimable again
 * without a restart.
 */
export const useDailyBonus = () => {
  const [lastClaimDay, setLastClaimDay] = useState<string | null>(null);
  const [isLoaded, setIsLoaded] = useState(false);

  useEffect(() => {
    let isMounted = true;
    readJson<StoredDailyBonus>(DAILY_BONUS_KEY, { lastClaimDay: null }).then((stored) => {
      if (isMounted) {
        setLastClaimDay(stored.lastClaimDay);
        setIsLoaded(true);
      }
    });
    return () => {
      isMounted = false;
    };
  }, []);

  const markClaimed = useCallback(() => {
    const today = dayKeyOf(new Date());
    setLastClaimDay(today);
    void writeJson(DAILY_BONUS_KEY, { lastClaimDay: today });
  }, []);

  // Only claimable once storage has answered — a flash of "claim me" that
  // disappears when the stored day loads would read as a broken button.
  const canClaim = isLoaded && canClaimDailyBonus(lastClaimDay, new Date());

  return { canClaim, markClaimed };
};
