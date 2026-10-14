using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// One place for every duration in the app (port of src/motion/timings.ts). The game is played in a
    /// quiet, unhurried way, so nothing is bouncy for its own sake: tiles settle like objects with weight,
    /// chrome moves quickly enough to feel instant, and only celebratory moments may overshoot.
    /// </summary>
    public static class Motion
    {
        /// <summary>The player's Reduced Motion setting. Every animation helper reads it, so the switch takes effect everywhere at once.</summary>
        public static bool Reduced;

        /// <summary>Chrome that must not feel laggy — presses, ticks, hint text.</summary>
        public const float Quick = 0.14f;
        /// <summary>The default for anything appearing or changing state.</summary>
        public const float Base = 0.24f;
        /// <summary>Panels and screens, where the eye needs time to follow.</summary>
        public const float Slow = 0.38f;
        /// <summary>Score count-ups and end-of-hand flourishes.</summary>
        public const float Celebrate = 0.62f;
        /// <summary>Gap between siblings in a staggered entrance.</summary>
        public const float Stagger = 0.055f;
    }

    /// <summary>Easing curves standing in for RN's springs.</summary>
    public static class Ease
    {
        public static float Linear(float t) => t;
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float InOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

        /// <summary>Overshoots then settles; <paramref name="overshoot"/> ~1.1 is a firm landing, ~2.4 a celebratory pop.</summary>
        public static Func<float, float> OutBack(float overshoot)
        {
            return t =>
            {
                float c3 = overshoot + 1f;
                return 1f + c3 * Mathf.Pow(t - 1f, 3f) + overshoot * Mathf.Pow(t - 1f, 2f);
            };
        }

        /// <summary>A tile landing on the table: firm, one small settle, no wobble.</summary>
        public static readonly Func<float, float> Settle = OutBack(1.1f);
        /// <summary>The one place overshoot is wanted — a score popping onto the screen.</summary>
        public static readonly Func<float, float> Pop = OutBack(2.4f);
        /// <summary>A finger press releasing — fast enough to track the touch.</summary>
        public static readonly Func<float, float> Press = OutBack(1.6f);
        /// <summary>Panels arriving: soft, no visible overshoot.</summary>
        public static readonly Func<float, float> Panel = OutCubic;
    }

    public sealed class TweenHandle
    {
        internal bool Finished;

        /// <summary>Stops the tween where it is; its completion callback does not run.</summary>
        public void Cancel() => Finished = true;
    }

    /// <summary>
    /// A small tween engine pumped by <see cref="UiHost"/> once a frame. Tweens bound to an element end by
    /// themselves once that element has been attached and then removed (a screen was rebuilt), so a screen
    /// never leaks a running pulse.
    /// </summary>
    public static class Anim
    {
        private sealed class Tween
        {
            public TweenHandle Handle;
            public float Delay;
            public float Duration;
            public float Elapsed;
            public bool Loop;
            public Action<float> Apply;
            public Func<float, float> Ease;
            public Action OnComplete;
            public VisualElement Owner;
            public bool OwnerWasAttached;
        }

        private static readonly List<Tween> Active = new List<Tween>();

        public static void Tick(float deltaTime)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var tween = Active[i];
                if (tween.Handle.Finished || OwnerGone(tween))
                {
                    Active.RemoveAt(i);
                    continue;
                }

                if (tween.Delay > 0f)
                {
                    tween.Delay -= deltaTime;
                    if (tween.Delay > 0f) continue;
                    deltaTime += tween.Delay; // carry the overshoot into the first frame
                    tween.Delay = 0f;
                }

                tween.Elapsed += deltaTime;
                float t = tween.Duration <= 0f ? 1f : Mathf.Clamp01(tween.Elapsed / tween.Duration);
                if (tween.Loop && tween.Elapsed >= tween.Duration)
                {
                    tween.Elapsed = tween.Duration <= 0f ? 0f : tween.Elapsed % tween.Duration;
                    t = tween.Duration <= 0f ? 0f : tween.Elapsed / tween.Duration;
                }

                if (!TryRun(() => tween.Apply(tween.Ease != null ? tween.Ease(t) : t), tween))
                {
                    Active.RemoveAt(i);
                    continue;
                }

                if (!tween.Loop && t >= 1f)
                {
                    tween.Handle.Finished = true;
                    Active.RemoveAt(i);
                    TryRun(tween.OnComplete, tween);
                }
            }
        }

        /// <summary>
        /// Runs tween code so one failing animation ends itself (and is logged) instead of throwing out of every
        /// frame's tick and freezing everything after it. Returns false if the tween was ended by an exception.
        /// </summary>
        private static bool TryRun(Action action, Tween tween)
        {
            if (action == null) return true;
            try
            {
                action();
                return true;
            }
            catch (Exception error)
            {
                tween.Handle.Finished = true;
                Debug.LogException(error);
                return false;
            }
        }

        private static bool OwnerGone(Tween tween)
        {
            if (tween.Owner == null) return false;
            if (tween.Owner.panel != null)
            {
                tween.OwnerWasAttached = true;
                return false;
            }
            return tween.OwnerWasAttached;
        }

        /// <summary>Runs <paramref name="apply"/> with an eased 0→1 over <paramref name="seconds"/>.</summary>
        public static TweenHandle Run(float seconds, Action<float> apply, Func<float, float> ease = null,
            float delay = 0f, Action onComplete = null, VisualElement owner = null)
        {
            var handle = new TweenHandle();
            Active.Add(new Tween
            {
                Handle = handle,
                Delay = delay,
                Duration = seconds,
                Apply = apply,
                Ease = ease,
                OnComplete = onComplete,
                Owner = owner,
            });
            return handle;
        }

        /// <summary>Repeats 0→1 forever (until cancelled or the owner leaves the panel).</summary>
        public static TweenHandle Loop(float seconds, Action<float> apply, VisualElement owner)
        {
            var handle = new TweenHandle();
            Active.Add(new Tween
            {
                Handle = handle,
                Duration = seconds,
                Loop = true,
                Apply = apply,
                Owner = owner,
            });
            return handle;
        }

        /// <summary>Drops every running tween — used when the whole UI is torn down.</summary>
        public static void Clear() => Active.Clear();

        // ---- recipes -------------------------------------------------------------------------------

        /// <summary>
        /// Fade-and-settle for anything arriving on screen. Under reduced motion the element still fades in —
        /// a cross-fade carries no vestibular risk — but it never travels or scales.
        /// </summary>
        public static TweenHandle Entrance(VisualElement element, float delaySeconds = 0f, float translateY = 10f,
            float scaleFrom = 1f, Func<float, float> ease = null)
        {
            ease ??= Ease.Panel;
            bool reduced = Motion.Reduced;
            element.style.opacity = 0f;
            if (!reduced)
            {
                element.style.translate = new Translate(0, translateY);
                element.style.scale = new Scale(new Vector2(scaleFrom, scaleFrom));
            }

            return Run(reduced ? Motion.Quick : Motion.Slow, t =>
            {
                element.style.opacity = Mathf.Clamp01(t);
                if (reduced) return;
                element.style.translate = new Translate(0, Mathf.LerpUnclamped(translateY, 0f, t));
                float scale = Mathf.LerpUnclamped(scaleFrom, 1f, t);
                element.style.scale = new Scale(new Vector2(scale, scale));
            }, reduced ? Ease.OutQuad : ease, delaySeconds, () =>
            {
                element.style.opacity = 1f;
                if (!reduced)
                {
                    element.style.translate = new Translate(0, 0);
                    element.style.scale = new Scale(Vector2.one);
                }
            }, element);
        }

        /// <summary>Staggered panel reveal: panel <paramref name="index"/> of a screen arrives after the ones above it.</summary>
        public static TweenHandle Reveal(VisualElement element, int index, float translateY = 12f, float scaleFrom = 1f)
        {
            return Entrance(element, index * Motion.Stagger, translateY, scaleFrom);
        }

        /// <summary>
        /// The scale-down every pressable shares, so a tile and a button answer a finger the same way.
        /// Returns a callback to feed pointer-pressed changes into (see <see cref="Ui.OnTap{T}"/>).
        /// </summary>
        public static Action<bool> PressScale(VisualElement element, float pressedScale = 0.96f)
        {
            float current = 1f;
            TweenHandle running = null;
            return pressed =>
            {
                if (Motion.Reduced) return;
                running?.Cancel();
                float from = current;
                float to = pressed ? pressedScale : 1f;
                running = Run(Motion.Quick, t =>
                {
                    current = Mathf.LerpUnclamped(from, to, t);
                    element.style.scale = new Scale(new Vector2(current, current));
                }, pressed ? Ease.OutQuad : Ease.Press, 0f, null, element);
            };
        }

        /// <summary>
        /// A slow breath for whatever currently has the turn. Never starts under reduced motion — a pulse
        /// that cannot be stopped is exactly what that setting exists to prevent. Returns null in that case.
        /// </summary>
        public static TweenHandle Pulse(VisualElement owner, Action<float> apply)
        {
            if (Motion.Reduced)
            {
                apply(0f);
                return null;
            }
            return Loop(1.8f, t =>
            {
                // Out and back: 0 → 1 → 0 across the loop.
                float wave = t < 0.5f ? Ease.InOutQuad(t * 2f) : Ease.InOutQuad(2f - t * 2f);
                apply(wave);
            }, owner);
        }

        /// <summary>Counts a score up to its new value instead of swapping the digits.</summary>
        public static TweenHandle CountUp(Label label, int from, int to, Func<int, string> format = null, float seconds = Motion.Celebrate)
        {
            format ??= value => value.ToString();
            if (from == to || Motion.Reduced)
            {
                label.text = format(to);
                return null;
            }
            label.text = format(from);
            return Run(seconds, t => label.text = format(Mathf.RoundToInt(Mathf.LerpUnclamped(from, to, t))),
                Ease.OutCubic, 0f, () => label.text = format(to), label);
        }

        /// <summary>Opacity tween.</summary>
        public static TweenHandle Fade(VisualElement element, float from, float to, float seconds = Motion.Base, float delay = 0f, Action onComplete = null)
        {
            element.style.opacity = from;
            return Run(seconds, t => element.style.opacity = Mathf.Lerp(from, to, t), Ease.OutQuad, delay, onComplete, element);
        }
    }
}
