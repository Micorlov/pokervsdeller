import { Ante, minimumToPlay } from './bankroll';
import { BOT_PERSONAS, BotAvatar, BotPersona } from './bots';
import { Card, createDeck } from './cards';
import { HAND_SIZE, HandRank, evaluateHand } from './handRank';
import { Rng, shuffle } from './rng';
import {
  FORGE_LIMIT,
  PRESSURE_ROUND,
  SettleInput,
  ShowdownMove,
  ShowdownSettlement,
  bailRefund,
  settleSeats,
} from './showdownPayouts';

/**
 * One hand of Showdown, as a set of pure reducers.
 *
 * The shape mirrors the old engine deliberately: no timers, no presentation
 * state, every chip movement in exactly one function. What is new is the
 * clock inside the hand — the dealer's cards arrive one at a time, and the
 * table acts between them, so the phase loops kicker → actions → actions →
 * … → reveal instead of running straight through.
 */

export type ShowdownPhase = 'ante' | 'dealing' | 'kicker' | 'actions' | 'reveal' | 'settled';

/** Four regulars and you — derived so the roster and the felt cannot drift. */
export const BOT_COUNT = BOT_PERSONAS.length;

export const SEAT_COUNT = BOT_COUNT + 1;

/** The human always sits in the first seat. */
export const HUMAN_SEAT = 0;

/** Bots keep a session stack and quietly buy back in when it runs dry. */
export const BOT_STARTING_STACK = 500;

export interface ShowdownSeatState {
  readonly id: string;
  readonly name: string;
  readonly isHuman: boolean;
  readonly avatar: BotAvatar;
  /** Null for the human seat — only bots are played by a persona. */
  readonly persona: BotPersona | null;
  /** Chips in front of the seat. Everything committed has already left it. */
  readonly stack: number;
  readonly cards: readonly Card[];
  /** Which card is the kicker. Set during the kicker phase, then immutable. */
  readonly kickerIndex: number | null;
  readonly forgesUsed: number;
  readonly pressured: boolean;
  /** Chips this seat has put into the table pot. */
  readonly tableContrib: number;
  /** Chips this seat has staked against the house. */
  readonly dealerStake: number;
  readonly status: 'active' | 'bailed';
  /** The half-back a bail already returned to the stack. Zero otherwise. */
  readonly refund: number;
  /** This action round's move, cleared when the next dealer card lands. */
  readonly lastMove: ShowdownMove | null;
  readonly settlement: ShowdownSettlement | null;
}

export interface ShowdownRoundState {
  readonly phase: ShowdownPhase;
  readonly handNumber: number;
  readonly ante: Ante;
  readonly seats: readonly ShowdownSeatState[];
  /** The dealer's climb, all face up, oldest first. */
  readonly dealerCards: readonly Card[];
  /** The undealt remainder — dealer draws and forge replacements both come from here. */
  readonly stock: readonly Card[];
  readonly tablePot: number;
  /** Chips the house kept (positive) or paid out (negative). Bail refunds land here mid-hand. */
  readonly houseNet: number;
}

const clearedSeat = (seat: ShowdownSeatState): ShowdownSeatState => ({
  ...seat,
  cards: [],
  kickerIndex: null,
  forgesUsed: 0,
  pressured: false,
  tableContrib: 0,
  dealerStake: 0,
  status: 'active',
  refund: 0,
  lastMove: null,
  settlement: null,
});

/** The human's seat, plus the five regulars, before any cards are out. */
export const createShowdownTable = (
  humanStack: number,
  ante: Ante,
  humanName = 'You',
): ShowdownRoundState => ({
  phase: 'ante',
  handNumber: 0,
  ante,
  seats: [
    clearedSeat({
      id: 'you',
      name: humanName,
      isHuman: true,
      avatar: { kind: 'initial', value: humanName.charAt(0).toUpperCase() },
      persona: null,
      stack: humanStack,
      cards: [],
      kickerIndex: null,
      forgesUsed: 0,
      pressured: false,
      tableContrib: 0,
      dealerStake: 0,
      status: 'active',
      refund: 0,
      lastMove: null,
      settlement: null,
    }),
    ...BOT_PERSONAS.map((persona) =>
      clearedSeat({
        id: persona.id,
        name: persona.name,
        isHuman: false,
        avatar: persona.avatar,
        persona,
        stack: BOT_STARTING_STACK,
        cards: [],
        kickerIndex: null,
        forgesUsed: 0,
        pressured: false,
        tableContrib: 0,
        dealerStake: 0,
        status: 'active',
        refund: 0,
        lastMove: null,
        settlement: null,
      }),
    ),
  ],
  dealerCards: [],
  stock: [],
  tablePot: 0,
  houseNet: 0,
});

/**
 * Antes everyone up and deals the six player hands.
 *
 * Both antes leave every stack at once — one chip stack into the table pot,
 * one staked against the house. The dealer gets nothing yet; the rest of the
 * shuffled deck is kept as the stock the climb and every forge draw from.
 */
