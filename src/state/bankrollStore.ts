import { useCallback, useEffect, useState } from 'react';
import { STARTING_BANKROLL } from '../domain/bankroll';
import { readJson, writeJson } from '../storage/persistence';

const BANKROLL_KEY = 'bankroll';

interface StoredBankroll {
  readonly bankroll: number;
}

/**
 * The one number that survives between sessions: the player's chips. Bots
 * rebuy silently and the dealer is a bottomless house, so nothing else here
 * needs to persist.
 */
export const useBankroll = () => {
  const [bankroll, setBankroll] = useState(STARTING_BANKROLL);
  const [isLoaded, setIsLoaded] = useState(false);

  useEffect(() => {
    let isMounted = true;
    readJson<StoredBankroll>(BANKROLL_KEY, { bankroll: STARTING_BANKROLL }).then((stored) => {
      if (isMounted) {
        setBankroll(stored.bankroll);
        setIsLoaded(true);
      }
    });
    return () => {
      isMounted = false;
    };
  }, []);

  const save = useCallback((value: number) => {
    setBankroll(value);
    void writeJson(BANKROLL_KEY, { bankroll: value });
  }, []);

  const reset = useCallback(() => save(STARTING_BANKROLL), [save]);

  return { bankroll, isLoaded, save, reset };
};
