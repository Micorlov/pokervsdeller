import { chooseBotKicker, chooseForgeDiscard, decideShowdownMove } from '../bots';
import { mulberry32 } from '../rng';
import {
  SEAT_COUNT,
  ShowdownRoundState,
  allActed,
  allKickersSet,
  applyMove,
  createShowdownTable,
  dealDealerCard,
  finishDealing,
  humanSeat,
  setKicker,
  settleShowdown,
  startShowdownRound,
} from '../showdown';
import { ShowdownMove } from '../showdownPayouts';

const ANTE = 10;
const HUMAN_STACK = 500;

/**
 * The ledger every reducer must respect: chips on the table are only ever
 * moved, never made. Mid-hand the pots and stakes are still in play; once
 * settled they have all landed in stacks or on the house ledger.
 */
const chipTotal = (state: ShowdownRoundState): number => {
  const stacks = state.seats.reduce((total, seat) => total + seat.stack, 0);
  if (state.phase === 'settled' || state.phase === 'ante') return stacks + state.houseNet;
  // A bailed seat's stake already lives in the table pot, so only active
  // stakes are still outstanding against the house.
  const stakes = state.seats.reduce(
    (total, seat) => total + (seat.status === 'active' ? seat.dealerStake : 0),
    0,
  );
  return stacks + state.tablePot + stakes + state.houseNet;
};

type HumanPolicy = (state: ShowdownRoundState, round: number) => ShowdownMove;

const alwaysHold: HumanPolicy = () => ({ kind: 'hold', pressure: false });

/** Forges early, pressures in round three — touches every mechanic. */
const adventurous: HumanPolicy = (state, round) => {
  const seat = humanSeat(state);
  if (round <= 2 && seat.forgesUsed < 2 && seat.kickerIndex !== null && seat.stack >= state.ante) {
    return {
      kind: 'forge',
      forgeDiscardIndex: chooseForgeDiscard(seat.cards, seat.kickerIndex),
      pressure: false,
    };
  }
  if (round === 3 && seat.stack >= state.ante) return { kind: 'hold', pressure: true };
  return { kind: 'hold', pressure: false };
};

/** Plays one full hand from a seed, checking the ledger after every reducer. */
const playHand = (seed: number, humanPolicy: HumanPolicy): ShowdownRoundState => {
  const rng = mulberry32(seed);
  let state = createShowdownTable(HUMAN_STACK, ANTE);
  const initial = chipTotal(state);
  const step = (next: ShowdownRoundState): ShowdownRoundState => {
    expect(chipTotal(next)).toBe(initial);
    return next;
  };

  state = step(startShowdownRound(state, rng));
  state = step(finishDealing(state));
  for (let i = 0; i < SEAT_COUNT; i += 1) {
    state = step(setKicker(state, i, chooseBotKicker(state.seats[i].cards)));
  }
  expect(allKickersSet(state)).toBe(true);

  while (state.phase === 'kicker' || state.phase === 'actions') {
    state = step(dealDealerCard(state));
    if (state.phase !== 'actions') break;
    const round = state.dealerCards.length;
    for (let i = 0; i < SEAT_COUNT; i += 1) {
      const seat = state.seats[i];
      if (seat.status === 'bailed') continue;
      const move = seat.isHuman
        ? humanPolicy(state, round)
        : decideShowdownMove(
            seat,
            state.dealerCards,
            round,
            state.ante,
            seat.persona as NonNullable<typeof seat.persona>,
            rng,
          );
      state = step(applyMove(state, i, move));
    }
    expect(allActed(state)).toBe(true);
  }

  expect(state.phase).toBe('reveal');
  return step(settleShowdown(state));
};

describe('a full hand of Showdown', () => {
  it.each([1, 2, 3, 4, 5, 6, 7, 8, 9, 10])('conserves every chip on seed %i', (seed) => {
    const settled = playHand(seed, alwaysHold);
    expect(settled.phase).toBe('settled');

    const playersNet = settled.seats.reduce(
      (total, seat) => total + (seat.settlement?.net ?? 0),
      0,
    );
    // Summed rather than negated: a fully zero-sum hand makes `-playersNet`
    // a negative zero, which Object.is treats as different from zero.
    expect(settled.houseNet + playersNet).toBe(0);

    settled.seats.forEach((seat) => {
      expect(seat.settlement).not.toBeNull();
    });
  });

  it.each([11, 12, 13, 14, 15, 16, 17, 18, 19, 20])(
    'survives forges and pressure on seed %i',
    (seed) => {
      const settled = playHand(seed, adventurous);
      const playersNet = settled.seats.reduce(
        (total, seat) => total + (seat.settlement?.net ?? 0),
        0,
      );
      expect(settled.houseNet + playersNet).toBe(0);
    },
  );

  it('deals only unique cards to the showdown', () => {
    const settled = playHand(7, adventurous);
    const shown = [
      ...settled.dealerCards.map((card) => card.id),
      ...settled.seats.flatMap((seat) => seat.cards.map((card) => card.id)),
      ...settled.stock.map((card) => card.id),
    ];
    expect(new Set(shown).size).toBe(shown.length);
    expect(settled.dealerCards).toHaveLength(5);
  });

  it('replays identically from the same seed', () => {
    expect(playHand(42, adventurous)).toEqual(playHand(42, adventurous));
  });

  it('carries the human stack through both antes and back out', () => {
    const settled = playHand(3, alwaysHold);
    const seat = humanSeat(settled);
    expect(seat.settlement).not.toBeNull();
    expect(seat.stack).toBe(HUMAN_STACK + (seat.settlement?.net ?? 0));
  });
});

