using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Showdown
{
    /// <summary>
    /// Visual QA without a device: <c>Showdown.app --capture out.png [--capture-delay 2.5]</c> renders the
    /// first screen, saves a screenshot and quits. Only active when asked for on the command line.
    /// </summary>
    public sealed class QaCapture : MonoBehaviour
    {
        private const float DefaultDelaySeconds = 2.5f;
        private const int SettleFrames = 3;

        /// <summary>After a scenario reports ready, wait this long so entrance animations finish before the shot.</summary>
        private const float ScenarioSettleSeconds = 1.0f;

        private string path;
        private float delay;
        private bool captured;
        private int framesAfterCapture;
        private float readyAt = -1f;

        private static QaCapture instance;

        private static bool IsDesktopPlayer => Application.platform == RuntimePlatform.OSXPlayer
                                               || Application.platform == RuntimePlatform.WindowsPlayer
                                               || Application.platform == RuntimePlatform.LinuxPlayer;

        /// <summary>A scenario (QaScenarios) calls this once the screen under test is showing.</summary>
        public static void Ready()
        {
            if (instance != null && instance.readyAt < 0f) instance.readyAt = Time.realtimeSinceStartup;
        }

        private static bool QaScenarioRequested => ArgumentValue("--scenario") != null;

        public static bool Requested => ArgumentValue("--capture") != null;

        private void Awake()
        {
            // A player launched from a script is never the frontmost app; without this it would not run frames at all.
            Application.runInBackground = true;
            instance = this;
            path = Path.GetFullPath(ArgumentValue("--capture"));
            string delayText = ArgumentValue("--capture-delay");
            delay = delayText != null && float.TryParse(delayText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : DefaultDelaySeconds;
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        }

        private void Update()
        {
            if (!captured)
            {
                bool scenario = QaScenarioRequested;
                if (scenario ? readyAt < 0f || Time.realtimeSinceStartup < readyAt + ScenarioSettleSeconds : Time.realtimeSinceStartup < delay) return;
                ScreenCapture.CaptureScreenshot(path);
                captured = true;
                return;
            }

            // The screenshot is written at the end of the frame; give it a few frames before quitting.
            framesAfterCapture++;
            if (framesAfterCapture >= SettleFrames) Application.Quit();
        }

        /// <summary>
        /// Command-line flags are honoured only in the Editor and development builds: on Android another app can
        /// launch a release build with arbitrary extras, and a store build has no use for QA hooks.
        /// </summary>
        public static string ArgumentValue(string name)
        {
            if (!Debug.isDebugBuild && !Application.isEditor && !IsDesktopPlayer) return null;
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
