import AsyncStorage from '@react-native-async-storage/async-storage';

const NAMESPACE = 'ukc.poker-vs-dealer';

const keyFor = (key: string): string => `${NAMESPACE}.${key}`;

/** Reads and JSON-parses a namespaced key; never throws — returns fallback on any failure. */
export const readJson = async <T,>(key: string, fallback: T): Promise<T> => {
  try {
    const raw = await AsyncStorage.getItem(keyFor(key));
    if (raw === null) return fallback;
    return JSON.parse(raw) as T;
  } catch {
    return fallback;
  }
};

/** Writes a namespaced key as JSON; never throws — failures are swallowed to degrade gracefully. */
export const writeJson = async (key: string, value: unknown): Promise<void> => {
  try {
    await AsyncStorage.setItem(keyFor(key), JSON.stringify(value));
  } catch {
    // Storage can fail (quota, private mode equivalents) — the game must keep running.
  }
};

export const removeKey = async (key: string): Promise<void> => {
  try {
    await AsyncStorage.removeItem(keyFor(key));
  } catch {
    // Ignore — nothing meaningful to recover from a failed removal.
  }
};
