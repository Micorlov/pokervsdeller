import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Animated, Easing } from 'react-native';
import { useReducedMotion } from './MotionContext';
import { durations, springs } from './timings';

interface EntranceOptions {
  /** Milliseconds to wait before starting — used to stagger a row or list. */
  readonly delay?: number;
  /** Distance the element travels up into place. Negative moves it down. */
  readonly translateY?: number;
  /** Starting scale. 1 means the element only fades and slides. */
  readonly scaleFrom?: number;
  readonly spring?: { friction: number; tension: number };
  /** Re-runs the entrance whenever this changes — e.g. a tile's id. */
  readonly key?: string | number | null;
}

/**
 * Fade-and-settle for anything arriving on screen.
 *
 * Under reduced motion the element still fades in — a cross-fade carries no
 * vestibular risk — but it never travels or scales.
 */
export const useEntrance = ({
  delay = 0,
  translateY = 10,
  scaleFrom = 1,
  spring = springs.panel,
  key = null,
}: EntranceOptions = {}) => {
  const reducedMotion = useReducedMotion();
  // Lazy state, not a ref: the value is read during render, which the
  // react-hooks/refs rule forbids for refs. The instance is still created once.
  const [progress] = useState(() => new Animated.Value(0));

  useEffect(() => {
    progress.setValue(0);
    const animation = reducedMotion
      ? Animated.timing(progress, {
          toValue: 1,
          duration: durations.quick,
          delay,
          easing: Easing.out(Easing.quad),
          useNativeDriver: true,
        })
      : Animated.spring(progress, { toValue: 1, delay, ...spring, useNativeDriver: true });
    animation.start();
    return () => animation.stop();
  }, [progress, reducedMotion, delay, spring, key]);

  return useMemo(() => {
    if (reducedMotion) return { opacity: progress };
    return {
      // Clamped: the springs overshoot past 1, and opacity must not.
      opacity: progress.interpolate({ inputRange: [0, 1], outputRange: [0, 1], extrapolate: 'clamp' }),
      transform: [
        { translateY: progress.interpolate({ inputRange: [0, 1], outputRange: [translateY, 0] }) },
        { scale: progress.interpolate({ inputRange: [0, 1], outputRange: [scaleFrom, 1] }) },
      ],
    };
  }, [progress, reducedMotion, translateY, scaleFrom]);
};

/**
 * The scale-down every pressable shares, so a tile and a button answer a
 * finger the same way.
 */
export const usePressScale = (pressedScale = 0.96) => {
  const reducedMotion = useReducedMotion();
  const [scale] = useState(() => new Animated.Value(1));

  const animateTo = useCallback(
    (toValue: number) => {
      if (reducedMotion) return;
      Animated.spring(scale, { toValue, ...springs.press, useNativeDriver: true }).start();
    },
    [scale, reducedMotion],
  );

  const onPressIn = useCallback(() => animateTo(pressedScale), [animateTo, pressedScale]);
  const onPressOut = useCallback(() => animateTo(1), [animateTo]);

  return { scale, onPressIn, onPressOut };
};

/**
 * A slow breath for whatever currently has the turn. Loops until `enabled`
 * goes false, and never starts at all under reduced motion — a pulse that
 * cannot be stopped is exactly what that setting exists to prevent.
 */
export const usePulse = (enabled: boolean) => {
  const reducedMotion = useReducedMotion();
  const [pulse] = useState(() => new Animated.Value(0));

  useEffect(() => {
    if (!enabled || reducedMotion) {
      pulse.setValue(0);
      return undefined;
    }
    const loop = Animated.loop(
      Animated.sequence([
        Animated.timing(pulse, { toValue: 1, duration: 900, easing: Easing.inOut(Easing.quad), useNativeDriver: true }),
        Animated.timing(pulse, { toValue: 0, duration: 900, easing: Easing.inOut(Easing.quad), useNativeDriver: true }),
      ]),
    );
    loop.start();
    return () => loop.stop();
  }, [pulse, enabled, reducedMotion]);

  return pulse;
};

