import { ACHIEVEMENTS, newlyUnlockedAchievements, unlockedAchievements } from '../achievements';
import { Statistics, defaultStatistics } from '../stats';

const stats = (overrides: Partial<Statistics> = {}): Statistics => ({
  ...defaultStatistics,
  ...overrides,
});

describe('ACHIEVEMENTS', () => {
  test('every id is unique', () => {
    const ids = ACHIEVEMENTS.map((a) => a.id);
    expect(new Set(ids).size).toBe(ids.length);
  });
});

describe('unlockedAchievements', () => {
  test('nothing is unlocked from a fresh record', () => {
    expect(unlockedAchievements(defaultStatistics)).toEqual([]);
  });

  test('first hand and first win unlock together after one win', () => {
    const ids = unlockedAchievements(stats({ handsPlayed: 1, handsWon: 1 })).map((a) => a.id);
    expect(ids).toContain('firstHand');
    expect(ids).toContain('firstWin');
  });

  test('first hand unlocks without a win', () => {
    const ids = unlockedAchievements(stats({ handsPlayed: 1, handsWon: 0 })).map((a) => a.id);
    expect(ids).toContain('firstHand');
    expect(ids).not.toContain('firstWin');
  });

  test('sweepMaster requires ten sweeps, not just one', () => {
    expect(unlockedAchievements(stats({ sweeps: 1 })).map((a) => a.id)).not.toContain('sweepMaster');
    expect(unlockedAchievements(stats({ sweeps: 10 })).map((a) => a.id)).toContain('sweepMaster');
  });

  test('highRoller unlocks at 500 biggest win', () => {
    expect(unlockedAchievements(stats({ biggestWin: 499 })).map((a) => a.id)).not.toContain('highRoller');
    expect(unlockedAchievements(stats({ biggestWin: 500 })).map((a) => a.id)).toContain('highRoller');
  });

  test('hotHand and unstoppable read off bestStreak, not currentStreak', () => {
    const ids = unlockedAchievements(stats({ currentStreak: 0, bestStreak: 5 })).map((a) => a.id);
    expect(ids).toContain('hotHand');
    expect(ids).not.toContain('unstoppable');
  });

  test('fullHouseOrBetter unlocks for full house and every stronger category, not weaker ones', () => {
    expect(unlockedAchievements(stats({ bestHandCategory: 'flush' })).map((a) => a.id)).not.toContain(
      'fullHouseOrBetter',
    );
    expect(unlockedAchievements(stats({ bestHandCategory: 'fullHouse' })).map((a) => a.id)).toContain(
      'fullHouseOrBetter',
    );
    expect(unlockedAchievements(stats({ bestHandCategory: 'royalFlush' })).map((a) => a.id)).toContain(
      'fullHouseOrBetter',
    );
  });

  test('royalty requires the royal flush specifically', () => {
    expect(unlockedAchievements(stats({ bestHandCategory: 'straightFlush' })).map((a) => a.id)).not.toContain(
      'royalty',
    );
    expect(unlockedAchievements(stats({ bestHandCategory: 'royalFlush' })).map((a) => a.id)).toContain('royalty');
  });

  test('kickerKing and ironNerve read their own counters', () => {
    expect(unlockedAchievements(stats({ kickerStrikes: 5 })).map((a) => a.id)).toContain('kickerKing');
    expect(unlockedAchievements(stats({ pressures: 10 })).map((a) => a.id)).toContain('ironNerve');
  });

  test('regular unlocks at 100 hands played', () => {
    expect(unlockedAchievements(stats({ handsPlayed: 99 })).map((a) => a.id)).not.toContain('regular');
    expect(unlockedAchievements(stats({ handsPlayed: 100 })).map((a) => a.id)).toContain('regular');
  });

  test('unlocked badges come back in book order, not unlock order', () => {
    const ids = unlockedAchievements(
      stats({ handsPlayed: 100, handsWon: 1, sweeps: 1 }),
    ).map((a) => a.id);
    expect(ids).toEqual(['firstHand', 'firstWin', 'sweep', 'regular']);
  });
});

describe('newlyUnlockedAchievements', () => {
  test('empty when nothing crossed a threshold', () => {
    const prev = stats({ handsPlayed: 5, handsWon: 2 });
    const next = stats({ handsPlayed: 6, handsWon: 2 });
    expect(newlyUnlockedAchievements(prev, next)).toEqual([]);
  });

  test('reports only the badges the next record newly qualifies for', () => {
    const prev = stats({ handsPlayed: 0, handsWon: 0 });
    const next = stats({ handsPlayed: 1, handsWon: 1 });

    const ids = newlyUnlockedAchievements(prev, next).map((a) => a.id);
    expect(ids).toEqual(['firstHand', 'firstWin']);
  });

  test('never re-reports a badge already unlocked in prev', () => {
    const prev = stats({ handsPlayed: 1, handsWon: 1 });
    const next = stats({ handsPlayed: 2, handsWon: 2 });
    expect(newlyUnlockedAchievements(prev, next)).toEqual([]);
  });

  test('a single hand can cross more than one threshold at once', () => {
    const prev = stats({ sweeps: 9, biggestWin: 400 });
    const next = stats({ sweeps: 10, biggestWin: 600 });

    const ids = newlyUnlockedAchievements(prev, next).map((a) => a.id);
    expect(ids).toEqual(expect.arrayContaining(['sweepMaster', 'highRoller']));
  });
});
