using System;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Showdown.EditorTools
{
    /// <summary>
    /// Build entry points, called from the Showdown menu or <c>scripts/unity.sh</c>:
    /// a development APK for the phone, a signed release AAB for Play, and a Mac player for UI captures.
    /// </summary>
    public static class ShowdownBuilds
    {
        public const string DevApkPath = "artifacts/android/showdown-dev.apk";
        public const string ReleaseBundlePath = "artifacts/android/showdown-release.aab";
        public const string MacAppPath = "artifacts/macos/Showdown.app";

        [MenuItem("Showdown/Build Android APK (development)")]
        public static void AndroidApk()
        {
            BuildAndroid(DevApkPath, release: false);
        }

        [MenuItem("Showdown/Build Android AAB (release)")]
        public static void AndroidBundle()
        {
            BuildAndroid(ReleaseBundlePath, release: true);
        }

        [MenuItem("Showdown/Build macOS QA player")]
        public static void Mac()
        {
            ShowdownProject.Configure();
            Build(BuildTarget.StandaloneOSX, MacAppPath, BuildOptions.None);
        }

        private static void BuildAndroid(string path, bool release)
        {
            ShowdownProject.Configure();
            if (release && ShowdownProject.RequireExplicitVersionCode)
            {
                // Play rejects a repeated versionCode; refuse to build a release on a silent default.
                throw new InvalidOperationException("A release build needs SHOWDOWN_VERSION_CODE set to a number higher than the last Play upload.");
            }
            ShowdownSigning.Apply(release);
            UseEmbeddedAndroidTools();
            EditorUserBuildSettings.buildAppBundle = release;

            // A development APK is for the phone on the desk: one architecture builds in half the time.
            PlayerSettings.Android.targetArchitectures = release
                ? AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7
                : AndroidArchitecture.ARM64;

            try
            {
                Build(BuildTarget.Android, path, release ? BuildOptions.None : BuildOptions.Development);
            }
            finally
            {
                // The keystore path and alias must not linger in ProjectSettings after a release build.
                ShowdownSigning.Apply(release: false);
                PlayerSettings.Android.keystoreName = string.Empty;
                PlayerSettings.Android.keyaliasName = string.Empty;
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// Editor-wide external-tool preferences are shared by every Unity project on this machine, and another
        /// project may have pointed them somewhere else. Pin this build to the SDK, NDK and JDK that ship with the Editor.
        /// </summary>
        private static void UseEmbeddedAndroidTools()
        {
            string engine = BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None);
            AndroidExternalToolsSettings.sdkRootPath = Path.Combine(engine, "SDK");
            AndroidExternalToolsSettings.ndkRootPath = Path.Combine(engine, "NDK");
            AndroidExternalToolsSettings.jdkRootPath = Path.Combine(engine, "OpenJDK");
            Debug.Log("Showdown: Android tools sdk=" + AndroidExternalToolsSettings.sdkRootPath
                      + " ndk=" + AndroidExternalToolsSettings.ndkRootPath
                      + " jdk=" + AndroidExternalToolsSettings.jdkRootPath);
        }

        private static void Build(BuildTarget target, string path, BuildOptions options)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

            var group = BuildPipeline.GetBuildTargetGroup(target);
            if (EditorUserBuildSettings.activeBuildTarget != target
                && !EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
            {
                throw new InvalidOperationException("Showdown build: could not switch to " + target);
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ShowdownProject.ScenePath },
                locationPathName = path,
                target = target,
                options = options,
            });

            var summary = report.summary;
            Debug.Log("Showdown build: " + summary.result + ", " + summary.totalSize + " bytes, "
                      + summary.totalErrors + " errors -> " + path);
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("Showdown build failed with " + summary.totalErrors + " errors.");
            }
        }
    }
}
