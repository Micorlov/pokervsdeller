import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { shadows, tabularNums } from '../theme';

interface PokerChipProps {
  readonly value: number;
  readonly size?: number;
}

/** Casino colour code: red fives, blue tens, green quarters, gold otherwise. */
const CHIP_GRADIENTS: Record<number, readonly [string, string]> = {
  5: ['#D14848', '#8E2323'],
  10: ['#3E7BC0', '#27508A'],
  25: ['#35A868', '#1D6B40'],
};

const FALLBACK_GRADIENT: readonly [string, string] = ['#F7DE96', '#CBA345'];

/**
 * A chip drawn in code on the tactile-object recipe: gradient face, dashed
 * white edge ring where a real chip carries its inserts, lifted by shadow.
 */
export const PokerChip: React.FC<PokerChipProps> = ({ value, size = 36 }) => {
  const [top, bottom] = CHIP_GRADIENTS[value] ?? FALLBACK_GRADIENT;
  return (
    <View
      accessible
      accessibilityLabel={`${value} chip`}
      style={[
        styles.chip,
        {
          width: size,
          height: size,
          borderRadius: size / 2,
          backgroundColor: bottom,
          experimental_backgroundImage: [
            {
              type: 'linear-gradient',
              direction: 'to bottom',
              colorStops: [
                { color: top, positions: ['0%'] },
                { color: bottom, positions: ['100%'] },
              ],
            },
          ],
        },
      ]}
    >
      <View style={[styles.ring, { borderRadius: size / 2, margin: size * 0.09 }]} />
      <Text style={[styles.value, tabularNums, { fontSize: size * 0.34 }]}>{value}</Text>
    </View>
  );
};

const styles = StyleSheet.create({
  chip: {
    alignItems: 'center',
    justifyContent: 'center',
    borderTopWidth: 1,
    borderBottomWidth: 2,
    borderTopColor: 'rgba(255, 255, 255, 0.5)',
    borderBottomColor: 'rgba(0, 0, 0, 0.45)',
    boxShadow: shadows.pillLift,
  },
  ring: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    borderWidth: 1.5,
    borderStyle: 'dashed',
    borderColor: 'rgba(255, 255, 255, 0.75)',
  },
  value: {
    color: '#FFFFFF',
    fontWeight: '800',
    textShadowColor: 'rgba(0, 0, 0, 0.35)',
    textShadowOffset: { width: 0, height: 1 },
    textShadowRadius: 1,
  },
});
