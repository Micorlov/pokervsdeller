import React, { useEffect } from 'react';
import { Animated, StyleSheet, Text, View } from 'react-native';
import { useEntrance } from '../motion/hooks';
import { springs } from '../motion/timings';
import { colors, radii, shadows, spacing } from '../theme';

/** Same beat as AchievementToast — long enough to read, short of blocking Next Hand. */
const AUTO_DISMISS_MS = 3200;

interface StreakBonusToastProps {
  /** The bonus the hand that just settled paid, or null when there's nothing to show. */
  readonly bonus: { readonly streak: number; readonly amount: number } | null;
  readonly onDismiss: () => void;
}

/** The streak-bonus pop: fires on the hand that lands a streak on 3/5/7/10. */
export const StreakBonusToast: React.FC<StreakBonusToastProps> = ({ bonus, onDismiss }) => {
  const entrance = useEntrance({
    scaleFrom: 0.6,
    translateY: -8,
    spring: springs.pop,
    key: bonus ? `${bonus.streak}-${bonus.amount}` : null,
  });

  useEffect(() => {
    if (!bonus) return undefined;
    const id = setTimeout(onDismiss, AUTO_DISMISS_MS);
    return () => clearTimeout(id);
  }, [bonus, onDismiss]);

  if (!bonus) return null;

  return (
    <Animated.View style={[styles.toast, entrance]} accessible accessibilityLiveRegion="polite">
      <Text style={styles.icon}>🔥</Text>
      <View style={styles.copy}>
        <Text style={styles.title}>{bonus.streak}-Win Streak</Text>
        <Text style={styles.subtitle}>+{bonus.amount} on the house</Text>
      </View>
    </Animated.View>
  );
};

const styles = StyleSheet.create({
  toast: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    alignSelf: 'center',
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
    borderRadius: radii.lg,
    borderWidth: 1.5,
    borderColor: colors.gold,
    backgroundColor: 'rgba(255, 201, 60, 0.16)',
    boxShadow: shadows.pillLift,
  },
  icon: { fontSize: 24 },
  copy: { gap: 2 },
  title: { fontSize: 15, fontWeight: '800', color: colors.gold, letterSpacing: -0.2 },
  subtitle: { fontSize: 12, fontWeight: '600', color: colors.textMuted },
});
