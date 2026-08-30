import React, { PropsWithChildren } from 'react';
import { StyleSheet, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { appBackground } from '../theme';

/**
 * The ground every screen sits on. The glow is painted on the outer view so it
 * stays anchored to the top of the device rather than starting below the notch.
 *
 * `SafeAreaView` comes from `react-native-safe-area-context`, not from
 * `react-native` — the built-in one is iOS-only and silently renders nothing on
 * Android, which put the header under the status bar.
 */
export const PubScreen: React.FC<PropsWithChildren> = ({ children }) => (
  <View style={styles.ground}>
    <SafeAreaView style={styles.safe} edges={['top', 'bottom']}>
      {children}
    </SafeAreaView>
  </View>
);

const styles = StyleSheet.create({
  ground: { flex: 1, ...appBackground },
  safe: { flex: 1 },
});
