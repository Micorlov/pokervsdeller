using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The frame the four non-table screens share: a vertical scroller holding one phone-width column
    /// (RN <c>content</c> style: padding 24, children 16 apart, capped at 560 and centred on tablets).
    /// </summary>
    public static class ScreenLayout
    {
        private const float TitleSize = 32;

        /// <summary>A scrolling page whose children are stacked in <paramref name="column"/>.</summary>
        public static ScrollView Page(out VisualElement column)
        {
            var scroll = Ui.Scroll();
            column = Ui.View().Pad(Theme.Spacing.Lg).WPct(100).MaxW(Theme.ContentMaxWidth).AlignSelf(Align.Center);
            scroll.Add(column);
            return scroll;
        }

        /// <summary>Adds children to a page column with the screens' standard 16px rhythm.</summary>
        public static void Stack(VisualElement column, params VisualElement[] children)
        {
            Ui.AddAll(column, Theme.Spacing.Md, children);
        }

        /// <summary>The screen title ("How to play", "Settings", "Your record") in the table names' italic serif.</summary>
        public static Label Title(string text)
        {
            var title = Ui.Serif(text, TitleSize + 6f, Theme.Colors.Cream, TextAnchor.MiddleLeft);
            title.style.whiteSpace = WhiteSpace.Normal;
            return title;
        }

        /// <summary>Calls <paramref name="onAttach"/> / <paramref name="onDetach"/> as the screen enters and leaves the panel, so a screen never leaks a store subscription.</summary>
        public static void WhileAttached(VisualElement screen, Action onAttach, Action onDetach)
        {
            screen.RegisterCallback<AttachToPanelEvent>(_ => onAttach());
            screen.RegisterCallback<DetachFromPanelEvent>(_ => onDetach());
        }
    }
}
