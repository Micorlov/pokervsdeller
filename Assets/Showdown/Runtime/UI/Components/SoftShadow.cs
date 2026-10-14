using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// Stands in for RN's <c>boxShadow</c>, which UI Toolkit lacks: a pre-blurred rounded-rect texture laid
    /// behind its owner. The owner must be the parent of this element, must not clip its children, and must
    /// add the shadow before its real content so it paints underneath.
    /// </summary>
    public static class SoftShadow
    {
        /// <summary>Texels per dp. The edge is a soft ramp, so a coarse texture loses nothing.</summary>
        private const float TexelsPerDp = 2f;

        /// <summary>Gaussian blur radius → sigma, the way CSS defines it.</summary>
        private const float SigmaPerBlur = 0.5f;

        /// <summary>Slope of the logistic curve that approximates the Gaussian edge.</summary>
        private const float LogisticSlope = 1.702f;

        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>RN's <c>shadows.pillLift</c>: a small lift for pills and seats.</summary>
        public static VisualElement PillLift(float width, float height, float radius)
        {
            return Create(width, height, radius, 3f, 8f, -2f, Theme.Rgba(0, 0, 0, 0.35f));
        }

        /// <summary>RN's <c>shadows.tileLift</c>: the heavier shadow under a playing card.</summary>
        public static VisualElement TileLift(float width, float height, float radius)
        {
            return Create(width, height, radius, 6f, 14f, -4f, Theme.Rgba(0, 0, 0, 0.4f));
        }

        /// <summary>
        /// A shadow for an element of <paramref name="width"/> x <paramref name="height"/>, positioned relative
        /// to that element's top-left corner. <paramref name="spread"/> grows (or, negative, shrinks) the shadow's rectangle.
        /// </summary>
        public static VisualElement Create(float width, float height, float radius, float offsetY, float blur, float spread, Color color)
        {
            float rectWidth = Mathf.Max(1f, width + 2f * spread);
            float rectHeight = Mathf.Max(1f, height + 2f * spread);
            float pad = blur;
            var texture = TextureFor(rectWidth, rectHeight, Mathf.Max(0f, radius + spread), blur);

            var element = Ui.View().NoPick();
            element.style.position = Position.Absolute;
            element.style.left = -spread - pad;
            element.style.top = -spread - pad + offsetY;
            element.style.width = rectWidth + 2f * pad;
            element.style.height = rectHeight + 2f * pad;
            element.BgImage(texture).Tint(color);
            return element;
        }

        /// <summary>
        /// A shadow that follows its owner's size, for owners whose box is not known when they are built
        /// (pills sized by their text). Same placement rules as <see cref="Create"/>; the texture is nine-sliced.
        /// </summary>
        public static VisualElement Fit(float radius, float offsetY, float blur, float spread, Color color)
        {
            float shadowRadius = Mathf.Max(1f, radius + spread);
            float pad = blur;
            var texture = TextureFor(2f * shadowRadius + 2f, 2f * shadowRadius + 2f, shadowRadius, blur);
            int slice = Mathf.CeilToInt((shadowRadius + pad) * TexelsPerDp);

            var element = Ui.View().NoPick();
            element.style.position = Position.Absolute;
            element.style.left = -spread - pad;
            element.style.right = -spread - pad;
            element.style.top = -spread - pad + offsetY;
            element.style.bottom = -spread - pad - offsetY;
            element.BgImage(texture).Tint(color);
            element.style.unitySliceLeft = slice;
            element.style.unitySliceRight = slice;
            element.style.unitySliceTop = slice;
            element.style.unitySliceBottom = slice;
            element.style.unitySliceScale = 1f / TexelsPerDp;
            return element;
        }

        /// <summary><see cref="Fit"/> with RN's <c>shadows.pillLift</c> values.</summary>
        public static VisualElement PillLiftFit(float radius)
        {
            return Fit(radius, 3f, 8f, -2f, Theme.Rgba(0, 0, 0, 0.35f));
        }

        private static Texture2D TextureFor(float rectWidth, float rectHeight, float radius, float blur)
        {
            string key = $"{rectWidth:0.##}x{rectHeight:0.##}r{radius:0.##}b{blur:0.##}";
            if (Cache.TryGetValue(key, out var cached)) return cached;

            int pixelsWide = Mathf.CeilToInt((rectWidth + 2f * blur) * TexelsPerDp);
            int pixelsHigh = Mathf.CeilToInt((rectHeight + 2f * blur) * TexelsPerDp);
            var pixels = new Color32[pixelsWide * pixelsHigh];
            float sigma = Mathf.Max(0.001f, blur * SigmaPerBlur);
            float halfWidth = rectWidth / 2f;
            float halfHeight = rectHeight / 2f;
            float clampedRadius = Mathf.Min(radius, Mathf.Min(halfWidth, halfHeight));

            for (int y = 0; y < pixelsHigh; y++)
            {
                // Texture row 0 is the bottom edge; the shape is vertically symmetric about its centre anyway.
                float py = (y + 0.5f) / TexelsPerDp - pixelsHigh / (2f * TexelsPerDp);
                for (int x = 0; x < pixelsWide; x++)
                {
                    float px = (x + 0.5f) / TexelsPerDp - pixelsWide / (2f * TexelsPerDp);
                    float distance = RoundedRectDistance(px, py, halfWidth, halfHeight, clampedRadius);
                    float alpha = 1f / (1f + Mathf.Exp(LogisticSlope * distance / sigma));
                    pixels[y * pixelsWide + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
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

        /// <summary>Signed distance from a point to a rounded rectangle centred on the origin; negative inside.</summary>
        internal static float RoundedRectDistance(float px, float py, float halfWidth, float halfHeight, float radius)
        {
            float qx = Mathf.Abs(px) - (halfWidth - radius);
            float qy = Mathf.Abs(py) - (halfHeight - radius);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }
    }
}
