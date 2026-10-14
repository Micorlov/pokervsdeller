using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// Lettering printed on the felt, bent to follow the rail: each character is its own label, placed along a
    /// circular arc that dips by <c>sag</c> in the middle and tilted to the arc's tangent. Characters are laid out
    /// once the element has a width, since a glyph cannot be measured before the panel knows its font.
    /// </summary>
    public sealed class ArcText : VisualElement
    {
        private const float SpaceToSize = 0.3f;
        private const long RetryMs = 50;

        private readonly List<Label> glyphs = new List<Label>();
        private readonly float baseSize;
        private readonly float letterSpacing;
        private readonly float sag;
        private readonly float lineHeight;
        private float laidOutWidth = -1f;

        public ArcText(string text, FontAsset font, float size, Color color, float letterSpacing, float sag)
        {
            this.letterSpacing = letterSpacing;
            this.sag = sag;
            baseSize = size;
            lineHeight = Mathf.Round(size * 1.25f);
            pickingMode = PickingMode.Ignore;
            style.flexShrink = 0;
            style.alignSelf = Align.Stretch;
            style.height = lineHeight + sag;

            foreach (char character in text)
            {
                var glyph = Ui.TextIn(font, character.ToString(), size, color, TextAnchor.MiddleCenter, 0f, false);
                glyph.style.position = Position.Absolute;
                glyph.style.height = lineHeight;
                glyph.OriginCenter();
                glyphs.Add(glyph);
                Add(glyph);
            }
            RegisterCallback<GeometryChangedEvent>(_ => Layout());
        }

        private void Layout()
        {
            float width = contentRect.width;
            if (float.IsNaN(width) || width <= 0f || Mathf.Approximately(width, laidOutWidth)) return;
            laidOutWidth = width;

            var advances = new float[glyphs.Count];
            float total = Measure(advances, out bool unmeasured);
            if (unmeasured)
            {
                // The face had not loaded yet: try again shortly rather than keep placeholder widths.
                laidOutWidth = -1f;
                schedule.Execute(Layout).ExecuteLater(RetryMs);
            }
            // A narrow phone gets smaller lettering, never lettering cut off at the edges. Glyph widths scale with
            // the font size, so the measured run is scaled rather than measured again before the new size applies.
            float scale = total > width ? width / total : 1f;
            if (scale < 1f)
            {
                for (int i = 0; i < glyphs.Count; i++)
                {
                    glyphs[i].style.fontSize = baseSize * scale;
                    advances[i] *= scale;
                    glyphs[i].style.width = advances[i];
                }
                total *= scale;
            }
            float spacing = letterSpacing * scale;

            // The circle through both ends of the run and the bottom of the dip.
            float radius = sag > 0f ? (total * total / 4f + sag * sag) / (2f * sag) : float.PositiveInfinity;
            float centreX = width / 2f;
            float bottomY = lineHeight / 2f + sag;
            float along = -total / 2f;
            for (int i = 0; i < glyphs.Count; i++)
            {
                float middle = along + advances[i] / 2f;
                float angle = float.IsInfinity(radius) ? 0f : middle / radius;
                float x = float.IsInfinity(radius) ? centreX + middle : centreX + radius * Mathf.Sin(angle);
                float y = float.IsInfinity(radius) ? bottomY : bottomY - radius * (1f - Mathf.Cos(angle));

                var glyph = glyphs[i];
                glyph.style.left = x - advances[i] / 2f;
                glyph.style.top = y - lineHeight / 2f;
                glyph.Rotate(-angle * Mathf.Rad2Deg);
                along += advances[i] + spacing;
            }
        }

        /// <summary>Measures every glyph at the base size; returns the run's total length.</summary>
        private float Measure(float[] advances, out bool unmeasured)
        {
            unmeasured = false;
            float total = 0f;
            for (int i = 0; i < glyphs.Count; i++)
            {
                var glyph = glyphs[i];
                float measured = glyph.MeasureTextSize(glyph.text, 0f, MeasureMode.Undefined, 0f, MeasureMode.Undefined).x;
                if (glyph.text == " ") measured = baseSize * SpaceToSize;
                else if (measured <= 0f)
                {
                    unmeasured = true;
                    measured = baseSize * SpaceToSize;
                }
                advances[i] = measured;
                glyph.style.width = measured;
                total += measured + (i > 0 ? letterSpacing : 0f);
            }
            return total;
        }
    }
}
