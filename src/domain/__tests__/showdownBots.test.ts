import {
  BOT_PERSONAS,
  BotPersona,
  ShowdownBotSeat,
  chooseBotKicker,
  chooseForgeDiscard,
  decideShowdownMove,
} from '../bots';
import { Card, Suit, cardId } from '../cards';
import { Rng, mulberry32 } from '../rng';

const SUIT_OF: Record<string, Suit> = { s: 'spades', h: 'hearts', d: 'diamonds', c: 'clubs' };
const RANK_OF: Record<string, number> = { T: 10, J: 11, Q: 12, K: 13, A: 14 };

/** "As Kd 7h" → cards. */
const hand = (spec: string): Card[] =>
  spec.split(' ').map((token) => {
    const rank = RANK_OF[token[0]] ?? Number(token[0]);
    const suit = SUIT_OF[token[1]];
    return { rank, suit, id: cardId(rank, suit) };
  });

const ANTE = 10;

const persona = (id: string): BotPersona => {
  const found = BOT_PERSONAS.find((candidate) => candidate.id === id);
  if (!found) throw new Error(`No persona ${id}`);
  return found;
};

const seatOf = (spec: string, overrides: Partial<ShowdownBotSeat> = {}): ShowdownBotSeat => ({
  cards: hand(spec),
  kickerIndex: 0,
  forgesUsed: 0,
  pressured: false,
  stack: 480,
  ...overrides,
});

const countingRng = (seed: number): { rng: Rng; draws: () => number } => {
  const base = mulberry32(seed);
  let drawn = 0;
  return {
    rng: () => {
      drawn += 1;
      return base();
    },
    draws: () => drawn,
  };
};

describe('chooseBotKicker', () => {
  it('marks the highest spare card outside the made core', () => {
    expect(chooseBotKicker(hand('Qc Qd 7h 5s 3c'))).toBe(2);
    expect(chooseBotKicker(hand('4c 9s 4d 4h Kc'))).toBe(4);
  });

  it('marks the highest card of a pat hand', () => {
    expect(chooseBotKicker(hand('8c 7d 6h 5s 4c'))).toBe(0);
  });
});

describe('chooseForgeDiscard', () => {
  it('never trades the kicker away', () => {
    const specs = ['Qc Qd 7h 5s 3c', 'Ah Kh 9h 4h 2c', 'Kc 8d 5h 4s 2c'];
    specs.forEach((spec) => {
      for (let kicker = 0; kicker < 5; kicker += 1) {
        expect(chooseForgeDiscard(hand(spec), kicker)).not.toBe(kicker);
      }
    });
  });

  it('breaks toward the four-flush by shedding the offsuit card', () => {
    expect(chooseForgeDiscard(hand('Ah Kh 9h 4h 2c'), 0)).toBe(4);
  });

  it('sheds the low spare, not the pair', () => {
    expect(chooseForgeDiscard(hand('Qc Qd 7h 5s 3c'), 2)).toBe(4);
  });
});

describe('decideShowdownMove', () => {
  it('draws from the rng exactly twice on every branch', () => {
    const branches: [ShowdownBotSeat, string, number][] = [
      [seatOf('Kc 8d 5h 4s 2c'), '9c 9d', 2], // bail territory
      [seatOf('Qc Qd 7h 5s 3c'), '9c 5d 2h', 3], // pressure territory
      [seatOf('Ah Kh 9h 4h 2c'), '9c 5d', 2], // forge territory
      [seatOf('Kc Kd 5h 5s 9c'), 'Ac Ad Ah 2s', 4], // made hand holds
    ];
    branches.forEach(([seat, dealer, round]) => {
      BOT_PERSONAS.forEach((who) => {
        const { rng, draws } = countingRng(round * 100 + who.tightness * 1000);
        decideShowdownMove(seat, hand(dealer), round, ANTE, who, rng);
        expect(draws()).toBe(2);
      });
    });
  });

  it('never bails a made hand of two pair or better', () => {
    const seat = seatOf('Kc Kd 5h 5s 9c');
    const dealer = hand('Ac Ad Ah');
    for (let seed = 1; seed <= 200; seed += 1) {
      BOT_PERSONAS.forEach((who) => {
        const move = decideShowdownMove(seat, dealer, 3, ANTE, who, mulberry32(seed));
        expect(move.kind).not.toBe('bail');
      });
    }
  });

  it('has Duke bailing a beaten hand more often than Gus', () => {
    const seat = seatOf('Kc 8d 5h 4s 2c');
    const dealer = hand('9c 9d');
    let dukeBails = 0;
    let gusBails = 0;
    for (let seed = 1; seed <= 200; seed += 1) {
      if (decideShowdownMove(seat, dealer, 2, ANTE, persona('duke'), mulberry32(seed)).kind === 'bail') {
        dukeBails += 1;
      }
      if (decideShowdownMove(seat, dealer, 2, ANTE, persona('gus'), mulberry32(seed)).kind === 'bail') {
        gusBails += 1;
      }
    }
    expect(dukeBails).toBeGreaterThan(gusBails);
    expect(gusBails).toBeGreaterThan(0);
  });

  it('has Gus pressuring a modest lead that Duke sits on', () => {
    const seat = seatOf('Qc Qd 7h 5s 3c');
    const dealer = hand('9c 5d 2h');
    let dukePressures = 0;
    let gusPressures = 0;
    for (let seed = 1; seed <= 200; seed += 1) {
      if (decideShowdownMove(seat, dealer, 3, ANTE, persona('duke'), mulberry32(seed)).pressure) {
        dukePressures += 1;
      }
      if (decideShowdownMove(seat, dealer, 3, ANTE, persona('gus'), mulberry32(seed)).pressure) {
        gusPressures += 1;
      }
    }
    expect(gusPressures).toBeGreaterThan(dukePressures);
  });

  it('keeps every forge inside the limit and off the kicker', () => {
    for (let seed = 1; seed <= 100; seed += 1) {
      const seat = seatOf('Ah Kh 9h 4h 2c', { kickerIndex: 1 });
      const move = decideShowdownMove(seat, hand('9c 5d'), 2, ANTE, persona('gus'), mulberry32(seed));
      if (move.kind === 'forge') {
        expect(move.forgeDiscardIndex).not.toBe(1);
      }
    }
    const spent = seatOf('Ah Kh 9h 4h 2c', { forgesUsed: 2 });
    for (let seed = 1; seed <= 100; seed += 1) {
      const move = decideShowdownMove(spent, hand('9c 5d'), 2, ANTE, persona('gus'), mulberry32(seed));
      expect(move.kind).not.toBe('forge');
    }
  });

  it('never pressures outside the third round or beyond the stack', () => {
    const seat = seatOf('Qc Qd 7h 5s 3c');
    const broke = seatOf('Qc Qd 7h 5s 3c', { stack: 0 });
    for (let seed = 1; seed <= 100; seed += 1) {
      expect(
        decideShowdownMove(seat, hand('9c 5d'), 2, ANTE, persona('gus'), mulberry32(seed)).pressure,
      ).toBe(false);
      expect(
        decideShowdownMove(broke, hand('9c 5d 2h'), 3, ANTE, persona('gus'), mulberry32(seed))
          .pressure,
      ).toBe(false);
    }
  });
});
