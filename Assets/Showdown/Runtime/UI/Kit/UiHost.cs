using System;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// Owns the UI Toolkit panel. Everything the app draws lives under <see cref="SafeRoot"/>, which sits on
    /// the painted <see cref="Ground"/> and is inset by the display's system bars and cutouts (RN's
    /// SafeAreaView with edges top and bottom).
    ///
    /// The panel is scaled so that 1 USS pixel equals 1 Android dp — exactly the unit the original React
    /// Native layout was written in — which lets sizes carry over unchanged.
    /// </summary>
    public sealed class UiHost : MonoBehaviour
    {
        private const string PanelResource = "ShowdownPanel";
        private const float DesktopReferenceWidth = 390f;

        public static UiHost Instance { get; private set; }

        /// <summary>Fires every frame with unscaled delta seconds, after tweens have advanced.</summary>
        public static event Action<float> Frame;

        public VisualElement Ground { get; private set; }
        public VisualElement SafeRoot { get; private set; }
        public float PanelScale { get; private set; } = 1f;

        /// <summary>The usable width in USS pixels (the panel's, not the screen's).</summary>
        public float PanelWidth => Ground != null ? Ground.resolvedStyle.width : 0f;

        private PanelSettings panelSettings;
        private UIDocument document;
        private Vector2Int lastScreen;
        private Rect lastSafeArea;

        private void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            LoadFonts();
            CreatePanel();
            BuildGround();
            ApplyScaleAndInsets();
        }

        private void OnDestroy()
        {
            Anim.Clear();
            if (Instance == this) Instance = null;
            if (panelSettings != null) Destroy(panelSettings);
        }

        private void Update()
        {
            // A long gap (the app was backgrounded mid-hand) must not fire every pending beat in one frame.
            float delta = Mathf.Min(Time.unscaledDeltaTime, MaxFrameSeconds);
            Anim.Tick(delta);
            InvokeFrame(delta);

            if (Screen.width != lastScreen.x || Screen.height != lastScreen.y || Screen.safeArea != lastSafeArea)
            {
                ApplyScaleAndInsets();
            }
        }

        private const float MaxFrameSeconds = 0.1f;

        /// <summary>Each subscriber runs on its own, so one throwing never starves the game clock or the back key.</summary>
        private static void InvokeFrame(float delta)
        {
            var handlers = Frame;
            if (handlers == null) return;
            foreach (Action<float> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(delta);
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                }
            }
        }

        /// <summary>
        /// The design's four faces, each backed by Noto Sans for any glyph it lacks; if a face is missing, Noto
        /// stands in for it, so a bad import degrades the look rather than the text.
        /// </summary>
        private static void LoadFonts()
        {
            var noto = MakeFont("Fonts/NotoSans", null);
            var notoBold = MakeFont("Fonts/NotoSans-Bold", null) ?? noto;
            Ui.RegularFont = MakeFont("Fonts/Manrope-SemiBold", noto) ?? noto;
            Ui.BoldFont = MakeFont("Fonts/Manrope-ExtraBold", notoBold) ?? notoBold;
            Ui.SerifFont = MakeFont("Fonts/CormorantGaramond-SemiBoldItalic", notoBold) ?? Ui.BoldFont;
            Ui.CapsFont = MakeFont("Fonts/CormorantSC-SemiBold", notoBold) ?? Ui.BoldFont;
            Ui.MonoFont = MakeFont("Fonts/DMMono-Medium", notoBold) ?? Ui.BoldFont;
        }

        private static FontAsset MakeFont(string resourcePath, FontAsset fallback)
        {
            var font = Resources.Load<Font>(resourcePath);
            if (font == null)
            {
                Debug.LogError("Showdown: missing bundled font " + resourcePath);
                return null;
            }
            var asset = FontAsset.CreateFontAsset(font);
            if (asset == null)
            {
                Debug.LogError("Showdown: could not build a font asset from " + resourcePath);
                return null;
            }
            asset.name = font.name + " Runtime";
            if (fallback != null) asset.fallbackFontAssetTable = new System.Collections.Generic.List<FontAsset> { fallback };
            return asset;
        }

        private void CreatePanel()
        {
            var template = Resources.Load<PanelSettings>(PanelResource);
            if (template == null)
            {
                // A runtime-made PanelSettings has no ICU data, so text layout fails. The asset is made by
                // "Showdown > Configure project".
                Debug.LogError("Showdown: ShowdownPanel.asset is missing from Resources; run Showdown > Configure project.");
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            }
            else
            {
                panelSettings = Instantiate(template);
            }
            panelSettings.name = "Showdown Runtime Panel";
            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            panelSettings.sortingOrder = 10;

            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.sortingOrder = 10;
        }

        private void BuildGround()
        {
            var root = document.rootVisualElement;
            root.style.flexGrow = 1;
            root.style.width = Length.Percent(100);
            root.style.height = Length.Percent(100);

            // The lamp glow sits over the middle of the table, not the top edge (RN: radial-gradient ellipse 140% 90% at 50% 38%).
            var glow = Gfx.Radial("ground", 0.5f, 0.38f, 1.4f, 0.9f,
                new Gfx.Stop(0f, Theme.Colors.BgGlow),
                new Gfx.Stop(0.52f, Theme.Colors.BgMid),
                new Gfx.Stop(0.82f, Theme.Colors.BgBase),
                new Gfx.Stop(1f, Theme.Colors.BgEdge));

            Ground = Ui.View("ground").AbsFill().Bg(Theme.Colors.BgBase).BgImage(glow);
            Ground.style.flexShrink = 1;
            root.Add(Ground);

            SafeRoot = Ui.View("safe-root").AbsFill();
            Ground.Add(SafeRoot);
        }

        private void ApplyScaleAndInsets()
        {
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            lastSafeArea = Screen.safeArea;

            PanelScale = ComputeScale();
            panelSettings.scale = PanelScale;

            // Screen.safeArea is in screen pixels with the origin at the bottom-left.
            var safe = lastSafeArea;
            float left = safe.xMin / PanelScale;
            float right = (Screen.width - safe.xMax) / PanelScale;
            float top = (Screen.height - safe.yMax) / PanelScale;
            float bottom = safe.yMin / PanelScale;
            SafeRoot.style.paddingLeft = left;
            SafeRoot.style.paddingRight = right;
            SafeRoot.style.paddingTop = top;
            SafeRoot.style.paddingBottom = bottom;
        }

        private static float ComputeScale()
        {
            var args = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args, "--ui-scale");
            if (flag >= 0 && args.Length > flag + 1 && float.TryParse(args[flag + 1],
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var forced) && forced > 0f)
            {
                return forced;
            }

            if (Application.isMobilePlatform)
            {
                float density = AndroidDensity();
                if (density > 0f) return density;
                return Screen.dpi > 0f ? Mathf.Max(1f, Screen.dpi / 160f) : Mathf.Max(1f, Screen.width / DesktopReferenceWidth);
            }

            // Desktop QA window: size the column like a phone, whatever the window is.
            return Mathf.Max(1f, Screen.width / DesktopReferenceWidth);
        }

        /// <summary>Android's display density (dpi / 160) — what RN divides pixels by to get dp.</summary>
        private static float AndroidDensity()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var resources = activity.Call<AndroidJavaObject>("getResources"))
                using (var metrics = resources.Call<AndroidJavaObject>("getDisplayMetrics"))
                {
                    return metrics.Get<float>("density");
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning("Showdown: could not read display density: " + error.Message);
            }
#endif
            return 0f;
        }
    }
}
