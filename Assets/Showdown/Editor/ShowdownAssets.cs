using System;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Showdown.EditorTools
{
    /// <summary>Generated project assets: the UI panel, the bootstrap scene, import settings, launcher icons.</summary>
    public static class ShowdownAssets
    {
        public const string PanelPath = "Assets/Showdown/Resources/ShowdownPanel.asset";
        private const string IconFolder = "Assets/Showdown/Art/Icons";
        private const string RuntimeArtFolder = "Assets/Showdown/Resources/Art";

        public static void EnsureAll()
        {
            ConfigureTextures();
            EnsurePanelSettings();
            EnsureScene();
            ApplyLauncherIcons();
        }

        // ---- UI panel ------------------------------------------------------------------------------

        /// <summary>
        /// A runtime-made PanelSettings has no ICU data, so UI Toolkit text layout throws every frame and nothing
        /// renders. The asset has to be created in the Editor, through Unity's own menu command where possible.
        /// </summary>
        private static void EnsurePanelSettings()
        {
            if (AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath) != null) return;

            Directory.CreateDirectory(Path.GetDirectoryName(PanelPath));
            var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Path.GetDirectoryName(PanelPath));
            Selection.activeObject = folder;
            EditorApplication.ExecuteMenuItem("Assets/Create/UI Toolkit/Panel Settings Asset");
            AssetDatabase.Refresh();

            var created = FindCreatedPanelSettings();
            if (created == null)
            {
                Debug.LogWarning("Showdown: the Panel Settings menu command made nothing; creating the asset directly.");
                created = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(created, PanelPath);
            }
            else if (AssetDatabase.GetAssetPath(created) != PanelPath)
            {
                string error = AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(created), PanelPath);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException("Could not place the panel asset: " + error);
            }
            AssetDatabase.SaveAssets();
        }

        private static PanelSettings FindCreatedPanelSettings()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:PanelSettings", new[] { "Assets" }))
            {
                var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetDatabase.GUIDToAssetPath(guid));
                if (settings != null) return settings;
            }
            return null;
        }

        // ---- scene ---------------------------------------------------------------------------------

        private static void EnsureScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // UI Toolkit needs no camera, but a player with none shows undefined colour between frames.
            var cameraObject = new GameObject("Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1176f, 0.4196f, 0.2353f); // felt, #1E6B3C
            camera.orthographic = true;
            camera.cullingMask = 0;
            camera.depth = -1;

            var app = new GameObject("Showdown");
            app.AddComponent(ResolveAppType());

            Directory.CreateDirectory(Path.GetDirectoryName(ShowdownProject.ScenePath));
            EditorSceneManager.SaveScene(scene, ShowdownProject.ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ShowdownProject.ScenePath, true) };
        }

        /// <summary>The runtime entry point lives in another assembly; look it up by name so this one stays decoupled.</summary>
        private static Type ResolveAppType()
        {
            var type = Type.GetType("Showdown.ShowdownApp, Showdown.Runtime");
            if (type == null) throw new InvalidOperationException("Showdown.ShowdownApp was not found in Showdown.Runtime.");
            return type;
        }

        // ---- textures ------------------------------------------------------------------------------

        private static void ConfigureTextures()
        {
            // The home screen draws the icon at 52 dp and the dealer at 120 dp; full-size sources would waste memory.
            // Both are drawn far smaller than their size, so they get mipmaps: without them the downscale shimmers during entrances.
            ConfigureTexture(RuntimeArtFolder + "/icon.png", 256, mipmaps: true);
            ConfigureTexture(RuntimeArtFolder + "/dealer-emerald-dress.png", 1024, mipmaps: true);
            foreach (var path in Directory.GetFiles(IconFolder, "*.png"))
            {
                ConfigureTexture(path.Replace('\\', '/'), 2048, uncompressed: true);
            }
        }

        private static void ConfigureTexture(string path, int maxSize, bool uncompressed = false, bool mipmaps = false)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;

            bool changed = importer.maxTextureSize != maxSize || importer.mipmapEnabled != mipmaps || importer.isReadable
                           || importer.wrapMode != TextureWrapMode.Clamp || importer.alphaIsTransparency == false
                           || importer.npotScale != TextureImporterNPOTScale.None;
            if (!changed) return;

            importer.textureType = TextureImporterType.Default;
            importer.maxTextureSize = maxSize;
            importer.mipmapEnabled = mipmaps;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            if (uncompressed) importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // ---- launcher icon -------------------------------------------------------------------------

        private static void ApplyLauncherIcons()
        {
            var background = LoadIcon("android-icon-background.png");
            var foreground = LoadIcon("android-icon-foreground.png");
            var monochrome = LoadIcon("android-icon-monochrome.png");
            var legacy = AssetDatabase.LoadAssetAtPath<Texture2D>(RuntimeArtFolder + "/icon.png");
            if (background == null || foreground == null || legacy == null)
            {
                Debug.LogWarning("Showdown: launcher icon sources are missing; keeping the default icon.");
                return;
            }

            var target = UnityEditor.Build.NamedBuildTarget.Android;
            SetIcons(target, AndroidPlatformIconKind.Adaptive, background, foreground, monochrome);
            SetIcons(target, AndroidPlatformIconKind.Round, legacy);
            SetIcons(target, AndroidPlatformIconKind.Legacy, legacy);
        }

        private static Texture2D LoadIcon(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(IconFolder + "/" + fileName);
        }

        /// <summary>Fills every slot of an icon kind; layered kinds take background, foreground, then monochrome.</summary>
        private static void SetIcons(UnityEditor.Build.NamedBuildTarget target, PlatformIconKind kind, params Texture2D[] layers)
        {
            var icons = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (var icon in icons)
            {
                for (int layer = 0; layer < icon.layerCount && layer < layers.Length; layer++)
                {
                    if (layers[layer] != null) icon.SetTexture(layers[layer], layer);
                }
            }
            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }
    }
}
