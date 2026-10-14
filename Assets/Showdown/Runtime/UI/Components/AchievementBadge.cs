using Showdown.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// One tile of the Stats achievements grid: a medal (earned) or padlock (locked) over the badge's name.
    /// A locked badge dims to 70% as a whole — stacking a 0.45 alpha on the muted colour landed near a quarter.
    /// </summary>
    public static class AchievementBadge
    {
        private const float IconSize = 28;
        private const float LockedOpacity = 0.7f;
        // Noto Sans Bold runs wider than the system face the tile was sized for, and "Unstoppable" would touch both edges.
        private const float TitleTracking = -0.35f;

        public static VisualElement Create(Achievement achievement, bool unlocked)
        {
            var badge = Ui.View().Flex().AlignItems(Align.Center).Pad(Theme.Spacing.Sm, 0)
                .Radius(Theme.Radii.Sm).Bg(Theme.Colors.SurfaceRaised);
            if (!unlocked) badge.Opacity(LockedOpacity);

            badge.Add(Ui.GlyphView(unlocked ? Glyph.Medal : Glyph.Lock, IconSize));

            // A badge is a fixed third of the row and the longest names already fill it, so the title never scales with the reading copy.
            var title = Ui.Text(achievement.Title, 15, unlocked ? Theme.Colors.Text : Theme.Colors.TextFaint,
                true, TextAnchor.MiddleCenter);
            title.style.marginTop = 4;
            title.style.letterSpacing = TitleTracking;
            badge.Add(title);
            return badge;
        }

        /// <summary>An empty slot that pads a short last row so its badges keep the same width as the rest.</summary>
        public static VisualElement Spacer()
        {
            return Ui.View().Flex();
        }
    }
}
