using System;
using Showdown.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    public enum PubButtonVariant
    {
        Primary,
        Secondary,
        Danger,
        Bonus,
    }

    /// <summary>
    /// Port of PubButton.tsx, in the walnut design. Primary is the one gold bar on a screen, set in dark
    /// uppercase — everything else is outlined glass (mint for a move, rose for bail, gold for a bonus), so the
    /// button that commits a move is impossible to mistake. The press dips the whole button rather than just
    /// fading it, which is what makes a flat rectangle feel like something you pushed.
    /// </summary>
    public sealed class PubButton : VisualElement
    {
        private readonly Label labelView;
        private readonly Label detailView;
        private readonly Action onPress;
        private readonly Cue? cue;
        private readonly Action<bool> pressScale;
        private const float CornerRadius = 16f;
        private const float PrimaryLabelSize = 22f;
        private const float MoveLabelSize = 20f;
        private const float DetailSize = 15f;

        private string label;

        private PubButtonVariant variant;
        private bool disabled;
        private bool pressed;

        /// <param name="cue">The sound this press makes. Pass null for buttons whose result already has a cue.</param>
        public PubButton(string label, Action onPress, PubButtonVariant variant = PubButtonVariant.Primary,
            string detail = null, bool disabled = false, Cue? cue = Cue.Tap)
        {
            this.onPress = onPress;
            this.variant = variant;
            this.disabled = disabled;
            this.cue = cue;

            style.flexShrink = 0;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.paddingLeft = Theme.Spacing.Md;
            style.paddingRight = Theme.Spacing.Md;
            style.paddingTop = Theme.Spacing.Sm;
            style.paddingBottom = Theme.Spacing.Sm;
            this.Radius(CornerRadius);

            this.label = label ?? string.Empty;
            labelView = Ui.Text(this.label, PrimaryLabelSize, Theme.Colors.Text, true, TextAnchor.MiddleCenter);
            detailView = Ui.Mono(detail ?? string.Empty, DetailSize, Theme.Colors.TextMuted, TextAnchor.MiddleCenter);
            detailView.style.whiteSpace = WhiteSpace.Normal;
            detailView.style.marginTop = 1;
            Add(labelView);
            Add(detailView);

            pressScale = Anim.PressScale(this, 0.965f);
            this.OnTap(HandlePress, isPressed =>
            {
                pressed = isPressed && !this.disabled;
                pressScale(pressed);
                ApplyStyle();
            });
            ApplyStyle();
        }

        public string Label
        {
            get => label;
            set
            {
                label = value ?? string.Empty;
                ApplyStyle();
            }
        }

        public string Detail
        {
            get => detailView.text;
            set
            {
                detailView.text = value ?? string.Empty;
                detailView.Show(!string.IsNullOrEmpty(value));
            }
        }

        public bool Disabled
        {
            get => disabled;
            set
            {
                disabled = value;
                ApplyStyle();
            }
        }

        public PubButtonVariant Variant
        {
            get => variant;
            set
            {
                variant = value;
                ApplyStyle();
            }
        }

        private void HandlePress()
        {
            if (disabled) return;
            if (cue.HasValue) AppServices.Play(cue.Value);
            onPress?.Invoke();
        }

        private void ApplyStyle()
        {
            detailView.Show(!string.IsNullOrEmpty(detailView.text));
            style.opacity = pressed ? 0.85f : 1f; // kept alongside the scale so reduced-motion users still see a press land

            ResetBackground();
            // The gold bar shouts in capitals, like a dealer's call; every other button speaks in sentence case.
            bool primary = variant == PubButtonVariant.Primary;
            labelView.text = primary ? label.ToUpperInvariant() : label;
            labelView.style.letterSpacing = primary ? 1.2f : 0f;
            if (disabled)
            {
                // Disabled drops to plain glass: no glow, no action colour, nothing to press.
                this.Bg(Theme.Colors.Surface).MinH(MinHeight(variant));
                this.Border(1, Theme.Colors.Border);
                StyleLabels(Theme.Colors.TextFaint, Theme.Colors.TextFaint, primary ? PrimaryLabelSize : MoveLabelSize);
                return;
            }

            switch (variant)
            {
                case PubButtonVariant.Primary:
                    this.BgGradient(Theme.Colors.Orange, Theme.Colors.OrangeDeep).MinH(MinHeight(variant));
                    this.Border(0, Theme.Colors.Transparent);
                    this.BorderTop(1, Theme.Rgba(255, 255, 255, 0.6f));
                    this.BorderBottom(3, Theme.Rgba(90, 60, 20, 0.6f));
                    StyleLabels(Theme.Colors.OrangeInk, Theme.Rgba(42, 26, 10, 0.8f), PrimaryLabelSize);
                    break;
                case PubButtonVariant.Secondary:
                    this.Bg(Theme.Rgba(255, 255, 255, 0.04f)).MinH(MinHeight(variant));
                    this.Border(1.5f, Theme.Colors.MintLine);
                    StyleLabels(Theme.Hex("#E8F5EE"), Theme.Colors.TextMuted, MoveLabelSize);
                    break;
                case PubButtonVariant.Danger:
                    this.Bg(Theme.Colors.RedSurface).MinH(MinHeight(variant));
                    this.Border(1.5f, Theme.Colors.RedSurfaceEdge);
                    StyleLabels(Theme.Colors.RedInk, Theme.Rgba(255, 194, 196, 0.8f), MoveLabelSize);
                    break;
                case PubButtonVariant.Bonus:
                    // Free chips, or a raised stake: a gift, not the move — gold ink in a gold ring.
                    this.Bg(Theme.Rgba(70, 48, 16, 0.6f)).MinH(MinHeight(variant));
                    this.Border(1.5f, Theme.Colors.Gold);
                    StyleLabels(Theme.Colors.Gold, Theme.Rgba(230, 197, 126, 0.85f), MoveLabelSize);
                    break;
            }
        }

        private static float MinHeight(PubButtonVariant variant)
        {
            return variant == PubButtonVariant.Primary ? 62 : 60;
        }

        private void ResetBackground()
        {
            style.backgroundImage = StyleKeyword.None;
            style.backgroundColor = StyleKeyword.None;
        }

        private void StyleLabels(Color labelColor, Color detailColor, float labelSize)
        {
            labelView.style.color = labelColor;
            labelView.style.fontSize = labelSize;
            detailView.style.color = detailColor;
        }
    }
}
