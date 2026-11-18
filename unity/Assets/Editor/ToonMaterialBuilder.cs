// ToonMaterialBuilder — 烘焙 AstralBloom/Toon 材质资产（保证 shader 被构建包含）。
// 做法与 WorldMapBuilder.BuildWaterMaterial 一致：编辑器生成 .mat 文件。
// 颜色沿用 MaterialLibrary 现有配色（群青世界 + 月白 + 樱粉小点缀）。
using UnityEditor;
using UnityEngine;
using System.IO;

namespace GalgameOnline.Editor
{
    public static class ToonMaterialBuilder
    {
        const string Dir = "Assets/Resources/Toon";

        [MenuItem("GalgameOnline/Build Toon Materials")]
        public static void BuildToonMaterials()
        {
            var shader = Shader.Find("AstralBloom/Toon");
            if (shader == null)
            {
                Debug.LogError("[Toon] AstralBloom/Toon shader not found. Check Assets/Shaders/AstralToon.shader compiles.");
                return;
            }
            Directory.CreateDirectory(Dir);

            // 角色：群青布料 / 月白衬里 / 肌肤 / 银白头发（头发开各向异性高光）。
            Build("ToonCloth", new Color(0.14f, 0.30f, 0.67f), shader, outline: 0.02f);
            Build("ToonClothLight", new Color(0.86f, 0.93f, 0.99f), shader, outline: 0.02f);
            Build("ToonSkin", new Color(0.99f, 0.86f, 0.78f), shader, outline: 0.014f, specStrength: 0.35f);
            Build("ToonHair", new Color(0.92f, 0.95f, 1.0f), shader, outline: 0.02f,
                hairStrength: 1.2f, specStrength: 0.9f, gloss: 32f);
            Build("ToonBoots", new Color(0.18f, 0.20f, 0.28f), shader, outline: 0.016f);
            Build("ToonLegs", new Color(0.20f, 0.26f, 0.40f), shader, outline: 0.016f);

            // 布景：青绿叶 / 棕木 / 灰石 / 暗影敌（描边略粗，轮廓更漫画）。
            // 阴影色用各自色调的暗色，避免树冠/岩石整面变黑（与 MaterialLibrary.MakeToon 一致）。
            Build("ToonFoliage", new Color(0.20f, 0.45f, 0.42f), shader, outline: 0.022f, shadowColor: new Color(0.10f, 0.26f, 0.24f), ambientStrength: 0.85f);
            Build("ToonWood", new Color(0.35f, 0.26f, 0.19f), shader, outline: 0.02f, shadowColor: new Color(0.16f, 0.11f, 0.08f));
            Build("ToonStone", new Color(0.47f, 0.47f, 0.50f), shader, outline: 0.02f, shadowColor: new Color(0.20f, 0.21f, 0.26f));
            Build("ToonEnemy", new Color(0.08f, 0.10f, 0.32f), shader, outline: 0.024f, rimStrength: 0.9f);
            Build("ToonEnemyDark", new Color(0.03f, 0.04f, 0.12f), shader, outline: 0.024f, rimStrength: 0.9f);

            // WorldMap 玩家胶囊：群青。
            Build("ToonWorldMapPlayer", new Color(0.141f, 0.302f, 0.671f), shader, outline: 0.02f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Toon] materials baked to " + Dir);
        }

        static void Build(string name, Color color, Shader shader,
            float outline = 0.018f, float specStrength = 0.6f, float gloss = 24f,
            float hairStrength = 0f, float rimStrength = 0.5f, Color? shadowColor = null,
            float ambientStrength = 0.45f)
        {
            var mat = new Material(shader);
            mat.SetColor("_Color", color);
            mat.SetColor("_LightColor", new Color(0.90f, 0.93f, 1.0f, 1f));
            mat.SetColor("_ShadowColor", shadowColor ?? new Color(0.10f, 0.16f, 0.32f, 1f));
            mat.SetFloat("_ShadowThreshold", 0.5f);
            mat.SetFloat("_ShadowSmoothness", 0.05f);
            mat.SetFloat("_ShadowSteps", 3f);
            mat.SetColor("_SpecularColor", Color.white);
            mat.SetFloat("_Gloss", gloss);
            mat.SetFloat("_SpecularStrength", specStrength);
            mat.SetFloat("_SpecularThreshold", 0.75f);
            mat.SetFloat("_SpecularSmoothness", 0.08f);
            mat.SetFloat("_SpecularType", 0f);
            mat.SetColor("_RimColor", new Color(0.87f, 0.69f, 0.80f, 1f));
            mat.SetFloat("_RimStrength", rimStrength);
            mat.SetFloat("_RimThreshold", 0.35f);
            mat.SetFloat("_RimSmoothness", 0.15f);
            mat.SetColor("_HairSpecularColor", new Color(1f, 0.85f, 0.65f, 1f));
            mat.SetFloat("_HairSpecularStrength", hairStrength);
            mat.SetFloat("_AmbientStrength", ambientStrength);
            mat.SetColor("_OutlineColor", new Color(0.05f, 0.07f, 0.16f, 1f));
            mat.SetFloat("_OutlineWidth", outline);
            mat.SetFloat("_Cutoff", 0.5f);

            string path = Dir + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
        }
    }
}
