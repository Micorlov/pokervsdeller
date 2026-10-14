using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The on/off switch of the settings rows (RN <c>Switch</c>): a dark track that turns action green, a white thumb
    /// that slides across. The thumb moves with a quick tween, or snaps under reduced motion.
    /// </summary>
    public sealed class ToggleSwitch : VisualElement
    {
        private const float TrackWidth = 52;
        private const float TrackHeight = 32;
        private const float ThumbSize = 24;
        private const float ThumbInset = (TrackHeight - ThumbSize) / 2f;
        private const float ThumbTravel = TrackWidth - ThumbSize - ThumbInset * 2f;
        // The hit area is taller than the track: a 54pt touch target, like the rest of the table.
        private const float HitHeight = Theme.MinTouchTarget;

        private readonly VisualElement track;
        private readonly VisualElement thumb;
        private readonly Action<bool> onChange;
        private TweenHandle slide;
        private bool value;
        private float shown;

        public ToggleSwitch(bool value, Action<bool> onChange)
        {
            this.value = value;
            this.onChange = onChange;

            style.flexShrink = 0;
            this.Size(TrackWidth + Theme.Spacing.Sm * 2f, HitHeight).Center();

            track = Ui.View().Size(TrackWidth, TrackHeight).Radius(TrackHeight / 2f);
            thumb = Ui.View().Size(ThumbSize, ThumbSize).Radius(ThumbSize / 2f).Bg(Theme.Colors.Text)
                .Abs(top: ThumbInset, left: ThumbInset);
            track.Add(thumb);
            Add(track);

            this.OnTap(() => onChange?.Invoke(!this.value));
            Paint(value ? 1f : 0f);
        }

        public bool Value => value;

        /// <summary>Moves the switch to match the stored setting; the player's own tap goes through the owner.</summary>
        public void SetValue(bool next)
        {
            if (next == value) return;
            value = next;
            slide?.Cancel();
            float target = next ? 1f : 0f;
            if (Motion.Reduced)
            {
                Paint(target);
                return;
            }
            float from = shown;
            slide = Anim.Run(Motion.Quick, t => Paint(Mathf.Lerp(from, target, t)), Ease.OutQuad, 0f, null, this);
        }

        /// <summary>Paints the switch at <paramref name="position"/>: 0 is off, 1 is on, in between only mid-slide.</summary>
        private void Paint(float position)
        {
            shown = position;
            track.style.backgroundColor = Color.Lerp(Theme.Colors.Track, Theme.Colors.Green, position);
            thumb.style.translate = new Translate(ThumbTravel * position, 0);
        }
    }
}
