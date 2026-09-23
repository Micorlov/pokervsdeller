import React from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { PubButton } from '../components/PubButton';
import { RE_UP_AMOUNT } from '../domain/bankroll';
import { BOT_PERSONAS } from '../domain/bots';
import { SEAT_COUNT } from '../domain/showdown';
import {
  FORGE_LIMIT,
  PRESSURE_ODDS,
  SWEEP_BONUS_UNITS,
} from '../domain/showdownPayouts';
import { STREAK_BONUS_UNITS } from '../domain/streakBonus';
import { Reveal } from '../motion/Reveal';
import { CONTENT_MAX_WIDTH, colors, radii, spacing, tabularNums, typography } from '../theme';

interface HowToPlayScreenProps {
  readonly onBack: () => void;
}

/**
 * The rules screen reads every payout and persona off the domain rather than
 * restating them, so the table can change size without the copy going stale.
 * The seat count is the one number that still needed spelling out.
 */
const SEAT_WORDS: Record<number, string> = {
  2: 'two',
  3: 'three',
  4: 'four',
  5: 'five',
  6: 'six',
};
const seatWord = SEAT_WORDS[SEAT_COUNT] ?? String(SEAT_COUNT);

const STEPS: { step: string; title: string; body: string }[] = [
  {
    step: '1',
    title: 'Two antes, two battles',
    body: 'Every seat puts one ante into the Table Pot and stakes another against the house. You are fighting the dealer for your stake and the whole table for the pot — and the pot swallows the chips of everyone who loses along the way.',
  },
  {
    step: '2',
    title: 'Pick your kicker',
    body: "You get five cards; the dealer starts with none. Before the climb, secretly mark one card as your kicker. If its rank matches the dealer's final card and you beat the dealer, your winnings double — a Kicker Strike.",
  },
  {
    step: '3',
    title: "The house's climb",
    body: `The dealer builds a hand one face-up card at a time. After each of the first four cards you choose: HOLD and stay in, FORGE a card away for a fresh one from the deck (one ante into the pot, ${FORGE_LIMIT} per hand, never the kicker), or BAIL and take half of everything you have put in straight back — the stake you abandon falls into the Table Pot.`,
  },
  {
    step: '4',
    title: 'The pressure round',
    body: `After the dealer's third card only, you may double your stake against the house. Beat the dealer from there and the doubled stake pays ${PRESSURE_ODDS} to 1 instead of even money.`,
  },
  {
    step: '5',
    title: 'Showdown',
    body: `The fifth card ends the climb. Beat the dealer's five and your stake pays out; every beaten stake joins the pot, and the best hand left at the table takes all of it — antes, forge fees, and everything the losers left behind. Do both outright and that is a Sweep — ${SWEEP_BONUS_UNITS} antes extra, on the house.`,
  },
];

/** Book order: the streak counts that pay, lowest first. */
const STREAK_BONUS_THRESHOLDS = Object.keys(STREAK_BONUS_UNITS)
  .map(Number)
  .sort((a, b) => a - b);

const PAY_LINES: { name: string; pays: string }[] = [
  { name: 'Beat the dealer', pays: '1 to 1' },
  { name: 'Beat the dealer, pressured', pays: `${PRESSURE_ODDS} to 1` },
  { name: 'Kicker Strike', pays: '×2 winnings' },
  { name: 'Best hand at the table', pays: 'the Table Pot' },
  { name: 'Sweep (both, outright)', pays: `+${SWEEP_BONUS_UNITS} antes` },
  {
    name: `Win streak (${STREAK_BONUS_THRESHOLDS.join('/')})`,
    pays: `+${STREAK_BONUS_THRESHOLDS.map((streak) => STREAK_BONUS_UNITS[streak]).join('/')} antes`,
  },
  { name: 'Bail, any time you act', pays: 'half back' },
];

