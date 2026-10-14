using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The flame the streak toast used as an emoji, drawn in code — the bundled font has no emoji.
    /// </summary>
    public static class ToastIcons
    {
        private const int TextureSize = 96;
        private const int Samples = 3;
        private static Texture2D flame;

        /// <summary>A flame, standing in for the fire emoji.</summary>
        public static VisualElement Flame(float size)
        {
            return Ui.Picture(FlameTexture(), size, size);
        }

        private static Texture2D FlameTexture()
        {
            if (flame != null) return flame;

            var pixels = new Color[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++) pixels[y * TextureSize + x] = Supersample(x, y);
            }
            flame = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            flame.SetPixels(pixels);
            flame.Apply(false, true);
            return flame;
        }

        private static Color Supersample(int px, int py)
        {
            float r = 0, g = 0, b = 0, a = 0;
            for (int sy = 0; sy < Samples; sy++)
            {
                for (int sx = 0; sx < Samples; sx++)
                {
                    float u = (px + (sx + 0.5f) / Samples) / TextureSize;
                    float v = (py + (sy + 0.5f) / Samples) / TextureSize;
                    var sample = FlameShade(u, v);
                    r += sample.r * sample.a;
                    g += sample.g * sample.a;
                    b += sample.b * sample.a;
                    a += sample.a;
                }
            }
            if (a <= 0f) return new Color(1, 1, 1, 0);
            return new Color(r / a, g / a, b / a, a / (Samples * Samples));
        }

        /// <summary>A teardrop whose tip leans right, with a smaller yellow teardrop at its heart.</summary>
        private static Color FlameShade(float u, float v)
        {
            if (InTeardrop(u, v, 0.5f, 0.32f, 0.3f, 0.96f, 0.07f))
            {
                if (InTeardrop(u, v, 0.5f, 0.27f, 0.15f, 0.6f, 0.02f)) return Theme.Hex("#FFD84A");
                return Color.Lerp(Theme.Hex("#F04A1E"), Theme.Hex("#FF9A2A"), Mathf.Clamp01(v * 1.4f));
            }
            return new Color(1, 1, 1, 0);
        }

        /// <summary>A circle at the base that tapers to a tip at height <paramref name="tipY"/>, drifting by <paramref name="lean"/>.</summary>
        private static bool InTeardrop(float u, float v, float cx, float cy, float radius, float tipY, float lean)
        {
            float dx = u - cx;
            float dy = v - cy;
            if (dx * dx + dy * dy <= radius * radius) return true;
            if (v < cy || v > tipY) return false;
            float t = (v - cy) / (tipY - cy);
            float half = radius * Mathf.Pow(1f - t, 1.5f);
            return Mathf.Abs(u - (cx + lean * t * t)) <= half;
        }
    }
}
