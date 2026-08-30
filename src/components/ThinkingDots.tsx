import React, { useEffect, useState } from 'react';
import { Animated, StyleSheet, Text, View } from 'react-native';
import { colors, typography } from '../theme';
import { useReducedMotion } from '../motion/MotionContext';

interface ThinkingDotsProps {
  readonly label: string;
}

const DOT_COUNT = 3;
const STEP_MS = 160;
const RISE_MS = 380;

/**
 * The "… is thinking" line.
 *
 * The AI takes a deliberate beat before playing so its move is readable; three
 * dots travelling left to right make that pause look like consideration rather
 * than the app having stalled. Reduced motion gets the same text, held still.
 */
export const ThinkingDots: React.FC<ThinkingDotsProps> = ({ label }) => {
  const reducedMotion = useReducedMotion();
  const [dots] = useState(() => Array.from({ length: DOT_COUNT }, () => new Animated.Value(0)));

  useEffect(() => {
    if (reducedMotion) return undefined;
    const loops = dots.map((value, index) =>
      Animated.loop(
        Animated.sequence([
          Animated.delay(index * STEP_MS),
          Animated.timing(value, { toValue: 1, duration: RISE_MS, useNativeDriver: true }),
          Animated.timing(value, { toValue: 0, duration: RISE_MS, useNativeDriver: true }),
          Animated.delay((DOT_COUNT - 1 - index) * STEP_MS),
        ]),
      ),
    );
    loops.forEach((loop) => loop.start());
    return () => loops.forEach((loop) => loop.stop());
  }, [dots, reducedMotion]);

  return (
    <View style={styles.row} accessibilityLabel={label}>
      <Text style={styles.label}>{label}</Text>
      <View style={styles.dots} accessibilityElementsHidden importantForAccessibility="no-hide-descendants">
        {dots.map((value, index) => (
          <Animated.View
            key={index}
            style={[
              styles.dot,
              {
                opacity: value.interpolate({ inputRange: [0, 1], outputRange: [0.28, 1] }),
                transform: [{ translateY: value.interpolate({ inputRange: [0, 1], outputRange: [0, -3] }) }],
              },
            ]}
          />
        ))}
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 7 },
  label: { ...typography.caption },
  dots: { flexDirection: 'row', alignItems: 'center', gap: 4, paddingBottom: 1 },
  dot: { width: 3.5, height: 3.5, borderRadius: 2, backgroundColor: colors.textMuted },
});
