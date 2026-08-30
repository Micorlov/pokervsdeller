import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { colors, spacing, typography } from '../theme';
import { PubButton } from './PubButton';

interface ActionBarProps {
  readonly ante: number;
  readonly forgesLeft: number;
  /** Whether a forge is affordable and allowed right now. */
  readonly canForge: boolean;
  /** What a bail hands back right now — shown plainly on the button. */
  readonly bailRefund: number;
  /** True only in the pressure round, with the stake still single and affordable. */
  readonly pressureAvailable: boolean;
  readonly pressureArmed: boolean;
  readonly onTogglePressure: (armed: boolean) => void;
  /** True while the player is picking which card a forge trades away. */
  readonly forgeArming: boolean;
  readonly onHold: () => void;
  readonly onBeginForge: () => void;
  readonly onCancelForge: () => void;
  readonly onBail: () => void;
}

/**
 * The player's move for one round of the climb: bail, forge, or hold, with
 * the pressure toggle folded in when the round offers it. The bail button
 * states its refund outright — leaving a hand is a priced decision here, not
 * a defeat.
 */
export const ActionBar: React.FC<ActionBarProps> = ({
  ante,
  forgesLeft,
  canForge,
  bailRefund,
  pressureAvailable,
  pressureArmed,
  onTogglePressure,
  forgeArming,
  onHold,
  onBeginForge,
  onCancelForge,
  onBail,
}) => {
  if (forgeArming) {
    return (
      <View style={styles.bar}>
        <Text style={styles.hint} accessibilityLiveRegion="polite">
          Tap a card to trade it in — the ★ kicker stays.
        </Text>
        <PubButton label="Cancel Forge" onPress={onCancelForge} variant="secondary" cue={null} />
      </View>
    );
  }

  return (
    <View style={styles.bar}>
      {pressureAvailable ? (
        <PubButton
          label={pressureArmed ? `Pressure armed — win pays 3:1` : `Pressure the house +${ante}`}
          onPress={() => onTogglePressure(!pressureArmed)}
          variant="secondary"
          cue={null}
        />
      ) : null}
      {/* Hold gets a line to itself: it is the move most hands end on, and at
          this type size three labels never fit across one row. */}
      <View style={styles.row}>
        <View style={styles.button}>
          <PubButton label={`Bail +${bailRefund}`} onPress={onBail} variant="danger" cue={null} />
        </View>
        <View style={styles.button}>
          <PubButton
            label={`Forge −${ante} (${forgesLeft})`}
            onPress={onBeginForge}
            variant="secondary"
            disabled={!canForge}
            cue={null}
          />
        </View>
      </View>
      <PubButton label="Hold" onPress={onHold} cue={null} />
    </View>
  );
};

const styles = StyleSheet.create({
  bar: { gap: spacing.sm },
  row: { flexDirection: 'row', gap: spacing.sm },
  button: { flex: 1 },
  hint: { ...typography.caption, textAlign: 'center', color: colors.gold },
});
