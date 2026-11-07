import AsyncStorage from '@react-native-async-storage/async-storage';
import { useSyncExternalStore } from 'react';
import mobileAds, { AdEventType, AdsConsent, InterstitialAd } from 'react-native-google-mobile-ads';
import { AD_UNITS } from './adConfig';
import { shouldShowInterstitial } from './interstitialGate';

const LAUNCHED_KEY = 'ads.launchedBefore';

let allowed = false;
const listeners = new Set<() => void>();
let firstLaunch = true;
let breaks = 0;
let lastShownAt: number | null = null;
let interstitial: InterstitialAd | null = null;
let loaded = false;

const setAllowed = (): void => {
  allowed = true;
  listeners.forEach((l) => l());
};

const loadInterstitial = (): void => {
  loaded = false;
  interstitial = InterstitialAd.createForAdRequest(AD_UNITS.interstitial);
  interstitial.addAdEventListener(AdEventType.LOADED, () => {
    loaded = true;
  });
  interstitial.addAdEventListener(AdEventType.CLOSED, loadInterstitial);
  interstitial.addAdEventListener(AdEventType.ERROR, () => {
    loaded = false;
  });
  interstitial.load();
};

/** Consent first (form only where the law needs it), then the SDK. Never throws: ads must not block the game. */
export const initAds = async (): Promise<void> => {
  try {
    firstLaunch = (await AsyncStorage.getItem(LAUNCHED_KEY)) === null;
    await AsyncStorage.setItem(LAUNCHED_KEY, '1');
  } catch {
    // Storage failure just means we treat this as a first launch.
  }
  try {
    // Consent from an earlier session counts while this session's check runs.
    const { canRequestAds } = await AdsConsent.gatherConsent().catch(() => AdsConsent.getConsentInfo());
    if (!canRequestAds) return;
    await mobileAds().initialize();
    setAllowed();
    loadInterstitial();
  } catch (e) {
    console.warn('Ads did not start', e);
  }
};

/** Call at a natural break (hand/game over). Shows an interstitial if one is loaded and the gate allows. */
export const onNaturalBreak = (): void => {
  breaks += 1;
  if (!allowed || !loaded || !interstitial) return;
  const now = Date.now();
  if (!shouldShowInterstitial({ firstLaunch, breaksThisSession: breaks, lastShownAt, now })) return;
  lastShownAt = now;
  loaded = false;
  interstitial.show().catch(loadInterstitial);
};

const subscribe = (l: () => void): (() => void) => {
  listeners.add(l);
  return () => listeners.delete(l);
};

/** True once consent allows ad requests and the SDK is up. */
export const useAdsAllowed = (): boolean => useSyncExternalStore(subscribe, () => allowed);
