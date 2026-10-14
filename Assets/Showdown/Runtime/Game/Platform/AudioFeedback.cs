using System;
using System.Collections.Generic;
using UnityEngine;

namespace Showdown.Game
{
    /// <summary>
    /// Turns a cue into "make that noise and that buzz" (src/feedback/FeedbackProvider.tsx). Both channels are
    /// optional, so a cue with the sound off still fires its haptic and vice versa; both settings are read at
    /// play time, so toggling them takes effect immediately.
    ///
    /// Each cue owns its own <see cref="AudioSource"/>, so a fast run of the same cue restarts the sound instead of
    /// queueing behind itself. Audio failures are deliberately non-fatal: a cue that cannot load is a missing sound
    /// effect, not a broken game, so playback degrades to silence (with one warning per cue) rather than throwing.
    /// </summary>
    public sealed class AudioFeedback : IFeedback
    {
        private const string SoundsFolder = "Sounds/";

        private readonly ISettingsStore settings;
        private readonly IHaptics haptics;
        private readonly Dictionary<Cue, AudioSource> sources = new Dictionary<Cue, AudioSource>();
        private readonly HashSet<Cue> warned = new HashSet<Cue>();

        /// <param name="parent">The object the per-cue sources hang under; they die with it.</param>
        public AudioFeedback(Transform parent, ISettingsStore settings, IHaptics haptics)
        {
            this.settings = settings;
            this.haptics = haptics;
            foreach (var cue in CueSpecs.All)
            {
                TryCreateSource(parent, cue);
            }
        }

        public void Play(Cue cue)
        {
            try
            {
                var spec = CueSpecs.For(cue);
                var current = settings.Current;
                if (current.HapticsOn && spec.Haptic.HasValue) haptics.Pulse(spec.Haptic.Value);
                if (current.SoundOn && sources.TryGetValue(cue, out var source) && source != null) source.Play();
            }
            catch (Exception error)
            {
                WarnOnce(cue, "could not play: " + error.Message);
            }
        }

        private void TryCreateSource(Transform parent, Cue cue)
        {
            var spec = CueSpecs.For(cue);
            AudioClip clip = null;
            try
            {
                clip = Resources.Load<AudioClip>(SoundsFolder + spec.ResourceName);
            }
            catch (Exception error)
            {
                WarnOnce(cue, "could not load clip '" + spec.ResourceName + "': " + error.Message);
                return;
            }

            // Decode now: an imported clip otherwise decompresses on its first Play, a hitch on the very first deal or win.
            if (clip != null) clip.LoadAudioData();

            if (clip == null)
            {
                WarnOnce(cue, "no AudioClip at Resources/" + SoundsFolder + spec.ResourceName + "; this cue will be silent.");
                return;
            }

            var holder = new GameObject("Cue " + spec.ResourceName);
            holder.transform.SetParent(parent, false);
            var source = holder.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = spec.Volume;
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            sources[cue] = source;
        }

        private void WarnOnce(Cue cue, string message)
        {
            if (!warned.Add(cue)) return;
            Debug.LogWarning("Showdown: cue " + cue + " " + message);
        }
    }
}
