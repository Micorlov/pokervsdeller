import { ACE, Card } from './cards';
import { CATEGORY_ORDER, HAND_SIZE, HandRank, evaluateHand } from './handRank';

/**
 * How scary a hand looks before it is finished.
 *
 * The dealer's hand grows one face-up card at a time, so the table spends most
 * of the round staring at 1–4 cards that `evaluateHand` refuses to rank. This
 * module scores that partial hand on a 0..1 scale — made components plus
 * drawing potential — so bots can weigh their moves and the threat meter has
 * something honest to show. At five cards it defers to the real evaluator.
 */

const clamp01 = (value: number): number => Math.min(1, Math.max(0, value));

/** Weight of the strongest made component among the visible cards. */
const MADE_QUADS = 0.85;
const MADE_TRIPS = 0.6;
const MADE_TWO_PAIR = 0.45;
const MADE_PAIR = 0.3;

/** High-card menace: an ace showing is worth this much on its own. */
const HIGH_CARD_WEIGHT = 0.15;

/** Per-card weight of an unbroken flush draw among the visible cards. */
const FLUSH_DRAW_WEIGHT = 0.06;
/** Per-card weight of ranks tight enough to still make a straight. */
const STRAIGHT_DRAW_WEIGHT = 0.05;
/** Weight of the average rank — a table of paint threatens more than rags. */
const RANK_DENSITY_WEIGHT = 0.1;

const rankCounts = (cards: readonly Card[]): number[] => {
  const counts = new Map<number, number>();
  cards.forEach((card) => counts.set(card.rank, (counts.get(card.rank) ?? 0) + 1));
  return [...counts.values()].sort((a, b) => b - a);
};

const madeComponent = (cards: readonly Card[]): number => {
  const counts = rankCounts(cards);
  if (counts[0] === 4) return MADE_QUADS;
  if (counts[0] === 3) return MADE_TRIPS;
  if (counts[0] === 2) return counts[1] === 2 ? MADE_TWO_PAIR : MADE_PAIR;
  return 0;
};

/** Draw weight for cards that could still become a flush or straight. */
const drawComponent = (cards: readonly Card[]): number => {
  if (cards.length < 2) return 0;

  const allSuited = cards.every((card) => card.suit === cards[0].suit);
  const flush = allSuited ? FLUSH_DRAW_WEIGHT * cards.length : 0;

  const ranks = [...new Set(cards.map((card) => card.rank))].sort((a, b) => b - a);
  const allUnique = ranks.length === cards.length;
  const tightSpan = allUnique && ranks[0] - ranks[ranks.length - 1] <= HAND_SIZE - 1;
  const straight = tightSpan ? STRAIGHT_DRAW_WEIGHT * cards.length : 0;

  return flush + straight;
};

/**
 * A finished hand's strength on the same 0..1 scale, monotone with
 * `compareHands` across categories and with the leading tiebreak inside one.
 */
export const handStrengthScore = (rank: HandRank): number => {
  const category = CATEGORY_ORDER[rank.category] / 10;
  const lead = (rank.tiebreak[0] ?? 0) / ACE / 10;
  return clamp01(category + lead);
};

/**
 * Scores a partial hand of 0–5 cards.
 *
 * Made pairs and trips carry the score; unfinished flush and straight draws
 * and general rank density add potential, scaled down as the remaining draws
 * run out. An empty hand is no threat at all, and a complete hand is scored by
 * the real evaluator so the meter never disagrees with the showdown.
 */
export const partialThreat = (cards: readonly Card[]): number => {
  if (cards.length === 0) return 0;
  if (cards.length === HAND_SIZE) return handStrengthScore(evaluateHand(cards));

  const highCard = (Math.max(...cards.map((card) => card.rank)) / ACE) * HIGH_CARD_WEIGHT;
  const averageRank = cards.reduce((sum, card) => sum + card.rank, 0) / cards.length;
  const density = (averageRank / ACE) * RANK_DENSITY_WEIGHT;

  const drawsLeft = (HAND_SIZE - cards.length) / (HAND_SIZE - 1);
  const potential = (drawComponent(cards) + density) * drawsLeft;

  return clamp01(madeComponent(cards) + highCard + potential);
};
