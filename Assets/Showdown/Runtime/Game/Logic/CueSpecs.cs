#nullable enable
using System;
using System.Collections.Generic;

namespace Showdown.Game
{
    /// <summary>What the phone's vibration motor is asked to say (<c>HapticKind</c> in src/feedback/cues.ts).</summary>
    public enum HapticKind
    {
        Selection,
        Light,
        Medium,
        Heavy,
        Success,
        Warning,
        Error,
    }

    /// <summary>
    /// A cue pairs a sound with the haptic that belongs to it, because the two are one event to the player — the
    /// card snaps and the phone answers. <see cref="Volume"/> is 0–1, set so no cue is startling next to the one
    /// before it.
    /// </summary>
    public sealed class CueSpec
    {
        public CueSpec(string resourceName, float volume, HapticKind? haptic)
        {
            ResourceName = resourceName;
            Volume = volume;
            Haptic = haptic;
        }

        /// <summary>The clip's path under <c>Resources/Sounds</c>, without extension.</summary>
        public string ResourceName { get; }

        public float Volume { get; }

        public HapticKind? Haptic { get; }
    }

    /// <summary>
    /// The app's feedback vocabulary in one table: volumes are balanced here rather than in the files so the mix
    /// can be tuned by reading a single column. Mirrors <c>cues</c> in src/feedback/cues.ts.
    /// </summary>
    public static class CueSpecs
    {
        private static readonly Dictionary<Cue, CueSpec> Table = new Dictionary<Cue, CueSpec>
        {
            // A card sliding off the deck. Fires once per card, so it stays soft.
            { Cue.Deal, new CueSpec("deal", 0.50f, HapticKind.Light) },
            // A card turning over — crisper than the deal, still chrome-quiet.
            { Cue.Flip, new CueSpec("flip", 0.40f, HapticKind.Selection) },
            // A chip landing on the felt — antes and the bots' raises.
            { Cue.Chip, new CueSpec("chip", 0.45f, HapticKind.Light) },
            // The player's own raise: two chips, so committing reads bigger than anteing.
            { Cue.Raise, new CueSpec("raise", 0.60f, HapticKind.Medium) },
            // Cards pushed away — a dull knuckle knock, deliberately unresonant.
            { Cue.Fold, new CueSpec("fold", 0.50f, HapticKind.Medium) },
            // The dealer's hole cards turning — the beat the whole hand waits for.
            { Cue.Reveal, new CueSpec("reveal", 0.60f, HapticKind.Medium) },
            { Cue.Win, new CueSpec("win", 0.65f, HapticKind.Success) },
            // Flush or better earns the arpeggio instead of a louder version of the same.
            { Cue.WinBig, new CueSpec("winBig", 0.70f, HapticKind.Success) },
            { Cue.Lose, new CueSpec("lose", 0.60f, HapticKind.Warning) },
            // A push or a no-qualify: one soft note — nothing happened, and it sounds like it.
            { Cue.Push, new CueSpec("push", 0.40f, HapticKind.Light) },
            // The stack is gone. Low and slow, short of a cadence.
            { Cue.Bust, new CueSpec("bust", 0.70f, HapticKind.Error) },
            // Menus and chrome. Deliberately the quietest cue in the set.
            { Cue.Tap, new CueSpec("tap", 0.28f, HapticKind.Light) },
        };

        public static CueSpec For(Cue cue) =>
            Table.TryGetValue(cue, out var spec) ? spec : throw new ArgumentOutOfRangeException(nameof(cue), cue, "No spec for this cue.");

        public static IEnumerable<Cue> All => Table.Keys;
    }
}
