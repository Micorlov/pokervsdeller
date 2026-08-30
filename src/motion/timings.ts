/**
 * One place for every duration and spring in the app.
 *
 * The game is played in a quiet, unhurried way, so nothing here is bouncy for
 * its own sake: tiles settle like objects with weight, chrome moves quickly
 * enough to feel instant, and only the celebratory moments are allowed to
 * overshoot.
 */

export const durations = {
  /** Chrome that must not feel laggy — presses, ticks, hint text. */
  quick: 140,
  /** The default for anything appearing or changing state. */
  base: 240,
  /** Panels and screens, where the eye needs time to follow. */
  slow: 380,
  /** Score count-ups and end-of-hand flourishes. */
  celebrate: 620,
};

/** Springs are given in RN's `friction`/`tension` form for `Animated.spring`. */
export const springs = {
  /** A tile landing on the table: firm, one small settle, no wobble. */
  settle: { friction: 8, tension: 140 },
  /** A finger press releasing — fast enough to track the touch. */
  press: { friction: 7, tension: 300 },
  /** Panels arriving: soft, slightly slower, no visible overshoot. */
  panel: { friction: 11, tension: 90 },
  /**
   * Board geometry — every tile's slide to a new slot.
   * Critically damped: a whole board overshooting its new scale reads as the
   * screen flashing, where a single tile's settle reads as weight.
   */
  glide: { friction: 12, tension: 140 },
  /** The one place overshoot is wanted — a score popping onto the screen. */
  pop: { friction: 5.5, tension: 180 },
};

/** Gap between siblings in a staggered entrance. */
export const STAGGER_MS = 55;
