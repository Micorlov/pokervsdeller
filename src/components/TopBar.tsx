import React from 'react';
import { Animated, Pressable, StyleSheet, Text, View } from 'react-native';
import { colors, MIN_TOUCH_TARGET, radii, shadows, spacing } from '../theme';
import { useFeedback } from '../feedback/FeedbackProvider';
import { usePressScale } from '../motion/hooks';

interface TopBarProps {
  /** The game, set small above the stake — e.g. "Showdown". */
  readonly modeTitle: string;
  /** The stake, which is the part worth reading — e.g. "Ante 10". */
  readonly modeValue: string;
  readonly onHome: () => void;
  readonly onMenu: () => void;
  readonly onRules: () => void;
}

interface IconButtonProps {
  readonly glyph: string;
  readonly label: string;
  readonly onPress: () => void;
}

const IconButton: React.FC<IconButtonProps> = ({ glyph, label, onPress }) => {
  const { play } = useFeedback();
  const press = usePressScale(0.9);
  const handlePress = (): void => {
    play('tap');
    onPress();
  };
  return (
    <Animated.View style={{ transform: [{ scale: press.scale }] }}>
      <Pressable
        onPress={handlePress}
        onPressIn={press.onPressIn}
        onPressOut={press.onPressOut}
        accessibilityRole="button"
        accessibilityLabel={label}
        style={({ pressed }) => [styles.iconButton, pressed && styles.pressed]}
      >
        <Text style={styles.iconGlyph}>{glyph}</Text>
      </Pressable>
    </Animated.View>
  );
};

/**
 * The table screen's chrome: home on the left, the stakes pill in the centre,
 * rules and menu on the right — the casual-game header layout.
 *
 * The pill stacks its two lines rather than running them together: at this
 * type size "Showdown · Ante 10" wraps beside three buttons, and the stake is
 * the half a player actually comes back to check.
 */
export const TopBar: React.FC<TopBarProps> = ({ modeTitle, modeValue, onHome, onMenu, onRules }) => (
  <View style={styles.bar}>
    <IconButton glyph="⌂" label="Home" onPress={onHome} />
    <View
      style={styles.modePill}
      accessible
      accessibilityLabel={`${modeTitle}, ${modeValue}`}
    >
      <Text style={styles.modeTitle} numberOfLines={1}>
        {modeTitle}
      </Text>
      <Text style={styles.modeValue} numberOfLines={1}>
        {modeValue}
      </Text>
    </View>
    <View style={styles.rightGroup}>
      <IconButton glyph="?" label="How to play" onPress={onRules} />
      <IconButton glyph="☰" label="Menu" onPress={onMenu} />
    </View>
  </View>
);

const styles = StyleSheet.create({
  bar: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    paddingHorizontal: spacing.md,
    paddingTop: spacing.sm,
    paddingBottom: spacing.sm,
  },
  iconButton: {
    width: MIN_TOUCH_TARGET,
    height: MIN_TOUCH_TARGET,
    borderRadius: radii.xs + 6,
    backgroundColor: colors.surfaceRaised,
    borderWidth: 1,
    borderColor: colors.borderStrong,
    alignItems: 'center',
    justifyContent: 'center',
    boxShadow: shadows.pillLift,
  },
  iconGlyph: { fontSize: 26, fontWeight: '700', color: colors.text },
  modePill: {
    flex: 1,
    minHeight: 58,
    borderRadius: radii.pill,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: spacing.sm,
  },
  modeTitle: {
    fontSize: 13,
    fontWeight: '700',
    letterSpacing: 1.2,
    textTransform: 'uppercase',
    color: colors.textMuted,
  },
  modeValue: { fontSize: 22, fontWeight: '800', letterSpacing: -0.2, color: colors.text },
  rightGroup: { flexDirection: 'row', gap: spacing.sm },
  pressed: { opacity: 0.85 },
});
