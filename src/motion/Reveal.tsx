import React, { PropsWithChildren } from 'react';
import { Animated, StyleProp, ViewStyle } from 'react-native';
import { useEntrance } from './hooks';
import { STAGGER_MS } from './timings';

interface RevealProps {
  /** Position in a staggered group. Ignored when `delay` is given. */
  readonly index?: number;
  readonly delay?: number;
  readonly translateY?: number;
  readonly scaleFrom?: number;
  /**
   * The wrapped element's own layout style. It belongs on the wrapper, not
   * inside it, or the extra view collapses the layout it was standing in for.
   */
  readonly style?: StyleProp<ViewStyle>;
}

/**
 * Reveals a block of a screen, one after another down the page.
 *
 * Screens are built as a handful of panels, so staggering at panel level gives
 * an arrival that reads top-to-bottom without every label animating separately.
 */
export const Reveal: React.FC<PropsWithChildren<RevealProps>> = ({
  index = 0,
  delay,
  translateY = 12,
  scaleFrom = 1,
  style,
  children,
}) => {
  const entrance = useEntrance({ delay: delay ?? index * STAGGER_MS, translateY, scaleFrom });
  return <Animated.View style={[style, entrance]}>{children}</Animated.View>;
};
