#nullable enable
using System.Collections.Generic;
using System.Linq;
using Showdown.Domain;

namespace Showdown.Game
{
    /// <summary>
    /// <c>{"soundOn":true,"hapticsOn":true,"reducedMotion":false,"ante":10}</c> — the shape src/state/settingsStore.ts
    /// wrote. Decoding is <c>{...defaults, ...loaded}</c>: any missing or wrongly typed field keeps its default.
    /// </summary>
    public static class SettingsCodec
    {
        public static AppSettings Decode(object? tree)
        {
            var defaults = new AppSettings();
            var ante = StoredJson.IntField(tree, "ante");
            return new AppSettings
            {
                SoundOn = StoredJson.BoolField(tree, "soundOn") ?? defaults.SoundOn,
                HapticsOn = StoredJson.BoolField(tree, "hapticsOn") ?? defaults.HapticsOn,
                ReducedMotion = StoredJson.BoolField(tree, "reducedMotion") ?? defaults.ReducedMotion,
                // An ante outside the offered stakes could never be selected, so it is as good as missing.
                Ante = ante.HasValue && Bankroll.AnteOptions.Contains(ante.Value) ? ante.Value : defaults.Ante,
            };
        }

        public static object Encode(AppSettings settings) =>
            new Dictionary<string, object?>
            {
                { "soundOn", settings.SoundOn },
                { "hapticsOn", settings.HapticsOn },
                { "reducedMotion", settings.ReducedMotion },
                { "ante", settings.Ante },
            };
    }
}
