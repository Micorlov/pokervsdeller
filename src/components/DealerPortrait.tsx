import React from 'react';
import { Image } from 'react-native';

/** Local artwork stays available offline and never overlaps the cards. */
export const DealerPortrait: React.FC<{ readonly height?: number }> = ({ height = 120 }) => (
  <Image
    source={require('../../assets/dealer-emerald-dress.png')}
    style={{ width: height * (2 / 3), height, alignSelf: 'center' }}
    resizeMode="contain"
    accessible
    accessibilityLabel="Poker dealer in an emerald dress"
  />
);
