using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The player's own hand, named: a gold disc carrying the kicker's rank and star, then the made hand ("Pair",
    /// "Two Pair") and an optional word after it ("holding", "best hand"). A pair or better wears the gold ring —
    /// the hand worth saying out loud; anything less sits in plain dark glass.
    /// </summary>
    public sealed class HandRankBadge : VisualElement
    {
        private const float DiscSize = 40f;
        /// <summary>Half the badge's height with the disc (5 + 40 + 5 padding, 3 border).</summary>
        private const float RadiusWithDisc = 26.5f;
        /// <summary>Half the badge's height without it (8 + 25 + 8 padding, 3 border).</summary>
        private const float RadiusPlain = 22f;

        private readonly VisualElement disc;
        private readonly Label discRank;
        private readonly Label words;
        private readonly Label sub;

        public HandRankBadge()
        {
            style.flexShrink = 0;
            style.alignSelf = Align.Center;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            Add(SoftShadow.PillLiftFit(RadiusWithDisc));

            disc = Ui.View().Size(DiscSize, DiscSize).Radius(DiscSize / 2f).Row().Center()
                .BgGradient(Theme.Hex("#F6DC9B"), Theme.Hex("#C99A43")).MarginRight(10);
            discRank = Ui.Mono(string.Empty, 18, Theme.Colors.OrangeInk, TextAnchor.MiddleCenter);
            disc.Add(discRank);
            disc.Add(Ui.GlyphView(Glyph.Star, 11, Theme.Colors.OrangeInk).MarginLeft(1));
            Add(disc);

            words = Ui.Text(string.Empty, 21, Theme.Colors.Text, true, TextAnchor.MiddleLeft, 0f, false);
            sub = Ui.Text(string.Empty, 15, Theme.Colors.CreamMuted, false, TextAnchor.MiddleLeft, 0f, false);
            sub.style.marginLeft = 8;
            Add(words);
            Add(sub);
        }

        /// <param name="kicker">The kicker's rank label ("8", "A"), or null to drop the disc.</param>
        /// <param name="celebrated">A pair or better: gold ring instead of plain glass.</param>
        public void Set(string kicker, string text, string detail, bool celebrated)
        {
            bool hasDisc = !string.IsNullOrEmpty(kicker);
            disc.Show(hasDisc);
            discRank.SetText(kicker ?? string.Empty);
            words.SetText(text);
            sub.SetText(detail ?? string.Empty);
            sub.Show(!string.IsNullOrEmpty(detail));

            float radius = hasDisc ? RadiusWithDisc : RadiusPlain;
            if (hasDisc) this.Pad(5, 18, 5, 5);
            else this.Pad(8, 20);
            this.Radius(radius).Bg(Theme.Rgba(12, 26, 19, 0.9f))
                .Border(1.5f, celebrated ? Theme.Colors.Gold : Theme.Rgba(255, 255, 255, 0.18f));
        }
    }
}
