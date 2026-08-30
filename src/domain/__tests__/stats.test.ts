import { ShowdownSettlement } from '../showdownPayouts';
import {
  HISTORY_LIMIT,
  HandHistoryEntry,
  defaultStatistics,
  migrateStatistics,
  outcomeOf,
  recordHandResult,
  winRatePercent,
} from '../stats';

const entry = (overrides: Partial<HandHistoryEntry> = {}): HandHistoryEntry => ({
  category: 'pair',
  outcome: 'beatDealer',
  kickerStrike: false,
  pressured: false,
  forges: 0,
  net: 20,
  at: 1_000,
  ...overrides,
});

const settlement = (overrides: Partial<ShowdownSettlement> = {}): ShowdownSettlement => ({
  vsDealer: 'win',
  wonTable: false,
  outright: false,
  kickerStrike: false,
  sweep: false,
  dealerDelta: 10,
  tableDelta: -10,
  sweepBonus: 0,
  net: 0,
  ...overrides,
});

describe('outcomeOf', () => {
  test('reads each settlement as its scoreboard line', () => {
    expect(outcomeOf(settlement({ sweep: true, wonTable: true, outright: true }))).toBe('sweep');
    expect(outcomeOf(settlement({ wonTable: true, outright: true }))).toBe('beatBoth');
    expect(outcomeOf(settlement())).toBe('beatDealer');
    expect(outcomeOf(settlement({ vsDealer: 'lose', wonTable: true }))).toBe('beatTable');
    expect(outcomeOf(settlement({ vsDealer: 'push' }))).toBe('push');
    expect(outcomeOf(settlement({ vsDealer: 'lose' }))).toBe('lose');
    expect(outcomeOf(settlement({ vsDealer: 'bailed' }))).toBe('bail');
  });
});

describe('recordHandResult', () => {
  test('counts a won hand and starts a streak', () => {
    const next = recordHandResult(defaultStatistics, entry({ net: 30 }));

    expect(next.handsPlayed).toBe(1);
    expect(next.handsWon).toBe(1);
    expect(next.biggestWin).toBe(30);
    expect(next.currentStreak).toBe(1);
    expect(next.bestStreak).toBe(1);
  });

  test('a loss ends the streak but best streak survives', () => {
    const afterTwoWins = recordHandResult(recordHandResult(defaultStatistics, entry()), entry());

    const next = recordHandResult(afterTwoWins, entry({ outcome: 'lose', net: -30 }));

    expect(next.currentStreak).toBe(0);
    expect(next.bestStreak).toBe(2);
    expect(next.handsWon).toBe(2);
  });

  test('a push leaves the streak untouched', () => {
    const afterWin = recordHandResult(defaultStatistics, entry());

    const next = recordHandResult(afterWin, entry({ outcome: 'push', net: 0 }));

    expect(next.currentStreak).toBe(1);
    expect(next.handsPlayed).toBe(2);
    expect(next.handsWon).toBe(1);
  });

  test('tallies sweeps, strikes, bails and pressures', () => {
    const one = recordHandResult(
      defaultStatistics,
      entry({ outcome: 'sweep', kickerStrike: true, pressured: true, net: 120 }),
    );
    const two = recordHandResult(one, entry({ outcome: 'bail', net: -10 }));

    expect(two.sweeps).toBe(1);
    expect(two.kickerStrikes).toBe(1);
    expect(two.pressures).toBe(1);
    expect(two.bails).toBe(1);
  });

  test('remembers the strongest hand ever made, even when it lost', () => {
    const afterPair = recordHandResult(defaultStatistics, entry());

    const next = recordHandResult(afterPair, entry({ category: 'flush', outcome: 'lose', net: -30 }));

    expect(next.bestHandCategory).toBe('flush');

    const later = recordHandResult(next, entry({ category: 'twoPair' }));
    expect(later.bestHandCategory).toBe('flush');
  });

  test('tallies made hands per category, bails included', () => {
    const one = recordHandResult(defaultStatistics, entry());
    const two = recordHandResult(one, entry({ outcome: 'bail', net: -10 }));

    expect(two.categoryCounts.pair).toBe(2);
    expect(two.categoryCounts.flush).toBeUndefined();
  });

  test('keeps history newest first and capped', () => {
    const filled = Array.from({ length: HISTORY_LIMIT + 3 }, (_, i) => i).reduce(
      (stats, i) => recordHandResult(stats, entry({ at: i })),
      defaultStatistics,
    );

    expect(filled.history).toHaveLength(HISTORY_LIMIT);
    expect(filled.history[0].at).toBe(HISTORY_LIMIT + 2);
  });

  test('does not mutate the previous statistics', () => {
    const prev = defaultStatistics;

    recordHandResult(prev, entry());

    expect(prev).toEqual(defaultStatistics);
  });
});

