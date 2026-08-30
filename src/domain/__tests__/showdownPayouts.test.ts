import { Card, Suit, cardId } from '../cards';
import { evaluateHand } from '../handRank';
import { SettleInput, ShowdownSettlement, bailRefund, settleSeats } from '../showdownPayouts';

const SUIT_OF: Record<string, Suit> = { s: 'spades', h: 'hearts', d: 'diamonds', c: 'clubs' };
const RANK_OF: Record<string, number> = { T: 10, J: 11, Q: 12, K: 13, A: 14 };

/** "As Kd 7h 4c 2s" → cards. */
const hand = (spec: string): Card[] =>
  spec.split(' ').map((token) => {
    const rank = RANK_OF[token[0]] ?? Number(token[0]);
    const suit = SUIT_OF[token[1]];
    return { rank, suit, id: cardId(rank, suit) };
  });

const ANTE = 10;
/** The dealer shows down a pair of nines; the last card off the deck is the 2♣. */
const DEALER_RANK = evaluateHand(hand('9c 9d 5h 3s 2c'));
const FINAL_CARD = hand('2c')[0];

const TRIPS = 'Qc Qd Qh 3s 2s';
const TRIPS_TWIN = 'Qs Qh Qd 3h 2h';
const ACE_HIGH = 'Ac Kd 9h 5s 4c';
const KING_HIGH = 'Kc Qd 9d 5c 4d';
const DEALER_TWIN = '9h 9s 5d 3c 2d';

const seatOf = (overrides: Partial<SettleInput> = {}): SettleInput => ({
  rank: null,
  kickerRank: 7,
  dealerStake: ANTE,
  tableContrib: ANTE,
  pressured: false,
  bailed: false,
  refund: 0,
  ...overrides,
});

const survivor = (spec: string, overrides: Partial<SettleInput> = {}): SettleInput =>
  seatOf({ rank: evaluateHand(hand(spec)), ...overrides });

/** Chips created must equal chips destroyed, in every single scenario. */
const expectConserved = (
  seats: readonly SettleInput[],
  settlements: readonly ShowdownSettlement[],
  houseDelta: number,
) => {
  const refunds = seats.reduce((total, seat) => total + seat.refund, 0);
  const nets = settlements.reduce((total, settlement) => total + settlement.net, 0);
  expect(houseDelta).toBe(refunds - nets);
};

const settle = (seats: readonly SettleInput[], tablePot: number) => {
  const result = settleSeats(seats, DEALER_RANK, FINAL_CARD, tablePot, ANTE);
  expectConserved(seats, result.settlements, result.houseDelta);
  return result;
};

describe('bailRefund', () => {
  it('returns half of everything committed, rounded down', () => {
    expect(bailRefund(20)).toBe(10);
    expect(bailRefund(25)).toBe(12);
    expect(bailRefund(0)).toBe(0);
  });
});

describe('the dealer battle', () => {
  it('pays a plain dealer-beat at even money', () => {
    const seats = [survivor(TRIPS), survivor(TRIPS_TWIN)];
    const { settlements } = settle(seats, 20);
    settlements.forEach((s) => {
      expect(s.vsDealer).toBe('win');
      expect(s.dealerDelta).toBe(ANTE);
    });
  });

  it('takes the stake on a loss and pushes a tie', () => {
    const seats = [survivor(ACE_HIGH), survivor(DEALER_TWIN)];
    const { settlements } = settle(seats, 20);
    expect(settlements[0].vsDealer).toBe('lose');
    expect(settlements[0].dealerDelta).toBe(-ANTE);
    expect(settlements[1].vsDealer).toBe('push');
    expect(settlements[1].dealerDelta).toBe(0);
  });

  it('pays a pressured winning stake at three to one', () => {
    const seats = [
      survivor(TRIPS, { pressured: true, dealerStake: ANTE * 2 }),
      survivor(TRIPS_TWIN),
    ];
    const { settlements } = settle(seats, 20);
    expect(settlements[0].dealerDelta).toBe(ANTE * 2 * 3);
    expect(settlements[1].dealerDelta).toBe(ANTE);
  });
});

