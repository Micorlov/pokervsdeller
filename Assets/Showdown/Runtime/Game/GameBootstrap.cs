using System;
using Showdown.UI;
using UnityEngine;

namespace Showdown.Game
{
    /// <summary>
    /// Builds the game layer's services and publishes them through <see cref="AppServices"/>: storage (after the
    /// one-time legacy import), settings, the daily bonus, the clock, sound and haptics, and the table controller.
    /// </summary>
    public static class GameBootstrap
    {
        public static void Create(UiHost host)
        {
            var storage = new PlayerPrefsStore();
            LegacyStorageImport.RunOnce(storage);

            var settings = new SettingsStore(storage);
            var dailyBonus = new DailyBonusStore(storage, () => DateTime.Now);
            var scheduler = new UnityScheduler();

            EnsureAudioListener(host.gameObject);
            var feedback = new AudioFeedback(host.transform, settings, new AndroidHaptics());
            var table = new TableController(new BankrollStore(storage), new StatsStore(storage), feedback, scheduler);

            AppServices.Settings = settings;
            AppServices.DailyBonus = dailyBonus;
            AppServices.Feedback = feedback;
            AppServices.Table = table;

            KeepReducedMotionInSync(settings);
            host.gameObject.AddComponent<GameLifetime>().Own(table, scheduler);
        }

        /// <summary>AudioSources are silent without a listener, and this scene has no camera to carry one.</summary>
        private static void EnsureAudioListener(GameObject host)
        {
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() != null) return;
            host.AddComponent<AudioListener>();
        }

        /// <summary>The motion kit reads one static flag; it follows the setting from the first frame and on every change.</summary>
        private static void KeepReducedMotionInSync(ISettingsStore settings)
        {
            UI.Motion.Reduced = settings.Current.ReducedMotion;
            settings.Changed += () => UI.Motion.Reduced = settings.Current.ReducedMotion;
        }
    }
}
