import React, { PropsWithChildren, createContext, useCallback, useContext, useEffect, useMemo, useRef } from 'react';
import { createAudioPlayer, setAudioModeAsync } from 'expo-audio';
import type { AudioPlayer } from 'expo-audio';
import * as Haptics from 'expo-haptics';
import { CueName, HapticKind, cues } from './cues';

interface FeedbackApi {
  readonly play: (cue: CueName) => void;
}

const FeedbackContext = createContext<FeedbackApi>({ play: () => undefined });

const HAPTICS: Record<HapticKind, () => Promise<void>> = {
  selection: () => Haptics.selectionAsync(),
  light: () => Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light),
  medium: () => Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Medium),
  heavy: () => Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Heavy),
  success: () => Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success),
  warning: () => Haptics.notificationAsync(Haptics.NotificationFeedbackType.Warning),
  error: () => Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error),
};

type PlayerBank = Partial<Record<CueName, AudioPlayer>>;

/**
 * Restart a cue that may still be ringing — a fast run of placements has to
 * retrigger the knock, not queue up behind itself. `seekTo` is async but the
 * play call does not wait on it; that is the documented replay pattern.
 */
const replay = (player: AudioPlayer): void => {
  void player.seekTo(0).catch(() => undefined);
  player.play();
};

interface FeedbackProviderProps {
  readonly soundOn: boolean;
  readonly hapticsOn: boolean;
}

/**
 * Owns the app's sound players and turns a cue name into "make that noise and
 * that buzz". Both channels are optional, so a cue with the sound off still
 * fires its haptic and vice versa.
 *
 * Audio failures are deliberately non-fatal: a cue that cannot load is a
 * missing sound effect, not a broken game, so playback degrades to silence
 * rather than propagating. Nothing else in the app swallows errors this way.
 */
export const FeedbackProvider: React.FC<PropsWithChildren<FeedbackProviderProps>> = ({
  soundOn,
  hapticsOn,
  children,
}) => {
  const playersRef = useRef<PlayerBank>({});

  useEffect(() => {
    if (!soundOn) return undefined;

    // Effects should sit alongside whatever the player is listening to, and
    // still be audible with the ringer switch off — this is a game, not media.
    void setAudioModeAsync({
      playsInSilentMode: true,
      interruptionMode: 'mixWithOthers',
      shouldPlayInBackground: false,
    }).catch(() => undefined);

    const bank: PlayerBank = {};
    (Object.keys(cues) as CueName[]).forEach((name) => {
      try {
        const player = createAudioPlayer(cues[name].source);
        player.volume = cues[name].volume;
        bank[name] = player;
      } catch {
        // Leave this cue silent; the rest of the bank still works.
      }
    });
    playersRef.current = bank;

    return () => {
      Object.values(bank).forEach((player) => {
        try {
          player.remove();
        } catch {
          // Already released by the runtime — nothing left to free.
        }
      });
      playersRef.current = {};
    };
  }, [soundOn]);

  const play = useCallback(
    (cue: CueName) => {
      const spec = cues[cue];
      if (hapticsOn && spec.haptic) {
        void HAPTICS[spec.haptic]().catch(() => undefined);
      }
      const player = playersRef.current[cue];
      if (!player) return;
      try {
        replay(player);
      } catch {
        // A player torn down mid-gesture; silence is the right fallback.
      }
    },
    [hapticsOn],
  );

  const api = useMemo(() => ({ play }), [play]);

  return <FeedbackContext.Provider value={api}>{children}</FeedbackContext.Provider>;
};

export const useFeedback = (): FeedbackApi => useContext(FeedbackContext);
