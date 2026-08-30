import React from 'react';
import { Animated, StyleSheet, Text, View } from 'react-native';
import { Card, cardName, isRedSuit, rankLabel, suitSymbol } from '../domain/cards';
import { useEntrance, useToggleSpring } from '../motion/hooks';
import { springs } from '../motion/timings';
import { CardSkin, getCardSkin, shadows } from '../theme';

interface PlayingCardProps {
  readonly card: Card;
  readonly faceUp: boolean;
  /** Folded hands dim and carry an ✕ — never state by colour alone. */
  readonly dimmed?: boolean;
  readonly scale?: number;
  /** Plays a deal/land entrance when the card first appears. */
  readonly animateIn?: boolean;
  /** Staggers this card behind its siblings, in milliseconds. */
  readonly entranceDelay?: number;
  readonly skin?: CardSkin;
  /**
   * Overrides the index size for cards read through a narrow sliver of a fan,
   * where the rank has to grow out of proportion to stay legible.
   */
  readonly rankSize?: number;
}

export const CARD_WIDTH = 62;
export const CARD_HEIGHT = 88;
const CORNER_RADIUS = 9;

/**
 * Jumbo index, the way a senior card room's deck is printed: the rank runs
 * about a third of the card's height instead of the sixth a standard deck
 * uses. The gradient, bevel and gloss are untouched — only the index grew.
 */
const RANK_TO_HEIGHT = 0.34;
/** The suit under the rank reads as its companion, not its equal. */
const SUIT_TO_RANK = 0.7;
/** The big corner pip, and the shortest card with room to carry one. */
const PIP_TO_HEIGHT = 0.34;
const PIP_MIN_HEIGHT = 70;

/**
 * A playing card drawn entirely in code, on the same tactile recipe as the
 * app's buttons: gradient face, light top bevel, heavy dark bottom bevel,
 * gloss pooling on top, drop shadow underneath.
 *
 * The flip is two-stage scaleX — the back squeezes to nothing, the face grows
 * out of it — which stays on the native driver and never needs a perspective
 * transform.
 *
 * The face carries one jumbo index in the top-left corner and a large pip
 * opposite it. A printed card repeats its index upside-down in the far corner
 * because a hand can be held either way up; a card on a screen never is, so
 * that second index is spent on making the first one twice the size.
 */
