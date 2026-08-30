import { CATEGORY_ORDER, HandCategory } from './handRank';
import { ShowdownSettlement } from './showdownPayouts';

/**
 * The player's lifetime record, held as one immutable value and advanced by a
 * pure reducer. The store persists it whole; the reducer is the only place a
 * settled hand touches it, which keeps every derived number consistent.
 */

/** How many settled hands the history keeps, newest first. */
export const HISTORY_LIMIT = 20;

/** Best-first: a sweep took both battles, a bail took the half-back and left. */
export type ShowdownOutcome =
  | 'sweep'
  | 'beatBoth'
  | 'beatDealer'
  | 'beatTable'
  | 'push'
  | 'lose'
  | 'bail';

export interface HandHistoryEntry {
  /** The hand the player held at showdown (or when bailing). */
  readonly category: HandCategory;
  readonly outcome: ShowdownOutcome;
  readonly kickerStrike: boolean;
  readonly pressured: boolean;
  readonly forges: number;
  /** Chips won or lost over the whole hand, both battles together. */
  readonly net: number;
  /** Epoch milliseconds when the hand settled. */
  readonly at: number;
}

export interface Statistics {
  readonly handsPlayed: number;
  readonly handsWon: number;
  readonly biggestWin: number;
  readonly reUps: number;
  /** Consecutive winning hands right now. Pushes leave it untouched. */
  readonly currentStreak: number;
  readonly bestStreak: number;
  /** The strongest hand ever made, or null before the first deal settles. */
  readonly bestHandCategory: HandCategory | null;
  /** How many times each hand has been made. Absent categories are zero. */
  readonly categoryCounts: Partial<Record<HandCategory, number>>;
  readonly sweeps: number;
  readonly kickerStrikes: number;
  readonly bails: number;
  readonly pressures: number;
  readonly history: readonly HandHistoryEntry[];
}

export const defaultStatistics: Statistics = {
  handsPlayed: 0,
  handsWon: 0,
  biggestWin: 0,
  reUps: 0,
  currentStreak: 0,
  bestStreak: 0,
  bestHandCategory: null,
  categoryCounts: {},
  sweeps: 0,
  kickerStrikes: 0,
  bails: 0,
  pressures: 0,
  history: [],
};

/** Reads one settlement as the single line the scoreboard shows. */
export const outcomeOf = (settlement: ShowdownSettlement): ShowdownOutcome => {
  if (settlement.vsDealer === 'bailed') return 'bail';
  if (settlement.sweep) return 'sweep';
  if (settlement.vsDealer === 'win') return settlement.wonTable ? 'beatBoth' : 'beatDealer';
  if (settlement.wonTable) return 'beatTable';
  return settlement.vsDealer === 'push' ? 'push' : 'lose';
};

const strongerCategory = (a: HandCategory | null, b: HandCategory): HandCategory =>
  a !== null && CATEGORY_ORDER[a] >= CATEGORY_ORDER[b] ? a : b;

/**
 * Folds one settled hand into the record.
 *
 * A win extends the streak, a loss ends it, and a push leaves it be — the
 * convention every card room scoreboard uses. The hand always counts toward
 * the made-hands table, bailed or not: the player still held it.
 */
export const recordHandResult = (prev: Statistics, entry: HandHistoryEntry): Statistics => {
  const isWin = entry.net > 0;
  const isLoss = entry.net < 0;
  const currentStreak = isWin ? prev.currentStreak + 1 : isLoss ? 0 : prev.currentStreak;

  return {
    ...prev,
    handsPlayed: prev.handsPlayed + 1,
    handsWon: prev.handsWon + (isWin ? 1 : 0),
    biggestWin: Math.max(prev.biggestWin, entry.net),
    currentStreak,
    bestStreak: Math.max(prev.bestStreak, currentStreak),
    bestHandCategory: strongerCategory(prev.bestHandCategory, entry.category),
    categoryCounts: {
      ...prev.categoryCounts,
      [entry.category]: (prev.categoryCounts[entry.category] ?? 0) + 1,
    },
    sweeps: prev.sweeps + (entry.outcome === 'sweep' ? 1 : 0),
    kickerStrikes: prev.kickerStrikes + (entry.kickerStrike ? 1 : 0),
    bails: prev.bails + (entry.outcome === 'bail' ? 1 : 0),
    pressures: prev.pressures + (entry.pressured ? 1 : 0),
    history: [entry, ...prev.history].slice(0, HISTORY_LIMIT),
  };
};

/** Whole-percent win rate, or null before any hand has been played. */
export const winRatePercent = (stats: Statistics): number | null =>
  stats.handsPlayed === 0 ? null : Math.round((stats.handsWon / stats.handsPlayed) * 100);

const OUTCOMES: readonly ShowdownOutcome[] = [
  'sweep',
  'beatBoth',
  'beatDealer',
  'beatTable',
  'push',
  'lose',
  'bail',
];

/** How the old game's outcomes read in the new one's terms. */
const LEGACY_OUTCOME: Record<string, ShowdownOutcome> = {
  fold: 'bail',
  noQualify: 'beatDealer',
  win: 'beatDealer',
  lose: 'lose',
  push: 'push',
};

const migrateEntry = (raw: unknown): HandHistoryEntry | null => {
  if (typeof raw !== 'object' || raw === null) return null;
  const record = raw as Record<string, unknown>;
  const { category, outcome, net, at } = record;

  if (typeof category !== 'string' || !(category in CATEGORY_ORDER)) return null;
  if (typeof net !== 'number' || typeof at !== 'number') return null;
  const mapped =
    typeof outcome === 'string'
      ? OUTCOMES.includes(outcome as ShowdownOutcome)
        ? (outcome as ShowdownOutcome)
        : LEGACY_OUTCOME[outcome]
      : undefined;
  if (!mapped) return null;

  return {
    category: category as HandCategory,
    outcome: mapped,
    kickerStrike: record.kickerStrike === true,
    pressured: record.pressured === true,
    forges: typeof record.forges === 'number' ? record.forges : 0,
    net,
    at,
  };
};

/**
 * Brings whatever the store holds up to the current shape.
 *
 * The lifetime aggregates carry straight over — the player keeps their
 * record from the old game. History entries written by the old game are
 * translated (its fold is a bail, its wins are dealer-beats), anything
 * unreadable is dropped, and already-current data passes through untouched.
 */
export const migrateStatistics = (loaded: unknown): Statistics => {
  if (typeof loaded !== 'object' || loaded === null) return defaultStatistics;
  const record = loaded as Partial<Statistics> & { history?: unknown };

  const history = Array.isArray(record.history)
    ? record.history
        .map(migrateEntry)
        .filter((entry): entry is HandHistoryEntry => entry !== null)
        .slice(0, HISTORY_LIMIT)
    : [];

  return { ...defaultStatistics, ...record, history };
};
