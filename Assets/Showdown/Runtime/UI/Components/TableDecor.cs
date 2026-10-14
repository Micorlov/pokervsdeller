using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The props that make the felt read as a casino table rather than a green rectangle: the ante placard, the
    /// dealer's chip tray, the card shoe, the lamp glow in the walnut header and the chip stacks in front of the
    /// player. None of them carry state the player has to read, so none of them take touches.
    /// </summary>
    public static class TableDecor
    {
        private static readonly Color[] TrayColours =
        {
            Theme.Hex("#F4F0E6"), Theme.Hex("#C0392B"), Theme.Hex("#C0392B"), Theme.Hex("#1E8449"), Theme.Hex("#1E8449"),
            Theme.Hex("#1F2A44"), Theme.Hex("#1F2A44"), Theme.Hex("#6C3483"), Theme.Hex("#D4AC0D"), Theme.Hex("#D4AC0D"),
        };

        private static readonly Color[] StackColours = { Theme.Hex("#C0392B"), Theme.Hex("#1E8449"), Theme.Hex("#1F2A44") };
        private static readonly int[] StackChips = { 6, 4, 5 };

        /// <summary>A small cream card standing on the felt; its two lines are set by <see cref="SetPlacard"/>.</summary>
        public static VisualElement Placard()
        {
            var placard = Ui.View().NoPick().Pad(3, 7).Radius(3).Bg(Theme.Colors.PanelCream);
            placard.Add(SoftShadow.PillLiftFit(3));
            var first = Ui.Mono(string.Empty, 12, Theme.Colors.InkText);
            var second = Ui.Mono(string.Empty, 12, Theme.Colors.InkText);
            placard.Add(first);
            placard.Add(second);
            placard.userData = new[] { first, second };
            return placard;
        }

        public static void SetPlacard(VisualElement placard, string first, string second)
        {
            var lines = (Label[])placard.userData;
            lines[0].SetText(first);
            lines[1].SetText(second);
        }

        /// <summary>The dealer's rack: a dark tray of chip columns in the house colours.</summary>
        public static VisualElement ChipTray()
        {
            var tray = Ui.View().NoPick().Row().Pad(3, 8).Radius(4).BgGradient(Theme.Hex("#1A1A1A"), Theme.Hex("#2E2E2E"));
            tray.Add(SoftShadow.PillLiftFit(4));
            for (int i = 0; i < TrayColours.Length; i++)
            {
                var column = Ui.View().Size(9, 22).Radius(2).BgGradient(TrayColours[i], Theme.WithAlpha(TrayColours[i], 0.8f));
                if (i > 0) column.style.marginLeft = 2;
                tray.Add(column);
            }
            return tray;
        }

        /// <summary>The card shoe: a black box with the next card's white edge showing.</summary>
        public static VisualElement Shoe()
        {
            var shoe = Ui.View().NoPick().Size(50, 30).Radius(5).BgGradient(Theme.Hex("#2A2A2A"), Theme.Hex("#111111"));
            shoe.Add(SoftShadow.PillLiftFit(5));
            shoe.Add(Ui.View().Abs(11, 7, null, 7).H(7).Radius(1).Bg(Theme.Hex("#F3EFE6")));
            return shoe;
        }

        /// <summary>Three short chip stacks, the player's chips sitting on the rail.</summary>
        public static VisualElement ChipStacks()
        {
            var row = Ui.View().NoPick().Row().AlignItems(Align.FlexEnd);
            for (int s = 0; s < StackColours.Length; s++)
            {
                var stack = Ui.View().W(18);
                if (s > 0) stack.style.marginLeft = 4;
                for (int c = 0; c < StackChips[s]; c++)
                {
                    var chip = Ui.View().H(4).Radius(1).Bg(StackColours[s]).BorderBottom(1, Theme.Rgba(255, 255, 255, 0.85f));
                    stack.Add(chip);
                }
                row.Add(stack);
            }
            return row;
        }

        /// <summary>A soft amber glow, the out-of-focus lamps behind the walnut header.</summary>
        public static VisualElement Bokeh(float centreX, float centreY, float radius)
        {
            var glow = Gfx.Radial("bokeh", 0.5f, 0.5f, 0.5f, 0.5f,
                new Gfx.Stop(0f, Theme.Rgba(255, 190, 110, 0.5f)),
                new Gfx.Stop(0.7f, Theme.Rgba(255, 170, 80, 0.08f)),
                new Gfx.Stop(1f, Theme.Rgba(255, 170, 80, 0f)));
            return Ui.View().NoPick().Abs(centreY - radius, null, null, centreX - radius).Size(radius * 2f, radius * 2f).BgImage(glow);
        }
    }
}
