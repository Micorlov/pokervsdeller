import { Card, Rank } from './cards';
import { CATEGORY_ORDER, evaluateHand } from './handRank';
import { Rng } from './rng';
import { FORGE_LIMIT, PRESSURE_ROUND, ShowdownMove } from './showdownPayouts';
import { handStrengthScore, partialThreat } from './threat';

/**
 * The two regulars at the table.
 *
 * Each one weighs its hand against the dealer's visible climb and then
 * deviates by `tightness`: a rock bails early and pressures only certainties,
 * a gambler rides bad hands and doubles light. The deviation is what makes
 * the seats readable as people rather than copies of the same solver.
 */

export interface BotAvatar {
  readonly kind: 'initial' | 'emoji';
  readonly value: string;
  readonly gradient?: readonly [string, string];
}

export interface BotPersona {
  readonly id: string;
  readonly name: string;
  readonly avatar: BotAvatar;
  /** 0 = calls anything, 1 = folds all but the certainties. */
  readonly tightness: number;
  /** The one-line read shown under the seat on the rules screen. */
  readonly style: string;
}

/**
 * Two regulars, spread tight to loose. The table seats one of each, so the
 * spread matters more than the headcount.
 */
export const BOT_PERSONAS: readonly BotPersona[] = [
  { id: 'duke', name: 'Duke', avatar: { kind: 'emoji', value: '🎩' }, tightness: 0.88, style: 'Waits all night for a pair.' },
  { id: 'gus', name: 'Gus', avatar: { kind: 'emoji', value: '🎲' }, tightness: 0.12, style: 'Here for the action.' },
];

/** How far a persona's tightness drifts hand to hand, so nobody is a metronome. */
export const TIGHTNESS_JITTER = 0.18;

const clamp01 = (value: number): number => Math.min(1, Math.max(0, value));

/** How far behind a seat lets itself fall before bailing, at tightness 0. */
const BAIL_MARGIN_BASE = 0.32;
/** Tight players bail that much earlier when behind. */
const BAIL_MARGIN_TIGHTNESS = 0.22;
/** Bail probability once behind: base plus tightness. */
const BAIL_ROLL_BASE = 0.35;
const BAIL_ROLL_TIGHTNESS = 0.5;
/** How often a drawy hand trades a card in: base plus looseness. */
const FORGE_ROLL_BASE = 0.2;
const FORGE_ROLL_LOOSENESS = 0.45;
/** Margin needed to double the dealer stake, at tightness 0. */
const PRESSURE_MARGIN_BASE = 0.05;
/** Tight players want that much more edge before pressuring. */
const PRESSURE_MARGIN_TIGHTNESS = 0.25;
/** A low card at or under this is worth trading away from an unmade hand. */
const DEAD_CARD_RANK = 6;

const HOLD: ShowdownMove = { kind: 'hold', pressure: false };

const countsByRank = (cards: readonly Card[]): Map<Rank, number> => {
  const counts = new Map<Rank, number>();
  cards.forEach((card) => counts.set(card.rank, (counts.get(card.rank) ?? 0) + 1));
  return counts;
};

/**
 * Which card a bot marks as its kicker. Deterministic, zero rng draws: the
 * highest card outside the made core, because the pairs and trips are doing
 * their own work and the spare card is the one free bet on the dealer's
 * final rank. A pat hand just nominates its highest card.
 */
export const chooseBotKicker = (cards: readonly Card[]): number => {
  const counts = countsByRank(cards);
  const spare = cards
    .map((card, index) => ({ card, index }))
    .filter(({ card }) => counts.get(card.rank) === 1);
  const pool = spare.length > 0 ? spare : cards.map((card, index) => ({ card, index }));
  return pool.reduce((best, entry) => (entry.card.rank > best.card.rank ? entry : best)).index;
};

/**
 * Which card leaves the hand on a forge. Deterministic, zero rng draws: the
 * least useful card that is not the kicker — unpaired before paired, off the
 * dominant suit before on it, low before high.
 */