/** State advanced to the first action round, human kicker on card 0. */
const firstAction = (seed = 5): ShowdownRoundState => {
  const rng = mulberry32(seed);
  let state = startShowdownRound(createShowdownTable(HUMAN_STACK, ANTE), rng);
  state = finishDealing(state);
  for (let i = 0; i < SEAT_COUNT; i += 1) {
    state = setKicker(state, i, i === 0 ? 0 : chooseBotKicker(state.seats[i].cards));
  }
  return dealDealerCard(state);
};

const holdEveryoneElse = (state: ShowdownRoundState): ShowdownRoundState => {
  let next = state;
  for (let i = 1; i < SEAT_COUNT; i += 1) {
    if (next.seats[i].status === 'active') {
      next = applyMove(next, i, { kind: 'hold', pressure: false });
    }
  }
  return next;
};

describe('the table protocol', () => {
  it('refuses to draw before every kicker is set', () => {
    const rng = mulberry32(5);
    let state = startShowdownRound(createShowdownTable(HUMAN_STACK, ANTE), rng);
    state = finishDealing(state);
    expect(() => dealDealerCard(state)).toThrow(/kicker/);
  });

  it('refuses to draw while seats still owe a move', () => {
    const state = firstAction();
    expect(state.phase).toBe('actions');
    expect(() => dealDealerCard(state)).toThrow(/owe a move/);
  });

  it('refuses to forge the kicker away', () => {
    const state = firstAction();
    expect(() =>
      applyMove(state, 0, { kind: 'forge', forgeDiscardIndex: 0, pressure: false }),
    ).toThrow(/kicker/);
  });

  it('refuses a third forge', () => {
    let state = firstAction();
    state = applyMove(state, 0, { kind: 'forge', forgeDiscardIndex: 1, pressure: false });
    state = holdEveryoneElse(state);
    state = dealDealerCard(state);
    state = applyMove(state, 0, { kind: 'forge', forgeDiscardIndex: 2, pressure: false });
    state = holdEveryoneElse(state);
    state = dealDealerCard(state);
    expect(() =>
      applyMove(state, 0, { kind: 'forge', forgeDiscardIndex: 3, pressure: false }),
    ).toThrow(/no forges left/);
  });

  it('only allows pressure in the third action round', () => {
    const state = firstAction();
    expect(() => applyMove(state, 0, { kind: 'hold', pressure: true })).toThrow(/card 3/);
  });

  it('never lets a bail carry pressure', () => {
    let state = firstAction();
    state = holdEveryoneElse(state);
    state = applyMove(state, 0, { kind: 'hold', pressure: false });
    state = dealDealerCard(state);
    state = holdEveryoneElse(state);
    state = applyMove(state, 0, { kind: 'hold', pressure: false });
    state = dealDealerCard(state);
    expect(() => applyMove(state, 0, { kind: 'bail', pressure: true })).toThrow(/pressure/);
  });

  it('hands the bail refund over immediately and pours the stake into the pot', () => {
    const state = firstAction();
    const before = humanSeat(state);
    const bailed = applyMove(state, 0, { kind: 'bail', pressure: false });
    const after = humanSeat(bailed);
    expect(after.status).toBe('bailed');
    expect(after.refund).toBe(ANTE);
    expect(after.stack).toBe(before.stack + ANTE);
    expect(bailed.houseNet).toBe(state.houseNet - ANTE);
    expect(bailed.tablePot).toBe(state.tablePot + before.dealerStake);
  });

  it('ignores a second move in the same round', () => {
    let state = firstAction();
    state = applyMove(state, 0, { kind: 'hold', pressure: false });
    expect(applyMove(state, 0, { kind: 'bail', pressure: false })).toBe(state);
  });

  it('ignores a move from a bailed seat', () => {
    let state = firstAction();
    state = applyMove(state, 0, { kind: 'bail', pressure: false });
    state = holdEveryoneElse(state);
    state = dealDealerCard(state);
    expect(applyMove(state, 0, { kind: 'hold', pressure: false })).toBe(state);
  });

  it('treats a settle outside the reveal as a no-op', () => {
    const state = firstAction();
    expect(settleShowdown(state)).toBe(state);
  });
});