interface CountUpOptions {
  readonly duration?: number;
  /**
   * Where the roll starts on first render. Without it a freshly-mounted
   * component opens on its final number and never counts at all — which is
   * exactly the case on the end-of-hand screen, where the count is the point.
   */
  readonly from?: number;
}

/**
 * Counts a score up to its new value instead of swapping the digits.
 *
 * The number is driven from JS rather than `Animated` because the digits
 * themselves have to change, not a style property.
 */
export const useCountUp = (target: number, { duration = durations.celebrate, from }: CountUpOptions = {}): number => {
  const reducedMotion = useReducedMotion();
  const initial = reducedMotion ? target : from ?? target;
  const [display, setDisplay] = useState(initial);
  const currentRef = useRef(initial);

  useEffect(() => {
    const start = currentRef.current;
    if (start === target || reducedMotion) {
      currentRef.current = target;
      setDisplay(target);
      return undefined;
    }

    const startedAt = Date.now();
    let frame = requestAnimationFrame(function tick() {
      const t = Math.min(1, (Date.now() - startedAt) / duration);
      const eased = 1 - (1 - t) ** 3;
      const value = Math.round(start + (target - start) * eased);
      currentRef.current = value;
      setDisplay(value);
      if (t < 1) frame = requestAnimationFrame(tick);
    });

    return () => cancelAnimationFrame(frame);
  }, [target, duration, reducedMotion]);

  return display;
};

/**
 * The progress bar is driven by `scaleX` rather than `width` so it can run on
 * the native driver — animating width would hand every frame back to JS.
 */
export const useProgressBar = (progress: number): Animated.Value => {
  const reducedMotion = useReducedMotion();
  const [value] = useState(() => new Animated.Value(progress));

  useEffect(() => {
    if (reducedMotion) {
      value.setValue(progress);
      return undefined;
    }
    const animation = Animated.timing(value, {
      toValue: progress,
      duration: durations.celebrate,
      useNativeDriver: true,
    });
    animation.start();
    return () => animation.stop();
  }, [value, progress, reducedMotion]);

  return value;
};

interface SpringToOptions {
  readonly spring?: { friction: number; tension: number };
  /** Layout properties (height, width) cannot ride the native driver. */
  readonly native?: boolean;
  /**
   * Springs animate intent, not geometry: when this token changes alongside
   * the target — the container was resized, the window zoomed — the value
   * snaps straight there instead of visibly travelling.
   */
  readonly snapToken?: string | number;
}

/**
 * Springs a value toward whatever target it is handed, so a change of target
 * glides instead of snapping — each tile's slide to a new slot rides on
 * this. Snaps straight to the target under reduced motion.
 */
export const useSpringTo = (
  target: number,
  { spring = springs.settle, native = true, snapToken }: SpringToOptions = {},
): Animated.Value => {
  const reducedMotion = useReducedMotion();
  const [value] = useState(() => new Animated.Value(target));
  const lastTokenRef = useRef(snapToken);

  useEffect(() => {
    const resized = lastTokenRef.current !== snapToken;
    lastTokenRef.current = snapToken;
    if (reducedMotion || resized) {
      value.setValue(target);
      return undefined;
    }
    const animation = Animated.spring(value, { toValue: target, ...spring, useNativeDriver: native });
    animation.start();
    return () => animation.stop();
  }, [value, target, reducedMotion, spring, native, snapToken]);

  return value;
};

/**
 * Drives a 0→1 value from a boolean, for anything that has an "on" pose: a
 * selected tile lifting, a chip growing, a panel opening.
 *
 * Returns the raw progress value so callers can interpolate it into whichever
 * properties their pose needs.
 */
export const useToggleSpring = (active: boolean, spring = springs.settle): Animated.Value => {
  const reducedMotion = useReducedMotion();
  const [progress] = useState(() => new Animated.Value(active ? 1 : 0));

  useEffect(() => {
    const toValue = active ? 1 : 0;
    if (reducedMotion) {
      progress.setValue(toValue);
      return undefined;
    }
    const animation = Animated.spring(progress, { toValue, ...spring, useNativeDriver: true });
    animation.start();
    return () => animation.stop();
  }, [progress, active, reducedMotion, spring]);

  return progress;
};
