using UnityEngine;

namespace Showdown.UI
{
    /// <summary>
    /// "Walnut and felt" (design/walnut-felt): a lamp-lit walnut room, a deep green table with a wood rail, cream
    /// type, gold for the call to action and for celebration, mint for money won. Panels are dark glass: low-alpha
    /// black over the walnut ground.
    /// </summary>
    public static class Theme
    {
        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.magenta;
        }

        public static Color Rgba(int r, int g, int b, float a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        public static class Colors
        {
            // Ground — the room around the table, lit by a lamp over the middle.
            public static readonly Color BgBase = Hex("#170D08");
            public static readonly Color BgMid = Hex("#24140B");
            public static readonly Color BgGlow = Hex("#3D2617");
            public static readonly Color BgEdge = Hex("#0D0704");

            // The walnut header and the table's wood rail.
            public static readonly Color WalnutTop = Hex("#3D2617");
            public static readonly Color WalnutBottom = Hex("#24140B");
            public static readonly Color WalnutEdge = Hex("#6E4224");
            public static readonly Color RailHigh = Hex("#8A5632");
            public static readonly Color RailLow = Hex("#4A2814");
            public static readonly Color RailShade = Hex("#5A331B");

            // The felt, lamp-lit in the middle and darker toward the rail.
            public static readonly Color FeltHigh = Hex("#2F8A5A");
            public static readonly Color FeltMid = Hex("#1F6E47");
            public static readonly Color FeltLow = Hex("#145236");
            public static readonly Color FeltLine = Rgba(233, 200, 120, 0.32f);
            /// <summary>Lettering printed on the felt.</summary>
            public static readonly Color FeltInk = Rgba(244, 230, 200, 0.72f);
            /// <summary>Labels that sit on the felt: dark glass so they read over the green.</summary>
            public static readonly Color FeltLabel = Rgba(12, 26, 19, 0.86f);

            // Action green — confirmations and the fresh-card ring.
            public static readonly Color Green = Hex("#2FB37A");
            public static readonly Color GreenLight = Hex("#8FE3C0");
            public static readonly Color GreenInk = Hex("#0B3D24");
            public static readonly Color GreenDim = Rgba(143, 227, 192, 0.14f);
            public static readonly Color GreenLine = Rgba(143, 227, 192, 0.55f);

            // Money won reads mint; money lost reads a soft rose, never alarm red.
            public static readonly Color Mint = Hex("#8FE3C0");
            public static readonly Color MintLine = Rgba(143, 227, 192, 0.55f);
            public static readonly Color Rose = Hex("#FFB3B3");
            public static readonly Color RoseLine = Rgba(255, 140, 140, 0.45f);

            // The primary call to action is a gold bar with dark ink (the names are kept from the orange era).
            public static readonly Color Orange = Hex("#F2D898");
            public static readonly Color OrangeDeep = Hex("#D7AE5E");
            public static readonly Color OrangeInk = Hex("#2A1A0A");

            // Celebration, selection, the status chip's ring.
            public static readonly Color Gold = Hex("#E6C57E");
            public static readonly Color GoldDeep = Hex("#C9A259");
            public static readonly Color GoldGlass = Rgba(70, 48, 16, 0.88f);

            public static readonly Color Red = Hex("#FF8A8A");
            public static readonly Color RedDim = Rgba(255, 120, 120, 0.16f);
            // A destructive control cannot be a red word on a translucent wash: solid dark ground, pale red ink.
            public static readonly Color RedSurface = Hex("#3B1517");
            public static readonly Color RedSurfaceEdge = Rgba(255, 120, 120, 0.5f);
            public static readonly Color RedInk = Hex("#FFC2C4");

            // Dark glass surfaces — alpha black over the walnut ground.
            public static readonly Color Surface = Rgba(0, 0, 0, 0.22f);
            public static readonly Color SurfaceRaised = Rgba(0, 0, 0, 0.32f);
            public static readonly Color Border = Rgba(255, 220, 170, 0.16f);
            public static readonly Color BorderStrong = Rgba(255, 220, 170, 0.26f);
            public static readonly Color Track = Rgba(0, 0, 0, 0.35f);
            public static readonly Color TrackFill = Hex("#E4A43A");

            // Modals and the result sheet sit on opaque panels so the scrim behind them reads as depth.
            public static readonly Color PanelSolid = Hex("#2A1A10");
            public static readonly Color Sheet = Hex("#0C1914");
            public static readonly Color Scrim = Rgba(0, 0, 0, 0.55f);

            // Light cards (results, rules, the ante placard) and the ink that sits on them.
            public static readonly Color PanelCream = Hex("#F3EAD6");
            public static readonly Color InkText = Hex("#2A2016");
            public static readonly Color InkTextMuted = Rgba(42, 32, 22, 0.66f);

            public static readonly Color Text = Hex("#FFF7E8");
            public static readonly Color TextMuted = Rgba(244, 230, 200, 0.8f);
            public static readonly Color TextFaint = Rgba(244, 230, 200, 0.58f);
            public static readonly Color Cream = Hex("#F4E6C8");
            public static readonly Color CreamMuted = Hex("#CDB88F");

            // Card stock — a warm ivory, not paper white.
            public static readonly Color CardFace = Hex("#FFFDF7");
            public static readonly Color CardFaceEdge = Hex("#EFE7D4");
            public static readonly Color CardInk = Hex("#1D1F26");

            public static readonly Color FeltDock = Hex("#145236");
            public static readonly Color Transparent = new Color(0, 0, 0, 0);
        }

        /// <summary>Layout rhythm, in USS pixels (this app scales the panel so 1 USS px = 1 Android dp).</summary>
        public static class Spacing
        {
            public const float Xs = 4;
            public const float Sm = 8;
            public const float Md = 16;
            public const float Lg = 24;
            public const float Xl = 32;
        }

        public static class Radii
        {
            public const float Xl = 26;
            public const float Lg = 22;
            public const float Md = 20;
            public const float Sm = 14;
            public const float Xs = 12;
            public const float Tile = 14;
            public const float Pill = 999;
        }

        /// <summary>Screens cap their content on tablets so the table keeps a phone-like column.</summary>
        public const float ContentMaxWidth = 560;

        /// <summary>Well above the 44pt platform floor — these hands are not always steady.</summary>
        public const float MinTouchTarget = 54;

        /// <summary>
        /// The type ramp is sized large on purpose — this table is played by people in their sixties
        /// and up, so nothing drops below 14 and reading copy sits at 19.
        /// </summary>
        public static class Type
        {
            public const float Title = 42;
            public const float Heading = 27;
            public const float Body = 19;
            public const float Caption = 17;
            public const float Label = 14;
            public const float Button = 26;
        }

        /// <summary>One recipe, any palette: gradient face, bevel edges, suit inks.</summary>
        public sealed class CardSkin
        {
            public string Id;
            public string Name;
            public Color FaceTop;
            public Color FaceBottom;
            public Color BevelTop;
            public Color BevelBottom;
            public Color InkBlack;
            public Color InkRed;
            public Color BackTop;
            public Color BackBottom;
            public Color BackBorder;
        }

        private static CardSkin[] cardSkins;

        /// <summary>
        /// Built on first use, not in a static initialiser: the skins read <see cref="Colors"/>, whose own initialisers call
        /// back into <see cref="Hex"/>, and that cycle left card colours at transparent black depending on touch order.
        /// </summary>
        public static CardSkin[] CardSkins => cardSkins ??= BuildCardSkins();

        private static CardSkin[] BuildCardSkins() => new[]
        {
            new CardSkin
            {
                Id = "classic", Name = "Classic Ivory",
                FaceTop = Colors.CardFace, FaceBottom = Colors.CardFaceEdge,
                BevelTop = Rgba(255, 255, 255, 0.95f), BevelBottom = Rgba(103, 94, 72, 0.55f),
                InkBlack = Colors.CardInk, InkRed = Hex("#C4303B"),
                // A deep casino red with a pale drawn border.
                BackTop = Hex("#B83A46"), BackBottom = Hex("#8E2A36"), BackBorder = Rgba(243, 239, 230, 0.6f),
            },
            new CardSkin
            {
                Id = "noir", Name = "Midnight",
                FaceTop = Colors.CardFace, FaceBottom = Colors.CardFaceEdge,
                BevelTop = Rgba(255, 255, 255, 0.95f), BevelBottom = Rgba(103, 94, 72, 0.55f),
                InkBlack = Colors.CardInk, InkRed = Hex("#C0273A"),
                BackTop = Hex("#3A3D4A"), BackBottom = Hex("#14161F"), BackBorder = Rgba(255, 201, 60, 0.4f),
            },
        };

        public const string DefaultCardSkinId = "classic";

        public static CardSkin GetCardSkin(string id)
        {
            foreach (var skin in CardSkins)
            {
                if (skin.Id == id) return skin;
            }
            return CardSkins[0];
        }
    }
}
