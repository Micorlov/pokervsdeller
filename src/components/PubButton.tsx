import React from 'react';
import { Animated, Pressable, StyleSheet, Text } from 'react-native';
import { colors, radii, shadows, spacing, typography } from '../theme';
import { CueName } from '../feedback/cues';
import { useFeedback } from '../feedback/FeedbackProvider';
import { usePressScale } from '../motion/hooks';

type PubButtonVariant = 'primary' | 'secondary' | 'danger';

interface PubButtonProps {
  readonly label: string;
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
        accessibilityLabel={label}
        accessibilityState={{ disabled: Boolean(disabled) }}
        style={({ pressed }) => [
          styles.base,
          styles[variant],
          disabled && styles.disabled,
          pressed && !disabled && styles.pressed,
        ]}
      >
        <Text style={[styles.label, styles[`${variant}Label`], disabled && styles.disabledLabel]}>{label}</Text>
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
  secondary: {
    minHeight: 64,
    backgroundColor: colors.surfaceRaised,
    borderWidth: 1,
    borderColor: colors.borderStrong,
  },
  danger: {
    minHeight: 64,
    backgroundColor: colors.redSurface,
    borderWidth: 1,
    borderColor: colors.redSurfaceEdge,
  },
  label: { ...typography.button },
  primaryLabel: { color: colors.text },
  secondaryLabel: { fontSize: 20, fontWeight: '700', letterSpacing: 0, color: colors.text },
  dangerLabel: { fontSize: 20, fontWeight: '700', letterSpacing: 0, color: colors.redInk },
  // Disabled drops to plain glass: no glow, no action colour, nothing to press.
  disabled: { backgroundColor: colors.border, borderColor: 'transparent', boxShadow: [] },
  disabledLabel: { color: colors.textFaint },
  // Kept alongside the scale so reduced-motion users still see a press land.
  pressed: { opacity: 0.85 },
});
