import { INTERSTITIAL_GAP_MS } from './adConfig';

export interface GateState {
  /** True when this process is the first ever launch of the install. */
  readonly firstLaunch: boolean;
  /** Natural breaks (finished games/hands) reached this session, including this one. */
  readonly breaksThisSession: number;
  /** When the last interstitial was shown, in ms, or null if none yet. */
  readonly lastShownAt: number | null;
  readonly now: number;
}

/** Never on a fresh install's first game, never twice within the gap. */
export const shouldShowInterstitial = (s: GateState): boolean => {
  if (s.firstLaunch && s.breaksThisSession <= 1) return false;
  return s.lastShownAt === null || s.now - s.lastShownAt >= INTERSTITIAL_GAP_MS;
};
