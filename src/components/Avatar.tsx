import React from 'react';
import { StyleSheet, Text, View } from 'react-native';

export interface AvatarSpec {
  /** 'initial' draws a letter on a gradient; 'emoji' renders the glyph itself. */
  readonly kind: 'initial' | 'emoji';
  readonly value: string;
  /** Gradient behind an initial, top to bottom. */
  readonly gradient?: readonly [string, string];
}

interface AvatarProps extends AvatarSpec {
  readonly size?: number;
}

const DEFAULT_GRADIENT: readonly [string, string] = ['#4A7BAF', '#2E5580'];

/**
 * A rounded-square avatar chip drawn entirely in code — an initial on a
 * gradient for the player, an emoji face for the AI. No image assets.
 */
export const Avatar: React.FC<AvatarProps> = ({ kind, value, gradient = DEFAULT_GRADIENT, size = 44 }) => {
  const [gradTop, gradBottom] = gradient;
  return (
    <View
      style={[
        styles.frame,
        {
          width: size,
          height: size,
          borderRadius: size * 0.28,
          backgroundColor: gradBottom,
          experimental_backgroundImage: [
            {
              type: 'linear-gradient',
              direction: 'to bottom',
              colorStops: [
                { color: gradTop, positions: ['0%'] },
                { color: gradBottom, positions: ['100%'] },
              ],
            },
          ],
        },
      ]}
    >
      <Text
        style={[
          kind === 'initial' ? styles.initial : styles.emoji,
          { fontSize: size * (kind === 'initial' ? 0.42 : 0.58) },
        ]}
      >
        {value}
      </Text>
    </View>
  );
};

const styles = StyleSheet.create({
  frame: {
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.22)',
  },
  initial: { fontWeight: '600', color: '#FFFFFF', letterSpacing: 0.5 },
  emoji: { lineHeight: undefined },
});
