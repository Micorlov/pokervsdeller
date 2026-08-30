import { BoxShadowValue, TextStyle, ViewStyle } from 'react-native';

/**
 * "Table felt" — a bright casual card-room.
 *
 * A lamp-lit green felt table, glossy ivory cards with a visible bevel,
 * and warm orange/gold chrome for scores and calls to action. Panels are
 * dark felt-glass: low-alpha black fills over the green ground.
 */

export const colors = {
  // Ground — the felt, lit from a lamp above the middle of the table.
  bgBase: '#1E6B3C',
  bgMid: '#2A8A4E',
  bgGlow: '#3DA45F',
  bgEdge: '#14522C',

  // Action green — buttons and confirmations, brighter than the felt.
  green: '#2ECC71',
  greenLight: '#6FE5A3',
  greenInk: '#0B3D24',
  greenDim: 'rgba(46, 204, 113, 0.18)',
  greenLine: 'rgba(46, 204, 113, 0.45)',

  // Score pills and the primary call to action.
  orange: '#F5872E',
  orangeDeep: '#D96B12',
  orangeInk: '#5A2B05',

  // Reserved for celebration and emphasis — the star burst, rank, selection.
  gold: '#FFC93C',
  goldDeep: '#D99E1B',

  red: '#FF5A5F',
  redDim: 'rgba(255, 90, 95, 0.16)',

  // Felt-glass surfaces — alpha black over the green ground.
  surface: 'rgba(0, 0, 0, 0.18)',
  surfaceRaised: 'rgba(0, 0, 0, 0.26)',
  border: 'rgba(255, 255, 255, 0.16)',
  borderStrong: 'rgba(255, 255, 255, 0.26)',
  track: 'rgba(0, 0, 0, 0.32)',
  trackFill: '#FFB13C',

  // Modals sit on an opaque panel so the scrim behind them reads as depth.
  panelSolid: '#175A32',
  scrim: 'rgba(0, 0, 0, 0.55)',

  // Light cards (results, rules) and the ink that sits on them.
  panelCream: '#FFF8EC',
  inkText: '#243447',
  inkTextMuted: 'rgba(36, 52, 71, 0.62)',

  text: '#ffffff',
  textMuted: 'rgba(255, 255, 255, 0.78)',
  textFaint: 'rgba(255, 255, 255, 0.58)',

  // Classic tile colours — the default skin reads from these.
  cardFace: '#FFFFFF',
  cardFaceEdge: '#EDE8DA',
  cardInk: '#22242E',
};

/** The lamp glow sits over the middle of the table, not the top edge. */
export const appBackground: ViewStyle = {
  backgroundColor: colors.bgBase,
  experimental_backgroundImage: [
    {
      type: 'radial-gradient',
      shape: 'ellipse',
      size: { x: '140%', y: '90%' },
      position: { top: '38%', left: '50%' },
      colorStops: [
        { color: colors.bgGlow, positions: ['0%'] },
        { color: colors.bgMid, positions: ['52%'] },
        { color: colors.bgBase, positions: ['82%'] },
        { color: colors.bgEdge, positions: ['100%'] },
      ],
    },
  ],
};

export const shadows: Record<'ctaGlow' | 'tileLift' | 'pillLift' | 'panelLift', readonly BoxShadowValue[]> = {
  ctaGlow: [{ offsetX: 0, offsetY: 10, blurRadius: 24, spreadDistance: -8, color: 'rgba(217, 107, 18, 0.6)' }],
  tileLift: [{ offsetX: 0, offsetY: 6, blurRadius: 14, spreadDistance: -4, color: 'rgba(0, 0, 0, 0.4)' }],
  pillLift: [{ offsetX: 0, offsetY: 3, blurRadius: 8, spreadDistance: -2, color: 'rgba(0, 0, 0, 0.35)' }],
  panelLift: [{ offsetX: 0, offsetY: 14, blurRadius: 34, spreadDistance: -12, color: 'rgba(0, 0, 0, 0.45)' }],
};

export const spacing = {
  xs: 4,
  sm: 8,
  md: 16,
  lg: 24,
  xl: 32,
};

export const radii = {
  xl: 26,
  lg: 22,
  md: 20,
  sm: 14,
  xs: 12,
  tile: 14,
  pill: 999,
};

