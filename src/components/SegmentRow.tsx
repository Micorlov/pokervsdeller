import React from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { colors, MIN_TOUCH_TARGET, radii, spacing } from '../theme';

export interface SegmentOption<T> {
  readonly label: string;
  readonly value: T;
}

interface SegmentRowProps<T> {
  readonly title: string;
  readonly options: readonly SegmentOption<T>[];
  readonly selected: T;
  readonly onSelect: (value: T) => void;
}

/** A labelled row of choice pills — the segmented sliders of the reference panel. */
export const SegmentRow = <T,>({
  title,
  options,
  selected,
  onSelect,
}: SegmentRowProps<T>): React.ReactElement => (
  <View style={styles.segmentRow}>
    <Text style={styles.segmentTitle}>{title}</Text>
    <View style={styles.segmentOptions}>
      {options.map((option) => {
        const isSelected = option.value === selected;
        return (
          <Pressable
            key={option.label}
            onPress={() => onSelect(option.value)}
            accessibilityRole="button"
            accessibilityLabel={`${title}: ${option.label}`}
            accessibilityState={{ selected: isSelected }}
            style={({ pressed }) => [
              styles.segment,
              isSelected && styles.segmentSelected,
              pressed && styles.segmentPressed,
            ]}
          >
            <Text style={[styles.segmentLabel, isSelected && styles.segmentLabelSelected]}>
              {option.label}
            </Text>
          </Pressable>
        );
      })}
    </View>
  </View>
);

const styles = StyleSheet.create({
  segmentRow: { gap: spacing.sm },
  segmentTitle: { fontSize: 17, fontWeight: '800', color: colors.text },
  segmentOptions: { flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' },
  // The stake picker is the first thing a player touches, and it was the one
  // control in the app sitting under both the 44pt platform floor and the
  // 54pt target the rest of this table is built to.
  segment: {
    minWidth: 72,
    minHeight: MIN_TOUCH_TARGET,
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
    borderRadius: radii.pill,
    backgroundColor: colors.surfaceRaised,
    borderWidth: 1,
    borderColor: colors.border,
    alignItems: 'center',
    justifyContent: 'center',
  },
  segmentSelected: {
    backgroundColor: colors.orangeDeep,
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
    borderColor: 'rgba(255, 255, 255, 0.4)',
  },
  segmentPressed: { opacity: 0.85 },
  segmentLabel: { fontSize: 19, fontWeight: '700', color: colors.textMuted },
  segmentLabelSelected: { color: colors.text },
});
