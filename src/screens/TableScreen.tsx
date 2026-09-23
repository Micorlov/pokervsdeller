import React from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { ActionBar } from '../components/ActionBar';
import { AchievementToast } from '../components/AchievementToast';
import { CardFan } from '../components/CardFan';
import { DealerClimb } from '../components/DealerClimb';
import { HandRankBadge } from '../components/HandRankBadge';
import { PlayerSeat } from '../components/PlayerSeat';
import { PotsRail } from '../components/PotsRail';
import { PubButton } from '../components/PubButton';
import { ResultFlash, ResultTone } from '../components/ResultFlash';
import { StreakBonusToast } from '../components/StreakBonusToast';
import { TopBar } from '../components/TopBar';
import { RE_UP_AMOUNT, canPlay } from '../domain/bankroll';
import { CATEGORY_ORDER, evaluateHand, handName } from '../domain/handRank';
import { ShowdownRoundState, dealerRankOf, humanSeat } from '../domain/showdown';
import {
  FORGE_LIMIT,
  PRESSURE_ROUND,
  ShowdownSettlement,
  bailRefund,
} from '../domain/showdownPayouts';
import { UseShowdownTableResult } from '../state/useShowdownTable';
import { CONTENT_MAX_WIDTH, colors, spacing, typography } from '../theme';

interface TableScreenProps {
  readonly table: UseShowdownTableResult;
  readonly onHome: () => void;
  readonly onRules: () => void;
  readonly onMenu: () => void;
}

/** The player's own hand is the one they hold — dealt largest of all. */
const PLAYER_CARD_SCALE = 1.15;

/**
 * How far the felt runs past the content column on each side, and the corner
 * radius that turns its top edge into the table's far rail. The radius is
 * half the felt's own width at phone size, which makes that edge a true
 * semicircle rather than a rounded rectangle.
 */
const TABLE_BLEED = 85;
const RAIL_CAP_RADIUS = 264;
const RAIL_THICKNESS = 14;

const flashFor = (settlement: ShowdownSettlement): { text: string; tone: ResultTone } => {
  const plus = `+${settlement.net}`;
  if (settlement.sweep) return { text: `${plus} · SWEEP!`, tone: 'win' };
  if (settlement.kickerStrike) return { text: `${plus} · Kicker Strike ×2`, tone: 'win' };
  if (settlement.vsDealer === 'bailed') {
    return { text: 'Bailed · half back', tone: 'push' };
  }
  if (settlement.vsDealer === 'win' && settlement.wonTable) {
    return { text: `${plus} · Beat them all`, tone: 'win' };
  }
  if (settlement.vsDealer === 'win') return { text: `${plus} · Beat the house`, tone: 'win' };
  if (settlement.wonTable && settlement.net >= 0) {
    return { text: `${plus} · Took the table`, tone: 'win' };
  }
  if (settlement.net === 0) return { text: 'Push', tone: 'push' };
  if (settlement.net > 0) return { text: plus, tone: 'win' };
  return { text: `−${Math.abs(settlement.net)}`, tone: 'lose' };
};

const statusLine = (
  round: ShowdownRoundState,
  kickerLocked: boolean,
  humanPending: boolean,
  humanBailed: boolean,
): string | null => {
  if (round.phase === 'dealing') return 'Dealing…';
  if (round.phase === 'kicker') {
    return kickerLocked
      ? 'The house draws…'
      : "Pick your kicker — match the house's last card to double a win.";
  }
  if (round.phase === 'actions') {
    if (humanBailed) return 'You bailed — the hand plays out.';
    return humanPending ? null : 'Around the table…';
  }
  if (round.phase === 'reveal') return 'Showdown…';
  return null;
};