/** Screens cap their content on tablets so portrait iPad keeps a phone-like column. */
export const CONTENT_MAX_WIDTH = 560;

/**
 * Bricolage Grotesque and Instrument Sans carry the display type on the web
 * build; native has no bundled font files, so the ramp reproduces their
 * character with weight and tracking on the system face instead.
 *
 * The ramp is sized large on purpose — this table is played by people in
 * their sixties and up, so nothing drops below 14pt and reading copy sits at
 * 19pt. The uppercase labels gave up their old 10pt/1.8 tracking for the same
 * reason: small letterspaced caps are the first thing an older eye loses.
 */
export const typography = {
  title: { fontSize: 42, fontWeight: '800' as const, letterSpacing: -1.3, color: colors.text },
  heading: { fontSize: 27, fontWeight: '800' as const, letterSpacing: -0.7, color: colors.text },
  body: { fontSize: 19, fontWeight: '500' as const, color: colors.text },
  bodyMuted: { fontSize: 19, fontWeight: '500' as const, color: colors.textMuted },
  caption: { fontSize: 17, fontWeight: '500' as const, color: colors.textMuted },
  /** The small uppercase labels above a panel. */
  label: {
    fontSize: 14,
    fontWeight: '700' as const,
    letterSpacing: 1.4,
    textTransform: 'uppercase' as const,
    color: colors.textMuted,
  },
  button: { fontSize: 26, fontWeight: '700' as const, letterSpacing: -0.2 },
};

/** Scores are compared column-to-column — never let the digits reflow. */
export const tabularNums: TextStyle = { fontVariant: ['tabular-nums'] };

/** Well above the 44pt platform floor — these hands are not always steady. */
export const MIN_TOUCH_TARGET = 54;

// ---------------------------------------------------------------------------
// Card skins — the cards are drawn entirely in code: gradient stops, bevel
// edges, suit inks. One recipe, any palette.
// ---------------------------------------------------------------------------

interface GradientStop {
  readonly color: string;
  readonly positions: readonly [string];
}

export interface CardSkin {
  readonly id: string;
  readonly name: string;
  /** Top-to-bottom linear-gradient stops for the card face. */
  readonly faceStops: readonly GradientStop[];
  /** Light catching the top edge of the card. */
  readonly bevelTop: string;
  /** Shadowed bottom edge that sells the 3D thickness. */
  readonly bevelBottom: string;
  /** Ink for spades and clubs. */
  readonly inkBlack: string;
  /** Ink for hearts and diamonds. */
  readonly inkRed: string;
  /** The card back: its own gradient plus a drawn inner border. */
  readonly backStops: readonly GradientStop[];
  readonly backBorder: string;
}

export const cardSkins: readonly CardSkin[] = [
  {
    id: 'classic',
    name: 'Classic Ivory',
    faceStops: [
      { color: colors.cardFace, positions: ['0%'] },
      { color: colors.cardFaceEdge, positions: ['100%'] },
    ],
    bevelTop: 'rgba(255, 255, 255, 0.95)',
    bevelBottom: 'rgba(103, 94, 72, 0.55)',
    inkBlack: colors.cardInk,
    inkRed: '#C0273A',
    // The back borrows the red-marble tile recipe: a deep casino red with a
    // pale drawn border where a printed deck would carry its filigree.
    backStops: [
      { color: '#B03A4A', positions: ['0%'] },
      { color: '#7E2433', positions: ['100%'] },
    ],
    backBorder: 'rgba(255, 233, 236, 0.45)',
  },
  {
    id: 'noir',
    name: 'Midnight',
    faceStops: [
      { color: colors.cardFace, positions: ['0%'] },
      { color: colors.cardFaceEdge, positions: ['100%'] },
    ],
    bevelTop: 'rgba(255, 255, 255, 0.95)',
    bevelBottom: 'rgba(103, 94, 72, 0.55)',
    inkBlack: colors.cardInk,
    inkRed: '#C0273A',
    backStops: [
      { color: '#3A3D4A', positions: ['0%'] },
      { color: '#14161F', positions: ['100%'] },
    ],
    backBorder: 'rgba(255, 201, 60, 0.4)',
  },
];

export const defaultCardSkinId = 'classic';

export const getCardSkin = (id: string | undefined): CardSkin =>
  cardSkins.find((skin) => skin.id === id) ?? cardSkins[0];
