import { CATEGORY_ORDER } from './handRank';
import { Statistics } from './stats';

/**
 * Badges are read straight off the lifetime record — no separate storage, no
 * separate reducer. A badge is unlocked exactly when its check passes against
 * the current `Statistics`, so the book can never drift out of sync with the
 * numbers it's celebrating.
 */

export type AchievementId =
  | 'firstHand'
  | 'firstWin'
  | 'sweep'
  | 'sweepMaster'
  | 'highRoller'
  | 'hotHand'
  | 'unstoppable'
  | 'fullHouseOrBetter'
  | 'royalty'
  | 'kickerKing'
  | 'ironNerve'
  | 'regular';

export interface Achievement {
  readonly id: AchievementId;
  readonly title: string;
  readonly description: string;
  readonly isUnlocked: (stats: Statistics) => boolean;
}

/** Every badge the book can award, in the order the Achievements panel shows them. */
export const ACHIEVEMENTS: readonly Achievement[] = [
  {
    id: 'firstHand',
    title: 'First Deal',
    description: 'Play your first hand.',
    isUnlocked: (stats) => stats.handsPlayed >= 1,
  },
  {
    id: 'firstWin',
    title: 'On the Board',
    description: 'Win your first hand.',
    isUnlocked: (stats) => stats.handsWon >= 1,
  },
  {
    id: 'sweep',
    title: 'Clean Sweep',
    description: 'Beat the dealer and the table in the same hand.',
    isUnlocked: (stats) => stats.sweeps >= 1,
  },
  {
    id: 'sweepMaster',
    title: 'Sweep Master',
    description: 'Sweep the table 10 times.',
    isUnlocked: (stats) => stats.sweeps >= 10,
  },
  {
    id: 'highRoller',
    title: 'High Roller',
    description: 'Win 500 chips in a single hand.',
    isUnlocked: (stats) => stats.biggestWin >= 500,
  },
  {
    id: 'hotHand',
    title: 'Hot Hand',
    description: 'Win 5 hands in a row.',
    isUnlocked: (stats) => stats.bestStreak >= 5,
  },
  {
    id: 'unstoppable',
    title: 'Unstoppable',
    description: 'Win 10 hands in a row.',
    isUnlocked: (stats) => stats.bestStreak >= 10,
  },
  {
    id: 'fullHouseOrBetter',
    title: 'Full Boat',
    description: 'Make a full house or better.',
    isUnlocked: (stats) =>
      stats.bestHandCategory !== null &&
      CATEGORY_ORDER[stats.bestHandCategory] >= CATEGORY_ORDER.fullHouse,
  },
  {
    id: 'royalty',
    title: 'Royalty',
    description: 'Make a royal flush.',
    isUnlocked: (stats) => stats.bestHandCategory === 'royalFlush',
  },
  {
    id: 'kickerKing',
    title: 'Kicker King',
    description: 'Land 5 kicker strikes.',
    isUnlocked: (stats) => stats.kickerStrikes >= 5,
  },
  {
    id: 'ironNerve',
    title: 'Iron Nerve',
    description: 'Press your bet 10 times.',
    isUnlocked: (stats) => stats.pressures >= 10,
  },
  {
    id: 'regular',
    title: 'Regular',
    description: 'Play 100 hands.',
    isUnlocked: (stats) => stats.handsPlayed >= 100,
  },
];

/** Every badge the current record has earned, in book order. */
export const unlockedAchievements = (stats: Statistics): readonly Achievement[] =>
  ACHIEVEMENTS.filter((achievement) => achievement.isUnlocked(stats));

/** Badges `next` earns that `prev` had not — the moment a settled hand should celebrate. */
export const newlyUnlockedAchievements = (
  prev: Statistics,
  next: Statistics,
): readonly Achievement[] =>
  ACHIEVEMENTS.filter((achievement) => !achievement.isUnlocked(prev) && achievement.isUnlocked(next));