/** The felt itself: the house's climb up top, two regulars, your fan. */
export const TableScreen: React.FC<TableScreenProps> = ({ table, onHome, onRules, onMenu }) => {
  const { round } = table;
  if (!round) return null;

  const human = humanSeat(round);
  const bots = round.seats.filter((seat) => !seat.isHuman);
  const settled = round.phase === 'settled';
  const humanBailed = human.status === 'bailed';
  const humanPending = round.phase === 'actions' && !humanBailed && human.lastMove === null;
  const pickingKicker = round.phase === 'kicker' && !table.kickerLocked;
  const madeRank = human.cards.length === 5 ? evaluateHand(human.cards) : null;
  const isBusted = settled && !canPlay(human.stack, round.ante);
  const flash = settled && human.settlement ? flashFor(human.settlement) : null;
  const status = statusLine(round, table.kickerLocked, humanPending, humanBailed);

  const canForge = humanPending && human.forgesUsed < FORGE_LIMIT && human.stack >= round.ante;
  const pressureAvailable =
    humanPending &&
    round.dealerCards.length === PRESSURE_ROUND &&
    !human.pressured &&
    human.stack >= round.ante;

  const fanMode = pickingKicker ? 'kicker' : humanPending && table.forgeArming ? 'discard' : null;
  const onSelectCard = pickingKicker ? table.selectKicker : table.forge;

  return (
    <View style={styles.screen}>
      <View style={styles.column}>
        <TopBar
          stake={`Ante ${round.ante}`}
          bankroll={human.stack}
          onHome={onHome}
          onRules={onRules}
          onMenu={onMenu}
        />
        <ScrollView style={styles.scrollView} contentContainerStyle={styles.scroll} showsVerticalScrollIndicator={false}>
          {/* The felt runs past both edges and only its far rail curves away,
              so the player reads as sitting at the near side of a big table
              rather than looking down at a small one. */}
          <View style={styles.table}>
            <View style={styles.rail} pointerEvents="none" />
            <View style={styles.felt} pointerEvents="none" />

            <View style={styles.tableContent}>
              <DealerClimb
                cards={round.dealerCards}
                rank={round.dealerCards.length === 5 ? dealerRankOf(round) : null}
                kickerStruck={settled && (human.settlement?.kickerStrike ?? false)}
              />

              <View style={styles.seatRow}>
                {bots.map((seat, index) => (
                  <PlayerSeat
                    key={seat.id}
                    seat={seat}
                    settled={settled}
                    isThinking={table.thinkingSeat === index + 1}
                  />
                ))}
              </View>
            </View>
          </View>
        </ScrollView>

        {/* The two prizes are pinned out of the scroller. In the pressure round
            the action bar grows a row, the felt above gives up the height, and
            these are the exact numbers the player is being asked to bet on —
            they must never be the thing that gets cut. */}
        <View style={styles.potsDock}>
          <PotsRail
            tablePot={round.tablePot}
            dealerStake={human.dealerStake}
            pressured={human.pressured}
          />
        </View>

        {/* Your own cards, your result and your move never scroll away: the
            table above can grow as long as it likes, but the three things a
            player acts on stay docked at the near edge. */}
        <View style={styles.handDock}>
          {table.newAchievements.length > 0 ? (
            <AchievementToast
              achievements={table.newAchievements}
              onDismiss={table.dismissAchievements}
            />
          ) : null}
          {table.streakBonus ? (
            <StreakBonusToast bonus={table.streakBonus} onDismiss={table.dismissStreakBonus} />
          ) : null}
          {flash ? (
            <ResultFlash text={flash.text} tone={flash.tone} flashKey={round.handNumber} />
          ) : null}
          <View style={styles.handArea}>
            <CardFan
              cards={human.cards}
              faceUp
              dimmed={humanBailed}
              scale={PLAYER_CARD_SCALE}
              animateIn={round.phase === 'dealing'}
              kickerIndex={human.kickerIndex}
              freshIndex={table.freshCardIndex}
              selectableMode={fanMode}
              onSelectCard={onSelectCard}
            />
            {madeRank && !humanBailed ? (
              <HandRankBadge
                label={handName(madeRank)}
                celebrated={CATEGORY_ORDER[madeRank.category] >= CATEGORY_ORDER.pair}
              />
            ) : null}
          </View>
          {status ? (
            <Text style={styles.status} accessibilityLiveRegion="polite">
              {status}
            </Text>
          ) : null}
        </View>

        <View style={styles.actionBar}>
          {pickingKicker ? (
            <PubButton
              label="Lock Kicker"
              onPress={table.lockKicker}
              disabled={human.kickerIndex === null}
              cue={null}
            />
          ) : null}
          {humanPending ? (
            <ActionBar
              ante={round.ante}
              forgesLeft={FORGE_LIMIT - human.forgesUsed}
              canForge={canForge}
              bailRefund={bailRefund(human.tableContrib + human.dealerStake)}
              pressureAvailable={pressureAvailable}
              pressureArmed={table.pressureArmed}
              onTogglePressure={table.setPressureArmed}
              forgeArming={table.forgeArming}
              onHold={table.hold}
              onBeginForge={table.beginForge}
              onCancelForge={table.cancelForge}
              onBail={table.bail}
            />
          ) : null}
          {settled ? (
            isBusted ? (
              <PubButton
                label={`Re-up +${RE_UP_AMOUNT}`}
                onPress={() => table.reUpNow(round.ante)}
                cue={null}
              />
            ) : (
              <PubButton label="Next Hand" onPress={table.nextHand} cue={null} />
            )
          ) : null}
        </View>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  screen: { flex: 1, alignItems: 'center' },
  // The felt is wider than the column, so the column has to clip it.
  column: { flex: 1, width: '100%', maxWidth: CONTENT_MAX_WIDTH, overflow: 'hidden' },
  scrollView: { flex: 1 },
  // flexGrow lets the felt reach the bottom of the scroller when the climb is
  // short: without it the table stops at its content and the ground shows
  // through as a hard horizontal seam across the middle of the screen.
  scroll: { paddingHorizontal: spacing.md, paddingBottom: spacing.sm, flexGrow: 1 },

  table: { position: 'relative', flex: 1 },
  // Both layers bleed past the column and round only their top corners: what
  // you see of the table's far edge is one broad arc, and the near edge runs
  // off the bottom of the screen towards the player.
  rail: {
    position: 'absolute',
    top: 0,
    bottom: 0,
    left: -TABLE_BLEED,
    right: -TABLE_BLEED,
    borderTopLeftRadius: RAIL_CAP_RADIUS,
    borderTopRightRadius: RAIL_CAP_RADIUS,
    backgroundColor: colors.bgEdge,
    experimental_backgroundImage: [
      {
        type: 'linear-gradient',
        direction: 'to bottom',
        colorStops: [
          { color: '#1F7342', positions: ['0%'] },
          { color: '#123F22', positions: ['62%'] },
          { color: '#0D3018', positions: ['100%'] },
        ],
      },
    ],
  },
  felt: {
    position: 'absolute',
    top: RAIL_THICKNESS,
    bottom: 0,
    left: -TABLE_BLEED + RAIL_THICKNESS,
    right: -TABLE_BLEED + RAIL_THICKNESS,
    borderTopLeftRadius: RAIL_CAP_RADIUS - RAIL_THICKNESS,
    borderTopRightRadius: RAIL_CAP_RADIUS - RAIL_THICKNESS,
    backgroundColor: colors.bgMid,
    experimental_backgroundImage: [
      {
        type: 'radial-gradient',
        shape: 'ellipse',
        size: { x: '78%', y: '52%' },
        position: { top: '26%', left: '50%' },
        colorStops: [
          { color: '#48B36C', positions: ['0%'] },
          { color: '#319A59', positions: ['48%'] },
          { color: '#217A42', positions: ['100%'] },
        ],
      },
    ],
  },
  tableContent: { gap: spacing.sm, paddingTop: spacing.sm, paddingBottom: spacing.xs },

  seatRow: { flexDirection: 'row', gap: spacing.sm },

  // Both docks carry the felt colour on so they read as the table's near edge
  // rather than separate trays bolted under it.
  potsDock: {
    paddingHorizontal: spacing.md,
    paddingTop: spacing.xs,
    paddingBottom: spacing.xs,
    backgroundColor: '#217A42',
  },
  handDock: {
    gap: spacing.sm,
    paddingHorizontal: spacing.md,
    paddingTop: spacing.sm,
    paddingBottom: spacing.sm,
    backgroundColor: '#217A42',
  },
  handArea: { gap: spacing.sm, alignItems: 'center' },
  status: { ...typography.body, textAlign: 'center', color: colors.textMuted },
  actionBar: {
    gap: spacing.sm,
    padding: spacing.md,
    backgroundColor: colors.surfaceRaised,
    borderTopWidth: 1,
    borderTopColor: colors.borderStrong,
  },
});
