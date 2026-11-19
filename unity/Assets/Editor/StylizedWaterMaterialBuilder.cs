// StylizedWaterMaterialBuilder — 烘焙风格化水材质资产（保证 shader 被构建包含）。
// 做法来源：danielshervheim/Stylized-Water（BSD-3，见 Assets/StylizedWater/LICENSE-BSD3.txt），
// shader 与纹理原样复制，只改材质参数；不复制其代码逻辑之外的东西。
// 做法与 ToonMaterialBuilder 一致：编辑器生成 .mat 文件到 Resources（必进构建）。
// 配色按 ASTRAL BLOOM 群青世界调：深群青深水 / 月白浅滩 / 远景群青；波长按 2000 m
// 地块放大（顶点波长 30–45 m，配合运行时 128×72 细分网格，约 3 个顶点/波长）。
using UnityEditor;
using UnityEngine;
using System.IO;

namespace GalgameOnline.Editor
{
    public static class StylizedWaterMaterialBuilder
    {
        const string Dir = "Assets/Resources/StylizedWater";
        const string TexDir = "Assets/StylizedWater/Textures";

        [MenuItem("GalgameOnline/Build Stylized Water Materials")]
        public static void BuildStylizedWaterMaterials()
        {
            var shader = Shader.Find("Stylized/Water");
            if (shader == null)
            {
                Debug.LogError("[Water] Stylized/Water shader not found. Check Assets/StylizedWater/Shaders/StylizedWater.shader compiles.");
                return;
            }
            Directory.CreateDirectory(Dir);

            // 纹理导入设置：法线贴图必须标为 NormalMap（shader 里 UnpackNormal），
            // 三张都开 Repeat（世界空间 UV 平铺）。
            SetTextureImport(TexDir + "/GentleWaves.bmp", true);
            SetTextureImport(TexDir + "/SeaPattern.TGA", true);
            SetTextureImport(TexDir + "/CausticsShallow.jpg", false);
            AssetDatabase.Refresh();

            var waveNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/GentleWaves.bmp");
            var foam = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/CausticsShallow.jpg");
            var sparkle = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/SeaPattern.TGA");
            if (waveNormal == null || foam == null || sparkle == null)
            {
                Debug.LogError("[Water] water textures not found after import.");
                return;
            }

            // 深水（星环城 / 苍穹）：深群青。
            Build("WaterDeepStylized", shader, waveNormal, foam, sparkle,
                deep: new Color(0.01f, 0.07f, 0.22f),
                shallow: new Color(0.45f, 0.75f, 0.95f),
                far: new Color(0.05f, 0.16f, 0.42f));
            // 冰水（雾海 / 白塔）：冷灰蓝。
            Build("WaterIceStylized", shader, waveNormal, foam, sparkle,
                deep: new Color(0.10f, 0.28f, 0.38f),
                shallow: new Color(0.75f, 0.88f, 0.95f),
                far: new Color(0.35f, 0.55f, 0.65f));
            // 湖水（永夜之森）：暗群青。
            Build("WaterLakeStylized", shader, waveNormal, foam, sparkle,
                deep: new Color(0.01f, 0.06f, 0.18f),
                shallow: new Color(0.40f, 0.65f, 0.85f),
                far: new Color(0.04f, 0.12f, 0.35f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Water] materials baked to " + Dir);
        }

        static void SetTextureImport(string path, bool normalMap)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[Water] no importer for " + path);
                return;
            }
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.sRGBTexture = !normalMap;
            importer.SaveAndReimport();
        }

        static void Build(string name, Shader shader, Texture2D waveNormal, Texture2D foam, Texture2D sparkle,
            Color deep, Color shallow, Color far)
        {
            var mat = new Material(shader);

            // 深度 / 距离衰减。
            mat.SetFloat("_DepthDensity", 0.5f);
            mat.SetFloat("_DistanceDensity", 0.02f);

            // 波浪法线（世界空间平铺，20 m 一格）。
            mat.SetTexture("_WaveNormalMap", waveNormal);
            mat.SetFloat("_WaveNormalScale", 20f);
            mat.SetFloat("_WaveNormalSpeed", 0.15f);

            // 基色：深水 / 浅滩（乘抓取的背景色）/ 远景。
            mat.SetColor("_ShallowColor", shallow);
            mat.SetColor("_DeepColor", deep);
            mat.SetColor("_FarColor", far);

            // 反射：场景无反射探针，unity_SpecCube0 约为黑，贡献调低，无害。
            mat.SetFloat("_ReflectionContribution", 0.6f);

            // 次表面散射：淡蓝（原预设偏绿，已改）。
            mat.SetColor("_SSSColor", new Color(0.25f, 0.45f, 0.65f));

            // 泡沫。
            mat.SetTexture("_FoamTexture", foam);
            mat.SetFloat("_FoamContribution", 0.5f);
            mat.SetFloat("_FoamScale", 12f);
            mat.SetFloat("_FoamSpeed", 0.12f);
            mat.SetFloat("_FoamNoiseScale", 0.4f);

            // 太阳高光 + 闪光。
            mat.SetColor("_SunSpecularColor", Color.white);
            mat.SetFloat("_SunSpecularExponent", 2000f);
            mat.SetTexture("_SparklesNormalMap", sparkle);
            mat.SetFloat("_SparkleScale", 60f);
            mat.SetFloat("_SparkleSpeed", 0.05f);
            mat.SetColor("_SparkleColor", new Color(0.85f, 0.95f, 1.0f));
            mat.SetFloat("_SparkleExponent", 20000f);

            // 岸边泡沫：光学深度 < ~2 m 处出泡沫（月白）。
            mat.SetColor("_EdgeFoamColor", new Color(0.92f, 0.96f, 1.0f));
            mat.SetFloat("_EdgeFoamDepth", 2.0f);

            // 阴影采样关（需要额外的 GrabShadowMap，不接）。
            mat.SetFloat("_FancyShadows", 0f);
            mat.DisableKeyword("SHADOWS");
            mat.SetColor("_ShadowColor", new Color(0.3f, 0.35f, 0.4f));
            mat.SetFloat("_MaxShadowDistance", 50f);

            // 顶点波：波长按地块尺度放大，振幅小（避免穿出地形）。
            mat.SetFloat("_Wave1Direction", 0.5f);
            mat.SetFloat("_Wave1Amplitude", 0.35f);
            mat.SetFloat("_Wave1Wavelength", 45f);
            mat.SetFloat("_Wave1Speed", 0.5f);
            mat.SetFloat("_Wave2Direction", 0.25f);
            mat.SetFloat("_Wave2Amplitude", 0.25f);
            mat.SetFloat("_Wave2Wavelength", 30f);
            mat.SetFloat("_Wave2Speed", 0.9f);

            string path = Dir + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
        }
    }
}
