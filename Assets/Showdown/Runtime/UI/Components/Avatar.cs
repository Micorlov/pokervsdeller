using Showdown.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// A rounded-square avatar chip drawn entirely in code (port of Avatar.tsx): an initial on a gradient for
    /// the player, a drawn face for the AI. The emoji avatars become the app's own hat and die glyphs.
    /// </summary>
    public sealed class Avatar : VisualElement
    {
        private const float CornerFraction = 0.28f;
        private const float InitialFraction = 0.42f;
        private const float GlyphFraction = 0.58f;
        private static readonly Color DefaultTop = Theme.Hex("#4A7BAF");
        private static readonly Color DefaultBottom = Theme.Hex("#2E5580");

        public Avatar(BotAvatar avatar, float size = 44f)
        {
            var top = DefaultTop;
            var bottom = DefaultBottom;
            if (avatar.Gradient != null && avatar.Gradient.Count >= 2)
            {
                top = Theme.Hex(avatar.Gradient[0]);
                bottom = Theme.Hex(avatar.Gradient[1]);
            }

            style.flexShrink = 0;
            this.Size(size, size).Radius(size * CornerFraction).Center().BgGradient(top, bottom)
                .Border(1, Theme.Rgba(255, 255, 255, 0.22f));

            if (avatar.Kind == BotAvatarKind.Initial)
            {
                Add(TableStyle.Line(avatar.Value, size * InitialFraction, Color.white, true, TextAnchor.MiddleCenter, 0.5f));
                return;
            }

            var glyph = Gfx.AvatarGlyph(avatar.Value);
            if (glyph.HasValue) Add(Ui.GlyphView(glyph.Value, size * GlyphFraction));
        }
    }
}
