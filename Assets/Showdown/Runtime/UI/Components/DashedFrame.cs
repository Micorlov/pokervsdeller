using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// A dashed rounded-rectangle outline, which USS cannot draw (<c>borderStyle: 'dashed'</c> in RN).
    /// The texture is white with alpha so one bitmap serves every colour; tint the element.
    /// </summary>
    public static class DashedFrame
    {
        private const float TexelsPerDp = 3f;

        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>A transparent box of the given size with a dashed outline of <paramref name="color"/> drawn just inside its edge.</summary>
        public static VisualElement Create(float width, float height, float radius, float stroke, Color color,
            float dash = 4.5f, float gap = 4.5f)
        {
            var element = Ui.View().Size(width, height).NoPick();
            element.BgImage(TextureFor(width, height, radius, stroke, dash, gap)).Tint(color);
            return element;
        }

        private static Texture2D TextureFor(float width, float height, float radius, float stroke, float dash, float gap)
        {
            string key = $"{width:0.##}x{height:0.##}r{radius:0.##}s{stroke:0.##}d{dash:0.##}g{gap:0.##}";
            if (Cache.TryGetValue(key, out var cached)) return cached;

            int pixelsWide = Mathf.CeilToInt(width * TexelsPerDp);
            int pixelsHigh = Mathf.CeilToInt(height * TexelsPerDp);
            var pixels = new Color32[pixelsWide * pixelsHigh];
            float halfWidth = width / 2f;
            float halfHeight = height / 2f;
            float clampedRadius = Mathf.Min(radius, Mathf.Min(halfWidth, halfHeight));
            float period = dash + gap;
            float texel = 1f / TexelsPerDp;

            for (int y = 0; y < pixelsHigh; y++)
            {
                // Texture row 0 is the bottom edge, so measure y upward from the centre.
                float py = (y + 0.5f) * texel - halfHeight;
                for (int x = 0; x < pixelsWide; x++)
                {
                    float px = (x + 0.5f) * texel - halfWidth;
                    float distance = SoftShadow.RoundedRectDistance(px, py, halfWidth, halfHeight, clampedRadius);
                    // The stroke occupies [-stroke, 0] inside the edge; coverage fades over one texel.
                    float overlap = stroke / 2f + texel / 2f - Mathf.Abs(distance + stroke / 2f);
                    float coverage = Mathf.Clamp(overlap, 0f, texel) / texel;
                    if (coverage <= 0f) continue;

                    float along = Perimeter(px, py, halfWidth, halfHeight, clampedRadius);
                    if (Mathf.Repeat(along, period) >= dash) continue;
                    pixels[y * pixelsWide + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            }

            var texture = new Texture2D(pixelsWide, pixelsHigh, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Cache[key] = texture;
            return texture;
        }

        /// <summary>
        /// Distance travelled clockwise along the outline from the start of the top edge, for the outline point
        /// nearest (px, py) (y up, origin at the centre). Dashes are laid by this so they flow round the corners.
        /// </summary>
        private static float Perimeter(float px, float py, float halfWidth, float halfHeight, float radius)
        {
            float flatX = halfWidth - radius;
            float flatY = halfHeight - radius;
            float edgeX = 2f * flatX;
            float edgeY = 2f * flatY;
            float quarter = Mathf.PI * radius / 2f;

            bool nearTop = py >= flatY;
            bool nearBottom = py <= -flatY;
            bool nearRight = px >= flatX;
            bool nearLeft = px <= -flatX;

            if (nearTop && nearRight) return edgeX + radius * (Mathf.PI / 2f - Mathf.Atan2(py - flatY, px - flatX));
            if (nearBottom && nearRight) return edgeX + quarter + edgeY + radius * -Mathf.Atan2(py + flatY, px - flatX);
            if (nearBottom && nearLeft)
            {
                float angle = Mathf.Atan2(py + flatY, px + flatX); // -pi/2 .. -pi
                return 2f * edgeX + 2f * quarter + edgeY + radius * (-Mathf.PI / 2f - angle);
            }
            if (nearTop && nearLeft)
            {
                float angle = Mathf.Atan2(py - flatY, px + flatX); // pi .. pi/2
                return 2f * edgeX + 3f * quarter + 2f * edgeY + radius * (Mathf.PI - angle);
            }
            if (nearTop) return px + flatX;
            if (nearRight) return edgeX + quarter + (flatY - py);
            if (nearBottom) return edgeX + 2f * quarter + edgeY + (flatX - px);
            return 2f * edgeX + 3f * quarter + edgeY + (py + flatY);
        }
    }
}
