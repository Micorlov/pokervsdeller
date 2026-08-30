import { canClaimDailyBonus, dayKeyOf } from '../dailyBonus';

describe('dayKeyOf', () => {
  test('formats a local calendar day with zero padding', () => {
    expect(dayKeyOf(new Date(2026, 0, 5, 23, 59))).toBe('2026-01-05');
  });
});

describe('canClaimDailyBonus', () => {
  test('claimable when never claimed', () => {
    expect(canClaimDailyBonus(null, new Date(2026, 7, 29))).toBe(true);
  });

  test('not claimable twice on the same day', () => {
    const morning = new Date(2026, 7, 29, 8, 0);
    const evening = new Date(2026, 7, 29, 22, 0);

    expect(canClaimDailyBonus(dayKeyOf(morning), evening)).toBe(false);
  });

  test('claimable again the next calendar day, even minutes later', () => {
    const lateNight = new Date(2026, 7, 29, 23, 55);
    const justPastMidnight = new Date(2026, 7, 30, 0, 5);

    expect(canClaimDailyBonus(dayKeyOf(lateNight), justPastMidnight)).toBe(true);
  });
});
