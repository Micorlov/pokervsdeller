import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { Card } from '../domain/cards';
import { HandRank, handName } from '../domain/handRank';
import { partialThreat } from '../domain/threat';
import { colors, radii, spacing, typography } from '../theme';
import { HandRankBadge } from './HandRankBadge';
import { CARD_HEIGHT, CARD_WIDTH, PlayingCard } from './PlayingCard';

interface DealerClimbProps {
  /** The dealer's cards so far, all face up, oldest first. */
  readonly cards: readonly Card[];
  /** The dealer's finished hand, once all five cards are out. */
  readonly rank: HandRank | null;
  /** True when the human's kicker matched the dealer's final card. */
  readonly kickerStruck: boolean;
}

const DEALER_CARD_SCALE = 0.85;
const SLOT_COUNT = 5;
const METER_SEGMENTS = 5;

/**
 * The house's side of the table: five fixed slots the dealer fills one card
 * at a time, every card face up. The dashed empty slots make the climb
 * legible at a glance — everyone can see exactly how much hand is still to
 * come — and the threat meter reads the partial hand honestly.
 */
export const DealerClimb: React.FC<DealerClimbProps> = ({ cards, rank, kickerStruck }) => {
  const threat = partialThreat(cards);
  const litSegments = Math.round(threat * METER_SEGMENTS);

  return (
    <View style={styles.area}>
      <Text style={styles.label}>The House</Text>
      <View style={styles.cards}>
        {Array.from({ length: SLOT_COUNT }, (_, index) => {
          const card = cards[index];
          return card ? (
            <PlayingCard
              key={card.id}
              card={card}
              faceUp
              scale={DEALER_CARD_SCALE}
              animateIn
              entranceDelay={0}
            />
          ) : (
            <View key={`slot-${index}`} style={styles.emptySlot} />
          );
        })}
      </View>

      <View
        style={styles.meterRow}
        accessible
        accessibilityLabel={`House threat ${litSegments} of ${METER_SEGMENTS}`}
      >
        <Text style={styles.meterLabel}>Threat</Text>
        <View style={styles.meter}>
          {Array.from({ length: METER_SEGMENTS }, (_, index) => (
            <View
              key={index}
              style={[
                styles.meterSegment,
                index < litSegments &&
                  (litSegments >= 4 ? styles.meterSegmentHot : styles.meterSegmentLit),
              ]}
            />
          ))}
        </View>
      </View>

      {rank ? (
        <View style={styles.verdict}>
          <HandRankBadge label={handName(rank)} />
          {kickerStruck ? (
            <Text style={styles.strike} accessibilityLiveRegion="polite">
              KICKER STRIKE ×2
            </Text>
          ) : null}
        </View>
      ) : null}
    </View>
  );
};

const styles = StyleSheet.create({
  area: { alignItems: 'center', gap: spacing.xs },
  label: { ...typography.label },
  cards: { flexDirection: 'row', gap: 6, justifyContent: 'center' },
  emptySlot: {
    width: CARD_WIDTH * DEALER_CARD_SCALE,
    height: CARD_HEIGHT * DEALER_CARD_SCALE,
    borderRadius: radii.sm,
    borderWidth: 1.5,
    borderStyle: 'dashed',
    borderColor: colors.border,
  },
  meterRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  meterLabel: { ...typography.label },
  meter: { flexDirection: 'row', gap: 4 },
  meterSegment: {
    width: 30,
    height: 11,
    borderRadius: 6,
    // A dark slot reads as empty on the felt; the old tinted green did not.
    backgroundColor: colors.track,
  },
  meterSegmentLit: { backgroundColor: colors.gold },
  meterSegmentHot: { backgroundColor: colors.red },
  verdict: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  strike: { fontSize: 15, fontWeight: '800', letterSpacing: 0.8, color: colors.gold },
});
