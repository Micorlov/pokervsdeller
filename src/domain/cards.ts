/**
 * A 52-card deck, described by rank and suit only — every card is drawn in
 * code from these two values, so there are no image assets anywhere.
 */

export type Suit = 'spades' | 'hearts' | 'diamonds' | 'clubs';

/** 2–10 are themselves; 11 = J, 12 = Q, 13 = K, 14 = A (always high). */
export type Rank = number;

export interface Card {
  readonly rank: Rank;
  readonly suit: Suit;
  /** Stable identity, so entrance animations can key off a card. */
  readonly id: string;
}

export const SUITS: readonly Suit[] = ['spades', 'hearts', 'diamonds', 'clubs'];

export const RANKS: readonly Rank[] = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14];

export const ACE = 14;
export const KING = 13;
export const QUEEN = 12;
export const JACK = 11;

const SUIT_LETTER: Record<Suit, string> = { spades: 's', hearts: 'h', diamonds: 'd', clubs: 'c' };

const SUIT_SYMBOL: Record<Suit, string> = { spades: '♠', hearts: '♥', diamonds: '♦', clubs: '♣' };

const SUIT_WORD: Record<Suit, string> = {
  spades: 'spades',
  hearts: 'hearts',
  diamonds: 'diamonds',
  clubs: 'clubs',
};

const FACE_LABEL: Record<number, string> = { 11: 'J', 12: 'Q', 13: 'K', 14: 'A' };

const FACE_WORD: Record<number, string> = { 11: 'Jack', 12: 'Queen', 13: 'King', 14: 'Ace' };

export const cardId = (rank: Rank, suit: Suit): string => `${rank}${SUIT_LETTER[suit]}`;

export const createDeck = (): Card[] =>
  SUITS.flatMap((suit) => RANKS.map((rank) => ({ rank, suit, id: cardId(rank, suit) })));

/** The corner index: "A", "K", "10", "7". */
export const rankLabel = (rank: Rank): string => FACE_LABEL[rank] ?? String(rank);

export const suitSymbol = (suit: Suit): string => SUIT_SYMBOL[suit];

/** Hearts and diamonds print red; the card skin supplies the two inks. */
export const isRedSuit = (suit: Suit): boolean => suit === 'hearts' || suit === 'diamonds';

/** Spoken form for screen readers — "Ace of spades", "Seven of hearts". */
export const cardName = (card: Card): string => {
  const rankWord = FACE_WORD[card.rank] ?? String(card.rank);
  return `${rankWord} of ${SUIT_WORD[card.suit]}`;
};
