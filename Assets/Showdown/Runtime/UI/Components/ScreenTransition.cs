using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The join between screens (ScreenTransition.tsx). Deliberately opacity-only: each screen already
    /// staggers its own panels in, and sliding the whole screen as well would make one arrival read as two.
    /// The old screen goes at once and the new one fades in over it, as in the RN app.
    /// </summary>
    public sealed class ScreenTransition : VisualElement
    {
        private TweenHandle fade;

        public ScreenTransition()
        {
            style.flexGrow = 1;
            style.flexShrink = 1;
        }

        /// <summary>Replaces whatever is showing with <paramref name="screen"/> and cross-fades it in.</summary>
        public void Show(VisualElement screen)
        {
            Clear();
            Add(screen);
            fade?.Cancel();
            fade = Anim.Entrance(this, 0f, 0f, 1f);
        }

        /// <summary>Shows only the felt — nothing mounted — until there is something honest to draw.</summary>
        public void ShowNothing()
        {
            fade?.Cancel();
            Clear();
            style.opacity = 1f;
        }
    }
}
