import React from 'react';
import { Image, ScrollView, StyleSheet, Text, View } from 'react-native';
import { DealerPortrait } from '../components/DealerPortrait';
import { PubButton } from '../components/PubButton';
import { SegmentRow } from '../components/SegmentRow';
import { ANTE_OPTIONS, Ante, RE_UP_AMOUNT, canPlay } from '../domain/bankroll';
import { DAILY_BONUS_AMOUNT } from '../domain/dailyBonus';
import { useCountUp } from '../motion/hooks';
import { Reveal } from '../motion/Reveal';
import { CONTENT_MAX_WIDTH, colors, radii, spacing, tabularNums, typography } from '../theme';

interface HomeScreenProps {
  readonly bankroll: number;
  readonly ante: Ante;
  readonly onSelectAnte: (ante: Ante) => void;
  readonly onPlay: () => void;
  readonly onReUp: () => void;
  readonly onHowToPlay: () => void;
  readonly onSettings: () => void;
  readonly onStats: () => void;
  readonly canClaimDailyBonus: boolean;
  readonly onClaimDailyBonus: () => void;
}

const ANTE_SEGMENTS = ANTE_OPTIONS.map((value) => ({ label: String(value), value }));

/** The front door: bankroll, stakes, and one orange way onto the felt. */
export const HomeScreen: React.FC<HomeScreenProps> = ({
  bankroll,
  ante,
  onSelectAnte,
  onPlay,
  onReUp,
  onHowToPlay,
  onSettings,
  onStats,
  canClaimDailyBonus,
  onClaimDailyBonus,
}) => {
  const displayBankroll = useCountUp(bankroll);
  const isBusted = !canPlay(bankroll, ante);

  return (
    <ScrollView contentContainerStyle={styles.screen}>
      <View style={styles.column}>
        <Reveal index={0}>
          <DealerPortrait />
          <View style={styles.titleRow}>
            <Image
              source={require('../../assets/icon.png')}
              style={styles.gameIcon}
              accessible
              accessibilityLabel="Poker vs Dealer app icon"
            />
            <Text style={styles.title} numberOfLines={1} adjustsFontSizeToFit minimumFontScale={0.6}>
              Showdown
            </Text>
          </View>
          <Text style={styles.subtitle}>
            Dealer vs Three — beat the house&apos;s climb, and the whole table
          </Text>
        </Reveal>

        <Reveal index={1} style={styles.panelWrap}>
          <View style={styles.panel}>
            <Text style={styles.panelLabel}>Bankroll</Text>
            <Text style={[styles.bankroll, tabularNums]}>{displayBankroll}</Text>
            {isBusted ? (
              <Text style={styles.bustNote}>Not enough to cover a full hand — re-up on the house.</Text>
            ) : null}
            {canClaimDailyBonus ? (
              <View style={styles.bonusButton}>
                <PubButton
                  label={`Daily bonus +${DAILY_BONUS_AMOUNT}`}
                  onPress={onClaimDailyBonus}
                  variant="secondary"
                  cue={null}
                />
              </View>
            ) : (
              <Text style={styles.bonusNote}>Daily bonus claimed — come back tomorrow.</Text>
            )}
          </View>
        </Reveal>

        <Reveal index={2} style={styles.panelWrap}>
          <View style={styles.panel}>
            <SegmentRow title="Table stakes" options={ANTE_SEGMENTS} selected={ante} onSelect={onSelectAnte} />
          </View>
        </Reveal>

        <Reveal index={3} style={styles.actions}>
          {isBusted ? (
            <PubButton label={`Re-up +${RE_UP_AMOUNT}`} onPress={onReUp} cue="chip" />
          ) : (
            <PubButton label="Take a Seat" onPress={onPlay} cue={null} />
          )}
          <PubButton label="Your Record" onPress={onStats} variant="secondary" />
          <View style={styles.secondaryRow}>
            <View style={styles.secondaryButton}>
              <PubButton label="How to Play" onPress={onHowToPlay} variant="secondary" />
            </View>
            <View style={styles.secondaryButton}>
              <PubButton label="Settings" onPress={onSettings} variant="secondary" />
            </View>
          </View>
        </Reveal>
      </View>
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  screen: { flexGrow: 1, alignItems: 'center', justifyContent: 'center', padding: spacing.lg },
  column: { width: '100%', maxWidth: CONTENT_MAX_WIDTH, gap: spacing.lg },
  titleRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: spacing.sm },
  gameIcon: { width: 52, height: 52, borderRadius: radii.md },
  title: { ...typography.title, textAlign: 'center', flexShrink: 1 },
  subtitle: { ...typography.bodyMuted, textAlign: 'center', marginTop: spacing.xs },
  panelWrap: { width: '100%' },
  panel: {
    backgroundColor: colors.surface,
    borderRadius: radii.lg,
    borderWidth: 1,
    borderColor: colors.border,
    padding: spacing.lg,
    gap: spacing.xs,
  },
  panelLabel: { ...typography.label },
  bankroll: { fontSize: 44, fontWeight: '800', letterSpacing: -1.3, color: colors.gold },
  bustNote: { ...typography.caption, color: colors.textMuted },
  bonusButton: { marginTop: spacing.sm },
  bonusNote: { ...typography.caption, color: colors.textFaint, marginTop: spacing.xs },
  actions: { gap: spacing.md },
  secondaryRow: { flexDirection: 'row', gap: spacing.sm },
  secondaryButton: { flex: 1 },
});
