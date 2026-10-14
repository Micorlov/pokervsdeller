using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The dealer's portrait (DealerPortrait.tsx). Local artwork stays available offline and never overlaps
    /// the cards. The picture is 2:3 and is fitted, not stretched.
    /// </summary>
    public static class DealerPortrait
    {
        private const string ResourcePath = "Art/dealer-emerald-dress";

        public static VisualElement Create(float height = 120)
        {
            var portrait = Ui.View().Size(height * (2f / 3f), height).AlignSelf(Align.Center).NoPick();
            var texture = Resources.Load<Texture2D>(ResourcePath);
            if (texture == null)
            {
                Debug.LogError("Showdown: missing dealer artwork " + ResourcePath);
                return portrait;
            }
            return portrait.BgContain(texture);
        }
    }
}