export const PlayingCard: React.FC<PlayingCardProps> = ({
  card,
  faceUp,
  dimmed = false,
  scale = 1,
  animateIn = false,
  entranceDelay = 0,
  skin,
  rankSize,
}) => {
  const cardSkin = skin ?? getCardSkin(undefined);
  const flip = useToggleSpring(faceUp, springs.settle);
  const entrance = useEntrance({
    delay: entranceDelay,
    translateY: -14,
    scaleFrom: 1.12,
    spring: springs.settle,
    key: card.id,
  });

  const width = CARD_WIDTH * scale;
  const height = CARD_HEIGHT * scale;
  const radius = CORNER_RADIUS * scale;
  const ink = isRedSuit(card.suit) ? cardSkin.inkRed : cardSkin.inkBlack;

  const rankFontSize = rankSize ?? height * RANK_TO_HEIGHT;
  const suitFontSize = rankFontSize * SUIT_TO_RANK;
  const pipFontSize = height >= PIP_MIN_HEIGHT ? height * PIP_TO_HEIGHT : 0;

  const faceScaleX = flip.interpolate({ inputRange: [0, 0.5, 1], outputRange: [0, 0, 1], extrapolate: 'clamp' });
  const backScaleX = flip.interpolate({ inputRange: [0, 0.5, 1], outputRange: [1, 0, 0], extrapolate: 'clamp' });

  const faceGradient = {
    backgroundColor: cardSkin.faceStops[0].color,
    experimental_backgroundImage: [
      {
        type: 'linear-gradient' as const,
        direction: 'to bottom',
        colorStops: cardSkin.faceStops.map((stop) => ({ color: stop.color, positions: [...stop.positions] })),
      },
    ],
    borderTopColor: cardSkin.bevelTop,
    borderBottomColor: cardSkin.bevelBottom,
  };

  const backGradient = {
    backgroundColor: cardSkin.backStops[cardSkin.backStops.length - 1].color,
    experimental_backgroundImage: [
      {
        type: 'linear-gradient' as const,
        direction: 'to bottom',
        colorStops: cardSkin.backStops.map((stop) => ({ color: stop.color, positions: [...stop.positions] })),
      },
    ],
  };

  const size = { width, height, borderRadius: radius };
  const glyph = suitSymbol(card.suit);

  const body = (
    <View
      style={size}
      accessible
      accessibilityLabel={faceUp ? cardName(card) : 'Face-down card'}
    >
      <Animated.View style={[styles.side, styles.card, faceGradient, size, dimmed && styles.dimmedFace, { transform: [{ scaleX: faceScaleX }] }]}>
        <View
          pointerEvents="none"
          style={[styles.gloss, { borderTopLeftRadius: radius, borderTopRightRadius: radius }]}
        />
        <View style={[styles.index, { top: height * 0.035, left: width * 0.07 }]}>
          <Text style={[styles.indexRank, { color: ink, fontSize: rankFontSize }]}>
            {rankLabel(card.rank)}
          </Text>
          <Text
            style={[styles.indexSuit, { color: ink, fontSize: suitFontSize, marginTop: -suitFontSize * 0.16 }]}
          >
            {glyph}
          </Text>
        </View>
        {pipFontSize > 0 ? (
          <Text
            style={[
              styles.pip,
              { color: ink, fontSize: pipFontSize, right: width * 0.06, bottom: height * 0.03 },
            ]}
          >
            {glyph}
          </Text>
        ) : null}
        {dimmed ? <Text style={[styles.foldedMark, { color: ink, fontSize: 13 * scale }]}>✕</Text> : null}
      </Animated.View>
      <Animated.View style={[styles.side, styles.card, styles.back, backGradient, size, { transform: [{ scaleX: backScaleX }] }]}>
        <View
          pointerEvents="none"
          style={[styles.gloss, { borderTopLeftRadius: radius, borderTopRightRadius: radius }]}
        />
        <View
          style={[
            styles.backInner,
            {
              borderColor: cardSkin.backBorder,
              margin: 5 * scale,
              borderRadius: Math.max(3, radius - 4 * scale),
            },
          ]}
        />
      </Animated.View>
    </View>
  );

  if (!animateIn) return body;
  return <Animated.View style={entrance}>{body}</Animated.View>;
};

const styles = StyleSheet.create({
  // The bevel is drawn with border colours — a light edge on top, a dark edge
  // below — which sells the card's thickness without any extra views.
  card: {
    borderTopWidth: 1.5,
    borderBottomWidth: 3,
    borderLeftWidth: 1,
    borderRightWidth: 1,
    borderLeftColor: 'rgba(0, 0, 0, 0.14)',
    borderRightColor: 'rgba(0, 0, 0, 0.14)',
    boxShadow: shadows.tileLift,
    overflow: 'hidden',
    alignItems: 'center',
    justifyContent: 'center',
  },
  side: { position: 'absolute', top: 0, left: 0 },
  back: {
    borderTopColor: 'rgba(255, 255, 255, 0.28)',
    borderBottomColor: 'rgba(0, 0, 0, 0.7)',
  },
  backInner: {
    flex: 1,
    alignSelf: 'stretch',
    borderWidth: 1.5,
    borderStyle: 'dashed',
  },
  gloss: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    height: '35%',
    experimental_backgroundImage: [
      {
        type: 'linear-gradient',
        direction: 'to bottom',
        colorStops: [
          { color: 'rgba(255, 255, 255, 0.32)', positions: ['0%'] },
          { color: 'rgba(255, 255, 255, 0)', positions: ['100%'] },
        ],
      },
    ],
  },
  index: { position: 'absolute', alignItems: 'center' },
  indexRank: { fontWeight: '800' },
  // The suit tucks up under the rank so the pair reads as one block of ink.
  indexSuit: {},
  pip: { position: 'absolute', opacity: 0.85 },
  dimmedFace: { opacity: 0.55 },
  foldedMark: {
    position: 'absolute',
    top: 3,
    right: 5,
    fontWeight: '700',
    opacity: 0.45,
  },
});
