import React, { PropsWithChildren, createContext, useContext } from 'react';

/**
 * Whether the player has asked for reduced motion in Settings.
 *
 * Every animation hook reads this rather than checking settings itself, so a
 * component never has to know where the preference came from — and turning the
 * switch on takes effect everywhere at once.
 */
const MotionContext = createContext<boolean>(false);

interface MotionProviderProps {
  readonly reducedMotion: boolean;
}

export const MotionProvider: React.FC<PropsWithChildren<MotionProviderProps>> = ({
  reducedMotion,
  children,
}) => <MotionContext.Provider value={reducedMotion}>{children}</MotionContext.Provider>;

export const useReducedMotion = (): boolean => useContext(MotionContext);
