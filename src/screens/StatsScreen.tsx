import React from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { PubButton } from '../components/PubButton';
import { ACHIEVEMENTS, unlockedAchievements } from '../domain/achievements';
import { HandCategory, handName } from '../domain/handRank';
import { Statistics, winRatePercent } from '../domain/stats';
import { Reveal } from '../motion/Reveal';
import { CONTENT_MAX_WIDTH, colors, radii, spacing, tabularNums, typography } from '../theme';

interface StatsScreenProps {
  readonly stats: Statistics;
  readonly onBack: () => void;
}

/** Strongest first — the same reading order as the paytable. */
const CATEGORY_STRONGEST_FIRST: readonly HandCategory[] = [
  'royalFlush',
  'straightFlush',
  'quads',
  'fullHouse',
  'flush',
  'straight',
  'trips',
  'twoPair',
  'pair',
  'highCard',
];

const categoryLabel = (category: HandCategory): string => handName({ category, tiebreak: [] });

const signed = (net: number): string => (net > 0 ? `+${net}` : String(net));

const netColor = (net: number): string =>
  net > 0 ? colors.gold : net < 0 ? colors.red : colors.textMuted;

const outcomeLine = (entry: Statistics['history'][number]): string => {
  const strike = entry.kickerStrike ? ' · Strike ×2' : '';
  if (entry.outcome === 'bail') return 'Bailed';
  if (entry.outcome === 'push') return 'Push';
  if (entry.outcome === 'sweep') return `${categoryLabel(entry.category)} · Sweep!${strike}`;
  if (entry.outcome === 'beatBoth') return `${categoryLabel(entry.category)} · Beat both${strike}`;
  if (entry.outcome === 'beatDealer') {
    return `${categoryLabel(entry.category)} · Beat the house${strike}`;
  }
  if (entry.outcome === 'beatTable') return `${categoryLabel(entry.category)} · Took the table`;
  return `${categoryLabel(entry.category)} · Lost`;
};

const Cell: React.FC<{ label: string; value: string }> = ({ label, value }) => (
  <View style={styles.cell}>
    <Text style={styles.cellLabel}>{label}</Text>
    <Text style={[styles.cellValue, tabularNums]}>{value}</Text>
  </View>
);

