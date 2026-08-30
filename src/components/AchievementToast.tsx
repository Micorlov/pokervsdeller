import React, { useEffect } from 'react';
import { Animated, StyleSheet, Text, View } from 'react-native';
import { Achievement } from '../domain/achievements';
import { useEntrance } from '../motion/hooks';
import { springs } from '../motion/timings';
import { colors, radii, shadows, spacing } from '../theme';

/** Long enough to read title and description, short enough not to block Next Hand. */
const AUTO_DISMISS_MS = 3200;

interface AchievementToastProps {
  /** Every badge the hand that just settled newly earned, in book order. */
  readonly achievements: readonly Achievement[];
  readonly onDismiss: () => void;
}

/**
 * The badge pop: shows the first freshly-earned achievement and rolls any
 * others into a "+N more" line rather than stacking several toasts at once.
 * Clears itself so a forgotten table never leaves it stuck on screen.
 */
export const AchievementToast: React.FC<AchievementToastProps> = ({ achievements, onDismiss }) => {
  const first = achievements[0] as Achievement | undefined;
  const entrance = useEntrance({ scaleFrom: 0.6, translateY: -8, spring: springs.pop, key: first?.id ?? null });

  useEffect(() => {
    if (!first) return undefined;
    const id = setTimeout(onDismiss, AUTO_DISMISS_MS);
    return () => clearTimeout(id);
  }, [first, onDismiss]);

  if (!first) return null;
  const extra = achievements.length - 1;

  return (
    <Animated.View style={[styles.toast, entrance]} accessible accessibilityLiveRegion="polite">
      <Text style={styles.icon}>🏅</Text>
      <View style={styles.copy}>
        <Text style={styles.title}>{first.title}</Text>
        <Text style={styles.subtitle}>
          {extra > 0 ? `+${extra} more badge${extra === 1 ? '' : 's'} unlocked` : first.description}
        </Text>
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
