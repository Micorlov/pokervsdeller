using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>Small styling helpers shared by the table's components.</summary>
    public static class TableStyle
    {
        /// <summary>One stop of <see cref="VerticalGradient"/>; <c>Position</c> runs 0 (top) to 1 (bottom).</summary>
        public readonly struct GradientStop
        {
            public readonly float Position;
            public readonly Color Color;

            public GradientStop(float position, Color color)
            {
                Position = position;
                Color = color;
            }
        }

        private static readonly Dictionary<string, Texture2D> GradientCache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// A top-to-bottom gradient with any number of stops (<see cref="Ui.BgGradient{T}"/> only does two) —
        /// RN's multi-stop <c>linear-gradient</c>.
        /// </summary>
        public static T BgVerticalGradient<T>(this T element, params GradientStop[] stops) where T : VisualElement
        {
            element.style.backgroundColor = stops[stops.Length - 1].Color;
            return element.BgImage(VerticalGradient(stops));
        }

        private static Texture2D VerticalGradient(GradientStop[] stops)
        {
            const int height = 256;
            string key = "vg:";
            foreach (var stop in stops) key += stop.Position.ToString("0.###") + ColorUtility.ToHtmlStringRGBA(stop.Color);
            if (GradientCache.TryGetValue(key, out var cached)) return cached;

            var texture = new Texture2D(2, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color[2 * height];
            for (int y = 0; y < height; y++)
            {
                // Texture row 0 is the bottom edge.
                float position = 1f - y / (float)(height - 1);
                var color = SampleStops(stops, position);
                pixels[y * 2] = color;
                pixels[y * 2 + 1] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            GradientCache[key] = texture;
            return texture;
        }

        private static Color SampleStops(GradientStop[] stops, float position)
        {
            if (position <= stops[0].Position) return stops[0].Color;
            for (int i = 1; i < stops.Length; i++)
            {
                if (position > stops[i].Position) continue;
                float span = stops[i].Position - stops[i - 1].Position;
                float k = span <= 0f ? 1f : (position - stops[i - 1].Position) / span;
                return Color.Lerp(stops[i - 1].Color, stops[i].Color, k);
            }
            return stops[stops.Length - 1].Color;
        }

        /// <summary>
        /// RN's <c>gap</c> for a column whose children come and go: every visible child but the first gets
        /// <paramref name="gap"/> above it. Call after changing which children are shown.
        /// </summary>
        public static void ApplyColumnGap(VisualElement column, float gap)
        {
            bool first = true;
            foreach (var child in column.Children())
            {
                if (child.style.display == DisplayStyle.None) continue;
                child.style.marginTop = first ? 0f : gap;
                first = false;
            }
        }

        /// <summary>
        /// A single-line label whose box is a fixed multiple of its font size. Noto Sans carries more leading
        /// than the system face the layout was drawn for, so the boxes are pinned to keep rows and stacks the
        /// height the original had; the glyphs stay centred in them.
        /// </summary>
        public static Label Line(string text, float size, Color color, bool bold = false,
            TextAnchor align = TextAnchor.MiddleCenter, float letterSpacing = 0f, float lineHeight = 1.2f)
        {
            var label = Ui.Text(text, size, color, bold, align, letterSpacing, false);
            label.style.height = Mathf.Round(size * lineHeight);
            label.style.flexShrink = 0;
            return label;
        }
    }
}
