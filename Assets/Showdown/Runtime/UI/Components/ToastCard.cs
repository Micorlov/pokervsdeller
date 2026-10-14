using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The gold pop shared by the achievement and streak toasts: an icon beside a title and a line of detail.
    /// It enters with the celebratory overshoot and clears itself after a beat, so a forgotten table never
    /// leaves it stuck on screen.
    /// </summary>
    public sealed class ToastCard : VisualElement
    {
        /// <summary>Long enough to read title and description, short enough not to block Next Hand.</summary>
        public const float AutoDismissSeconds = 3.2f;
        private const float PopScaleFrom = 0.6f;
        private const float PopLift = -8f;

        private readonly Label title;
        private readonly Label subtitle;
        private readonly Action onDismiss;
        private string key;
        private TweenHandle dismissTimer;

        public ToastCard(VisualElement icon, Action onDismiss)
        {
            this.onDismiss = onDismiss;
            style.flexShrink = 0;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.alignSelf = Align.Center;
            style.maxWidth = Length.Percent(100);
            this.Pad(Theme.Spacing.Sm, Theme.Spacing.Md).Radius(Theme.Radii.Lg)
                .Bg(Theme.Rgba(255, 201, 60, 0.16f)).Border(1.5f, Theme.Colors.Gold);
            Add(SoftShadow.PillLiftFit(Theme.Radii.Lg));

            icon.style.marginRight = Theme.Spacing.Sm;
            Add(icon);

            title = TableStyle.Line(string.Empty, 15, Theme.Colors.Gold, true, TextAnchor.MiddleLeft, -0.2f);
            // The detail line can be a whole sentence ("Beat the dealer and the table in the same hand."), so it wraps
            // inside the card instead of pushing it past the screen edge.
            subtitle = Ui.Text(string.Empty, 12, Theme.Colors.TextMuted, true, TextAnchor.MiddleLeft);
            subtitle.style.marginTop = 2;
            var copy = Ui.View();
            copy.style.flexShrink = 1;
            copy.Add(title);
            copy.Add(subtitle);
            Add(copy);
        }

        /// <summary>
        /// Shows this content. A new <paramref name="contentKey"/> replays the pop and restarts the dismiss
        /// timer; the same key only refreshes the text.
        /// </summary>
        public void Present(string contentKey, string titleText, string subtitleText)
        {
            title.SetText(titleText);
            subtitle.SetText(subtitleText);
            if (contentKey == key) return;

            key = contentKey;
            Anim.Entrance(this, 0f, PopLift, PopScaleFrom, Ease.Pop);
            dismissTimer?.Cancel();
            dismissTimer = Anim.Run(AutoDismissSeconds, _ => { }, null, 0f, onDismiss, this);
        }
    }
}
