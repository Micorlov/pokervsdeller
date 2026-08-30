import {
  ANTE_OPTIONS,
  MIN_BANKROLL_MULTIPLE,
  RE_UP_AMOUNT,
  STARTING_BANKROLL,
  canPlay,
  isBust,
  largestAffordableAnte,
  minimumToPlay,
  reUp,
} from '../bankroll';

describe('canPlay', () => {
  it('needs exactly five antes, and turns over at that boundary', () => {
    ANTE_OPTIONS.forEach((ante) => {
      const floor = ante * MIN_BANKROLL_MULTIPLE;
      expect(minimumToPlay(ante)).toBe(floor);
      expect(canPlay(floor, ante)).toBe(true);
      expect(canPlay(floor - 1, ante)).toBe(false);
      expect(isBust(floor - 1, ante)).toBe(true);
    });
  });

  it('seats a fresh player at every ante', () => {
    ANTE_OPTIONS.forEach((ante) => expect(canPlay(STARTING_BANKROLL, ante)).toBe(true));
  });
});

describe('reUp', () => {
  it('does nothing while the player can still cover a hand', () => {
    expect(reUp(60, 10)).toBe(60);
    expect(reUp(STARTING_BANKROLL, 25)).toBe(STARTING_BANKROLL);
  });

  it('tops the stack up only at bust, keeping whatever was left', () => {
    expect(reUp(49, 10)).toBe(49 + RE_UP_AMOUNT);
    expect(reUp(0, 5)).toBe(RE_UP_AMOUNT);
  });
});

describe('largestAffordableAnte', () => {
  it('picks the biggest stake the stack can still sit down for', () => {
    expect(largestAffordableAnte(130)).toBe(25);
    expect(largestAffordableAnte(124)).toBe(10);
    expect(largestAffordableAnte(49)).toBe(5);
    expect(largestAffordableAnte(30)).toBe(5);
  });

  it('is null when even the smallest ante is out of reach', () => {
    expect(largestAffordableAnte(24)).toBeNull();
  });
});
