import React from 'react';
import { View } from 'react-native';
import { BannerAd, BannerAdSize } from 'react-native-google-mobile-ads';
import { AD_UNITS } from './adConfig';
import { useAdsAllowed } from './ads';

/** Anchored adaptive banner in its own strip below the content, so it never covers controls.
 * Place it inside the screen's bottom safe-area padding. */
export const AdBanner: React.FC = () => {
  const allowed = useAdsAllowed();
  if (!allowed) return null;
  return (
    <View style={{ alignItems: 'center' }}>
      <BannerAd unitId={AD_UNITS.banner} size={BannerAdSize.ANCHORED_ADAPTIVE_BANNER} />
    </View>
  );
};
