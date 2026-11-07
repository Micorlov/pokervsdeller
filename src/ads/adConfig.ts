/** The one place ad unit IDs live. Set USE_TEST_ADS to false only for a store release. */
export const USE_TEST_ADS = true;

// Google's official sample units: safe to load and click on any device.
const TEST = {
  banner: 'ca-app-pub-3940256099942544/9214589741',
  interstitial: 'ca-app-pub-3940256099942544/1033173712',
};

// Real units from AdMob (REAL_UNITS.md); used only when USE_TEST_ADS is false.
const REAL = {
  banner: 'ca-app-pub-2693008307172998/6225628347',
  interstitial: 'ca-app-pub-2693008307172998/1158948588',
};

export const AD_UNITS = USE_TEST_ADS ? TEST : REAL;

/** At most one interstitial per this many milliseconds. */
export const INTERSTITIAL_GAP_MS = 90_000;