/** The scoreboard: lifetime numbers, the made-hands table, the last 20 hands. */
export const StatsScreen: React.FC<StatsScreenProps> = ({ stats, onBack }) => {
  const winRate = winRatePercent(stats);
  const madeCategories = CATEGORY_STRONGEST_FIRST.filter(
    (category) => (stats.categoryCounts[category] ?? 0) > 0,
  );
  const unlockedIds = new Set(unlockedAchievements(stats).map((achievement) => achievement.id));

  return (
    <ScrollView contentContainerStyle={styles.content} showsVerticalScrollIndicator={false}>
      <Reveal index={0}>
        <Text style={styles.title}>Your record</Text>
        <Text style={styles.subtitle}>
          {stats.bestHandCategory
            ? `Best hand made: ${categoryLabel(stats.bestHandCategory)}`
            : 'Play a hand and the book opens.'}
        </Text>
      </Reveal>

      <Reveal index={1} style={styles.panel}>
        <View style={styles.cellRow}>
          <Cell label="Hands" value={String(stats.handsPlayed)} />
          <Cell label="Won" value={String(stats.handsWon)} />
          <Cell label="Win rate" value={winRate === null ? '—' : `${winRate}%`} />
        </View>
        <View style={styles.cellRow}>
          <Cell label="Biggest win" value={String(stats.biggestWin)} />
          <Cell label="Streak" value={String(stats.currentStreak)} />
          <Cell label="Best streak" value={String(stats.bestStreak)} />
        </View>
        <View style={styles.cellRow}>
          <Cell label="Sweeps" value={String(stats.sweeps)} />
          <Cell label="Kicker strikes" value={String(stats.kickerStrikes)} />
          <Cell label="Bails" value={String(stats.bails)} />
        </View>
      </Reveal>

      <Reveal index={2} style={styles.panel}>
        <View style={styles.achievementsHeader}>
          <Text style={styles.panelLabel}>Achievements</Text>
          <Text style={[styles.achievementCount, tabularNums]}>
            {unlockedIds.size}/{ACHIEVEMENTS.length}
          </Text>
        </View>
        <View style={styles.achievementGrid}>
          {ACHIEVEMENTS.map((achievement) => {
            const unlocked = unlockedIds.has(achievement.id);
            return (
              <View key={achievement.id} style={[styles.badge, !unlocked && styles.badgeLocked]}>
                <Text style={styles.badgeIcon}>{unlocked ? '🏅' : '🔒'}</Text>
                <Text style={[styles.badgeTitle, !unlocked && styles.badgeTitleLocked]}>
                  {achievement.title}
                </Text>
              </View>
            );
          })}
        </View>
      </Reveal>

      <Reveal index={3} style={styles.creamCard}>
        <Text style={styles.creamLabel}>Hands made</Text>
        {madeCategories.length === 0 ? (
          <Text style={styles.creamEmpty}>Nothing in the book yet.</Text>
        ) : (
          madeCategories.map((category) => (
            <View key={category} style={styles.madeRow}>
              <Text style={styles.madeName}>{categoryLabel(category)}</Text>
              <Text style={[styles.madeCount, tabularNums]}>×{stats.categoryCounts[category]}</Text>
            </View>
          ))
        )}
      </Reveal>

      <Reveal index={4} style={styles.panel}>
        <Text style={styles.panelLabel}>Recent hands</Text>
        {stats.history.length === 0 ? (
          <Text style={styles.historyEmpty}>The last twenty hands will show up here.</Text>
        ) : (
          stats.history.map((entry, index) => (
            <View
              key={entry.at + index}
              style={[styles.historyRow, index === stats.history.length - 1 && styles.historyRowLast]}
            >
              <Text style={styles.historyLine}>{outcomeLine(entry)}</Text>
              <Text style={[styles.historyNet, tabularNums, { color: netColor(entry.net) }]}>
                {signed(entry.net)}
              </Text>
            </View>
          ))
        )}
      </Reveal>

      <Reveal index={5} style={styles.actions}>
        <PubButton label="Back" onPress={onBack} variant="secondary" />
      </Reveal>
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  content: {
    padding: spacing.lg,
    gap: spacing.md,
    width: '100%',
    maxWidth: CONTENT_MAX_WIDTH,
    alignSelf: 'center',
  },
  title: { ...typography.title, fontSize: 32 },
  subtitle: { ...typography.bodyMuted, marginTop: spacing.xs },
  panel: {
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radii.lg,
    padding: spacing.md,
    gap: spacing.sm,
  },
  panelLabel: { ...typography.label },
  achievementsHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  achievementCount: { ...typography.label, color: colors.textMuted },
  achievementGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  badge: {
    width: '30%',
    alignItems: 'center',
    gap: 4,
    paddingVertical: spacing.sm,
    borderRadius: radii.sm,
    backgroundColor: colors.surfaceRaised,
  },
  badgeLocked: { opacity: 0.45 },
  badgeIcon: { fontSize: 22 },
  badgeTitle: { fontSize: 11, fontWeight: '700', color: colors.text, textAlign: 'center' },
  badgeTitleLocked: { color: colors.textFaint },
  cellRow: { flexDirection: 'row', gap: spacing.sm },
  cell: { flex: 1, gap: 2 },
  cellLabel: { ...typography.label },
  cellValue: { fontSize: 24, fontWeight: '800', letterSpacing: -0.6, color: colors.text },
  creamCard: {
    backgroundColor: colors.panelCream,
    borderRadius: radii.lg,
    padding: spacing.md,
    gap: spacing.xs,
  },
  creamLabel: { ...typography.label, color: colors.inkTextMuted },
  creamEmpty: { fontSize: 14, fontWeight: '500', color: colors.inkTextMuted },
  madeRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  madeName: { fontSize: 15, fontWeight: '600', color: colors.inkText },
  madeCount: { fontSize: 15, fontWeight: '700', color: colors.inkTextMuted },
  historyEmpty: { ...typography.caption },
  historyRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: spacing.xs + 2,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  historyRowLast: { borderBottomWidth: 0 },
  historyLine: { ...typography.body, flexShrink: 1 },
  historyNet: { fontSize: 15, fontWeight: '800' },
  actions: { marginTop: spacing.xs },
});
