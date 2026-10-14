using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Showdown.EditorTools
{
    /// <summary>
    /// Project-wide settings, applied from code so a fresh checkout configures itself:
    /// <c>Showdown &gt; Configure project</c>, or <c>scripts/unity.sh configure</c>.
    /// </summary>
    public static class ShowdownProject
    {
        public const string ProductName = "Poker vs Dealer";
        /// <summary>Same package the Expo build shipped under, so this build updates the live Play listing.</summary>
        public const string PackageId = "com.micorlov.pokervsdealer";
        public const string ScenePath = "Assets/Showdown/Scenes/Showdown.unity";

        private const string DefaultVersionName = "2.0.0";
        /// <summary>The Expo build shipped versionCode 4; Play only accepts a higher one.</summary>
        private const int DefaultVersionCode = 5;
        /// <summary>VibrationEffect (haptics) needs API 26, which is also Unity 6.5's own floor.</summary>
        private const int MinSdk = 26;
        private const int TargetSdk = 36;

        /// <summary>True when SHOWDOWN_VERSION_CODE was missing or not a number, i.e. the built-in default is in use.</summary>
        public static bool RequireExplicitVersionCode { get; private set; }

        [MenuItem("Showdown/Configure project")]
        public static void Configure()
        {
            ApplyIdentity();
            ApplyAndroid();
            ApplyRendering();
            EditorSettings.serializationMode = SerializationMode.ForceText;

            ShowdownAssets.EnsureAll();
            ShowdownSigning.Apply();
            AssetDatabase.SaveAssets();
            Debug.Log("Showdown: project configured (" + PackageId + ", version "
                      + PlayerSettings.bundleVersion + " / " + PlayerSettings.Android.bundleVersionCode + ").");
        }

        private static void ApplyIdentity()
        {
            PlayerSettings.companyName = "Micorlov";
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = Environment.GetEnvironmentVariable("SHOWDOWN_VERSION_NAME") ?? DefaultVersionName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, PackageId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.defaultScreenWidth = 390;
            PlayerSettings.defaultScreenHeight = 844;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.accelerometerFrequency = 0;
        }

        private static void ApplyAndroid()
        {
            bool parsed = int.TryParse(Environment.GetEnvironmentVariable("SHOWDOWN_VERSION_CODE"), out int versionCode);
            PlayerSettings.Android.bundleVersionCode = parsed ? versionCode : DefaultVersionCode;
            RequireExplicitVersionCode = !parsed;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)MinSdk;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetSdk;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.Android.androidTVCompatibility = false;
            // The listing declares no data collection and the game never goes online: do not ask for the INTERNET permission.
            PlayerSettings.Android.forceInternetPermission = false;
            // The Expo app ran on 32-bit-only phones as well as 64-bit ones; keep both so no device drops off the listing.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        }

        private static void ApplyRendering()
        {
            // The felt colours were tuned in sRGB; Gamma keeps UI colours exactly as authored and avoids
            // needing sRGB framebuffer support on every GLES device.
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        }
    }
}
