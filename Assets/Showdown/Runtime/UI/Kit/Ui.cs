using System;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// A thin, React-Native-flavoured layer over UI Toolkit. UI Toolkit lays out with the same flexbox
    /// engine (Yoga) as React Native, so a StyleSheet maps almost line for line onto these helpers.
    ///
    /// Two RN defaults are restored on purpose: views do not shrink (<c>flexShrink: 0</c>) and text does
    /// (it wraps inside whatever width it is given).
    /// </summary>
    public static class Ui
    {
        /// <summary>Manrope SemiBold and ExtraBold, assigned by <see cref="UiHost"/> before any screen is built.</summary>
        public static FontAsset RegularFont;
        public static FontAsset BoldFont;
        /// <summary>Cormorant Garamond SemiBold Italic: table names, headlines.</summary>
        public static FontAsset SerifFont;
        /// <summary>Cormorant SC SemiBold: lettering printed on the felt and small caps labels.</summary>
        public static FontAsset CapsFont;
        /// <summary>DM Mono Medium: every number.</summary>
        public static FontAsset MonoFont;

        // ---- factories -----------------------------------------------------------------------------

        public static VisualElement View(string name = null)
        {
            var element = new VisualElement { name = name };
            element.style.flexShrink = 0;
            return element;
        }

        /// <summary>A horizontal stack. <paramref name="gap"/> is applied as leading margin on every child but the first.</summary>
        public static VisualElement Row(float gap, params VisualElement[] children)
        {
            var row = View().Row();
            AddAll(row, gap, children);
            return row;
        }

        /// <summary>A vertical stack with <paramref name="gap"/> between children.</summary>
        public static VisualElement Col(float gap, params VisualElement[] children)
        {
            var column = View();
            AddAll(column, gap, children);
            return column;
        }

        /// <summary>Adds children to a container, spacing them like RN's <c>gap</c>. Null children are skipped.</summary>
        public static T AddAll<T>(T container, float gap, params VisualElement[] children) where T : VisualElement
        {
            foreach (var child in children)
            {
                if (child != null) AddWithGap(container, child, gap);
            }
            return container;
        }

        /// <summary>Adds one child, putting <paramref name="gap"/> before it along the container's main axis.</summary>
        public static void AddWithGap(VisualElement container, VisualElement child, float gap)
        {
            if (container.childCount > 0 && gap > 0)
            {
                bool horizontal = container.resolvedStyle.flexDirection == FlexDirection.Row
                    || container.style.flexDirection == FlexDirection.Row
                    || container.style.flexDirection == FlexDirection.RowReverse;
                if (horizontal) child.style.marginLeft = gap;
                else child.style.marginTop = gap;
            }
            container.Add(child);
        }

        /// <summary>A text label. Wraps within its parent; pass <paramref name="wrap"/> false for a single line.</summary>
        public static Label Text(string text, float size, Color color, bool bold = false,
            TextAnchor align = TextAnchor.MiddleLeft, float letterSpacing = 0f, bool wrap = true)
        {
            return TextIn(bold ? BoldFont : RegularFont, text, size, color, align, letterSpacing, wrap);
        }

        /// <summary>A label in a specific face; a face that failed to load falls back to the bold sans.</summary>
        public static Label TextIn(FontAsset font, string text, float size, Color color,
            TextAnchor align = TextAnchor.MiddleLeft, float letterSpacing = 0f, bool wrap = true)
        {
            var label = new Label(text ?? string.Empty);
            font = font != null ? font : BoldFont;
            var style = label.style;
            if (font != null) style.unityFontDefinition = FontDefinition.FromSDFFont(font);
            style.fontSize = size;
            style.color = color;
            style.unityTextAlign = align;
            style.whiteSpace = wrap ? WhiteSpace.Normal : WhiteSpace.NoWrap;
            style.flexShrink = 1;
            style.marginLeft = 0;
            style.marginRight = 0;
            style.marginTop = 0;
            style.marginBottom = 0;
            style.paddingLeft = 0;
            style.paddingRight = 0;
            style.paddingTop = 0;
            style.paddingBottom = 0;
            if (Mathf.Abs(letterSpacing) > 0.001f) style.letterSpacing = letterSpacing;
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        /// <summary>Italic serif — table names and headlines.</summary>
        public static Label Serif(string text, float size, Color color, TextAnchor align = TextAnchor.MiddleCenter) =>
            TextIn(SerifFont, text, size, color, align, 0f, false);

        /// <summary>Small caps serif — felt lettering and section labels.</summary>
        public static Label Caps(string text, float size, Color color, TextAnchor align = TextAnchor.MiddleLeft, float letterSpacing = 2f) =>
            TextIn(CapsFont, text, size, color, align, letterSpacing, false);

        /// <summary>Monospaced figures, so a counting number never jitters sideways.</summary>
        public static Label Mono(string text, float size, Color color, TextAnchor align = TextAnchor.MiddleLeft, float letterSpacing = 0f) =>
            TextIn(MonoFont, text, size, color, align, letterSpacing, false);

        // Type ramp — Theme.Type, in the voices the screens use.
        public static Label Title(string text) => Text(text, Theme.Type.Title, Theme.Colors.Text, true, TextAnchor.MiddleCenter, -1.3f);
        public static Label Heading(string text) => Text(text, Theme.Type.Heading, Theme.Colors.Text, true, TextAnchor.MiddleLeft, -0.7f);
        public static Label Body(string text) => Text(text, Theme.Type.Body, Theme.Colors.Text);
        public static Label BodyMuted(string text) => Text(text, Theme.Type.Body, Theme.Colors.TextMuted);
        public static Label Caption(string text) => Text(text, Theme.Type.Caption, Theme.Colors.TextMuted);

        /// <summary>The small uppercase labels above a panel.</summary>
        public static Label LabelCaps(string text) =>
            Text((text ?? string.Empty).ToUpperInvariant(), Theme.Type.Label, Theme.Colors.TextMuted, true, TextAnchor.MiddleLeft, 1.4f);

        /// <summary>A tinted or full-colour picture stretched to its element's box.</summary>
        public static VisualElement Picture(Texture2D texture, float width, float height, Color? tint = null)
        {
            var element = View().W(width).H(height).BgImage(texture);
            if (tint.HasValue) element.style.unityBackgroundImageTintColor = tint.Value;
            element.pickingMode = PickingMode.Ignore;
            return element;
        }

        /// <summary>A glyph (suit, star, cross, avatar) at a given size, optionally tinted.</summary>
        public static VisualElement GlyphView(Glyph glyph, float size, Color? tint = null)
        {
            return Picture(Gfx.GlyphTexture(glyph), size, size, tint);
        }

        /// <summary>A vertical scroller with the hidden scrollbar and clamped touch behaviour phones expect.</summary>
        public static ScrollView Scroll()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped,
                nestedInteractionKind = ScrollView.NestedInteractionKind.Default,
            };
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            return scroll;
        }

        // ---- interaction ---------------------------------------------------------------------------

        /// <summary>
        /// Calls <paramref name="onTap"/> on a press-and-release that stays inside the element. A drag that
        /// turns into a scroll cancels the press, so tapping inside a ScrollView still works.
        /// </summary>
        public static T OnTap<T>(this T element, Action onTap, Action<bool> onPressedChanged = null) where T : VisualElement
        {
            int activePointer = -1;

            void SetPressed(bool pressed)
            {
                if (!pressed) activePointer = -1;
                onPressedChanged?.Invoke(pressed);
            }

            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (activePointer != -1) return;
                activePointer = evt.pointerId;
                SetPressed(true);
            });
            element.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (activePointer != evt.pointerId) return;
                bool inside = element.worldBound.Contains(evt.position);
                SetPressed(false);
                if (inside) onTap?.Invoke();
            });
            element.RegisterCallback<PointerCancelEvent>(evt =>
            {
                if (activePointer == evt.pointerId) SetPressed(false);
            });
            element.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                if (activePointer == evt.pointerId) SetPressed(false);
            });
            element.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                if (activePointer == evt.pointerId && evt.pointerType == UnityEngine.UIElements.PointerType.mouse) SetPressed(false);
            });
            return element;
        }

        // ---- sizing --------------------------------------------------------------------------------

        public static T W<T>(this T e, float px) where T : VisualElement { e.style.width = px; return e; }
        public static T H<T>(this T e, float px) where T : VisualElement { e.style.height = px; return e; }
        public static T Size<T>(this T e, float width, float height) where T : VisualElement { e.style.width = width; e.style.height = height; return e; }
        public static T WPct<T>(this T e, float percent) where T : VisualElement { e.style.width = Length.Percent(percent); return e; }
        public static T HPct<T>(this T e, float percent) where T : VisualElement { e.style.height = Length.Percent(percent); return e; }
        public static T MinW<T>(this T e, float px) where T : VisualElement { e.style.minWidth = px; return e; }
        public static T MinH<T>(this T e, float px) where T : VisualElement { e.style.minHeight = px; return e; }
        public static T MaxW<T>(this T e, float px) where T : VisualElement { e.style.maxWidth = px; return e; }
        public static T MaxH<T>(this T e, float px) where T : VisualElement { e.style.maxHeight = px; return e; }

        // ---- flex ----------------------------------------------------------------------------------

        /// <summary>RN's <c>flex: 1</c>: grow and shrink to fill the main axis from a zero basis.</summary>
        public static T Flex<T>(this T e, float grow = 1) where T : VisualElement
        {
            e.style.flexGrow = grow;
            e.style.flexShrink = 1;
            e.style.flexBasis = 0;
            return e;
        }

        public static T Grow<T>(this T e, float grow = 1) where T : VisualElement { e.style.flexGrow = grow; return e; }
        public static T Shrink<T>(this T e, float shrink) where T : VisualElement { e.style.flexShrink = shrink; return e; }
        public static T Row<T>(this T e) where T : VisualElement { e.style.flexDirection = FlexDirection.Row; return e; }
        public static T Wrap<T>(this T e) where T : VisualElement { e.style.flexWrap = UnityEngine.UIElements.Wrap.Wrap; return e; }
        public static T AlignItems<T>(this T e, Align align) where T : VisualElement { e.style.alignItems = align; return e; }
        public static T AlignSelf<T>(this T e, Align align) where T : VisualElement { e.style.alignSelf = align; return e; }
        public static T Justify<T>(this T e, Justify justify) where T : VisualElement { e.style.justifyContent = justify; return e; }

        /// <summary>Centres children on both axes.</summary>
        public static T Center<T>(this T e) where T : VisualElement
        {
            e.style.alignItems = Align.Center;
            e.style.justifyContent = UnityEngine.UIElements.Justify.Center;
            return e;
        }

        // ---- spacing -------------------------------------------------------------------------------

        public static T Pad<T>(this T e, float all) where T : VisualElement => e.Pad(all, all, all, all);
        public static T Pad<T>(this T e, float vertical, float horizontal) where T : VisualElement => e.Pad(vertical, horizontal, vertical, horizontal);

        public static T Pad<T>(this T e, float top, float right, float bottom, float left) where T : VisualElement
        {
            e.style.paddingTop = top;
            e.style.paddingRight = right;
            e.style.paddingBottom = bottom;
            e.style.paddingLeft = left;
            return e;
        }

        public static T Margin<T>(this T e, float all) where T : VisualElement => e.Margin(all, all, all, all);

        public static T Margin<T>(this T e, float top, float right, float bottom, float left) where T : VisualElement
        {
            e.style.marginTop = top;
            e.style.marginRight = right;
            e.style.marginBottom = bottom;
            e.style.marginLeft = left;
            return e;
        }

        public static T MarginTop<T>(this T e, float px) where T : VisualElement { e.style.marginTop = px; return e; }
        public static T MarginBottom<T>(this T e, float px) where T : VisualElement { e.style.marginBottom = px; return e; }
        public static T MarginLeft<T>(this T e, float px) where T : VisualElement { e.style.marginLeft = px; return e; }
        public static T MarginRight<T>(this T e, float px) where T : VisualElement { e.style.marginRight = px; return e; }

        // ---- position ------------------------------------------------------------------------------

        /// <summary>Absolute placement; pass only the edges that matter.</summary>
        public static T Abs<T>(this T e, float? top = null, float? right = null, float? bottom = null, float? left = null) where T : VisualElement
        {
            e.style.position = Position.Absolute;
            if (top.HasValue) e.style.top = top.Value;
            if (right.HasValue) e.style.right = right.Value;
            if (bottom.HasValue) e.style.bottom = bottom.Value;
            if (left.HasValue) e.style.left = left.Value;
            return e;
        }

        /// <summary>Absolute and stretched over its parent (top/right/bottom/left all 0).</summary>
        public static T AbsFill<T>(this T e) where T : VisualElement => e.Abs(0, 0, 0, 0);

        // ---- appearance ----------------------------------------------------------------------------

        public static T Bg<T>(this T e, Color color) where T : VisualElement { e.style.backgroundColor = color; return e; }

        /// <summary>A top-to-bottom gradient, standing in for RN's <c>experimental_backgroundImage</c> linear-gradient.</summary>
        public static T BgGradient<T>(this T e, Color top, Color bottom) where T : VisualElement
        {
            e.style.backgroundColor = bottom;
            return e.BgImage(Gfx.Gradient(top, bottom));
        }

        /// <summary>Stretches a texture over the element's whole box.</summary>
        public static T BgImage<T>(this T e, Texture2D texture) where T : VisualElement
        {
            e.style.backgroundImage = new StyleBackground(texture);
            e.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            e.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            return e;
        }

        /// <summary>Fits a texture inside the element without distortion, centred.</summary>
        public static T BgContain<T>(this T e, Texture2D texture) where T : VisualElement
        {
            e.style.backgroundImage = new StyleBackground(texture);
            e.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            e.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            e.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            e.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            return e;
        }

        public static T Tint<T>(this T e, Color color) where T : VisualElement { e.style.unityBackgroundImageTintColor = color; return e; }

        public static T Radius<T>(this T e, float radius) where T : VisualElement => e.Radius(radius, radius, radius, radius);

        public static T Radius<T>(this T e, float topLeft, float topRight, float bottomRight, float bottomLeft) where T : VisualElement
        {
            e.style.borderTopLeftRadius = topLeft;
            e.style.borderTopRightRadius = topRight;
            e.style.borderBottomRightRadius = bottomRight;
            e.style.borderBottomLeftRadius = bottomLeft;
            return e;
        }

        /// <summary>Uniform border.</summary>
        public static T Border<T>(this T e, float width, Color color) where T : VisualElement
        {
            e.style.borderTopWidth = width;
            e.style.borderRightWidth = width;
            e.style.borderBottomWidth = width;
            e.style.borderLeftWidth = width;
            e.style.borderTopColor = color;
            e.style.borderRightColor = color;
            e.style.borderBottomColor = color;
            e.style.borderLeftColor = color;
            return e;
        }

        public static T BorderTop<T>(this T e, float width, Color color) where T : VisualElement { e.style.borderTopWidth = width; e.style.borderTopColor = color; return e; }
        public static T BorderBottom<T>(this T e, float width, Color color) where T : VisualElement { e.style.borderBottomWidth = width; e.style.borderBottomColor = color; return e; }
        public static T BorderLeft<T>(this T e, float width, Color color) where T : VisualElement { e.style.borderLeftWidth = width; e.style.borderLeftColor = color; return e; }
        public static T BorderRight<T>(this T e, float width, Color color) where T : VisualElement { e.style.borderRightWidth = width; e.style.borderRightColor = color; return e; }

        public static T Opacity<T>(this T e, float opacity) where T : VisualElement { e.style.opacity = opacity; return e; }
        public static T Clip<T>(this T e) where T : VisualElement { e.style.overflow = Overflow.Hidden; return e; }
        public static T NoPick<T>(this T e) where T : VisualElement { e.pickingMode = PickingMode.Ignore; return e; }

        // ---- transform -----------------------------------------------------------------------------

        public static T Rotate<T>(this T e, float degrees) where T : VisualElement { e.style.rotate = new Rotate(new Angle(degrees, AngleUnit.Degree)); return e; }
        public static T ScaleBy<T>(this T e, float scaleX, float scaleY) where T : VisualElement { e.style.scale = new Scale(new Vector2(scaleX, scaleY)); return e; }
        public static T ScaleBy<T>(this T e, float scale) where T : VisualElement => e.ScaleBy(scale, scale);
        public static T Translate<T>(this T e, float x, float y) where T : VisualElement { e.style.translate = new Translate(x, y); return e; }
        public static T OriginCenter<T>(this T e) where T : VisualElement { e.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50)); return e; }
        public static T OriginBottomCenter<T>(this T e) where T : VisualElement { e.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100)); return e; }

        // ---- misc ----------------------------------------------------------------------------------

        /// <summary>Replaces a label's text without touching its style.</summary>
        public static Label SetText(this Label label, string text)
        {
            if (label.text != text) label.text = text;
            return label;
        }

        public static void Show(this VisualElement e, bool visible)
        {
            e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
