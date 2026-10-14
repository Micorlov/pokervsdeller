using System;
using Showdown.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The table screen's walnut header: back on the left, the table's name in italic serif with its ante in mono
    /// beneath, rules and menu on the right, and a gold-ringed chip under the title saying whose turn it is. Out-of-
    /// focus lamp glows sit behind it, so it reads as the room beyond the table rather than a toolbar.
    /// Screen-reader labels: "Back", "How to play", "Menu", and the chip's own words.
    /// </summary>
    public sealed class TopBar : VisualElement
    {
        private const float ButtonSize = Theme.MinTouchTarget;
        private const float GlyphSize = 22f;
        private const float PressedScale = 0.9f;
        private const float BarHeight = 112f;
        private const float EdgeHeight = 7f;
        /// <summary>Half the chip's height (3 + 20 + 3 padding, 3 border): UI Toolkit would turn a 999 radius into an ellipse.</summary>
        private const float ChipRadius = 14.5f;

        private readonly Label titleLabel;
        private readonly Label stakeLabel;
        private readonly Label chipLabel;
        private readonly VisualElement chip;

        public TopBar(Action onBack, Action onRules, Action onMenu)
        {
            style.flexShrink = 0;
            style.height = BarHeight;
            this.BgGradient(Theme.Colors.WalnutTop, Theme.Colors.WalnutBottom).Clip();

            Add(TableDecor.Bokeh(268, 20, 46));
            Add(TableDecor.Bokeh(304, 76, 26));
            Add(TableDecor.Bokeh(96, 78, 22));
            Add(TableDecor.Bokeh(72, 22, 14));

            var row = Ui.View().Row().AlignItems(Align.Center).Pad(12, 14, 0, 14);
            row.Add(IconButton(Ui.GlyphView(Glyph.Back, GlyphSize, Theme.Colors.Cream), onBack));

            var centre = Ui.View().Flex().AlignItems(Align.Center).MarginLeft(Theme.Spacing.Sm);
            titleLabel = Ui.Serif(string.Empty, 31, Theme.Colors.Cream);
            stakeLabel = Ui.Mono(string.Empty, 14, Theme.Colors.CreamMuted, TextAnchor.MiddleCenter, 1.5f);
            centre.Add(titleLabel);
            centre.Add(stakeLabel);
            row.Add(centre);

            var rules = IconButton(TableStyle.Line("?", 22, Theme.Colors.Cream, true), onRules);
            rules.style.marginLeft = Theme.Spacing.Sm;
            row.Add(rules);
            var menu = IconButton(Ui.GlyphView(Glyph.Kebab, GlyphSize, Theme.Colors.Cream), onMenu);
            menu.style.marginLeft = Theme.Spacing.Sm;
            row.Add(menu);
            Add(row);

            chipLabel = Ui.Text(string.Empty, 15, Theme.Colors.Cream, true, TextAnchor.MiddleCenter, 0f, false);
            chip = Ui.View().AlignSelf(Align.Center).Pad(3, 16).Radius(ChipRadius).Bg(Theme.Rgba(24, 12, 6, 0.75f))
                .Border(1.5f, Theme.Colors.GoldDeep).MarginTop(2);
            chip.Add(chipLabel);
            Add(chip);

            var edge = Ui.View().NoPick().Abs(null, 0, 0, 0).H(EdgeHeight)
                .BgGradient(Theme.Colors.WalnutEdge, Theme.Hex("#3C2010")).BorderTop(1, Theme.Rgba(255, 210, 160, 0.25f));
            Add(edge);
        }

        /// <param name="title">The table's name, e.g. "Bronze Table".</param>
        /// <param name="stake">The line under it, e.g. "ANTE 10".</param>
        /// <param name="status">Whose turn it is; null hides the chip.</param>
        public void Update(string title, string stake, string status)
        {
            titleLabel.SetText(title);
            stakeLabel.SetText(stake.ToUpperInvariant());
            chip.Show(!string.IsNullOrEmpty(status));
            chipLabel.SetText(status ?? string.Empty);
        }

        private static VisualElement IconButton(VisualElement content, Action onPress)
        {
            var button = Ui.View().Size(ButtonSize, ButtonSize).Radius(ButtonSize / 2f).Center()
                .Bg(Theme.Rgba(0, 0, 0, 0.28f)).Border(1, Theme.Rgba(255, 220, 170, 0.22f));
            button.Add(content);

            var pressScale = Anim.PressScale(button, PressedScale);
            button.OnTap(() =>
            {
                AppServices.Play(Cue.Tap);
                onPress?.Invoke();
            }, pressed =>
            {
                pressScale(pressed);
                button.style.opacity = pressed ? 0.85f : 1f;
            });
            return button;
        }
    }
}
