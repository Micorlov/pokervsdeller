#nullable enable
using System;
using System.Collections.Generic;

namespace Showdown.Game
{
    /// <summary>
    /// How one <see cref="HapticKind"/> is played on Android's <c>Vibrator</c>. Where the device supports it (API 29+)
    /// the platform's own predefined effect is used, because the manufacturer tuned it for the actuator; otherwise
    /// the waveform plays — alternating off/on durations in milliseconds starting with an off period, each with a
    /// 0–255 amplitude (zero for the off periods), the shape <c>VibrationEffect.createWaveform</c> takes.
    /// </summary>
    public sealed class HapticPattern
    {
        // VibrationEffect predefined effect ids.
        public const int EffectClick = 0;
        public const int EffectDoubleClick = 1;
        public const int EffectTick = 2;
        public const int EffectHeavyClick = 5;

        public HapticPattern(int? predefinedEffect, long[] timingsMs, int[] amplitudes)
        {
            PredefinedEffect = predefinedEffect;
            TimingsMs = timingsMs;
            Amplitudes = amplitudes;
        }

        public int? PredefinedEffect { get; }

        public IReadOnlyList<long> TimingsMs { get; }

        public IReadOnlyList<int> Amplitudes { get; }
    }

    public static class HapticPatterns
    {
        private static readonly Dictionary<HapticKind, HapticPattern> Table = new Dictionary<HapticKind, HapticPattern>
        {
            { HapticKind.Selection, new HapticPattern(HapticPattern.EffectTick, new long[] { 0, 6 }, new[] { 0, 40 }) },
            { HapticKind.Light, new HapticPattern(HapticPattern.EffectTick, new long[] { 0, 10 }, new[] { 0, 80 }) },
            { HapticKind.Medium, new HapticPattern(HapticPattern.EffectClick, new long[] { 0, 18 }, new[] { 0, 140 }) },
            { HapticKind.Heavy, new HapticPattern(HapticPattern.EffectHeavyClick, new long[] { 0, 30 }, new[] { 0, 255 }) },
            // Two rising taps: a small "yes".
            { HapticKind.Success, new HapticPattern(HapticPattern.EffectDoubleClick, new long[] { 0, 12, 60, 22 }, new[] { 0, 110, 0, 200 }) },
            // Two even, firmer taps: "careful".
            { HapticKind.Warning, new HapticPattern(null, new long[] { 0, 28, 70, 28 }, new[] { 0, 170, 0, 170 }) },
            // Three falling beats: "no".
            { HapticKind.Error, new HapticPattern(null, new long[] { 0, 40, 60, 40, 60, 70 }, new[] { 0, 230, 0, 230, 0, 255 }) },
        };

        public static HapticPattern For(HapticKind kind) =>
            Table.TryGetValue(kind, out var pattern) ? pattern : throw new ArgumentOutOfRangeException(nameof(kind), kind, "No pattern for this haptic.");
    }
}
