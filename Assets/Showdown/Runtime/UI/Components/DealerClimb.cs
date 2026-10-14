using System.Collections.Generic;
using Showdown.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The house's side of the table: five overlapping slots the dealer fills one card at a time, every card face
    /// up, with a dark "The House" box beside them saying how far the climb has got (and, once it is done, what it
    /// made). The dashed empty slots make the climb legible at a glance, and the threat meter inside the box reads
    /// the partial hand honestly. Screen-reader label for the meter: "House threat N of 5".
    /// </summary>
    public sealed class DealerClimb : VisualElement
    {
        private const float CardScale = 0.85f;
        /// <summary>How much of each card the next one leaves showing — the whole index and then some.</summary>
        private const float SlotStep = 32f;
        private const int SlotCount = 5;
        private const int MeterSegments = 5;
        private const int HotSegments = 4;

        private readonly VisualElement[] slotHolders = new VisualElement[SlotCount];
        private readonly string[] slotCardIds = new string[SlotCount];
        private readonly VisualElement[] meterSegments = new VisualElement[MeterSegments];
        private readonly Label houseValue;
        private readonly VisualElement meter;

        public DealerClimb()
        {
            style.flexShrink = 0;
            style.alignItems = Align.Center;

            var row = Ui.View().Row().AlignItems(Align.Center);
            row.Add(BuildCards());
            houseValue = Ui.Mono(string.Empty, 16, Theme.Colors.Text);
            meter = BuildMeter();
            row.Add(BuildHouseBox(houseValue, meter));
            Add(row);
        }

        /// <param name="cards">The dealer's cards so far, all face up, oldest first.</param>
        /// <param name="rank">The dealer's finished hand, once all five cards are out; otherwise null.</param>
        /// <param name="showMeter">False once the climb is over — a finished hand has nothing left to threaten.</param>
        public void Update(IReadOnlyList<Card> cards, HandRank rank, bool showMeter)
        {
            for (int i = 0; i < SlotCount; i++) SetSlot(i, i < cards.Count ? cards[i] : null);
            houseValue.SetText(ValueText(cards.Count, rank));

            meter.Show(showMeter);
            int lit = Mathf.FloorToInt((float)Threat.PartialThreat(cards) * MeterSegments + 0.5f);
            for (int i = 0; i < MeterSegments; i++) meterSegments[i].style.backgroundColor = SegmentColor(i, lit);
        }

        private static string ValueText(int dealt, HandRank rank)
        {
            if (rank != null) return HandRanks.HandName(rank);
            return dealt == 0 ? "waiting" : $"{dealt} of {SlotCount}";
        }

        private static Color SegmentColor(int index, int lit)
        {
            if (index >= lit) return Theme.Colors.Track;
            return lit >= HotSegments ? Theme.Colors.Red : Theme.Colors.TrackFill;
        }

        private VisualElement BuildCards()
        {
            float width = PlayingCard.CardWidth * CardScale;
            float height = PlayingCard.CardHeight * CardScale;
            var row = Ui.View().Size(width + SlotStep * (SlotCount - 1), height);
            for (int i = 0; i < SlotCount; i++)
            {
                var holder = Ui.View().Size(width, height).Abs(0, null, null, i * SlotStep);
                slotHolders[i] = holder;
                row.Add(holder);
            }
            return row;
        }

        private static VisualElement BuildHouseBox(Label value, VisualElement meter)
        {
            var box = Ui.View().Pad(7, 12).Radius(12).Bg(Theme.Rgba(10, 20, 15, 0.82f)).Border(1, Theme.Rgba(255, 255, 255, 0.16f))
                .MarginLeft(12);
            box.Add(Ui.Text("The House", 14, Theme.Colors.TextMuted, false, TextAnchor.MiddleLeft, 0f, false));
            value.style.marginTop = 2;
            box.Add(value);
            box.Add(meter);
            return box;
        }

        private VisualElement BuildMeter()
        {
            var meterRow = Ui.View().MarginTop(6);
            meterRow.Add(Ui.Caps("Threat", 14, Theme.Colors.FeltInk, TextAnchor.MiddleLeft, 1.5f));

            var track = Ui.View().Row().MarginTop(3);
            for (int i = 0; i < MeterSegments; i++)
            {
                var segment = Ui.View().Size(16, 7).Radius(3.5f).Bg(Theme.Colors.Track);
                if (i > 0) segment.style.marginLeft = 3;
                meterSegments[i] = segment;
                track.Add(segment);
            }
            meterRow.Add(track);
            return meterRow;
        }

        /// <summary>Puts a card in a slot, or an empty frame when there is none; an unchanged card is left alone so it never re-enters.</summary>
        private void SetSlot(int index, Card card)
        {
            string id = card?.Id;
            if (slotCardIds[index] == id && slotHolders[index].childCount > 0) return;

            slotCardIds[index] = id;
            var holder = slotHolders[index];
            holder.Clear();
            if (card != null)
            {
                holder.Add(new PlayingCard(card, true, false, CardScale, true, 0f));
                return;
            }

            // Only the last empty slot is drawn whole; the ones before it are cut to the strip the next card would
            // leave showing, so the outlines stack like cards instead of crossing each other.
            float width = index == SlotCount - 1 ? PlayingCard.CardWidth * CardScale : SlotStep - 4f;
            float height = PlayingCard.CardHeight * CardScale;
            holder.Add(DashedFrame.Create(width, height, Theme.Radii.Xs, 1.5f, Theme.Rgba(233, 220, 180, 0.35f)));
        }
    }
}
