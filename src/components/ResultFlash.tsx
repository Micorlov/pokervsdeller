import React from 'react';
import { Animated, StyleSheet, Text } from 'react-native';
import { useEntrance } from '../motion/hooks';
import { springs } from '../motion/timings';
import { colors, radii, shadows, tabularNums } from '../theme';

export type ResultTone = 'win' | 'lose' | 'push';

interface ResultFlashProps {
  readonly text: string;
  readonly tone: ResultTone;
  /** Re-pops the flash when it changes — one pop per settled hand. */
  readonly flashKey: string | number;
}

/**
 * The settlement banner: pops with the one overshooting spring the app allows
 * itself, then stands until the next hand clears it. Wins are the only gold
 * moment on the table; a loss stays muted — never alarming red.
 */
export const ResultFlash: React.FC<ResultFlashProps> = ({ text, tone, flashKey }) => {
  const entrance = useEntrance({ scaleFrom: 0.6, translateY: 0, spring: springs.pop, key: flashKey });
  return (
    <Animated.View
      style={[styles.flash, styles[tone], entrance]}
      accessible
      accessibilityLiveRegion="polite"
      accessibilityLabel={text}
    >
      <Text style={[styles.text, tabularNums, styles[`${tone}Text`]]}>{text}</Text>
    </Animated.View>
  );
};

const styles = StyleSheet.create({
  flash: {
    alignSelf: 'center',
    paddingVertical: 12,
    paddingHorizontal: 28,
    borderRadius: radii.pill,
    borderWidth: 2,
    boxShadow: shadows.pillLift,
  },
  win: { backgroundColor: 'rgba(255, 201, 60, 0.16)', borderColor: colors.gold },
  lose: { backgroundColor: colors.surfaceRaised, borderColor: colors.borderStrong },
  push: { backgroundColor: colors.surface, borderColor: colors.border },
  text: { fontSize: 26, fontWeight: '800', letterSpacing: -0.4 },
  winText: { color: colors.gold },
  loseText: { color: colors.textMuted },
  pushText: { color: colors.textMuted },
});
