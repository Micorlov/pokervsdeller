using System.Collections.Generic;
using Showdown.Domain;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The badge pop (port of AchievementToast.tsx): shows the first freshly-earned achievement and rolls any
    /// others into a "+N more" line rather than stacking several toasts at once.
    /// </summary>
    public sealed class AchievementToast : VisualElement
    {
        private const float MedalSize = 26f;

        private readonly ToastCard card;

        public AchievementToast(System.Action onDismiss)
        {
            style.flexShrink = 0;
            card = new ToastCard(Ui.GlyphView(Glyph.Medal, MedalSize), onDismiss);
            Add(card);
        }

        /// <param name="achievements">Every badge the hand that just settled newly earned, in book order; must not be empty.</param>
        public void Update(IReadOnlyList<Achievement> achievements)
        {
            var first = achievements[0];
            int extra = achievements.Count - 1;
            string detail = extra > 0
                ? $"+{extra} more badge{(extra == 1 ? string.Empty : "s")} unlocked"
                : first.Description;
            card.Present(first.Id, first.Title, detail);
        }
    }
}
