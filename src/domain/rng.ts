/**
 * Seeded randomness.
 *
 * Every shuffle and every bot decision draws from an injected generator rather
 * than `Math.random`, so a round can be replayed exactly from its seed — which
 * is what makes the engine testable at all.
 */

export type Rng = () => number;

/** mulberry32: small, fast, and good enough for dealing cards. */
export const mulberry32 = (seed: number): Rng => {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
};

/** Fisher-Yates. Returns a new array — the input is never touched. */
export const shuffle = <T>(items: readonly T[], rng: Rng): T[] => {
  const out = [...items];
  for (let i = out.length - 1; i > 0; i -= 1) {
    const j = Math.floor(rng() * (i + 1));
    [out[i], out[j]] = [out[j], out[i]];
  }
  return out;
};

/** A fresh seed for a real session, where replayability is not wanted. */
export const randomSeed = (): number => Math.floor(Math.random() * 0xffffffff);
