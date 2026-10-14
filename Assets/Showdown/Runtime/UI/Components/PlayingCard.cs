using System;
using Showdown.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// A playing card drawn entirely in code, on the same tactile recipe as the app's buttons: gradient
    /// face, light top bevel, heavy dark bottom bevel, gloss pooling on top, drop shadow underneath
    /// (port of PlayingCard.tsx).
    ///
    /// The flip is two-stage scaleX — the back squeezes to nothing, the face grows out of it — so it never
    /// needs a perspective transform. The face carries one jumbo index in the top-left corner and a large pip
    /// opposite it: a printed card repeats its index upside-down in the far corner because a hand can be held
    /// either way up; a card on a screen never is, so that second index is spent on making the first one twice
    /// the size. Accessibility label for a screen reader would be <c>Cards.CardName(Card)</c> when face up,
    /// "Face-down card" otherwise.
    /// </summary>
    public sealed class PlayingCard : VisualElement
    {
        public const float CardWidth = 62f;
        public const float CardHeight = 88f;

        private const float CornerRadius = 9f;
        /// <summary>The jumbo index: the rank runs about a third of the card's height, not the sixth a standard deck uses.</summary>
        private const float RankToHeight = 0.34f;
        /// <summary>The suit under the rank reads as its companion, not its equal.</summary>
        private const float SuitToRank = 0.7f;
        /// <summary>The big corner pip, and the shortest card with room to carry one.</summary>
        private const float PipToHeight = 0.34f;
        private const float PipMinHeight = 70f;
        private const float DimmedFaceOpacity = 0.55f;
        private const float FlipSeconds = 0.42f;
        private const float EntranceLift = -14f;
        private const float EntranceScaleFrom = 1.12f;

        // Text glyph metrics: a suit glyph drawn as a texture fills its box, where a font glyph sits in a smaller em.
        private const float SuitGlyphToFont = 0.86f;
        private const float PipGlyphToFont = 0.92f;

        private readonly VisualElement face;
        private readonly VisualElement back;
        private readonly VisualElement foldedMark;
        private float flip;
        private TweenHandle flipTween;

        public Card Card { get; }

        public PlayingCard(Card card, bool faceUp, bool dimmed = false, float scale = 1f, bool animateIn = false,
            float entranceDelay = 0f, Theme.CardSkin skin = null, float? rankSize = null)
        {
            Card = card;
            skin = Inked(skin ?? Theme.GetCardSkin(Theme.DefaultCardSkinId));

            float width = CardWidth * scale;
            float height = CardHeight * scale;
            float radius = CornerRadius * scale;

            style.width = width;
            style.height = height;
            style.flexShrink = 0;
            Add(SoftShadow.TileLift(width, height, radius));

            face = BuildFace(card, skin, width, height, radius, scale, rankSize, out foldedMark);
            back = BuildBack(skin, width, height, radius, scale);
            Add(face);
            Add(back);

            flip = faceUp ? 1f : 0f;
            ApplyFlip();
            SetDimmed(dimmed);

            if (animateIn) Anim.Entrance(this, entranceDelay, EntranceLift, EntranceScaleFrom, Ease.Settle);
        }

        /// <summary>Flips the card over with the squeeze-and-grow; a no-op if it is already that way up.</summary>
        public void SetFaceUp(bool faceUp)
        {
            float target = faceUp ? 1f : 0f;
            if (Mathf.Approximately(target, flip) && flipTween == null) return;
            flipTween?.Cancel();
            if (Motion.Reduced)
            {
                flip = target;
                ApplyFlip();
                return;
            }

            float from = flip;
            flipTween = Anim.Run(FlipSeconds, t =>
            {
                flip = Mathf.LerpUnclamped(from, target, t);
                ApplyFlip();
            }, Ease.Settle, 0f, () =>
            {
                flip = target;
                flipTween = null;
                ApplyFlip();
            }, this);
        }

        /// <summary>Folded hands dim and carry a cross — never state by colour alone.</summary>
        public void SetDimmed(bool dimmed)
        {
            face.style.opacity = dimmed ? DimmedFaceOpacity : 1f;
            foldedMark.Show(dimmed);
        }

        private void ApplyFlip()
        {
            float faceScale = Mathf.Clamp01((flip - 0.5f) * 2f);
            float backScale = Mathf.Clamp01(1f - flip * 2f);
            face.ScaleBy(Mathf.Max(faceScale, 0.001f), 1f);
            back.ScaleBy(Mathf.Max(backScale, 0.001f), 1f);
            face.Show(faceScale > 0f);
            back.Show(backScale > 0f);
        }

        private static VisualElement NewSide(float width, float height, float radius)
        {
            var side = Ui.View().Size(width, height).Radius(radius).OriginCenter();
            side.style.position = Position.Absolute;
            side.style.top = 0;
            side.style.left = 0;
            side.style.alignItems = Align.Center;
            side.style.justifyContent = Justify.Center;
            // The bevel is drawn with border colours — a light edge on top, a dark edge below — which sells the
            // card's thickness without any extra views.
            side.style.borderTopWidth = 1.5f;
            side.style.borderBottomWidth = 3f;
            side.style.borderLeftWidth = 1f;
            side.style.borderRightWidth = 1f;
            side.style.borderLeftColor = Theme.Rgba(0, 0, 0, 0.14f);
            side.style.borderRightColor = Theme.Rgba(0, 0, 0, 0.14f);
            return side;
        }

        private static VisualElement NewGloss(float radius)
        {
            var gloss = Ui.View().NoPick().BgGradient(Theme.Rgba(255, 255, 255, 0.32f), Theme.Rgba(255, 255, 255, 0f));
            gloss.style.backgroundColor = Theme.Colors.Transparent;
            gloss.Abs(0, 0, null, 0).HPct(35).Radius(radius, radius, 0, 0);
            return gloss;
        }

        private static VisualElement BuildFace(Card card, Theme.CardSkin skin, float width, float height, float radius,
            float scale, float? rankSize, out VisualElement cross)
        {
            var face = NewSide(width, height, radius).BgGradient(skin.FaceTop, skin.FaceBottom);
            face.style.borderTopColor = skin.BevelTop;
            face.style.borderBottomColor = skin.BevelBottom;
            face.Add(NewGloss(radius));

            var ink = Cards.IsRedSuit(card.Suit) ? skin.InkRed : skin.InkBlack;
            float rankFontSize = rankSize ?? height * RankToHeight;
            float suitFontSize = rankFontSize * SuitToRank;
            face.Add(BuildIndex(card, ink, height, width, rankFontSize, suitFontSize));

            if (height >= PipMinHeight)
            {
                float pip = height * PipToHeight * PipGlyphToFont;
                var pipView = Ui.GlyphView(SuitGlyph(card.Suit), pip, ink).Abs(null, width * 0.06f, height * 0.03f, null);
                pipView.style.opacity = 0.85f;
                face.Add(pipView);
            }

            cross = Ui.GlyphView(Glyph.Cross, 15f * scale, ink).Abs(3f, 5f);
            cross.style.opacity = 0.45f;
            face.Add(cross);
            return face;
        }

        private static VisualElement BuildIndex(Card card, Color ink, float height, float width, float rankFontSize, float suitFontSize)
        {
            var index = Ui.View().Abs(height * 0.035f, null, null, width * 0.07f);
            index.style.alignItems = Align.Center;

            index.Add(TableStyle.Line(Cards.RankLabel(card.Rank), rankFontSize, ink, true, TextAnchor.MiddleCenter, 0f, 1.05f));
            float suit = suitFontSize * SuitGlyphToFont;
            var suitView = Ui.GlyphView(SuitGlyph(card.Suit), suit, ink);
            suitView.style.marginTop = -suitFontSize * 0.04f;
            index.Add(suitView);
            return index;
        }

        private static VisualElement BuildBack(Theme.CardSkin skin, float width, float height, float radius, float scale)
        {
            var back = NewSide(width, height, radius).BgGradient(skin.BackTop, skin.BackBottom);
            back.style.borderTopColor = Theme.Rgba(255, 255, 255, 0.28f);
            back.style.borderBottomColor = Theme.Rgba(0, 0, 0, 0.7f);
            back.Add(NewGloss(radius));

            // A printed deck carries filigree here; this one carries a pale dashed frame.
            float margin = 5f * scale;
            float innerRadius = Mathf.Max(3f, radius - 4f * scale);
            float frameWidth = width - 2f - 2f * margin;
            float frameHeight = height - 4.5f - 2f * margin;
            var frame = DashedFrame.Create(frameWidth, frameHeight, innerRadius, 1.5f, skin.BackBorder);
            back.Add(frame);
            return back;
        }

        /// <summary>
        /// <see cref="Theme.CardSkins"/> copies <c>Theme.Colors.CardFace/CardFaceEdge/CardInk</c> in its static
        /// initialiser; when <c>Theme.Colors</c> is the first thing touched, that initialiser runs while the colours
        /// are still unassigned and the skin keeps transparent black. The three colours are read here, at use time.
        /// </summary>
        private static Theme.CardSkin Inked(Theme.CardSkin skin)
        {
            return new Theme.CardSkin
            {
                Id = skin.Id,
                Name = skin.Name,
                FaceTop = Theme.Colors.CardFace,
                FaceBottom = Theme.Colors.CardFaceEdge,
                BevelTop = skin.BevelTop,
                BevelBottom = skin.BevelBottom,
                InkBlack = Theme.Colors.CardInk,
                InkRed = skin.InkRed,
                BackTop = skin.BackTop,
                BackBottom = skin.BackBottom,
                BackBorder = skin.BackBorder,
            };
        }

        /// <summary>Which of the app's drawn suit shapes stands for a suit.</summary>
        public static Glyph SuitGlyph(Suit suit)
        {
            switch (suit)
            {
                case Suit.Spades: return Glyph.Spade;
                case Suit.Hearts: return Glyph.Heart;
                case Suit.Diamonds: return Glyph.Diamond;
                default: return Glyph.Club;
            }
        }
    }
}