describe('the kicker strike', () => {
  it('doubles the dealer winnings when the kicker matches the last card', () => {
    const seats = [survivor(TRIPS, { kickerRank: 2 }), survivor(TRIPS_TWIN)];
    const { settlements } = settle(seats, 20);
    expect(settlements[0].kickerStrike).toBe(true);
    expect(settlements[0].dealerDelta).toBe(ANTE * 2);
    expect(settlements[1].kickerStrike).toBe(false);
  });

  it('stacks with pressure for six times the stake', () => {
    const seats = [
      survivor(TRIPS, { kickerRank: 2, pressured: true, dealerStake: ANTE * 2 }),
      survivor(TRIPS_TWIN),
    ];
    const { settlements } = settle(seats, 20);
    expect(settlements[0].dealerDelta).toBe(ANTE * 2 * 3 * 2);
  });

  it('never fires on a loss or a push', () => {
    const seats = [
      survivor(ACE_HIGH, { kickerRank: 2 }),
      survivor(DEALER_TWIN, { kickerRank: 2 }),
    ];
    const { settlements } = settle(seats, 20);
    settlements.forEach((s) => {
      expect(s.kickerStrike).toBe(false);
    });
  });
});

describe('the table battle', () => {
  it('hands the pot plus the beaten stake to the single best hand', () => {
    const seats = [survivor(TRIPS), survivor(ACE_HIGH)];
    const { settlements } = settle(seats, 20);
    expect(settlements[0].wonTable).toBe(true);
    expect(settlements[0].outright).toBe(true);
    // The loser's stake joined the pot: 20 on the table + 10 beaten stake.
    expect(settlements[0].tableDelta).toBe(30 - ANTE);
    expect(settlements[1].wonTable).toBe(false);
    expect(settlements[1].tableDelta).toBe(-ANTE);
  });

  it('splits a tied pot, spare chips to the earlier seat', () => {
    // The bailed seat's stake was poured into the pot when it quit: 25 + 10.
    const seats = [
      survivor(TRIPS),
      survivor(TRIPS_TWIN),
      seatOf({ bailed: true, tableContrib: 5, refund: 7 }),
    ];
    const { settlements } = settle(seats, 35);
    expect(settlements[0].tableDelta).toBe(18 - ANTE);
    expect(settlements[1].tableDelta).toBe(17 - ANTE);
    expect(settlements[0].outright).toBe(false);
    expect(settlements[1].outright).toBe(false);
  });

  it('lets a sole survivor take the pot even while losing to the dealer', () => {
    // Pot at settle already holds both bailers' stakes: 30 antes + 2 × 10.
    const seats = [
      survivor(KING_HIGH),
      seatOf({ bailed: true, refund: 10 }),
      seatOf({ bailed: true, refund: 10 }),
    ];
    const { settlements } = settle(seats, 50);
    expect(settlements[0].vsDealer).toBe('lose');
    expect(settlements[0].wonTable).toBe(true);
    // Their own beaten stake fell into the pot they collect: 50 + 10 back out.
    expect(settlements[0].tableDelta).toBe(60 - ANTE);
    expect(settlements[0].net).toBe(60 - ANTE - ANTE);
    expect(settlements[0].sweep).toBe(false);
  });

  it('sends an unclaimed pot to the house when everyone bailed', () => {
    // Both stakes already sit in the pot: 20 antes + 2 × 10 stakes.
    const seats = [seatOf({ bailed: true, refund: 10 }), seatOf({ bailed: true, refund: 10 })];
    const { settlements, houseDelta } = settle(seats, 40);
    settlements.forEach((s) => {
      expect(s.vsDealer).toBe('bailed');
      expect(s.wonTable).toBe(false);
      expect(s.net).toBe(10 - 20);
    });
    expect(houseDelta).toBe(40);
  });
});

describe('the sweep', () => {
  it('pays five antes for beating dealer and table in one hand', () => {
    const seats = [survivor(TRIPS), survivor(ACE_HIGH)];
    const { settlements } = settle(seats, 20);
    expect(settlements[0].sweep).toBe(true);
    expect(settlements[0].sweepBonus).toBe(5 * ANTE);
    // Winnings + pot-with-beaten-stake + sweep: 10 + (30 − 10) + 50.
    expect(settlements[0].net).toBe(ANTE + 2 * ANTE + 5 * ANTE);
  });

  it('needs the outright table win — a split is not a sweep', () => {
    const seats = [survivor(TRIPS), survivor(TRIPS_TWIN)];
    const { settlements } = settle(seats, 20);
    settlements.forEach((s) => {
      expect(s.sweep).toBe(false);
      expect(s.sweepBonus).toBe(0);
    });
  });

  it('needs the dealer-beat — taking the table on a losing hand is not a sweep', () => {
    const seats = [survivor(KING_HIGH), survivor(ACE_HIGH)];
    const { settlements } = settle(seats, 20);
    expect(settlements[1].wonTable).toBe(true);
    expect(settlements[1].vsDealer).toBe('lose');
    expect(settlements[1].sweep).toBe(false);
    // Both beaten stakes fell into the pot the ace-high still collects.
    expect(settlements[1].tableDelta).toBe(40 - ANTE);
  });
});
