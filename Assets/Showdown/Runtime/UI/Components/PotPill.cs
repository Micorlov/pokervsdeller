using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The two prizes in one pill on the felt: the players-only table pot and the seat's own stake against the
    /// house. In the pressure round the player is asked to bet on exactly these numbers, so both always show,
    /// in mono gold, and a doubled stake wears a "3:1" tag. The table pot counts up instead of swapping digits.
    /// Screen-reader labels would read "Table pot N chips" and "Your stake against the house: N chips".
    /// </summary>
    public sealed class PotPill : VisualElement
    {
        private const float ChipSize = 26f;
        /// <summary>Half the pill's height (6 + 26 + 6 padding, 3 border): UI Toolkit would turn a 999 radius into an ellipse.</summary>
        private const float PillRadius = 20.5f;

        private readonly Label potValue;
        private readonly Label stakeValue;
        private readonly Label pressurePip;
        private readonly VisualElement chipSlot;
        private int shownPot;
        private int targetPot;
        private int shownAnte = -1;
        private bool hasValue;
        private TweenHandle countTween;

        public PotPill()
        {
            style.flexShrink = 0;
            style.alignSelf = Align.Center;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            this.Pad(6, 16, 6, 8).Radius(PillRadius).Bg(Theme.Colors.FeltLabel).Border(1.5f, Theme.Rgba(233, 200, 120, 0.55f));

            chipSlot = Ui.View().Size(ChipSize, ChipSize);
            potValue = Value();
            stakeValue = Value();
            pressurePip = TableStyle.Line("3:1", 13, Theme.Colors.InkText, true, TextAnchor.MiddleCenter, 0.5f, 1.1f);
            pressurePip.Bg(Theme.Colors.Gold).Radius(8).Pad(2, 7).Clip();
            pressurePip.style.height = StyleKeyword.Auto;

            var divider = Ui.View().Size(1, 18).Bg(Theme.Rgba(233, 200, 120, 0.4f));
            Ui.AddAll(this, 9, chipSlot, Caption("Table pot"), potValue, divider, Caption("vs house"), stakeValue, pressurePip);
            potValue.style.marginLeft = 6;
            stakeValue.style.marginLeft = 6;
            pressurePip.style.marginLeft = 6;
        }

        public void Update(int tablePot, int dealerStake, bool pressured, int ante)
        {
            SetPot(tablePot);
            stakeValue.SetText(dealerStake.ToString());
            pressurePip.Show(pressured);
            if (ante == shownAnte) return;
            shownAnte = ante;
            chipSlot.Clear();
            chipSlot.Add(PokerChip.Create(ante, ChipSize));
        }

        private void SetPot(int tablePot)
        {
            if (hasValue && tablePot == targetPot) return;
            targetPot = tablePot;
            countTween?.Cancel();
            if (!hasValue || Motion.Reduced)
            {
                shownPot = tablePot;
                potValue.SetText(tablePot.ToString());
                hasValue = true;
                return;
            }

            countTween = Anim.CountUp(potValue, shownPot, tablePot, value =>
            {
                shownPot = value;
                return value.ToString();
            });
            if (countTween == null) shownPot = tablePot;
        }

        private static Label Caption(string text) => Ui.Text(text, 15, Theme.Colors.TextMuted, false, TextAnchor.MiddleLeft, 0f, false);

        private static Label Value() => Ui.Mono("0", 18, Theme.Colors.Gold);
    }
}