describe('winRatePercent', () => {
  test('returns null before any hand', () => {
    expect(winRatePercent(defaultStatistics)).toBeNull();
  });

  test('rounds to a whole percent', () => {
    const one = recordHandResult(defaultStatistics, entry());
    const two = recordHandResult(one, entry({ outcome: 'lose', net: -30 }));
    const three = recordHandResult(two, entry({ outcome: 'lose', net: -30 }));

    expect(winRatePercent(three)).toBe(33);
  });
});

describe('migrateStatistics', () => {
  /** What the previous game actually wrote to storage. */
  const legacy = {
    handsPlayed: 40,
    handsWon: 17,
    biggestWin: 250,
    reUps: 2,
    currentStreak: 1,
    bestStreak: 5,
    bestHandCategory: 'flush',
    categoryCounts: { pair: 20, flush: 1 },
    history: [
      { category: 'pair', decision: 'raise', outcome: 'win', net: 20, at: 1_000 },
      { category: 'highCard', decision: 'fold', outcome: 'fold', net: -10, at: 900 },
      { category: 'pair', decision: 'raise', outcome: 'noQualify', net: 10, at: 800 },
      { category: 'twoPair', decision: 'raise', outcome: 'push', net: 0, at: 700 },
    ],
  };

  test('keeps the lifetime record and translates old history', () => {
    const migrated = migrateStatistics(legacy);

    expect(migrated.handsPlayed).toBe(40);
    expect(migrated.bestStreak).toBe(5);
    expect(migrated.bestHandCategory).toBe('flush');
    expect(migrated.sweeps).toBe(0);
    expect(migrated.history.map((h) => h.outcome)).toEqual([
      'beatDealer',
      'bail',
      'beatDealer',
      'push',
    ]);
    migrated.history.forEach((h) => {
      expect(h.kickerStrike).toBe(false);
      expect(h.forges).toBe(0);
    });
  });

  test('is idempotent on already-migrated data', () => {
    const once = migrateStatistics(legacy);
    expect(migrateStatistics(once)).toEqual(once);
  });

  test('falls back to defaults on garbage', () => {
    expect(migrateStatistics(null)).toEqual(defaultStatistics);
    expect(migrateStatistics('corrupt')).toEqual(defaultStatistics);
    expect(migrateStatistics(7)).toEqual(defaultStatistics);
  });

  test('drops unreadable history entries and keeps the rest', () => {
    const migrated = migrateStatistics({
      history: [
        { category: 'notAHand', outcome: 'win', net: 1, at: 1 },
        { category: 'pair', outcome: 'mystery', net: 1, at: 1 },
        { category: 'pair', outcome: 'win', net: 'lots', at: 1 },
        {
          category: 'trips',
          outcome: 'beatBoth',
          kickerStrike: true,
          pressured: true,
          forges: 1,
          net: 90,
          at: 2,
        },
      ],
    });

    expect(migrated.history).toHaveLength(1);
    expect(migrated.history[0].outcome).toBe('beatBoth');
    expect(migrated.history[0].kickerStrike).toBe(true);
  });
});
