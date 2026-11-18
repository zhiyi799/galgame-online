// BuildScript — Editor tooling for the GalgameOnline Unity client.
// Run in batchmode, e.g.:
//   Unity -batchmode -quit -projectPath <proj> -executeMethod GalgameOnline.Editor.BuildScript.GenerateFontAsset
//   Unity -batchmode -quit -projectPath <proj> -executeMethod GalgameOnline.Editor.BuildScript.BuildWindows
//   Unity -batchmode -quit -projectPath <proj> -executeMethod GalgameOnline.Editor.BuildScript.BuildLinux
//   Unity -batchmode -quit -projectPath <proj> -executeMethod GalgameOnline.Editor.BuildScript.CompileCheck
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using TMPro;

namespace GalgameOnline.Editor
{
    public static class BuildScript
    {
        public static readonly string[] Scenes =
        {
            "Assets/Scenes/Auth.unity",
            "Assets/Scenes/ProtagonistSelect.unity",
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/PlayerHome.unity",
            "Assets/Scenes/Exploration.unity",
            "Assets/Scenes/WorldMap.unity",
            "Assets/Scenes/Gacha.unity",
            "Assets/Scenes/Inventory.unity",
            "Assets/Scenes/Mail.unity",
            "Assets/Scenes/CloudSave.unity",
            "Assets/Scenes/Story.unity",
        };

        private const string FontTtfPath = "Assets/Fonts/NotoSansCJKsc-Regular.ttf";
        private const string FontAssetPath = "Assets/Resources/Fonts/NotoSansCJKsc SDF.asset";

        [MenuItem("GalgameOnline/Generate CJK Font Asset")]
        public static void GenerateFontAsset()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontTtfPath);
            if (font == null)
            {
                Debug.LogError("[Build] Font not found: " + FontTtfPath);
                return;
            }
            // TMP_FontAsset.CreateFontAsset needs TMP Settings + the TMP
            // shaders in the project. A fresh project has neither, and the
            // TMP package importer cannot locate the package (it only
            // searches Packages/ and Assets/Packages/), so import the
            // TMP Essential Resources package from the UGUI package cache.
            if (TMP_Settings.instance == null)
            {
                Debug.Log("[Build] Importing TMP Essential Resources (TMP Settings missing)...");
                string[] pkgs = Directory.GetFiles("Library/PackageCache",
                    "TMP Essential Resources.unitypackage", SearchOption.AllDirectories);
                if (pkgs.Length == 0)
                {
                    Debug.LogError("[Build] TMP Essential Resources package not found in Library/PackageCache.");
                    return;
                }
                AssetDatabase.ImportPackage(pkgs[0], false);
                AssetDatabase.Refresh();
            }
            Directory.CreateDirectory("Assets/Resources/Fonts");
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null) AssetDatabase.DeleteAsset(FontAssetPath);

            // Dynamic atlas: CJK glyphs are rasterised on demand at runtime.
            var fontAsset = TMP_FontAsset.CreateFontAsset(font);
            fontAsset.name = "NotoSansCJKsc SDF";
            // Make the CJK font the default for all TMP text in the project.
            TMP_Settings.defaultFontAsset = fontAsset;
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            // The atlas texture and material only exist in memory after
            // CreateFontAsset — they must be added as sub-assets or the saved
            // .asset keeps null references and every TMP text throws
            // NullReferenceException in the player (see smoke-test log).
            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0
                && fontAsset.atlasTextures[0] != null)
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            if (fontAsset.material != null)
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Build] CJK TMP font asset generated: " + FontAssetPath);
        }

        [MenuItem("GalgameOnline/Generate UI Material")]
        public static void GenerateUiMaterial()
        {
            // The UGUI package cache in this project is a stub: it has no
            // runtime UI shaders, so the built player has no UI/Default
            // shader and every Image (panel / button / input background)
            // renders magenta. Fix: bake a Material that references the
            // Editor's built-in UI/Default shader into the project, so the
            // build includes the shader. UiFactory assigns this material to
            // every Image it creates.
            const string dir = "Assets/Resources/UI";
            const string texPath = dir + "/White1x1.png";
            const string matPath = dir + "/DefaultUIMaterial.mat";
            Directory.CreateDirectory(dir);

            var shader = Shader.Find("UI/Default");
            if (shader == null)
            {
                Debug.LogError("[Build] UI/Default shader not found (Shader.Find returned null).");
                return;
            }

            // 1x1 white texture so the material is self-contained.
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
            var importedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (importedTex == null)
            {
                Debug.LogError("[Build] Failed to import white texture: " + texPath);
                return;
            }

            var mat = new Material(shader);
            mat.mainTexture = importedTex;
            mat.name = "DefaultUIMaterial";
            var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existingMat != null) AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(mat, matPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Build] UI material generated: " + matPath);
        }

        [MenuItem("GalgameOnline/Compile Check")]
        public static void CompileCheck()
        {
            // If any script has a compile error, the Editor refuses to run this
            // method at all — reaching this log line proves compilation is clean.
            Debug.Log("[Build] CompileCheck OK — all scripts compiled, all scenes load.");
            foreach (var s in Scenes)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(s);
                Debug.Log("[Build] scene loads: " + scene.name + " (" + scene.rootCount + " roots)");
            }
        }

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "GalgameOnline";
            PlayerSettings.productName = "GalgameOnline";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.colorSpace = ColorSpace.Gamma;
        }

        [MenuItem("GalgameOnline/Build Windows (PC)")]
        public static void BuildWindows()
        {
            ApplyPlayerSettings();
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = "Build/Windows/GalgameOnline.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            LogReport(report, "Windows");
        }

        [MenuItem("GalgameOnline/Build Linux (smoke test only)")]
        public static void BuildLinux()
        {
            ApplyPlayerSettings();
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = "Build/Linux/GalgameOnline.x86_64",
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            LogReport(report, "Linux");
        }

        private static void LogReport(BuildReport report, string label)
        {
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Build] {label} build SUCCEEDED: {report.summary.outputPath} " +
                          $"({report.summary.totalSize / 1024 / 1024} MB, " +
                          $"{report.summary.totalTime.TotalSeconds:F1}s)");
            }
            else
            {
                Debug.LogError($"[Build] {label} build FAILED: {report.summary.result} " +
                               $"({report.summary.totalErrors} errors)");
            }
        }
    }
}
