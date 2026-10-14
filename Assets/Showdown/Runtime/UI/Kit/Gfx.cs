using System;
using System.Collections.Generic;
using UnityEngine;

namespace Showdown.UI
{
    /// <summary>Shapes the app draws itself, because no bundled font carries suits and Unity has no emoji font.</summary>
    public enum Glyph
    {
        Spade,
        Heart,
        Diamond,
        Club,
        Star,
        Cross,
        /// <summary>Duke's avatar (was the top-hat emoji).</summary>
        Hat,
        /// <summary>Gus's avatar (was the die emoji).</summary>
        Die,
        /// <summary>Top-bar home button (was the house glyph).</summary>
        Home,
        /// <summary>Top-bar menu button (was the trigram glyph).</summary>
        Menu,
        /// <summary>The dashed insert ring on a poker chip.</summary>
        ChipRing,
        /// <summary>Stats badge that is earned (was the medal emoji); carries its own colours.</summary>
        Medal,
        /// <summary>Stats badge that is still locked (was the padlock emoji); carries its own colours.</summary>
        Lock,
        /// <summary>Top-bar back chevron.</summary>
        Back,
        /// <summary>Top-bar menu: three stacked dots.</summary>
        Kebab,
    }

    /// <summary>
    /// Procedural textures, generated once and cached. Gradients are 1-pixel-wide strips stretched to
    /// fill an element (USS has no gradient syntax); glyphs are anti-aliased masks that can be tinted.
    /// </summary>
    public static class Gfx
    {
        private const int GlyphSize = 128;
        private const int GlyphSamples = 4;

        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>Top-to-bottom linear gradient. Stretch it with BackgroundSize 100% / 100%.</summary>
        public static Texture2D Gradient(Color top, Color bottom, int height = 128)
        {
            string key = "g:" + ColorUtility.ToHtmlStringRGBA(top) + ColorUtility.ToHtmlStringRGBA(bottom) + height;
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var texture = NewTexture(2, height, TextureWrapMode.Clamp);
            var pixels = new Color[2 * height];
            for (int y = 0; y < height; y++)
            {
                // Texture row 0 is the bottom edge.
                var color = Color.Lerp(bottom, top, height == 1 ? 0 : y / (float)(height - 1));
                pixels[y * 2] = color;
                pixels[y * 2 + 1] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Cache[key] = texture;
            return texture;
        }

        /// <summary>One colour stop of <see cref="Radial"/>; <c>Position</c> runs 0 (centre) to 1 (ellipse edge).</summary>
        public readonly struct Stop
        {
            public readonly float Position;
            public readonly Color Color;

            public Stop(float position, Color color)
            {
                Position = position;
                Color = color;
            }
        }

        /// <summary>
        /// Elliptical radial gradient. Centre and radii are fractions of the texture (0..1); beyond the
        /// last stop the last colour continues.
        /// </summary>
        public static Texture2D Radial(string name, float centerX, float centerYFromTop, float radiusX, float radiusY, params Stop[] stops)
        {
            string key = "r:" + name;
            if (Cache.TryGetValue(key, out var cached)) return cached;

            const int size = 256;
            var texture = NewTexture(size, size, TextureWrapMode.Clamp);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = 1f - (y + 0.5f) / size; // v measured from the top
                float dy = (v - centerYFromTop) / radiusY;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float dx = (u - centerX) / radiusX;
                    pixels[y * size + x] = SampleStops(stops, Mathf.Sqrt(dx * dx + dy * dy));
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Cache[key] = texture;
            return texture;
        }

        private static Color SampleStops(Stop[] stops, float t)
        {
            if (t <= stops[0].Position) return stops[0].Color;
            for (int i = 1; i < stops.Length; i++)
            {
                if (t <= stops[i].Position)
                {
                    float span = stops[i].Position - stops[i - 1].Position;
                    float k = span <= 0 ? 1 : (t - stops[i - 1].Position) / span;
                    return Color.Lerp(stops[i - 1].Color, stops[i].Color, k);
                }
            }
            return stops[stops.Length - 1].Color;
        }

        /// <summary>The glyph mask: white with alpha (tint it), except Hat and Die which carry their own colours.</summary>
        public static Texture2D GlyphTexture(Glyph glyph)
        {
            string key = "glyph:" + glyph;
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var texture = NewTexture(GlyphSize, GlyphSize, TextureWrapMode.Clamp);
            var pixels = new Color[GlyphSize * GlyphSize];
            for (int y = 0; y < GlyphSize; y++)
            {
                for (int x = 0; x < GlyphSize; x++)
                {
                    pixels[y * GlyphSize + x] = Rasterize(glyph, x, y);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Cache[key] = texture;
            return texture;
        }

        /// <summary>Maps an avatar's emoji to the glyph that stands in for it; null for anything unknown.</summary>
        public static Glyph? AvatarGlyph(string emoji)
        {
            if (emoji == "\U0001F3A9") return Glyph.Hat;
            if (emoji == "\U0001F3B2") return Glyph.Die;
            return null;
        }

        private static Texture2D NewTexture(int width, int height, TextureWrapMode wrap)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = wrap,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        // ---- glyph rasterising -------------------------------------------------------------------

        /// <summary>Supersamples each pixel so edges are smooth at any size the glyph is scaled to.</summary>
        private static Color Rasterize(Glyph glyph, int px, int py)
        {
            float r = 0, g = 0, b = 0, a = 0;
            for (int sy = 0; sy < GlyphSamples; sy++)
            {
                for (int sx = 0; sx < GlyphSamples; sx++)
                {
                    float u = (px + (sx + 0.5f) / GlyphSamples) / GlyphSize;
                    float v = (py + (sy + 0.5f) / GlyphSamples) / GlyphSize; // v grows upward
                    var sample = ShadeAt(glyph, u, v);
                    r += sample.r * sample.a;
                    g += sample.g * sample.a;
                    b += sample.b * sample.a;
                    a += sample.a;
                }
            }
            float count = GlyphSamples * GlyphSamples;
            if (a <= 0) return new Color(1, 1, 1, 0);
            return new Color(r / a, g / a, b / a, a / count);
        }

        private static readonly Color Ink = new Color(1, 1, 1, 1);
        private static readonly Color Clear = new Color(1, 1, 1, 0);

        private static Color ShadeAt(Glyph glyph, float u, float v)
        {
            switch (glyph)
            {
                case Glyph.Heart: return HeartShape(u, v) ? Ink : Clear;
                case Glyph.Spade: return SpadeShape(u, v) ? Ink : Clear;
                case Glyph.Diamond: return DiamondShape(u, v) ? Ink : Clear;
                case Glyph.Club: return ClubShape(u, v) ? Ink : Clear;
                case Glyph.Star: return StarShape(u, v) ? Ink : Clear;
                case Glyph.Cross: return CrossShape(u, v) ? Ink : Clear;
                case Glyph.ChipRing: return ChipRingShape(u, v) ? Ink : Clear;
                case Glyph.Home: return HomeShape(u, v) ? Ink : Clear;
                case Glyph.Menu: return MenuShape(u, v) ? Ink : Clear;
                case Glyph.Back: return BackShape(u, v) ? Ink : Clear;
                case Glyph.Kebab: return KebabShape(u, v) ? Ink : Clear;
                case Glyph.Hat: return HatShade(u, v);
                case Glyph.Die: return DieShade(u, v);
                case Glyph.Medal: return AchievementGlyphs.Medal(u, v);
                case Glyph.Lock: return AchievementGlyphs.Lock(u, v);
                default: return Clear;
            }
        }

        // Shapes work in a centred space, x and y in roughly [-1, 1], y up.

        private static bool HeartShape(float u, float v)
        {
            float x = (u - 0.5f) * 2f * 1.2f;
            float y = (v - 0.5f) * 2f * 1.2f + 0.1f;
            float a = x * x + y * y - 1f;
            return a * a * a - x * x * y * y * y <= 0f;
        }

        private static bool SpadeShape(float u, float v)
        {
            // An upside-down heart (tip up, lobes down) with a flared stem under the cleft.
            float x = (u - 0.5f) * 2f * 1.36f;
            float y = (0.6f - v) * 2f * 1.36f;
            float a = x * x + y * y - 1f;
            if (a * a * a - x * x * y * y * y <= 0f) return true;

            if (v > 0.05f && v < 0.3f)
            {
                float t = (0.3f - v) / 0.25f; // 0 under the cleft, 1 at the foot
                float half = 0.05f + 0.19f * t * t;
                return Mathf.Abs(u - 0.5f) <= half;
            }
            return false;
        }

        private static bool DiamondShape(float u, float v)
        {
            float x = Mathf.Abs((u - 0.5f) * 2f) / 0.72f;
            float y = Mathf.Abs((v - 0.5f) * 2f) / 0.98f;
            return x + y <= 1f;
        }

        private static bool ClubShape(float u, float v)
        {
            float x = (u - 0.5f) * 2f;
            float y = (v - 0.5f) * 2f;
            if (InCircle(x, y, 0f, 0.5f, 0.38f)) return true;
            if (InCircle(x, y, -0.5f, -0.05f, 0.38f)) return true;
            if (InCircle(x, y, 0.5f, -0.05f, 0.38f)) return true;
            if (InCircle(x, y, 0f, 0.1f, 0.34f)) return true; // fills the join between the lobes
            if (y > -0.92f && y < -0.1f)
            {
                float t = (-0.1f - y) / 0.82f;
                float half = 0.1f + 0.42f * t * t;
                return Mathf.Abs(x) <= half;
            }
            return false;
        }

        private static bool InCircle(float x, float y, float cx, float cy, float radius)
        {
            float dx = x - cx;
            float dy = y - cy;
            return dx * dx + dy * dy <= radius * radius;
        }

        private static readonly Vector2[] StarPoints = BuildStarPoints();

        private static Vector2[] BuildStarPoints()
        {
            const float outer = 0.98f;
            const float inner = 0.44f;
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float radius = i % 2 == 0 ? outer : inner;
                float angle = Mathf.PI / 2f + i * Mathf.PI / 5f; // first point straight up
                points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }
            return points;
        }

        private static bool StarShape(float u, float v)
        {
            var p = new Vector2((u - 0.5f) * 2f, (v - 0.5f) * 2f - 0.04f);
            bool inside = false;
            for (int i = 0, j = StarPoints.Length - 1; i < StarPoints.Length; j = i++)
            {
                var a = StarPoints[i];
                var b = StarPoints[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        private static bool CrossShape(float u, float v)
        {
            float x = (u - 0.5f) * 2f;
            float y = (v - 0.5f) * 2f;
            const float half = 0.15f;
            const float reach = 0.72f;
            bool onDiagonalA = Mathf.Abs(x - y) <= half * 1.414f && Mathf.Abs(x) <= reach && Mathf.Abs(y) <= reach;
            bool onDiagonalB = Mathf.Abs(x + y) <= half * 1.414f && Mathf.Abs(x) <= reach && Mathf.Abs(y) <= reach;
            return onDiagonalA || onDiagonalB;
        }

        private static bool ChipRingShape(float u, float v)
        {
            float x = (u - 0.5f) * 2f;
            float y = (v - 0.5f) * 2f;
            float radius = Mathf.Sqrt(x * x + y * y);
            if (radius < 0.76f || radius > 0.9f) return false;
            // Eighteen dashes, each filling a little over half its slot.
            const int dashes = 18;
            float slot = Mathf.Repeat(Mathf.Atan2(y, x) / (Mathf.PI * 2f) * dashes, 1f);
            return slot < 0.58f;
        }

        private static bool HomeShape(float u, float v)
        {
            float x = (u - 0.5f) * 2f;
            float y = (v - 0.5f) * 2f;
            // Roof: a triangle whose apex is at the top; body: a box below it with a door cut out.
            bool roof = y >= -0.05f && y <= 0.85f && Mathf.Abs(x) <= (0.85f - y) * 1.02f;
            bool body = y >= -0.78f && y < -0.05f && Mathf.Abs(x) <= 0.62f;
            bool door = y >= -0.78f && y <= -0.2f && Mathf.Abs(x) <= 0.2f;
            return (roof || body) && !door;
        }

        /// <summary>A chevron pointing left: two round-capped strokes meeting at a point.</summary>
        private static bool BackShape(float u, float v)
        {
            var p = new Vector2((u - 0.5f) * 2f, (v - 0.5f) * 2f);
            const float half = 0.13f;
            var tip = new Vector2(-0.32f, 0f);
            return SegmentDistance(p, new Vector2(0.24f, 0.72f), tip) <= half
                || SegmentDistance(p, tip, new Vector2(0.24f, -0.72f)) <= half;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        private static readonly float[] KebabDotCentres = { -0.62f, 0f, 0.62f };

        private static bool KebabShape(float u, float v)
        {
            float x = (u - 0.5f) * 2f;
            float y = (v - 0.5f) * 2f;
            foreach (float centre in KebabDotCentres)
            {
                if (InCircle(x, y, 0f, centre, 0.17f)) return true;
            }
            return false;
        }

        private static readonly float[] MenuBarCentres = { -0.52f, 0f, 0.52f };

        private static bool MenuShape(float u, float v)
        {
            float x = (u - 0.5f) * 2f;
            float y = (v - 0.5f) * 2f;
            if (Mathf.Abs(x) > 0.78f) return false;
            // Three bars with softly rounded ends.
            foreach (float centre in MenuBarCentres)
            {
                float dy = Mathf.Abs(y - centre);
                if (dy <= 0.14f)
                {
                    float over = Mathf.Abs(x) - (0.78f - 0.14f);
                    if (over <= 0f || over * over + dy * dy <= 0.14f * 0.14f) return true;
                }
            }
            return false;
        }

        private static readonly Color HatBlack = Theme.Hex("#1B1D26");
        private static readonly Color HatHighlight = Theme.Hex("#3A3E4E");
        private static readonly Color HatBand = Theme.Hex("#C0273A");
        private static readonly Color DiePip = Theme.Hex("#22242E");
        private static readonly Color DieTop = Theme.Hex("#FFFFFF");
        private static readonly Color DieBottom = Theme.Hex("#E4E1D6");

        private static Color HatShade(float u, float v)
        {
            float x = (u - 0.5f) * 2f;
            float y = (v - 0.5f) * 2f;
            var black = HatBlack;
            var highlight = HatHighlight;
            var band = HatBand;

            // Brim: a flat ellipse near the bottom.
            float bx = x / 0.9f;
            float by = (y + 0.55f) / 0.17f;
            bool brim = bx * bx + by * by <= 1f;

            // Crown: a tall rectangle with softly rounded top corners.
            bool crown = Mathf.Abs(x) <= 0.5f && y >= -0.55f && y <= 0.62f;
            if (crown && y > 0.5f)
            {
                float cx = Mathf.Abs(x) - 0.38f;
                float cy = y - 0.5f;
                if (cx > 0 && cx * cx + cy * cy > 0.12f * 0.12f) crown = false;
            }

            if (crown && y >= -0.4f && y <= -0.16f) return band;
            if (crown && x < -0.28f && x > -0.4f && y > -0.1f && y < 0.5f) return highlight;
            if (crown || brim) return black;
            return Clear;
        }

        private static Color DieShade(float u, float v)
        {
            // A die tilted a little, as the emoji sits.
            float x = (u - 0.5f) * 2f;
            float y = (v - 0.5f) * 2f;
            const float tilt = -0.28f;
            float cos = Mathf.Cos(tilt);
            float sin = Mathf.Sin(tilt);
            float rx = x * cos - y * sin;
            float ry = x * sin + y * cos;

            const float half = 0.72f;
            const float corner = 0.2f;
            float qx = Mathf.Max(Mathf.Abs(rx) - (half - corner), 0f);
            float qy = Mathf.Max(Mathf.Abs(ry) - (half - corner), 0f);
            if (qx * qx + qy * qy > corner * corner) return Clear;

            // Five pips: the four corners and the centre.
            const float pipRadius = 0.115f;
            const float offset = 0.36f;
            if (InCircle(rx, ry, 0f, 0f, pipRadius)) return DiePip;
            if (InCircle(rx, ry, offset, offset, pipRadius)) return DiePip;
            if (InCircle(rx, ry, -offset, offset, pipRadius)) return DiePip;
            if (InCircle(rx, ry, offset, -offset, pipRadius)) return DiePip;
            if (InCircle(rx, ry, -offset, -offset, pipRadius)) return DiePip;

            // A touch of shading so the face does not read as a flat sticker.
            return Color.Lerp(DieTop, DieBottom, Mathf.Clamp01((0.6f - ry) / 1.2f));
        }
    }
}
