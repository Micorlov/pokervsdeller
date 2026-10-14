using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// RN's <c>lineHeight</c> for a paragraph. USS has no line-height property, so the paragraph gets a copy of the
    /// bundled font whose line advance is the wanted ratio of its size (Noto Sans sets about 1.36 by default, where
    /// the RN copy asked for roughly 1.1 to 1.45). One copy is made per ratio and shared.
    /// </summary>
    public static class LineHeight
    {
        private static readonly Dictionary<string, FontAsset> Cache = new Dictionary<string, FontAsset>();

        /// <summary>Sets the label's line advance to <paramref name="pixels"/> at its current font size.</summary>
        public static Label SetLineHeight(this Label label, float fontSize, float pixels, bool bold = false)
        {
            var source = bold ? Ui.BoldFont : Ui.RegularFont;
            if (source == null || fontSize <= 0f) return label;

            float ratio = Mathf.Round(pixels / fontSize * 100f) / 100f;
            string key = source.name + ratio;
            if (!Cache.TryGetValue(key, out var tuned))
            {
                tuned = Tune(source, ratio);
                Cache[key] = tuned;
            }
            label.style.unityFontDefinition = FontDefinition.FromSDFFont(tuned);
            return label;
        }

        private static FontAsset Tune(FontAsset source, float ratio)
        {
            var copy = FontAsset.CreateFontAsset(source.sourceFontFile);
            copy.name = source.name + " x" + ratio;
            var info = copy.faceInfo;
            info.lineHeight = info.pointSize * ratio;
            copy.faceInfo = info;
            return copy;
        }
    }
}
