import { useCallback, useEffect, useState } from 'react';
import { Statistics, defaultStatistics, migrateStatistics } from '../domain/stats';
import { readJson, writeJson } from '../storage/persistence';

const STATS_KEY = 'stats';

export type { Statistics };
export { defaultStatistics };

/** Lifetime table stats — small enough to write whole on every change. */
export const useStats = () => {
  const [stats, setStats] = useState<Statistics>(defaultStatistics);

  useEffect(() => {
    let isMounted = true;
    readJson<unknown>(STATS_KEY, defaultStatistics).then((loaded) => {
      // Stats written by older builds — including the previous game — are
      // translated to the current shape before they touch the UI.
      if (isMounted) setStats(migrateStatistics(loaded));
    });
    return () => {
      isMounted = false;
    };
  }, []);

  const update = useCallback((patch: (prev: Statistics) => Statistics) => {
    setStats((prev) => {
      const next = patch(prev);
      void writeJson(STATS_KEY, next);
      return next;
    });
  }, []);

  return { stats, update };
};