export const chooseForgeDiscard = (cards: readonly Card[], kickerIndex: number): number => {
  const counts = countsByRank(cards);
  const suitCounts = new Map<string, number>();
  cards.forEach((card) => suitCounts.set(card.suit, (suitCounts.get(card.suit) ?? 0) + 1));
  const dominantCount = Math.max(...suitCounts.values());

  const keepScore = (card: Card): number =>
    card.rank +
    ((counts.get(card.rank) ?? 0) > 1 ? 100 : 0) +
    (dominantCount >= 3 && suitCounts.get(card.suit) === dominantCount ? 50 : 0);

  return cards
    .map((card, index) => ({ card, index }))
    .filter(({ index }) => index !== kickerIndex)
    .reduce((worst, entry) => (keepScore(entry.card) < keepScore(worst.card) ? entry : worst)).index;
};

/** A hand worth trading into: unmade, with a live draw or a dead low card. */
const isDrawyHand = (cards: readonly Card[]): boolean => {
  const category = evaluateHand(cards).category;
  if (CATEGORY_ORDER[category] >= CATEGORY_ORDER.twoPair) return false;

  const suitCounts = new Map<string, number>();
  cards.forEach((card) => suitCounts.set(card.suit, (suitCounts.get(card.suit) ?? 0) + 1));
  if (Math.max(...suitCounts.values()) >= 4) return true;

  const unique = [...new Set(cards.map((card) => card.rank))].sort((a, b) => b - a);
  for (let i = 0; i + 3 < unique.length; i += 1) {
    if (unique[i] - unique[i + 3] <= 4) return true;
  }

  return Math.min(...cards.map((card) => card.rank)) <= DEAD_CARD_RANK;
};

export interface ShowdownBotSeat {
  readonly cards: readonly Card[];
  readonly kickerIndex: number | null;
  readonly forgesUsed: number;
  readonly pressured: boolean;
  readonly stack: number;
}

/**
 * A seat's move for one action round of the climb.
 *
 * Draws exactly twice from `rng` — tightness drift, then the action roll —
 * on every branch, so a hand replays identically from its seed. The margin
 * between the seat's made strength and the dealer's visible threat drives
 * everything: far enough behind an unmade hand bails, a drawy hand trades a
 * card, and in the pressure round a comfortable lead doubles the stake.
 * Made hands of two pair or better never bail — there is no version of this
 * game where that is right either.
 */
export const decideShowdownMove = (
  seat: ShowdownBotSeat,
  dealerCards: readonly Card[],
  round: number,
  ante: number,
  persona: BotPersona,
  rng: Rng,
): ShowdownMove => {
  const tightness = clamp01(persona.tightness + (rng() - 0.5) * TIGHTNESS_JITTER);
  const roll = rng();

  if (seat.kickerIndex === null) {
    throw new Error('A bot cannot act before its kicker is set');
  }

  const rank = evaluateHand(seat.cards);
  const margin = handStrengthScore(rank) - partialThreat(dealerCards);
  const madeHand = CATEGORY_ORDER[rank.category] >= CATEGORY_ORDER.twoPair;

  const bailMargin = -(BAIL_MARGIN_BASE - BAIL_MARGIN_TIGHTNESS * tightness);
  if (!madeHand && margin < bailMargin && roll < BAIL_ROLL_BASE + BAIL_ROLL_TIGHTNESS * tightness) {
    return { kind: 'bail', pressure: false };
  }

  const wantsPressure =
    round === PRESSURE_ROUND &&
    !seat.pressured &&
    margin > PRESSURE_MARGIN_BASE + PRESSURE_MARGIN_TIGHTNESS * tightness;

  const forgeRate = FORGE_ROLL_BASE + FORGE_ROLL_LOOSENESS * (1 - tightness);
  const canForge =
    seat.forgesUsed < FORGE_LIMIT && isDrawyHand(seat.cards) && seat.cards.length > 1;
  if (canForge && roll < forgeRate) {
    const pressure = wantsPressure && seat.stack >= ante * 2;
    if (seat.stack >= ante * (pressure ? 2 : 1)) {
      return {
        kind: 'forge',
        forgeDiscardIndex: chooseForgeDiscard(seat.cards, seat.kickerIndex),
        pressure,
      };
    }
  }

  return wantsPressure && seat.stack >= ante ? { kind: 'hold', pressure: true } : HOLD;
};
