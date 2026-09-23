import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { evaluateHand, handName } from '../domain/handRank';
import { ShowdownSeatState } from '../domain/showdown';
import { useCountUp } from '../motion/hooks';
import { colors, radii, shadows, spacing, tabularNums } from '../theme';
import { CardFan } from './CardFan';
import { ThinkingDots } from './ThinkingDots';

interface PlayerSeatProps {
  readonly seat: ShowdownSeatState;
  readonly isThinking?: boolean;
  /** True once the showdown is paid and settlements exist. */
  readonly settled?: boolean;
}

/**
 * A seat's hand is read across the table, never held: the cards are dealt at
 * a legible size and then overlapped hard, so what survives is a row of
 * ranks rather than five miniature card faces nobody can make out.
 */
const SEAT_CARD_SCALE = 0.6;
/** Wide enough for a two-digit rank at SEAT_RANK_SIZE, and no wider. */
const SEAT_STRIP_WIDTH = 30;
const SEAT_RANK_SIZE = 20;
/** Nearly flat — a tilted fan puts the five indices on five baselines. */
const SEAT_FAN_STEP_DEG = 1.5;

type BadgeStyle = 'hold' | 'forge' | 'bail';

const badgeFor = (seat: ShowdownSeatState): { text: string; style: BadgeStyle } | null => {
  if (seat.status === 'bailed') return { text: 'BAIL', style: 'bail' };
  if (seat.lastMove?.kind === 'forge') return { text: 'FORGE', style: 'forge' };
  if (seat.lastMove?.kind === 'hold') return { text: 'HOLD', style: 'hold' };
  return null;
};

/**
 * A bot's place at the table: who they are, what they just did, what it cost
 * them. One of these sits on the rail per bot, straddling the felt's edge. A pressured seat wears a gold ring for the rest of the hand.
 */
export const PlayerSeat: React.FC<PlayerSeatProps> = ({
  seat,
  isThinking = false,
  settled = false,
}) => {
  const net = settled ? seat.settlement?.net ?? 0 : 0;
  const displayStack = useCountUp(seat.stack);
  const badge = badgeFor(seat);
  const bailed = seat.status === 'bailed';
  const hasCards = seat.cards.length === 5;
  const wonTable = settled && (seat.settlement?.wonTable ?? false);

  const summary = [
    seat.name,
    `${seat.stack} chips`,
    bailed ? 'bailed out' : seat.pressured ? 'pressured the house' : null,
    seat.lastMove && !bailed ? `${seat.lastMove.kind} this round` : null,
    hasCards ? handName(evaluateHand(seat.cards)) : null,
    wonTable ? 'took the table pot' : null,
    settled && net !== 0 ? `${net > 0 ? 'won' : 'lost'} ${Math.abs(net)}` : null,
  ]
    .filter(Boolean)
    .join(', ');

  return (
    <View
      style={[styles.seat, bailed && styles.seatBailed, wonTable && styles.seatWonTable]}
      accessible
      accessibilityLabel={summary}
    >
      <Text
        style={[styles.name, seat.pressured && styles.namePressured]}
        numberOfLines={1}
      >
        {seat.name}
      </Text>
      {hasCards ? (
        <CardFan
          cards={seat.cards}
          faceUp
          dimmed={bailed}
          scale={SEAT_CARD_SCALE}
          stripWidth={SEAT_STRIP_WIDTH}
          fanStepDeg={SEAT_FAN_STEP_DEG}
          rankSize={SEAT_RANK_SIZE}
        />
      ) : null}
      {isThinking ? (
        <ThinkingDots label="thinking" />
      ) : settled && net !== 0 ? (
        <Text style={[styles.net, tabularNums, net > 0 ? styles.netWin : styles.netLoss]}>
          {net > 0 ? `+${net}` : net}
        </Text>
      ) : badge ? (
        <View style={[styles.badge, badgeStyles[badge.style]]}>
          <Text style={[styles.badgeText, badgeTextStyles[badge.style]]}>{badge.text}</Text>
        </View>
      ) : (
        <Text style={[styles.stack, tabularNums]} numberOfLines={1}>
          {displayStack}
        </Text>
      )}
    </View>
  );
};

const styles = StyleSheet.create({
  seat: {
    alignItems: 'center',
    gap: spacing.xs,
    paddingVertical: 6,
    paddingHorizontal: 5,
    borderRadius: radii.sm,
    // Seats straddle the rail, so they carry a heavier fill than the felt
    // panels do — they have to read as objects sitting on the table.
    backgroundColor: 'rgba(0, 0, 0, 0.34)',
    borderWidth: 1,
    borderColor: colors.borderStrong,
    boxShadow: shadows.pillLift,
    flex: 1,
    minWidth: 0,
  },
  seatBailed: { opacity: 0.55 },
  seatWonTable: { borderColor: colors.gold, borderWidth: 2 },
  name: { fontSize: 19, fontWeight: '700', color: colors.text },
  namePressured: { color: colors.gold },
  stack: { fontSize: 17, fontWeight: '500', color: colors.textMuted },
  badge: {
    paddingVertical: 6,
    paddingHorizontal: 14,
    borderRadius: radii.pill,
    borderWidth: 1,
  },
  badgeText: { fontSize: 14, fontWeight: '800', letterSpacing: 0.8 },
  net: { fontSize: 24, fontWeight: '800' },
  netWin: { color: colors.gold },
  netLoss: { color: colors.textFaint },
});

const badgeStyles = StyleSheet.create({
  hold: { borderColor: colors.border, backgroundColor: 'transparent' },
  forge: { borderColor: colors.greenLight, backgroundColor: colors.greenDim },
  bail: { borderColor: 'transparent', backgroundColor: colors.redDim },
});

const badgeTextStyles = StyleSheet.create({
  hold: { color: colors.textMuted },
  forge: { color: colors.greenLight },
  bail: { color: colors.red },
});
