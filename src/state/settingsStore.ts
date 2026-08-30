import { useCallback, useEffect, useState } from 'react';
import { readJson, writeJson } from '../storage/persistence';
import { Ante, ANTE_OPTIONS } from '../domain/bankroll';

const SETTINGS_KEY = 'settings';

export interface AppSettings {
  readonly soundOn: boolean;
  readonly hapticsOn: boolean;
  readonly reducedMotion: boolean;
  /** The table stake the player last chose — remembered between sessions. */
  readonly ante: Ante;
}

export const defaultAppSettings: AppSettings = {
  soundOn: true,
  hapticsOn: true,
  reducedMotion: false,
  ante: ANTE_OPTIONS[1],
};

export const useSettings = () => {
  const [settings, setSettings] = useState<AppSettings>(defaultAppSettings);
  const [isLoaded, setIsLoaded] = useState(false);

  useEffect(() => {
    let isMounted = true;
    readJson<AppSettings>(SETTINGS_KEY, defaultAppSettings).then((loaded) => {
      if (isMounted) {
        // Settings written by older builds may lack newly added keys.
        setSettings({ ...defaultAppSettings, ...loaded });
        setIsLoaded(true);
      }
    });
    return () => {
      isMounted = false;
    };
  }, []);

  const update = useCallback((patch: Partial<AppSettings>) => {
    setSettings((prev) => {
      const next = { ...prev, ...patch };
      void writeJson(SETTINGS_KEY, next);
      return next;
    });
  }, []);

  return { settings, isLoaded, update };
};
