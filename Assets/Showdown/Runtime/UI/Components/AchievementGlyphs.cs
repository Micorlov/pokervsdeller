using UnityEngine;

namespace Showdown.UI
{
    /// <summary>
    /// The two coloured pictures the achievement grid used emoji for (a medal for earned, a padlock for
    /// locked). The bundled font has no emoji, so they are drawn like the other glyphs and rasterised by
    /// <see cref="Gfx"/>. Each works in a centred space, x and y in roughly [-1, 1], y up.
    /// </summary>
    internal static class AchievementGlyphs
    {
        private static readonly Color Clear = new Color(1, 1, 1, 0);

        private static readonly Color Gold = Theme.Hex("#FFC93C");
        private static readonly Color GoldRim = Theme.Hex("#D99E1B");
        private static readonly Color GoldShine = Theme.Hex("#FFE38A");
        private static readonly Color RibbonBlue = Theme.Hex("#3E7BC0");
        private static readonly Color RibbonRed = Theme.Hex("#D14848");

        private static readonly Color LockBody = Theme.Hex("#F0B429");
        private static readonly Color LockHole = Theme.Hex("#5A2B05");
        private static readonly Color LockShackle = Theme.Hex("#B9C0CC");

        public static Color Medal(float u, float v)
        {
            float x = (u - 0.5f) * 2f;
            float y = (v - 0.5f) * 2f;

            float dx = x;
            float dy = y + 0.3f;
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            if (radius <= 0.56f)
            {
                if (radius >= 0.5f) return GoldRim;
                if (radius >= 0.36f && radius <= 0.4f) return GoldRim;
                return radius < 0.2f ? GoldShine : Gold;
            }

            return Ribbon(x, y);
        }

        /// <summary>Two strips running from the shoulders down behind the disc, crossing at its top.</summary>
        private static Color Ribbon(float x, float y)
        {
            if (y < 0f || y > 0.95f) return Clear;
            float lean = Mathf.Lerp(0.12f, 0.46f, y / 0.95f);
            const float halfWidth = 0.17f;
            if (Mathf.Abs(x + lean) <= halfWidth) return RibbonBlue;
            if (Mathf.Abs(x - lean) <= halfWidth) return RibbonRed;
            return Clear;
        }

        public static Color Lock(float u, float v)
        {
            float x = (u - 0.5f) * 2f;
            float y = (v - 0.5f) * 2f;

            if (InBody(x, y))
            {
                bool keyhole = InCircle(x, y, 0f, -0.34f, 0.13f) || (Mathf.Abs(x) <= 0.055f && y >= -0.68f && y <= -0.34f);
                return keyhole ? LockHole : LockBody;
            }

            return InShackle(x, y) ? LockShackle : Clear;
        }

        private static bool InBody(float x, float y)
        {
            const float halfWidth = 0.62f;
            const float corner = 0.16f;
            const float top = 0.08f;
            const float bottom = -0.9f;
            float qx = Mathf.Max(Mathf.Abs(x) - (halfWidth - corner), 0f);
            float centreY = (top + bottom) / 2f;
            float qy = Mathf.Max(Mathf.Abs(y - centreY) - ((top - bottom) / 2f - corner), 0f);
            return qx * qx + qy * qy <= corner * corner;
        }

        private static bool InShackle(float x, float y)
        {
            const float outer = 0.46f;
            const float inner = 0.27f;
            const float archCentreY = 0.2f;
            if (y >= archCentreY)
            {
                float radius = Mathf.Sqrt(x * x + (y - archCentreY) * (y - archCentreY));
                return radius <= outer && radius >= inner;
            }

            return y >= 0f && Mathf.Abs(x) <= outer && Mathf.Abs(x) >= inner;
        }

        private static bool InCircle(float x, float y, float cx, float cy, float radius)
        {
            float dx = x - cx;
            float dy = y - cy;
            return dx * dx + dy * dy <= radius * radius;
        }
    }
}