export const HowToPlayScreen: React.FC<HowToPlayScreenProps> = ({ onBack }) => (
  <ScrollView contentContainerStyle={styles.content} showsVerticalScrollIndicator={false}>
    <Reveal index={0}>
      <Text style={styles.title}>How to play</Text>
      <Text style={styles.subtitle}>
        Showdown — {seatWord} seats against the house&apos;s climb.
      </Text>
    </Reveal>

    {STEPS.map((step, index) => (
      <Reveal key={step.step} index={index + 1} style={styles.step}>
        <Text style={[styles.stepNumber, tabularNums]}>{step.step}</Text>
        <View style={styles.stepText}>
          <Text style={styles.stepTitle}>{step.title}</Text>
          <Text style={styles.stepBody}>{step.body}</Text>
        </View>
      </Reveal>
    ))}

    <Reveal index={STEPS.length + 1} style={styles.creamCard}>
      <Text style={styles.creamLabel}>The payouts</Text>
      {PAY_LINES.map((line) => (
        <View key={line.name} style={styles.payRow}>
          <Text style={styles.payName}>{line.name}</Text>
          <Text style={[styles.payOdds, tabularNums]}>{line.pays}</Text>
        </View>
      ))}
      <Text style={styles.creamNote}>
        Pressure and a Kicker Strike stack: a pressured strike pays six times the stake. A tie with
        the dealer pushes your stake back untouched.
      </Text>
    </Reveal>

    <Reveal index={STEPS.length + 2} style={styles.creamCard}>
      <Text style={styles.creamLabel}>The regulars</Text>
      {BOT_PERSONAS.map((persona) => (
        <View key={persona.id} style={styles.personaRow}>
          <Text style={styles.personaName}>{persona.name}</Text>
          <Text style={styles.personaStyle}>{persona.style}</Text>
        </View>
      ))}
      <Text style={styles.creamNote}>
        They play their own money and quietly buy back in when they bust — by design, their stacks
        reset each time you open the app. Only your bankroll is saved between sessions, and it can
        always be topped up by {RE_UP_AMOUNT} on the house.
      </Text>
    </Reveal>

    <Reveal index={STEPS.length + 3}>
      <PubButton label="Back" onPress={onBack} variant="secondary" />
    </Reveal>
  </ScrollView>
);

const styles = StyleSheet.create({
  content: {
    padding: spacing.lg,
    gap: spacing.md,
    width: '100%',
    maxWidth: CONTENT_MAX_WIDTH,
    alignSelf: 'center',
  },
  title: { ...typography.title, fontSize: 32 },
  subtitle: { ...typography.bodyMuted, fontSize: 13.5, marginTop: 4 },
  step: { flexDirection: 'row', gap: spacing.md, alignItems: 'flex-start' },
  stepNumber: {
    width: 28,
    height: 28,
    borderRadius: 14,
    backgroundColor: colors.greenDim,
    color: colors.greenLight,
    fontSize: 13,
    fontWeight: '800',
    textAlign: 'center',
    lineHeight: 28,
  },
  stepText: { flex: 1, gap: 3 },
  stepTitle: { ...typography.body, fontWeight: '700' },
  stepBody: { ...typography.caption, lineHeight: 19 },
  // The rules cards are the one light surface in the app — a printed table
  // laid on the felt, so the numbers read as house rules rather than chrome.
  creamCard: {
    backgroundColor: colors.panelCream,
    borderRadius: radii.lg,
    padding: spacing.md,
    gap: spacing.xs,
    marginTop: spacing.xs,
  },
  creamLabel: { ...typography.label, color: colors.inkTextMuted, marginBottom: spacing.xs },
  payRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 5,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(36, 52, 71, 0.1)',
  },
  payName: { fontSize: 14, fontWeight: '600', color: colors.inkText },
  payOdds: { fontSize: 14, fontWeight: '800', color: colors.inkText },
  creamNote: { fontSize: 12.5, lineHeight: 18, color: colors.inkTextMuted, marginTop: spacing.sm },
  personaRow: { paddingVertical: 5 },
  personaName: { fontSize: 14, fontWeight: '700', color: colors.inkText },
  personaStyle: { fontSize: 12.5, color: colors.inkTextMuted },
});
