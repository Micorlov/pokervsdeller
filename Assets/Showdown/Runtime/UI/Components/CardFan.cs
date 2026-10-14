using System;
using System.Collections.Generic;
using Showdown.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>What a tap on a fanned card means (RN's <c>selectableMode</c>).</summary>
    public enum FanSelectMode
    {
        None,
        /// <summary>Pick the kicker.</summary>
        Kicker,
        /// <summary>Pick a forge trade-in; the kicker card is off-limits and dims.</summary>
        Discard,
    }

    /// <summary>
    /// Five cards in a shallow fan, entering one after another the way a dealt hand arrives (port of
    /// CardFan.tsx). Stagger lives here at the row level, not in each card; so do the kicker star and the
    /// tap-to-pick modes, which are row concerns.
    ///
    /// Every card but the last is covered on its right, which leaves its index — the one corner that matters —
    /// showing. <c>stripWidth</c> sets how much of that corner survives, so a cramped seat can trade fan width
    /// for a legible rank.
    ///
    /// Cards are keyed by id: <see cref="Update"/> reuses a slot whose card is unchanged, so a card never
    /// replays its deal entrance when something else about the table changes. Slots are absolutely
    /// placed (the container's size is known from the card size) because UI Toolkit has no z-index; draw order
    /// is child order, which is sorted so the kicker and a freshly traded card ride above the rest.
    /// </summary>
    public sealed class CardFan : VisualElement
    {
        /// <summary>How far each card tips from its neighbour, in degrees.</summary>
        public const float DefaultStepDegrees = 3.5f;
        /// <summary>The outer cards drop slightly, so the row reads as held, not laid flat.</summary>
        private const float DropPerStep = 3f;
        private const float DefaultOverlap = 14f;
        /// <summary>How far the marked card rises out of the fan, in points.</summary>
        private const float KickerLift = 6f;
        private const float StarSize = 30f;
        private const float StarGlyphSize = 17f;

        private sealed class Slot
        {
            public string CardId;
            public VisualElement Root;
            public PlayingCard Card;
            public VisualElement FreshRing;
            public VisualElement Star;
        }

        private readonly List<Slot> slots = new List<Slot>();
        private readonly float scale;
        private readonly float overlap;
        private readonly float stepDegrees;
        private readonly float? rankSize;

        private FanSelectMode mode;
        private int? kickerIndex;
        private Action<int> onSelect;

        /// <param name="stripWidth">How much of each covered card stays visible; null takes the roomier default overlap.</param>
        public CardFan(float scale = 1f, float? stripWidth = null, float stepDegrees = DefaultStepDegrees, float? rankSize = null)
        {
            this.scale = scale;
            this.stepDegrees = stepDegrees;
            this.rankSize = rankSize;
            overlap = stripWidth.HasValue ? PlayingCard.CardWidth * scale - stripWidth.Value : DefaultOverlap * scale;
            style.flexShrink = 0;
        }

        public void Update(IReadOnlyList<Card> cards, bool faceUp, bool dimmed, bool animateIn, int? kickerIndex,
            int? freshIndex, FanSelectMode mode, Action<int> onSelect)
        {
            this.mode = mode;
            this.kickerIndex = kickerIndex;
            this.onSelect = onSelect;

            SyncSlots(cards, animateIn);
            float step = PlayingCard.CardWidth * scale - overlap;
            // An empty fan takes no room at all, as a row with no children does in RN.
            style.width = cards.Count == 0 ? 0f : PlayingCard.CardWidth * scale + (cards.Count - 1) * step;
            style.height = cards.Count == 0 ? 0f : PlayingCard.CardHeight * scale;

            float middle = (cards.Count - 1) / 2f;
            for (int i = 0; i < slots.Count; i++)
            {
                bool isKicker = i == kickerIndex;
                bool lockedOut = mode == FanSelectMode.Discard && isKicker;
                var slot = slots[i];
                slot.Card.SetFaceUp(faceUp);
                slot.Card.SetDimmed(dimmed || lockedOut);
                slot.FreshRing.Show(i == freshIndex);
                slot.Star.Show(isKicker);
                PlaceSlot(slot, i, step, i - middle, isKicker);
            }
            SortByDepth(freshIndex);
        }

        private void SyncSlots(IReadOnlyList<Card> cards, bool animateIn)
        {
            while (slots.Count > cards.Count)
            {
                slots[slots.Count - 1].Root.RemoveFromHierarchy();
                slots.RemoveAt(slots.Count - 1);
            }

            for (int i = 0; i < cards.Count; i++)
            {
                if (i < slots.Count && slots[i].CardId == cards[i].Id) continue;
                var slot = BuildSlot(cards[i], i, animateIn);
                if (i < slots.Count)
                {
                    slots[i].Root.RemoveFromHierarchy();
                    slots[i] = slot;
                }
                else
                {
                    slots.Add(slot);
                }
                Add(slot.Root);
            }
        }

        private Slot BuildSlot(Card card, int index, bool animateIn)
        {
            float width = PlayingCard.CardWidth * scale;
            float height = PlayingCard.CardHeight * scale;
            var playingCard = new PlayingCard(card, true, false, scale, animateIn, index * Motion.Stagger, null, rankSize);

            var root = Ui.View().Size(width, height);
            root.style.position = Position.Absolute;
            root.style.top = 0;
            root.Add(playingCard);

            // A traded card is replaced in its own slot, so the only thing that changes is the face. This ring
            // is what tells the player the trade actually landed.
            var ring = Ui.View().NoPick().Abs(-3, -3, -3, -3).Radius(12).Border(3, Theme.Colors.Green);
            root.Add(ring);

            var star = Ui.View().NoPick().Size(StarSize, StarSize).Radius(StarSize / 2f).Bg(Theme.Colors.Gold).Center()
                .Abs(-10, -6);
            star.Add(Ui.GlyphView(Glyph.Star, StarGlyphSize, Theme.Colors.InkText));
            root.Add(star);

            // Tapping is decided when it happens, not when the slot was built: the mode changes under a live card.
            root.OnTap(() => HandleTap(index));
            return new Slot { CardId = card.Id, Root = root, Card = playingCard, FreshRing = ring, Star = star };
        }

        private void HandleTap(int index)
        {
            bool tappable = mode != FanSelectMode.None && !(mode == FanSelectMode.Discard && index == kickerIndex);
            if (tappable) onSelect?.Invoke(index);
        }

        private void PlaceSlot(Slot slot, int index, float step, float offset, bool isKicker)
        {
            slot.Root.style.left = index * step;
            slot.Root.Rotate(offset * stepDegrees);
            slot.Root.Translate(0f, Mathf.Abs(offset) * DropPerStep * scale - (isKicker ? KickerLift : 0f));
        }

        /// <summary>
        /// The star sits on the top-right corner — exactly where the next card overlaps — so the marked card
        /// has to ride above the rest of the fan or its badge is covered on every card but the last. A card
        /// fresh off the stock comes forward for the same reason.
        /// </summary>
        private void SortByDepth(int? freshIndex)
        {
            var order = new List<int>(slots.Count);
            for (int i = 0; i < slots.Count; i++) order.Add(i);
            order.Sort((a, b) => Depth(a, freshIndex).CompareTo(Depth(b, freshIndex)));
            if (IsPaintedInOrder(order)) return;
            foreach (int index in order) slots[index].Root.BringToFront();
        }

        private bool IsPaintedInOrder(List<int> order)
        {
            for (int i = 0; i < order.Count; i++)
            {
                if (this[i] != slots[order[i]].Root) return false;
            }
            return childCount == order.Count;
        }

        private int Depth(int index, int? freshIndex)
        {
            if (index == freshIndex) return slots.Count + 1;
            if (index == kickerIndex) return slots.Count;
            return index;
        }
    }
}
