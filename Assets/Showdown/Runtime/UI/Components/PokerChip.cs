using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// A chip drawn in code on the tactile-object recipe: gradient face, dashed edge ring where a real chip
    /// carries its inserts (PokerChip.tsx). Casino colour code: red fives, blue tens, green quarters, gold otherwise.
    /// </summary>
    public static class PokerChip
    {
        private static readonly Color FallbackTop = Theme.Hex("#F7DE96");
        private static readonly Color FallbackBottom = Theme.Hex("#CBA345");

        private static void GradientFor(int value, out Color top, out Color bottom)
        {
            switch (value)
            {
                case 5:
                    top = Theme.Hex("#D14848");
                    bottom = Theme.Hex("#8E2323");
                    return;
                case 10:
                    top = Theme.Hex("#3E7BC0");
                    bottom = Theme.Hex("#27508A");
                    return;
                case 25:
                    top = Theme.Hex("#35A868");
                    bottom = Theme.Hex("#1D6B40");
                    return;
                default:
                    top = FallbackTop;
                    bottom = FallbackBottom;
                    return;
            }
        }

        public static VisualElement Create(int value, float size = 36f)
        {
            GradientFor(value, out var top, out var bottom);
            var chip = Ui.View().Size(size, size).Center().Radius(size / 2f)
                .BgGradient(top, bottom)
                .BorderTop(1, Theme.Rgba(255, 255, 255, 0.5f))
                .BorderBottom(2, Theme.Rgba(0, 0, 0, 0.45f));

            var ring = Ui.GlyphView(Glyph.ChipRing, size, Theme.Rgba(255, 255, 255, 0.75f)).AbsFill();
            chip.Add(ring);

            var text = Ui.Text(value.ToString(), size * 0.34f, Color.white, true, TextAnchor.MiddleCenter, 0f, false);
            text.style.textShadow = new TextShadow { offset = new Vector2(0, 1), blurRadius = 1, color = Theme.Rgba(0, 0, 0, 0.35f) };
            chip.Add(text);
            return chip;
        }
    }
}
