import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { colors, radii, spacing } from '../theme';

interface HandRankBadgeProps {
  readonly label: string;
  /** Pair or better gets the gold ring — the hand worth saying out loud. */
  readonly celebrated?: boolean;
}

/** The pill that names a made hand: "Two Pair", "Flush", "High Card". */
export const HandRankBadge: React.FC<HandRankBadgeProps> = ({ label, celebrated = false }) => (
  <View style={[styles.badge, celebrated && styles.celebrated]}>
    <Text style={[styles.label, celebrated && styles.celebratedLabel]}>{label}</Text>
  </View>
);

const styles = StyleSheet.create({
  badge: {
    alignSelf: 'center',
    paddingVertical: 8,
    paddingHorizontal: spacing.lg,
    borderRadius: radii.pill,
    backgroundColor: colors.surfaceRaised,
    borderWidth: 1,
    borderColor: colors.border,
  },
  celebrated: {
    borderColor: colors.gold,
    backgroundColor: 'rgba(255, 201, 60, 0.12)',
  },
  label: {
    fontSize: 19,
    fontWeight: '700',
    letterSpacing: 0.2,
    color: colors.textMuted,
  },
  celebratedLabel: { color: colors.gold },
});
