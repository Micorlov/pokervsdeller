import { streakBonusFor, streakBonusUnits } from '../streakBonus';

describe('streakBonusUnits', () => {
  test('zero below the first threshold', () => {
    expect(streakBonusUnits(0)).toBe(0);
    expect(streakBonusUnits(1)).toBe(0);
    expect(streakBonusUnits(2)).toBe(0);
  });

  test('pays at each threshold exactly', () => {
    expect(streakBonusUnits(3)).toBe(2);
    expect(streakBonusUnits(5)).toBe(4);
    expect(streakBonusUnits(7)).toBe(6);
    expect(streakBonusUnits(10)).toBe(10);
  });

  test('zero between thresholds', () => {
    expect(streakBonusUnits(4)).toBe(0);
    expect(streakBonusUnits(6)).toBe(0);
    expect(streakBonusUnits(8)).toBe(0);
    expect(streakBonusUnits(9)).toBe(0);
  });

  test('zero past the last threshold — no farming an endless streak', () => {
    expect(streakBonusUnits(11)).toBe(0);
    expect(streakBonusUnits(25)).toBe(0);
  });
});

describe('streakBonusFor', () => {
  test('scales the unit payout by the ante', () => {
    expect(streakBonusFor(3, 10)).toBe(20);
    expect(streakBonusFor(5, 10)).toBe(40);
    expect(streakBonusFor(7, 25)).toBe(150);
    expect(streakBonusFor(10, 5)).toBe(50);
  });

  test('zero chips off the ante for a non-threshold streak', () => {
    expect(streakBonusFor(4, 25)).toBe(0);
    expect(streakBonusFor(0, 25)).toBe(0);
  });
});