export const startShowdownRound = (
  state: ShowdownRoundState,
  rng: Rng,
  ante: Ante = state.ante,
): ShowdownRoundState => {
  const rebought = state.seats.map((seat) =>
    !seat.isHuman && seat.stack < minimumToPlay(ante)
      ? { ...clearedSeat(seat), stack: BOT_STARTING_STACK }
      : clearedSeat(seat),
  );

  const short = rebought.find((seat) => seat.stack < ante * 2);
  if (short) {
    throw new Error(`${short.name} cannot cover the two ${ante} antes with ${short.stack}`);
  }

  const deck = shuffle(createDeck(), rng);
  const hands: Card[][] = Array.from({ length: SEAT_COUNT }, () => []);
  let cursor = 0;
  for (let pass = 0; pass < HAND_SIZE; pass += 1) {
    for (let seat = 0; seat < SEAT_COUNT; seat += 1) {
      hands[seat].push(deck[cursor]);
      cursor += 1;
    }
  }

  return {
    phase: 'dealing',
    handNumber: state.handNumber + 1,
    ante,
    houseNet: 0,
    dealerCards: [],
    stock: deck.slice(cursor),
    tablePot: ante * SEAT_COUNT,
    seats: rebought.map((seat, index) => ({
      ...seat,
      stack: seat.stack - ante * 2,
      tableContrib: ante,
      dealerStake: ante,
      cards: hands[index],
    })),
  };
};

/** The last card has landed; everyone picks a kicker before the climb starts. */
export const finishDealing = (state: ShowdownRoundState): ShowdownRoundState =>
  state.phase === 'dealing' ? { ...state, phase: 'kicker' } : state;

/** Marks one card as the seat's kicker. Re-picking is fine until the climb starts. */
export const setKicker = (
  state: ShowdownRoundState,
  seatIndex: number,
  cardIndex: number,
): ShowdownRoundState => {
  const seat = state.seats[seatIndex];
  if (!seat) throw new Error(`No seat at index ${seatIndex}`);
  if (state.phase !== 'kicker') return state;
  if (cardIndex < 0 || cardIndex >= seat.cards.length) {
    throw new Error(`Kicker index ${cardIndex} is outside the hand`);
  }
  return {
    ...state,
    seats: state.seats.map((current, index) =>
      index === seatIndex ? { ...current, kickerIndex: cardIndex } : current,
    ),
  };
};

export const allKickersSet = (state: ShowdownRoundState): boolean =>
  state.seats.every((seat) => seat.kickerIndex !== null);

/** Which action round the table is in: the number of dealer cards showing. */
export const currentActionRound = (state: ShowdownRoundState): number => state.dealerCards.length;

export const allActed = (state: ShowdownRoundState): boolean =>
  state.seats.every((seat) => seat.status === 'bailed' || seat.lastMove !== null);

/**
 * The dealer draws.
 *
 * Legal from the kicker phase once every kicker is locked, and from an action
 * round once every active seat has moved. The fifth card ends the climb and
 * opens the reveal; every earlier card opens a fresh action round.
 */
export const dealDealerCard = (state: ShowdownRoundState): ShowdownRoundState => {
  if (state.phase !== 'kicker' && state.phase !== 'actions') return state;
  if (state.phase === 'kicker' && !allKickersSet(state)) {
    throw new Error('The dealer cannot draw before every kicker is set');
  }
  if (state.phase === 'actions' && !allActed(state)) {
    throw new Error('The dealer cannot draw while seats still owe a move');
  }

  const card = state.stock[0];
  if (!card) throw new Error('The stock ran dry mid-climb');

  const dealerCards = [...state.dealerCards, card];
  const stock = state.stock.slice(1);

  if (dealerCards.length === HAND_SIZE) {
    return { ...state, dealerCards, stock, phase: 'reveal' };
  }

  return {
    ...state,
    dealerCards,
    stock,
    phase: 'actions',
    seats: state.seats.map((seat) =>
      seat.status === 'active' ? { ...seat, lastMove: null } : seat,
    ),
  };
};

const validateMove = (
  state: ShowdownRoundState,
  seat: ShowdownSeatState,
  move: ShowdownMove,
): void => {
  if (move.pressure && move.kind === 'bail') {
    throw new Error('A bail cannot carry pressure');
  }
  if (move.pressure && currentActionRound(state) !== PRESSURE_ROUND) {
    throw new Error(`Pressure is only legal after the dealer's card ${PRESSURE_ROUND}`);
  }
  if (move.kind === 'forge') {
    const discard = move.forgeDiscardIndex;
    if (discard === undefined || discard < 0 || discard >= seat.cards.length) {
      throw new Error('A forge needs a card to trade away');
    }
    if (discard === seat.kickerIndex) {
      throw new Error('The kicker cannot be forged away');
    }
    if (seat.forgesUsed >= FORGE_LIMIT) {
      throw new Error(`Seat ${seat.id} has no forges left`);
    }
    if (state.stock.length === 0) {
      throw new Error('The stock ran dry mid-forge');
    }
  }
  const cost = (move.kind === 'forge' ? state.ante : 0) + (move.pressure ? state.ante : 0);
  if (seat.stack < cost) {
    throw new Error(`Seat ${seat.id} cannot cover a ${cost} move with ${seat.stack}`);
  }
};

