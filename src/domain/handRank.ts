import { ACE, Card, Rank } from './cards';

/**
 * Five-card hand evaluation.
 *
 * A rank is a category plus a tiebreak list read left to right, so two hands
 * of the same category are compared exactly the way a dealer would read them
 * out: the trips rank first, then the kickers in descending order.
 */

export type HandCategory =
  | 'highCard'
  | 'pair'
  | 'twoPair'
  | 'trips'
  | 'straight'
  | 'flush'
  | 'fullHouse'
  | 'quads'
  | 'straightFlush'
  | 'royalFlush';

/** Strength order. Only the relative values matter. */
export const CATEGORY_ORDER: Record<HandCategory, number> = {
  highCard: 0,
  pair: 1,
  twoPair: 2,
  trips: 3,
  straight: 4,
  flush: 5,
  fullHouse: 6,
  quads: 7,
  straightFlush: 8,
  royalFlush: 9,
};

const CATEGORY_NAME: Record<HandCategory, string> = {
  highCard: 'High Card',
  pair: 'Pair',
  twoPair: 'Two Pair',
  trips: 'Three of a Kind',
  straight: 'Straight',
  flush: 'Flush',
  fullHouse: 'Full House',
  quads: 'Four of a Kind',
  straightFlush: 'Straight Flush',
  royalFlush: 'Royal Flush',
};

export interface HandRank {
  readonly category: HandCategory;
  /** Descending significance — compared element by element. */
  readonly tiebreak: readonly number[];
}

export const HAND_SIZE = 5;

/** The wheel A-2-3-4-5 plays as a five-high straight, the lowest one there is. */
const WHEEL_HIGH = 5;

interface RankGroup {
  readonly rank: Rank;
  readonly count: number;
}

/** Groups by rank, ordered by count first and rank second — the reading order. */
const groupByRank = (cards: readonly Card[]): RankGroup[] => {
  const counts = new Map<Rank, number>();
  cards.forEach((card) => counts.set(card.rank, (counts.get(card.rank) ?? 0) + 1));
  return [...counts.entries()]
    .map(([rank, count]) => ({ rank, count }))
    .sort((a, b) => (b.count - a.count) || (b.rank - a.rank));
};

/** Returns the straight's high card, or null. Handles the wheel. */
const straightHigh = (ranks: readonly Rank[]): Rank | null => {
  const unique = [...new Set(ranks)].sort((a, b) => b - a);
  if (unique.length !== HAND_SIZE) return null;
  if (unique[0] - unique[4] === 4) return unique[0];
  const isWheel = unique[0] === ACE && unique[1] === 5 && unique[4] === 2;
  return isWheel ? WHEEL_HIGH : null;
};

/**
 * Reads a five-card hand. Throws on any other length: a mis-sized hand is a
 * bug in the dealer, not a weak hand, and must not be silently ranked.
 */
export const evaluateHand = (cards: readonly Card[]): HandRank => {
  if (cards.length !== HAND_SIZE) {
    throw new Error(`evaluateHand expects ${HAND_SIZE} cards, received ${cards.length}`);
  }

  const groups = groupByRank(cards);
  const isFlush = cards.every((card) => card.suit === cards[0].suit);
  const high = straightHigh(cards.map((card) => card.rank));

  if (isFlush && high !== null) {
    return high === ACE
      ? { category: 'royalFlush', tiebreak: [ACE] }
      : { category: 'straightFlush', tiebreak: [high] };
  }

  const shape = groups.map((group) => group.count).join('');
  const ranks = groups.map((group) => group.rank);

  if (shape === '41') return { category: 'quads', tiebreak: ranks };
  if (shape === '32') return { category: 'fullHouse', tiebreak: ranks };
  if (isFlush) return { category: 'flush', tiebreak: [...cards.map((c) => c.rank)].sort((a, b) => b - a) };
  if (high !== null) return { category: 'straight', tiebreak: [high] };
  if (shape === '311') return { category: 'trips', tiebreak: ranks };
  if (shape === '221') return { category: 'twoPair', tiebreak: ranks };
  if (shape === '2111') return { category: 'pair', tiebreak: ranks };
  return { category: 'highCard', tiebreak: ranks };
};

/** Positive when `a` beats `b`, negative when it loses, zero on a true tie. */
export const compareHands = (a: HandRank, b: HandRank): number => {
  const byCategory = CATEGORY_ORDER[a.category] - CATEGORY_ORDER[b.category];
  if (byCategory !== 0) return byCategory;
  for (let i = 0; i < Math.max(a.tiebreak.length, b.tiebreak.length); i += 1) {
    const diff = (a.tiebreak[i] ?? 0) - (b.tiebreak[i] ?? 0);
    if (diff !== 0) return diff;
  }
  return 0;
};

export const handName = (rank: HandRank): string => CATEGORY_NAME[rank.category];
