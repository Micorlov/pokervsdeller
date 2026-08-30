import { Card, Suit, cardId } from '../cards';
import { CATEGORY_ORDER, compareHands, evaluateHand, handName } from '../handRank';

const SUIT_OF: Record<string, Suit> = { s: 'spades', h: 'hearts', d: 'diamonds', c: 'clubs' };
const RANK_OF: Record<string, number> = { T: 10, J: 11, Q: 12, K: 13, A: 14 };

/** "As Kd 7h 4c 2s" → five cards. */
const hand = (spec: string): Card[] =>
  spec.split(' ').map((token) => {
    const rank = RANK_OF[token[0]] ?? Number(token[0]);
    const suit = SUIT_OF[token[1]];
    return { rank, suit, id: cardId(rank, suit) };
  });

describe('evaluateHand', () => {
  const CATEGORIES: [string, string][] = [
    ['As Ks Qs Js Ts', 'royalFlush'],
    ['9h 8h 7h 6h 5h', 'straightFlush'],
    ['7c 7d 7h 7s 2c', 'quads'],
    ['4c 4d 4h 9s 9c', 'fullHouse'],
    ['Ad Jd 8d 5d 2d', 'flush'],
    ['8c 7d 6h 5s 4c', 'straight'],
    ['Qc Qd Qh 3s 2c', 'trips'],
    ['Kc Kd 5h 5s 9c', 'twoPair'],
    ['Ac Ad 9h 5s 2c', 'pair'],
    ['Ac Kd 9h 5s 2c', 'highCard'],
  ];

  it.each(CATEGORIES)('reads %s as %s', (spec, category) => {
    expect(evaluateHand(hand(spec)).category).toBe(category);
  });

  it('names every category for the badge', () => {
    expect(handName(evaluateHand(hand('4c 4d 4h 9s 9c')))).toBe('Full House');
    expect(handName(evaluateHand(hand('Ac Kd 9h 5s 2c')))).toBe('High Card');
  });

  it('rejects a hand that is not five cards', () => {
    expect(() => evaluateHand(hand('Ac Kd 9h 5s'))).toThrow(/5 cards/);
  });
});

describe('the wheel', () => {
  it('reads A-2-3-4-5 as a five-high straight', () => {
    const wheel = evaluateHand(hand('Ac 2d 3h 4s 5c'));
    expect(wheel.category).toBe('straight');
    expect(wheel.tiebreak).toEqual([5]);
  });

  it('reads a suited wheel as a straight flush, not a royal', () => {
    const steel = evaluateHand(hand('Ah 2h 3h 4h 5h'));
    expect(steel.category).toBe('straightFlush');
    expect(steel.tiebreak).toEqual([5]);
  });

  it('loses to a six-high straight — the ace plays low, not high', () => {
    const wheel = evaluateHand(hand('Ac 2d 3h 4s 5c'));
    const sixHigh = evaluateHand(hand('6c 5d 4h 3s 2c'));
    expect(compareHands(sixHigh, wheel)).toBeGreaterThan(0);
  });

  it('is beaten by a wheel that is also a flush', () => {
    expect(
      compareHands(evaluateHand(hand('Ah 2h 3h 4h 5h')), evaluateHand(hand('Ac 2d 3h 4s 5c'))),
    ).toBeGreaterThan(0);
  });
});

describe('tiebreaks', () => {
  it('orders equal pairs by their kickers in turn', () => {
    const better = evaluateHand(hand('Ac Ad Kh 5s 2c'));
    const worse = evaluateHand(hand('Ac Ad Qh 5s 2c'));
    expect(compareHands(better, worse)).toBeGreaterThan(0);
  });

  it('reads two pair as high pair, low pair, kicker', () => {
    expect(evaluateHand(hand('Kc Kd 5h 5s 9c')).tiebreak).toEqual([13, 5, 9]);
    const better = evaluateHand(hand('Kc Kd 5h 5s 9c'));
    const worse = evaluateHand(hand('Kc Kd 5h 5s 8c'));
    expect(compareHands(better, worse)).toBeGreaterThan(0);
  });

  it('reads a full house as trips over pair', () => {
    expect(evaluateHand(hand('4c 4d 4h 9s 9c')).tiebreak).toEqual([4, 9]);
    expect(
      compareHands(evaluateHand(hand('9c 9d 9h 4s 4c')), evaluateHand(hand('4c 4d 4h 9s 9c'))),
    ).toBeGreaterThan(0);
  });

  it('ranks a flush by all five cards, high to low', () => {
    expect(evaluateHand(hand('Ad Jd 8d 5d 2d')).tiebreak).toEqual([14, 11, 8, 5, 2]);
  });

  it('calls a genuine tie a tie', () => {
    expect(compareHands(evaluateHand(hand('Ac Kd 9h 5s 2c')), evaluateHand(hand('Ah Ks 9c 5d 2s')))).toBe(0);
  });
});

describe('compareHands', () => {
  const SPECS = [
    'As Ks Qs Js Ts',
    '9h 8h 7h 6h 5h',
    '7c 7d 7h 7s 2c',
    '4c 4d 4h 9s 9c',
    'Ad Jd 8d 5d 2d',
    '8c 7d 6h 5s 4c',
    'Qc Qd Qh 3s 2c',
    'Kc Kd 5h 5s 9c',
    'Ac Ad 9h 5s 2c',
    'Ac Kd 9h 5s 2c',
  ];

  it('is antisymmetric across every pair of hands', () => {
    // Summed rather than negated: `Math.sign(0)` is -0 one way round and 0 the
    // other, and Jest reads those as different values.
    SPECS.forEach((left) => {
      SPECS.forEach((right) => {
        const a = evaluateHand(hand(left));
        const b = evaluateHand(hand(right));
        expect(Math.sign(compareHands(a, b)) + Math.sign(compareHands(b, a))).toBe(0);
      });
    });
  });

  it('ranks the ten categories in the published order', () => {
    const ordered = SPECS.map((spec) => evaluateHand(hand(spec)));
    ordered.forEach((rank, index) => {
      if (index === 0) return;
      expect(CATEGORY_ORDER[ordered[index - 1].category]).toBeGreaterThan(CATEGORY_ORDER[rank.category]);
      expect(compareHands(ordered[index - 1], rank)).toBeGreaterThan(0);
    });
  });
});
