import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { useCountUp } from '../motion/hooks';
import { colors, spacing, tabularNums, typography } from '../theme';

interface PotsRailProps {
  /** Everything the table is fighting over, forge fees included. */
  readonly tablePot: number;
  /** The human's chips staked against the house. */
  readonly dealerStake: number;
  /** True once the human has doubled that stake in the pressure round. */
  readonly pressured: boolean;
}

/**
 * The two prizes, side by side: the players-only table pot on the left, the
 * seat's own stake against the house on the right. Two pots is the whole
 * game, so they sit between the dealer and the table where both fights are
 * visible at once.
 *
 * This rail is pinned out of the scrolling felt — in the pressure round the
 * player is asked to bet on exactly these two numbers — so it is laid out on
 * one line and carries no chip graphic. The chip only ever restated the
 * number printed beside it, and pinned chrome has to earn its height.
 */
export const PotsRail: React.FC<PotsRailProps> = ({ tablePot, dealerStake, pressured }) => {
  const displayPot = useCountUp(tablePot);

  return (
    <View style={styles.rail}>
      <View style={styles.spot} accessible accessibilityLabel={`Table pot ${tablePot} chips`}>
        <Text style={styles.label} maxFontSizeMultiplier={1.3}>
          Table Pot
        </Text>
        <Text style={[styles.value, tabularNums]} maxFontSizeMultiplier={1.3}>
          {displayPot}
        </Text>
      </View>

      <View
        style={styles.spot}
        accessible
        accessibilityLabel={`Your stake against the house: ${dealerStake} chips${pressured ? ', pressured, pays three to one' : ''}`}
      >
        <Text style={styles.label} maxFontSizeMultiplier={1.3}>
          Vs the House
        </Text>
        <View style={styles.valueRow}>
          <Text style={[styles.value, tabularNums]} maxFontSizeMultiplier={1.3}>
            {dealerStake}
          </Text>
          {pressured ? (
            <Text style={styles.pressurePip} maxFontSizeMultiplier={1.3}>
              3:1
            </Text>
          ) : null}
        </View>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  // Label over figure, two columns. Side by side on one line they fit at the
  // sizes this table is drawn for but not at the worst case — a three-figure
  // pot, a doubled stake and the 3:1 pip, with the system text turned up —
  // and a number the player is about to bet on must never run off the edge.
  // Stacked, the rail cannot overflow at any width, and it still costs far
  // less height than the chip graphics it replaced.
  rail: {
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'flex-start',
    gap: spacing.xl,
  },
  spot: { alignItems: 'center', gap: 2 },
  valueRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.xs },
  label: { ...typography.label },
  value: { fontSize: 26, fontWeight: '800', letterSpacing: -0.6, color: colors.gold },
  pressurePip: {
    fontSize: 14,
    fontWeight: '800',
    letterSpacing: 0.5,
    color: colors.inkText,
    backgroundColor: colors.gold,
    borderRadius: 9,
    paddingHorizontal: 9,
    paddingVertical: 3,
    overflow: 'hidden',
  },
});
