import React, { useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { PubScreen } from './src/components/PubScreen';
import { FeedbackProvider } from './src/feedback/FeedbackProvider';
import { MotionProvider } from './src/motion/MotionContext';
import { ScreenTransition } from './src/motion/ScreenTransition';
import { HomeScreen } from './src/screens/HomeScreen';
import { HowToPlayScreen } from './src/screens/HowToPlayScreen';
import { SettingsScreen } from './src/screens/SettingsScreen';
import { StatsScreen } from './src/screens/StatsScreen';
import { TableScreen } from './src/screens/TableScreen';
import { ScreenName } from './src/screens/types';
import { useDailyBonus } from './src/state/dailyBonusStore';
import { AppSettings, useSettings } from './src/state/settingsStore';
import { useShowdownTable } from './src/state/useShowdownTable';

interface AppContentProps {
  readonly settings: AppSettings;
  readonly onUpdateSettings: (patch: Partial<AppSettings>) => void;
}

/**
 * Everything below the providers, so it can use sound and motion.
 *
 * There is no navigation library: four screens and one cross-fade is not a
 * navigation problem, and a stack would only add a back gesture the felt does
 * not want.
 */
const AppContent: React.FC<AppContentProps> = ({ settings, onUpdateSettings }) => {
  const [screen, setScreen] = useState<ScreenName>('home');
  const table = useShowdownTable();
  const dailyBonus = useDailyBonus();

  const claimDailyBonus = (): void => {
    if (!dailyBonus.canClaim) return;
    table.claimDailyBonus();
    dailyBonus.markClaimed();
  };

  /** Rules and settings are reachable from both places, so back goes wherever you were. */
  const returnScreen = (): ScreenName => (table.round ? 'table' : 'home');

  const takeSeat = (): void => {
    table.startHand(settings.ante);
    setScreen('table');
  };

  const leaveTable = (): void => {
    table.leaveTable();
    setScreen('home');
  };

  const resetBankroll = (): void => {
    table.leaveTable();
    table.resetBankroll();
    setScreen('home');
  };

  const home = (
    <HomeScreen
      bankroll={table.bankroll}
      ante={settings.ante}
      onSelectAnte={(ante) => onUpdateSettings({ ante })}
      onPlay={takeSeat}
      onReUp={() => table.reUpNow(settings.ante)}
      onHowToPlay={() => setScreen('howToPlay')}
      onSettings={() => setScreen('settings')}
      onStats={() => setScreen('stats')}
      canClaimDailyBonus={dailyBonus.canClaim}
      onClaimDailyBonus={claimDailyBonus}
    />
  );

  const content = ((): React.ReactElement => {
    // Until the stored bankroll lands, show only the felt: opening on the
    // default 500 and then snapping to the real number reads as a bug.
    if (!table.bankrollLoaded) return <View style={styles.pending} />;
    if (screen === 'howToPlay') return <HowToPlayScreen onBack={() => setScreen(returnScreen())} />;
    if (screen === 'stats') return <StatsScreen stats={table.stats} onBack={() => setScreen(returnScreen())} />;
    if (screen === 'settings') {
      return (
        <SettingsScreen
          settings={settings}
          onUpdate={onUpdateSettings}
          onResetBankroll={resetBankroll}
          onBack={() => setScreen(returnScreen())}
        />
      );
    }
    // A table screen with no round loaded has nothing to draw — that only
    // happens if the deal was refused, and home is the honest place to be.
    if (screen === 'table' && table.round) {
      return (
        <TableScreen
          table={table}
          onHome={leaveTable}
          onRules={() => setScreen('howToPlay')}
          onMenu={() => setScreen('settings')}
        />
      );
    }
    return home;
  })();

  return (
    <PubScreen>
      <ScreenTransition transitionKey={screen}>{content}</ScreenTransition>
      <StatusBar style="light" />
    </PubScreen>
  );
};

export default function App() {
  const { settings, update } = useSettings();

  return (
    <SafeAreaProvider>
      <MotionProvider reducedMotion={settings.reducedMotion}>
        <FeedbackProvider soundOn={settings.soundOn} hapticsOn={settings.hapticsOn}>
          <AppContent settings={settings} onUpdateSettings={update} />
        </FeedbackProvider>
      </MotionProvider>
    </SafeAreaProvider>
  );
}

const styles = StyleSheet.create({
  pending: { flex: 1 },
});
