import React, { PropsWithChildren } from 'react';
import { Animated, StyleSheet } from 'react-native';
import { useEntrance } from './hooks';
import { springs } from './timings';

interface ScreenTransitionProps {
  /** Changing this cross-fades to whatever is now inside. */
  readonly transitionKey: string;
}

/**
 * The join between screens.
 *
 * Deliberately opacity-only: each screen already staggers its own panels in,
 * and sliding the whole screen as well would make one arrival read as two.
 */
export const ScreenTransition: React.FC<PropsWithChildren<ScreenTransitionProps>> = ({
  transitionKey,
  children,
}) => {
  const entrance = useEntrance({ translateY: 0, scaleFrom: 1, spring: springs.panel, key: transitionKey });
  return <Animated.View style={[styles.fill, entrance]}>{children}</Animated.View>;
};

const styles = StyleSheet.create({
  fill: { flex: 1 },
});
