using System;
using System.Collections.Generic;
using UnityEngine;

namespace Showdown.Game
{
    /// <summary>
    /// Haptics through Android's <c>Vibrator</c> and <c>VibrationEffect</c> (minSdk 26). Off Android — the Editor,
    /// desktop QA builds — every pulse is a no-op.
    ///
    /// Vibrating needs <c>android.permission.VIBRATE</c>. Unity's Android build adds it automatically when managed
    /// code references <c>Handheld.Vibrate</c>, but not for JNI calls made by name, so the last-resort fallback below
    /// is a real, reachable use of that API (see <see cref="BuzzWithoutVibrator"/>).
    /// </summary>
    public sealed class AndroidHaptics : IHaptics
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private const string VibratorService = "vibrator";
        private const int PredefinedEffectsApi = 29;
        private const int NoRepeat = -1;

        private readonly Dictionary<HapticKind, AndroidJavaObject> effects = new Dictionary<HapticKind, AndroidJavaObject>();
        private AndroidJavaObject vibrator;
        private int sdkLevel;
        private bool disabled;

        public AndroidHaptics()
        {
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    sdkLevel = version.GetStatic<int>("SDK_INT");
                }

                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    var service = activity.Call<AndroidJavaObject>("getSystemService", VibratorService);
                    vibrator = service != null && service.Call<bool>("hasVibrator") ? service : null;
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning("Showdown: vibration unavailable: " + error.Message);
                vibrator = null;
            }
        }

        public void Pulse(HapticKind kind)
        {
            if (disabled) return;
            if (vibrator == null)
            {
                BuzzWithoutVibrator(kind);
                return;
            }

            try
            {
                vibrator.Call("vibrate", EffectFor(kind));
            }
            catch (Exception error)
            {
                disabled = true;
                Debug.LogWarning("Showdown: vibration failed, haptics turned off for this session: " + error.Message);
            }
        }

        private AndroidJavaObject EffectFor(HapticKind kind)
        {
            if (effects.TryGetValue(kind, out var cached)) return cached;

            var pattern = HapticPatterns.For(kind);
            using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
            {
                var effect = sdkLevel >= PredefinedEffectsApi && pattern.PredefinedEffect.HasValue
                    ? effectClass.CallStatic<AndroidJavaObject>("createPredefined", pattern.PredefinedEffect.Value)
                    : effectClass.CallStatic<AndroidJavaObject>(
                        "createWaveform", ToArray(pattern.TimingsMs), ToArray(pattern.Amplitudes), NoRepeat);
                effects[kind] = effect;
                return effect;
            }
        }

        private static T[] ToArray<T>(IReadOnlyList<T> source)
        {
            var copy = new T[source.Count];
            for (var i = 0; i < copy.Length; i += 1) copy[i] = source[i];
            return copy;
        }

        /// <summary>
        /// No vibrator service: only the weighty outcomes are worth the platform's single long buzz. This is also the
        /// managed reference to <c>Handheld.Vibrate</c> that makes Unity declare the VIBRATE permission.
        /// </summary>
        private static void BuzzWithoutVibrator(HapticKind kind)
        {
            if (kind == HapticKind.Error || kind == HapticKind.Heavy) Handheld.Vibrate();
        }
#else
        public void Pulse(HapticKind kind)
        {
        }
#endif
    }
}
