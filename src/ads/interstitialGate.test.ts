import { shouldShowInterstitial } from './interstitialGate';

const base = { firstLaunch: false, breaksThisSession: 1, lastShownAt: null, now: 1_000_000 };

describe('shouldShowInterstitial', () => {
  it('skips the first game of a fresh install', () => {
    expect(shouldShowInterstitial({ ...base, firstLaunch: true })).toBe(false);
    expect(shouldShowInterstitial({ ...base, firstLaunch: true, breaksThisSession: 2 })).toBe(true);
  });

  it('shows on a returning player’s first break', () => {
    expect(shouldShowInterstitial(base)).toBe(true);
  });

  it('waits 90 seconds between ads', () => {
    expect(shouldShowInterstitial({ ...base, lastShownAt: base.now - 89_999 })).toBe(false);
    expect(shouldShowInterstitial({ ...base, lastShownAt: base.now - 90_000 })).toBe(true);
  });
});