/**
 * Records one seat's move for the current action round, moving chips as it
 * goes: a forge fee sweetens the table pot, pressure doubles the dealer
 * stake, and a bail hands half of everything committed straight back.
 */
export const applyMove = (
  state: ShowdownRoundState,
  seatIndex: number,
  move: ShowdownMove,
): ShowdownRoundState => {
  const seat = state.seats[seatIndex];
  if (!seat) throw new Error(`No seat at index ${seatIndex}`);
  if (state.phase !== 'actions') return state;
  if (seat.status === 'bailed' || seat.lastMove !== null) return state;

  validateMove(state, seat, move);

  if (move.kind === 'bail') {
    const refund = bailRefund(seat.tableContrib + seat.dealerStake);
    // The house pays the half-back; the abandoned dealer stake pours into the
    // table pot on the spot, so the pot grows every time someone quits.
    return {
      ...state,
      houseNet: state.houseNet - refund,
      tablePot: state.tablePot + seat.dealerStake,
      seats: state.seats.map((current, index) =>
        index === seatIndex
          ? {
              ...current,
              status: 'bailed' as const,
              refund,
              stack: current.stack + refund,
              lastMove: move,
            }
          : current,
      ),
    };
  }

  const pressureCost = move.pressure ? state.ante : 0;
  const forgeCost = move.kind === 'forge' ? state.ante : 0;

  const forged =
    move.kind === 'forge'
      ? {
          cards: seat.cards.map((card, index) =>
            index === move.forgeDiscardIndex ? state.stock[0] : card,
          ),
          forgesUsed: seat.forgesUsed + 1,
        }
      : { cards: seat.cards, forgesUsed: seat.forgesUsed };

  return {
    ...state,
    stock: move.kind === 'forge' ? state.stock.slice(1) : state.stock,
    tablePot: state.tablePot + forgeCost,
    seats: state.seats.map((current, index) =>
      index === seatIndex
        ? {
            ...current,
            ...forged,
            stack: current.stack - forgeCost - pressureCost,
            tableContrib: current.tableContrib + forgeCost,
            dealerStake: current.dealerStake + pressureCost,
            pressured: current.pressured || move.pressure,
            lastMove: move,
          }
        : current,
    ),
  };
};

/** The dealer's finished hand, or null while the climb is still on. */
export const dealerRankOf = (state: ShowdownRoundState): HandRank | null =>
  state.dealerCards.length === HAND_SIZE ? evaluateHand(state.dealerCards) : null;

/**
 * Pays the table out on both fronts.
 *
 * Survivors get back their stakes and contributions plus their settlement
 * net; bailed seats were paid on the way out and get nothing more. The house
 * ledger picks up the settle-step delta on top of the refunds it already paid.
 */
export const settleShowdown = (state: ShowdownRoundState): ShowdownRoundState => {
  if (state.phase !== 'reveal') return state;
  const dealerRank = dealerRankOf(state);
  const finalCard = state.dealerCards[HAND_SIZE - 1];
  if (!dealerRank || !finalCard) throw new Error('Cannot settle before the climb is finished');

  const inputs: SettleInput[] = state.seats.map((seat) => {
    if (seat.kickerIndex === null) {
      throw new Error(`Seat ${seat.id} reached the settle without a kicker`);
    }
    return {
      rank: seat.status === 'bailed' ? null : evaluateHand(seat.cards),
      kickerRank: seat.cards[seat.kickerIndex].rank,
      dealerStake: seat.dealerStake,
      tableContrib: seat.tableContrib,
      pressured: seat.pressured,
      bailed: seat.status === 'bailed',
      refund: seat.refund,
    };
  });

  const { settlements, houseDelta } = settleSeats(
    inputs,
    dealerRank,
    finalCard,
    state.tablePot,
    state.ante,
  );

  return {
    ...state,
    phase: 'settled',
    tablePot: 0,
    houseNet: state.houseNet + houseDelta,
    seats: state.seats.map((seat, index) => {
      const settlement = settlements[index];
      const returned =
        seat.status === 'bailed' ? 0 : settlement.net + seat.dealerStake + seat.tableContrib;
      return { ...seat, settlement, stack: seat.stack + returned };
    }),
  };
};

export const humanSeat = (state: ShowdownRoundState): ShowdownSeatState => state.seats[HUMAN_SEAT];

export const activeSeats = (state: ShowdownRoundState): readonly ShowdownSeatState[] =>
  state.seats.filter((seat) => seat.status === 'active');
