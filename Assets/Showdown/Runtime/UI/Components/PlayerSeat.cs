using Showdown.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// A bot's place on the arc of the table: a dark pill with their name and what they just did, and their hand
    /// fanned small beneath it. Thinking lights the pill gold; a bail dims the
    /// cards, crosses them and says so in rose; at the showdown the pill names the hand, tinted by how it paid.
    ///
    /// The hand is read across the table, never held: cards overlap hard so what survives is a row of ranks.
    /// Screen-reader summary: name, last move or made hand, net.
    /// </summary>
    public sealed class PlayerSeat : VisualElement
    {
        private const float CardScale = 0.62f;
        private const float StripWidth = 18f;
        private const float RankSize = 16f;
        private const float FanStepDegrees = 5f;
        /// <summary>Half the pill's height (5 + 20 + 5 padding, 2 border): UI Toolkit would turn a 999 radius into an ellipse.</summary>
        private const float PillRadius = 16f;

        private readonly VisualElement pill;
        private readonly Label nameLabel;
        private readonly Label statusLabel;
        private readonly CardFan fan;

        public PlayerSeat()
        {
            style.flexShrink = 1;
            style.maxWidth = Length.Percent(49);
            style.alignItems = Align.Center;

            nameLabel = Ui.Text(string.Empty, 17, Theme.Colors.Text, true, TextAnchor.MiddleLeft, 0f, false);
            statusLabel = Ui.Text(string.Empty, 15, Theme.Colors.TextMuted, false, TextAnchor.MiddleLeft, 0f, false);
            statusLabel.style.marginLeft = 7;
            // On a narrow phone two long hand names cannot both fit: the status gives way with an ellipsis, the name never does.
            statusLabel.style.flexShrink = 1;
            statusLabel.style.overflow = Overflow.Hidden;
            statusLabel.style.textOverflow = TextOverflow.Ellipsis;
            pill = Ui.View().Row().AlignItems(Align.Center).Pad(5, 12).Radius(PillRadius).Shrink(1);
            pill.style.maxWidth = Length.Percent(100);
            pill.Add(SoftShadow.PillLiftFit(PillRadius));
            pill.Add(nameLabel);
            pill.Add(statusLabel);
            Add(pill);

            fan = new CardFan(CardScale, StripWidth, FanStepDegrees, RankSize);
            fan.style.marginTop = 6;
            Add(fan);
        }

        /// <param name="settled">True once the showdown is paid and settlements exist.</param>
        /// <param name="showdown">True from the reveal on, when every surviving hand is named.</param>
        public void Update(ShowdownSeatState seat, bool settled, bool showdown, bool isThinking)
        {
            bool bailed = seat.Status == SeatStatus.Bailed;
            nameLabel.SetText(seat.Name);
            nameLabel.style.color = seat.Pressured ? Theme.Colors.Gold : Theme.Colors.Text;

            fan.Show(seat.Cards.Count == HandRanks.HandSize);
            fan.Update(seat.Cards, true, bailed, false, null, null, FanSelectMode.None, null);

            var (status, ink, gold) = StatusFor(seat, settled, showdown, isThinking);
            statusLabel.SetText(status);
            statusLabel.style.color = ink;
            statusLabel.Show(!string.IsNullOrEmpty(status));
            pill.Bg(gold ? Theme.Colors.GoldGlass : Theme.Colors.FeltLabel)
                .Border(gold ? 1.5f : 1f, gold ? Theme.Colors.Gold : Theme.Rgba(255, 255, 255, 0.14f));
        }

        private static (string text, Color ink, bool gold) StatusFor(ShowdownSeatState seat, bool settled, bool showdown, bool isThinking)
        {
            if (isThinking) return ("thinking…", Theme.Colors.Gold, true);
            if (seat.Status == SeatStatus.Bailed) return ("bailed", Theme.Colors.Rose, false);
            if (showdown && seat.Cards.Count == HandRanks.HandSize)
            {
                int net = settled ? seat.Settlement?.Net ?? 0 : 0;
                var ink = net > 0 ? Theme.Colors.Mint : net < 0 ? Theme.Colors.Rose : Theme.Colors.TextMuted;
                return (HandRanks.HandName(HandRanks.EvaluateHand(seat.Cards)), ink, false);
            }
            switch (seat.LastMove?.Kind)
            {
                case ShowdownMoveKind.Forge: return ("forged", Theme.Colors.Mint, false);
                case ShowdownMoveKind.Hold: return ("holds", Theme.Colors.TextMuted, false);
                default: return (seat.KickerIndex.HasValue ? "ready" : string.Empty, Theme.Colors.TextMuted, false);
            }
        }
    }
}
