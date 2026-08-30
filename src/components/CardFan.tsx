import React from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { Card, cardName } from '../domain/cards';
import { STAGGER_MS } from '../motion/timings';
import { colors } from '../theme';
import { CARD_WIDTH, PlayingCard } from './PlayingCard';

interface CardFanProps {
  readonly cards: readonly Card[];
  readonly faceUp: boolean;
  readonly dimmed?: boolean;
  readonly scale?: number;
  readonly animateIn?: boolean;
  /** Which card wears the kicker star, if one has been picked. */
  readonly kickerIndex?: number | null;
  /**
   * When set, cards become tappable: 'kicker' to pick the kicker, 'discard'
   * to pick a forge trade-in (the kicker card is off-limits and dims).
   */
  readonly selectableMode?: 'kicker' | 'discard' | null;
  readonly onSelectCard?: (index: number) => void;
  /**
   * How wide a sliver of each covered card stays visible, in points. A seat's
   * hand sets this to whatever its index needs to stay readable; the player's
   * own hand leaves it unset and takes the roomier default overlap.
   */
  readonly stripWidth?: number;
  /** Flattens the arc, so a tight fan keeps its indices on one line. */
  readonly fanStepDeg?: number;
  readonly rankSize?: number;
}

/** How far each card tips from its neighbour, in degrees. */
const FAN_STEP_DEG = 3.5;
/** The outer cards drop slightly, so the row reads as held, not laid flat. */
const FAN_DROP_PX = 3;
const OVERLAP_PX = 14;

/**
 * Five cards in a shallow fan, entering one after another the way a dealt
 * hand arrives. Stagger lives here at the row level, not in each card; so
 * do the kicker star and the tap-to-pick modes, which are row concerns.
 *
 * Every card but the last is covered on its right, which leaves its index —
 * the one corner that matters — showing. `stripWidth` sets how much of that
 * corner survives, so a cramped seat can trade fan width for a legible rank.
 */
export const CardFan: React.FC<CardFanProps> = ({
  cards,
  faceUp,
  dimmed = false,
  scale = 1,
  animateIn = false,
  kickerIndex = null,
  selectableMode = null,
  onSelectCard,
  stripWidth,
  fanStepDeg = FAN_STEP_DEG,
  rankSize,
}) => {
  const middle = (cards.length - 1) / 2;
  const overlap = stripWidth === undefined ? OVERLAP_PX * scale : CARD_WIDTH * scale - stripWidth;
  return (
    <View
      style={styles.row}
      accessible={selectableMode === null}
      accessibilityLabel={faceUp ? 'Your hand' : 'A face-down hand'}
    >
      {cards.map((card, index) => {
        const offset = index - middle;
        const isKicker = index === kickerIndex;
        const isLockedOut = selectableMode === 'discard' && isKicker;
        const isTappable = selectableMode !== null && !isLockedOut;

        const face = (
          <PlayingCard
            card={card}
            faceUp={faceUp}
            dimmed={dimmed || isLockedOut}
            scale={scale}
            animateIn={animateIn}
            entranceDelay={index * STAGGER_MS}
            rankSize={rankSize}
          />
        );

        return (
          <View
            key={card.id}
            style={{
              marginLeft: index === 0 ? 0 : -overlap,
              transform: [
                { rotate: `${offset * fanStepDeg}deg` },
                { translateY: Math.abs(offset) * FAN_DROP_PX * scale },
              ],
              zIndex: index,
            }}
          >
            {isTappable ? (
              <Pressable
                onPress={() => onSelectCard?.(index)}
                accessibilityRole="button"
                accessibilityLabel={
                  selectableMode === 'kicker'
                    ? `Make ${cardName(card)} your kicker`
                    : `Trade ${cardName(card)} away`
                }
              >
                {face}
              </Pressable>
            ) : (
              face
            )}
            {isKicker ? (
              <View style={styles.kickerStar} pointerEvents="none">
                <Text style={styles.kickerStarText}>★</Text>
              </View>
            ) : null}
          </View>
        );
      })}
    </View>
  );
};

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
  },
  kickerStar: {
    position: 'absolute',
    top: -10,
    right: -6,
    width: 30,
    height: 30,
    borderRadius: 15,
    backgroundColor: colors.gold,
    alignItems: 'center',
    justifyContent: 'center',
    zIndex: 10,
  },
  kickerStarText: { fontSize: 17, fontWeight: '800', color: colors.inkText },
});
