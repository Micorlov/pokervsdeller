import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { useCountUp } from '../motion/hooks';
import { colors, spacing, tabularNums, typography } from '../theme';
import { PokerChip } from './PokerChip';

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
 */
export const PotsRail: React.FC<PotsRailProps> = ({ tablePot, dealerStake, pressured }) => {
  const displayPot = useCountUp(tablePot);

  return (
    <View style={styles.rail}>
      <View style={styles.spot} accessible accessibilityLabel={`Table pot ${tablePot} chips`}>
        <Text style={styles.label}>Table Pot</Text>
        <View style={styles.valueRow}>
          <PokerChip value={tablePot} size={42} />
          <Text style={[styles.value, tabularNums]}>{displayPot}</Text>
        </View>
      </View>

      <View
        style={styles.spot}
        accessible
        accessibilityLabel={`Your stake against the house: ${dealerStake} chips${pressured ? ', pressured, pays three to one' : ''}`}
      >
        <Text style={styles.label}>Vs the House</Text>
        <View style={styles.valueRow}>
          <PokerChip value={dealerStake} size={42} />
          <Text style={[styles.value, tabularNums]}>{dealerStake}</Text>
          {pressured ? <Text style={styles.pressurePip}>3:1</Text> : null}
        </View>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  rail: {
    flexDirection: 'row',
    justifyContent: 'center',
    gap: spacing.xl,
    paddingVertical: spacing.xs,
  },
  spot: { alignItems: 'center', gap: 6 },
  label: { ...typography.label },
  valueRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  value: { fontSize: 32, fontWeight: '800', letterSpacing: -0.6, color: colors.gold },
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
