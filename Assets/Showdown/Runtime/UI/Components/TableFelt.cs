using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The table seen from the player's chair: lamp-lit felt whose near edge curves away in a wood rail, with a
    /// thin gold line inlaid just inside it. UI Toolkit radii are circular and cannot draw that shallow ellipse,
    /// so the whole surface is painted into one texture and pinned to the element's bottom edge, where the rail is.
    /// Content showing and hiding changes the felt's height every hand; a shorter felt just crops the plain top of
    /// the texture, so only a new width or a taller felt pays for a repaint.
    /// </summary>
    public sealed class TableFelt : VisualElement
    {
        /// <summary>How far the rail's ellipse runs past each side of the screen: the curve starts off-screen.</summary>
        private const float RailBleed = 70f;
        /// <summary>The rail's depth at its lowest point, as a fraction of the felt's height (capped).</summary>
        private const float CurveDepth = 0.34f;
        private const float MaxCurveDepth = 200f;
        private const float RailThickness = 14f;
        private const float LineInset = 10f;
        private const float LineWidth = 1.5f;
        /// <summary>Texture pixels per USS pixel. Two keeps the rail edge crisp on a phone without a huge texture.</summary>
        private const float Density = 2f;
        private const int MaxTextureSide = 2048;

        private Texture2D texture;
        private Vector2 paintedSize;

        public TableFelt()
        {
            pickingMode = PickingMode.Ignore;
            this.AbsFill();
            RegisterCallback<GeometryChangedEvent>(_ => Repaint());
            RegisterCallback<DetachFromPanelEvent>(_ => Release());
        }

        /// <summary>A repaint adds this much height up front, so a felt that grows a little reuses the texture.</summary>
        private const float HeightHeadroom = 120f;

        private void Repaint()
        {
            float width = Mathf.Round(contentRect.width);
            float height = Mathf.Round(contentRect.height);
            if (float.IsNaN(width) || width < 1f || height < 1f) return;
            bool fits = texture != null && Mathf.Abs(width - paintedSize.x) < 2f && height <= paintedSize.y;
            if (fits) return;

            paintedSize = new Vector2(width, height + HeightHeadroom);
            Release();
            texture = Paint(paintedSize.x, paintedSize.y);
            style.backgroundImage = new StyleBackground(texture);
            style.backgroundSize = new BackgroundSize(Length.Percent(100), new Length(paintedSize.y, LengthUnit.Pixel));
            style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Bottom);
            style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
        }

        private void Release()
        {
            if (texture == null) return;
            style.backgroundImage = StyleKeyword.None;
            Object.Destroy(texture);
            texture = null;
        }

        private static Texture2D Paint(float width, float height)
        {
            float density = Mathf.Min(Density, MaxTextureSide / Mathf.Max(width, height));
            int w = Mathf.Max(2, Mathf.RoundToInt(width * density));
            int h = Mathf.Max(2, Mathf.RoundToInt(height * density));

            var rail = new Ellipse(width / 2f, height, width / 2f + RailBleed, Mathf.Min(height * CurveDepth, MaxCurveDepth));
            var felt = rail.Inset(RailThickness);
            var line = rail.Inset(RailThickness + LineInset);
            var pixels = new Color32[w * h];

            for (int py = 0; py < h; py++)
            {
                // Texture row 0 is the bottom edge; work in USS pixels measured from the top.
                float y = height - (py + 0.5f) / density;
                for (int px = 0; px < w; px++)
                {
                    float x = (px + 0.5f) / density;
                    pixels[py * w + px] = Shade(x, y, width, height, density, rail, felt, line);
                }
            }

            var result = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            result.SetPixels32(pixels);
            result.Apply(false, true);
            return result;
        }

        private static Color32 Shade(float x, float y, float width, float height, float density, Ellipse rail, Ellipse felt, Ellipse line)
        {
            float railCover = Coverage(rail.Distance(x, y), density);
            if (railCover <= 0f) return new Color32(0, 0, 0, 0);

            var color = RailColor(y / height);
            float feltCover = Coverage(felt.Distance(x, y), density);
            if (feltCover > 0f)
            {
                color = Color.Lerp(color, FeltColor(x / width, y / height), feltCover);
                float lineCover = Mathf.Clamp01(LineWidth / 2f + 0.5f - Mathf.Abs(line.Distance(x, y)));
                if (lineCover > 0f && y > line.CentreY) color = Color.Lerp(color, Theme.Colors.FeltLine, lineCover * Theme.Colors.FeltLine.a);
            }
            else
            {
                // A lit lip along the rail's outer edge sells it as rounded wood.
                float lip = Mathf.Clamp01(1f - Mathf.Abs(rail.Distance(x, y) + 2f) / 2f);
                color = Color.Lerp(color, Theme.Rgba(255, 210, 160, 1f), lip * 0.25f);
            }
            color.a = railCover;
            return color;
        }

        /// <summary>Anti-aliased coverage from a signed distance in USS pixels (negative is inside).</summary>
        private static float Coverage(float distance, float density) => Mathf.Clamp01(0.5f - distance * density / 2f);

        private static Color RailColor(float v)
        {
            if (v < 0.8f) return Color.Lerp(Theme.Colors.RailShade, Theme.Colors.RailHigh, v / 0.8f);
            return Color.Lerp(Theme.Colors.RailHigh, Theme.Colors.RailLow, (v - 0.8f) / 0.2f);
        }

        /// <summary>The lamp hangs over the middle of the far side of the table.</summary>
        private static Color FeltColor(float u, float v)
        {
            float dx = (u - 0.5f) / 0.78f;
            float dy = (v - 0.26f) / 0.7f;
            float t = Mathf.Sqrt(dx * dx + dy * dy);
            if (t < 0.46f) return Color.Lerp(Theme.Colors.FeltHigh, Theme.Colors.FeltMid, t / 0.46f);
            return Color.Lerp(Theme.Colors.FeltMid, Theme.Colors.FeltLow, Mathf.Clamp01((t - 0.46f) / 0.54f));
        }

        /// <summary>
        /// A rectangle whose bottom edge is the lower half of an ellipse: everything above the ellipse's centre
        /// line is inside, below it only what the ellipse covers.
        /// </summary>
        private readonly struct Ellipse
        {
            public readonly float CentreX;
            public readonly float CentreY;
            private readonly float radiusX;
            private readonly float radiusY;

            /// <param name="bottom">The lowest point of the curve, at the centre.</param>
            public Ellipse(float centreX, float bottom, float radiusX, float radiusY)
            {
                CentreX = centreX;
                CentreY = bottom - radiusY;
                this.radiusX = radiusX;
                this.radiusY = radiusY;
            }

            public Ellipse Inset(float by) =>
                new Ellipse(CentreX, CentreY + radiusY - by, radiusX - by, Mathf.Max(1f, radiusY - by));

            /// <summary>Approximate signed distance to the edge: the implicit function over its gradient.</summary>
            public float Distance(float x, float y)
            {
                float dx = x - CentreX;
                if (y <= CentreY) return Mathf.Abs(dx) - radiusX;
                float dy = y - CentreY;
                float f = dx * dx / (radiusX * radiusX) + dy * dy / (radiusY * radiusY) - 1f;
                float gx = 2f * dx / (radiusX * radiusX);
                float gy = 2f * dy / (radiusY * radiusY);
                float gradient = Mathf.Sqrt(gx * gx + gy * gy);
                return gradient < 1e-5f ? -radiusY : f / gradient;
            }
        }
    }
}
