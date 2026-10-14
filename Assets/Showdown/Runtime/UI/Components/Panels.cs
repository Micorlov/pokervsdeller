using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The two surfaces the non-table screens share: felt-glass (a low-alpha black wash over the felt) and the
    /// cream "printed table" card, the one light surface in the app, so rule numbers read as house rules rather than chrome.
    /// </summary>
    public static class Panels
    {
        /// <summary>The felt-glass panel (RN <c>panel</c> style) with the given inner padding on every side.</summary>
        public static VisualElement Glass(float padding = Theme.Spacing.Md)
        {
            return Ui.View().Bg(Theme.Colors.Surface).Radius(Theme.Radii.Lg)
                .Border(1, Theme.Colors.Border).Pad(padding);
        }

        /// <summary>The cream rules card (RN <c>creamCard</c> style).</summary>
        public static VisualElement Cream()
        {
            return Ui.View().Bg(Theme.Colors.PanelCream).Radius(Theme.Radii.Lg).Pad(Theme.Spacing.Md);
        }

        /// <summary>The small uppercase label at the top of a cream card.</summary>
        public static Label CreamLabel(string text)
        {
            return Ui.Text(text.ToUpperInvariant(), Theme.Type.Label, Theme.Colors.InkTextMuted, true,
                UnityEngine.TextAnchor.MiddleLeft, 1.4f);
        }
    }
}
