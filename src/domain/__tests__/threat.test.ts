import { Card, Suit, cardId } from '../cards';
import { CATEGORY_ORDER, evaluateHand } from '../handRank';
import { handStrengthScore, partialThreat } from '../threat';

const SUIT_OF: Record<string, Suit> = { s: 'spades', h: 'hearts', d: 'diamonds', c: 'clubs' };
const RANK_OF: Record<string, number> = { T: 10, J: 11, Q: 12, K: 13, A: 14 };

/** "As Kd 7h" → cards, any count. */
const hand = (spec: string): Card[] =>
  spec.split(' ').map((token) => {
    const rank = RANK_OF[token[0]] ?? Number(token[0]);
    const suit = SUIT_OF[token[1]];
    return { rank, suit, id: cardId(rank, suit) };
  });

describe('partialThreat', () => {
  it('scores an empty dealer hand as no threat at all', () => {
    expect(partialThreat([])).toBe(0);
  });

  it('stays inside the 0..1 meter at every hand size', () => {
    const climbs = ['Ah', 'Ah Ad', 'Ah Ad As', 'Ah Ad As Ac', 'Ah Ad As Ac Kh'];
    climbs.forEach((spec) => {
      const score = partialThreat(hand(spec));
      expect(score).toBeGreaterThan(0);
      expect(score).toBeLessThanOrEqual(1);
    });
  });

  it('rates a visible pair above rags', () => {
    expect(partialThreat(hand('Kc Kd'))).toBeGreaterThan(partialThreat(hand('7h 2s')));
  });

  it('rates trips above a pair on the same board size', () => {
    expect(partialThreat(hand('9c 9d 9h'))).toBeGreaterThan(partialThreat(hand('9c 9d 4h')));
  });

  it('rates a high suited companion above a low offsuit one', () => {
    expect(partialThreat(hand('9h Kh'))).toBeGreaterThan(partialThreat(hand('9h 2c')));
  });

  it('grows as a pairing card lands', () => {
    expect(partialThreat(hand('Ah Ad'))).toBeGreaterThan(partialThreat(hand('Ah')));
  });

  it('hands a complete dealer hand to the real evaluator', () => {
    const royal = partialThreat(hand('As Ks Qs Js Ts'));
    const rags = partialThreat(hand('Kc 9d 7h 5s 2c'));
    expect(royal).toBeGreaterThan(rags);
    expect(royal).toBe(handStrengthScore(evaluateHand(hand('As Ks Qs Js Ts'))));
  });
});

describe('handStrengthScore', () => {
  it('orders full hands the same way the category table does', () => {
    const ladder = [
      'Ac Kd 9h 5s 2c', // highCard
      'Ac Ad 9h 5s 2c', // pair
      'Kc Kd 5h 5s 9c', // twoPair
      'Qc Qd Qh 3s 2c', // trips
      '8c 7d 6h 5s 4c', // straight
      'Ad Jd 8d 5d 2d', // flush
      '4c 4d 4h 9s 9c', // fullHouse
      '7c 7d 7h 7s 2c', // quads
      '9h 8h 7h 6h 5h', // straightFlush
      'As Ks Qs Js Ts', // royalFlush
    ];
    const scores = ladder.map((spec) => handStrengthScore(evaluateHand(hand(spec))));
    const orders = ladder.map((spec) => CATEGORY_ORDER[evaluateHand(hand(spec)).category]);

    for (let i = 1; i < scores.length; i += 1) {
      expect(orders[i]).toBeGreaterThan(orders[i - 1]);
      expect(scores[i]).toBeGreaterThan(scores[i - 1]);
    }
  });

  it('separates hands in the same category by their lead tiebreak', () => {
    const aces = handStrengthScore(evaluateHand(hand('Ac Ad 9h 5s 2c')));
    const kings = handStrengthScore(evaluateHand(hand('Kc Kd 9h 5s 2c')));
    expect(aces).toBeGreaterThan(kings);
  });
});
