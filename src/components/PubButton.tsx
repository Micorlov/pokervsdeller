import React from 'react';
import { Animated, Pressable, StyleSheet, Text } from 'react-native';
import { colors, radii, shadows, spacing, typography } from '../theme';
import { CueName } from '../feedback/cues';
import { useFeedback } from '../feedback/FeedbackProvider';
import { usePressScale } from '../motion/hooks';

type PubButtonVariant = 'primary' | 'secondary' | 'danger' | 'bonus';

interface PubButtonProps {
  readonly label: string;
  /**
   * The terms of the move, on a smaller second line. Prices like "pay 10 · 2
   * left" used to share the label's line and broke wherever the width ran out,
   * splitting a number from the word it belonged to.
   */
  readonly detail?: string;
  readonly onPress: () => void;
  readonly variant?: PubButtonVariant;
  readonly disabled?: boolean;
  /**
   * The sound this press makes. Pass `null` for buttons whose *result* already
   * has a cue — the draw button's rattle should not be preceded by a click.
   */
  readonly cue?: CueName | null;
}

/**
 * Primary is the one juicy orange surface on a screen — everything else is
 * felt-glass, so the button that commits a move is impossible to mistake.
 *
 * The press dips the whole button rather than just fading it, which is what
 * makes a flat rectangle feel like something you pushed.
 */
export const PubButton: React.FC<PubButtonProps> = ({
  label,
  detail,
  onPress,
  variant = 'primary',
  disabled,
  cue = 'tap',
}) => {
  const { play } = useFeedback();
  const { scale, onPressIn, onPressOut } = usePressScale(0.965);

  const handlePress = (): void => {
    if (cue) play(cue);
    onPress();
  };

  return (
    <Animated.View style={{ transform: [{ scale }] }}>
      <Pressable
        onPress={handlePress}
        onPressIn={onPressIn}
        onPressOut={onPressOut}
        disabled={disabled}
        accessibilityRole="button"
        accessibilityLabel={detail ? `${label}, ${detail}` : label}
        accessibilityState={{ disabled: Boolean(disabled) }}
        style={({ pressed }) => [
          styles.base,
          styles[variant],
          disabled && styles.disabled,
          pressed && !disabled && styles.pressed,
        ]}
      >
        <Text style={[styles.label, styles[`${variant}Label`], disabled && styles.disabledLabel]}>{label}</Text>
        {detail ? (
          <Text style={[styles.detail, styles[`${variant}Detail`], disabled && styles.disabledLabel]}>
            {detail}
          </Text>
        ) : null}
      </Pressable>
    </Animated.View>
  );
};

const styles = StyleSheet.create({
  base: {
    borderRadius: radii.lg,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
  },
  primary: {
    minHeight: 76,
    backgroundColor: colors.orange,
    experimental_backgroundImage: [
      {
        type: 'linear-gradient',
        direction: 'to bottom',
        colorStops: [
          { color: colors.orange, positions: ['0%'] },
          { color: colors.orangeDeep, positions: ['100%'] },
        ],
      },
    ],
    // The dark bottom edge gives the casual-game "pressable candy" thickness.
    borderBottomWidth: 4,
    borderBottomColor: 'rgba(90, 43, 5, 0.55)',
    boxShadow: shadows.ctaGlow,
  },
  // Glass buttons carry a lit top edge and a shaded bottom one, so they read
  // as the same pressable family as the orange call to action, only quieter.
  secondary: {
    minHeight: 64,
    backgroundColor: colors.surfaceRaised,
    borderWidth: 1,
    borderColor: colors.borderStrong,
    borderTopColor: 'rgba(255, 255, 255, 0.34)',
    borderBottomWidth: 3,
    borderBottomColor: 'rgba(0, 0, 0, 0.28)',
  },
  danger: {
    minHeight: 64,
    backgroundColor: colors.redSurface,
    borderWidth: 1,
    borderColor: colors.redSurfaceEdge,
    borderBottomWidth: 3,
    borderBottomColor: 'rgba(0, 0, 0, 0.35)',
  },
  // Free chips are a gift, not a move: gold, the colour the theme keeps for
  // celebration, with dark ink so it never competes with the orange CTA.
  bonus: {
    minHeight: 64,
    backgroundColor: colors.gold,
    experimental_backgroundImage: [
      {
        type: 'linear-gradient',
        direction: 'to bottom',
        colorStops: [
          { color: colors.gold, positions: ['0%'] },
          { color: colors.goldDeep, positions: ['100%'] },
        ],
      },
    ],
    borderBottomWidth: 3,
    borderBottomColor: 'rgba(90, 60, 5, 0.5)',
    boxShadow: shadows.pillLift,
  },
  label: { ...typography.button },
  primaryLabel: { color: colors.text },
  secondaryLabel: { fontSize: 20, fontWeight: '700', letterSpacing: 0, color: colors.text },
  dangerLabel: { fontSize: 20, fontWeight: '700', letterSpacing: 0, color: colors.redInk },
  bonusLabel: { fontSize: 20, fontWeight: '800', letterSpacing: 0, color: colors.orangeInk },
  detail: { fontSize: 16, fontWeight: '600', textAlign: 'center', marginTop: 1 },
  primaryDetail: { color: 'rgba(255, 255, 255, 0.85)' },
  secondaryDetail: { color: colors.textMuted },
  dangerDetail: { color: 'rgba(255, 194, 196, 0.8)' },
  bonusDetail: { color: colors.orangeInk },
  // Disabled drops to plain glass: no glow, no action colour, nothing to press.
  disabled: {
    backgroundColor: colors.border,
    borderColor: 'transparent',
    borderTopColor: 'transparent',
    borderBottomColor: 'transparent',
    boxShadow: [],
  },
  disabledLabel: { color: colors.textFaint },
  // Kept alongside the scale so reduced-motion users still see a press land.
  pressed: { opacity: 0.85 },
});
