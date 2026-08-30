import React, { useEffect, useRef } from 'react';
import { Alert, ScrollView, StyleSheet, Switch, Text, View } from 'react-native';
import { PubButton } from '../components/PubButton';
import { STARTING_BANKROLL } from '../domain/bankroll';
import { useFeedback } from '../feedback/FeedbackProvider';
import { Reveal } from '../motion/Reveal';
import { CONTENT_MAX_WIDTH, MIN_TOUCH_TARGET, colors, radii, spacing, typography } from '../theme';
import { AppSettings } from '../state/settingsStore';

interface SettingsScreenProps {
  readonly settings: AppSettings;
  readonly onUpdate: (patch: Partial<AppSettings>) => void;
  readonly onResetBankroll: () => void;
  readonly onBack: () => void;
}

/** Long enough for the sound bank to finish loading after the switch flips. */
const SOUND_DEMO_DELAY_MS = 400;

const Row: React.FC<{
  label: string;
  value: boolean;
  onChange: (next: boolean) => void;
  isLast?: boolean;
}> = ({ label, value, onChange, isLast = false }) => (
  <View style={[styles.row, isLast && styles.rowLast]}>
    <Text style={styles.rowLabel}>{label}</Text>
    <Switch
      value={value}
      onValueChange={onChange}
      accessibilityLabel={label}
      trackColor={{ false: colors.track, true: colors.green }}
      thumbColor={colors.text}
      ios_backgroundColor={colors.track}
    />
  </View>
);

export const SettingsScreen: React.FC<SettingsScreenProps> = ({
  settings,
  onUpdate,
  onResetBankroll,
  onBack,
}) => {
  const { play } = useFeedback();
  const wasSoundOn = useRef(settings.soundOn);

  /**
   * Turning sound on plays a cue back, because a silent switch is no way to
   * find out whether sound now works. The wait covers the players being built.
   */
  useEffect(() => {
    const justEnabled = settings.soundOn && !wasSoundOn.current;
    wasSoundOn.current = settings.soundOn;
    if (!justEnabled) return undefined;
    const timer = setTimeout(() => play('chip'), SOUND_DEMO_DELAY_MS);
    return () => clearTimeout(timer);
  }, [settings.soundOn, play]);

  const toggle = (patch: Partial<AppSettings>): void => {
    play('tap');
    onUpdate(patch);
  };

  const confirmReset = (): void => {
    Alert.alert(
      'Reset bankroll',
      `This puts you back to ${STARTING_BANKROLL} chips and leaves the table. It cannot be undone.`,
      [
        { text: 'Cancel', style: 'cancel' },
        { text: 'Reset', style: 'destructive', onPress: onResetBankroll },
      ],
    );
  };

  return (
    <ScrollView contentContainerStyle={styles.content} showsVerticalScrollIndicator={false}>
      <Reveal index={0}>
        <Text style={styles.title}>Settings</Text>
      </Reveal>

      <Reveal index={1} style={styles.panel}>
        <Row label="Sound" value={settings.soundOn} onChange={(soundOn) => toggle({ soundOn })} />
        <Row label="Haptics" value={settings.hapticsOn} onChange={(hapticsOn) => toggle({ hapticsOn })} />
        <Row
          label="Reduced motion"
          value={settings.reducedMotion}
          onChange={(reducedMotion) => toggle({ reducedMotion })}
          isLast
        />
      </Reveal>

      <Reveal index={2}>
        <Text style={styles.hint}>
          Reduced motion keeps every sound and buzz, but stops cards travelling and flipping — they
          cross-fade into place instead.
        </Text>
      </Reveal>

      <Reveal index={3} style={styles.actions}>
        <PubButton label="Reset bankroll" onPress={confirmReset} variant="danger" />
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
  title: { ...typography.title, fontSize: 32, marginBottom: spacing.xs },
  panel: {
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radii.lg,
    paddingHorizontal: spacing.md,
  },
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    minHeight: MIN_TOUCH_TARGET + 10,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  rowLast: { borderBottomWidth: 0 },
  rowLabel: { ...typography.body },
  hint: { ...typography.caption, fontSize: 12.5, lineHeight: 18 },
  actions: { gap: spacing.sm, marginTop: spacing.xs },
});
