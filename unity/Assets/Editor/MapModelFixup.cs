// MapModelFixup — one-shot Editor fix-up after the first placement pass.
// 1. Skyvault: the first pass put the cooling-tower rows on a cliff face
//    (tower heights 565-1300 m). Re-place the whole industrial group on
//    the flattest 400x220 m window in the tile.
// 2. Wuhai bridge: the first pass put the two ends inside the canyon
//    (202 m / 114 m, canyon floor 118 m) with a roll instead of a pitch.
//    Re-place them on the canyon rims at rim height, inner ends dipped
//    (yaw * local-Z pitch composition).
// Run: -executeMethod GalgameOnline.Editor.MapModelFixup.Fixup
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GalgameOnline.WorldMap;

namespace GalgameOnline.Editor
{
    public static class MapModelFixup
    {
        private static float H(float x, float z) { return WorldMapTerrain.SampleHeight(x, z); }

        public static void Fixup()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/WorldMap.unity");
            FixSkyvault();
            FixBridge();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[FIXUP] DONE");
        }

        private static GameObject FindRoot(string name)
        {
            var go = GameObject.Find("MapModels_" + name);
            if (go == null) Debug.LogError("[FIXUP] root not found: MapModels_" + name);
            return go;
        }

        private static void DeleteChild(GameObject root, string name)
        {
            var t = root.transform.Find(name);
            if (t != null) Object.DestroyImmediate(t.gameObject);
            else Debug.LogWarning("[FIXUP] child not found for delete: " + name);
        }

        private static GameObject PlacePrefab(string prefabPath, GameObject parent, string name,
            Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = scale;
            Debug.Log("[FIXUP] " + name + " pos=(" + pos.x.ToString("F0") + "," + pos.y.ToString("F1")
                + "," + pos.z.ToString("F0") + ") scale=" + scale.x.ToString("F1"));
            return go;
        }

        private static void FixSkyvault()
        {
            var root = FindRoot("Skyvault");
            if (root == null) return;
            DeleteChild(root, "EnergyCore_Crystal");
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 8; i++) DeleteChild(root, "CoolingTower_" + row + "_" + i);
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 6; i++) DeleteChild(root, "SolarPanel_" + row + "_" + i);

            // Flattest 400x220 m window in the skyvault tile (x 2000-4000, z -1138-0).
            float bestRange = float.MaxValue; Vector2 bestC = new Vector2(3000f, -569f);
            for (float cx = 2200f; cx <= 3800f; cx += 50f)
                for (float cz = -1038f; cz <= -100f; cz += 50f)
                {
                    float mn = float.MaxValue, mx = float.MinValue;
                    for (float x = cx - 200f; x <= cx + 200f; x += 50f)
                        for (float z = cz - 110f; z <= cz + 110f; z += 50f)
                        {
                            float h = H(x, z);
                            if (h < mn) mn = h;
                            if (h > mx) mx = h;
                        }
                    float range = mx - mn;
                    if (range < bestRange) { bestRange = range; bestC = new Vector2(cx, cz); }
                }
            Debug.Log("[FIXUP] skyvault flattest window centre=(" + bestC.x.ToString("F0") + ","
                + bestC.y.ToString("F0") + ") range=" + bestRange.ToString("F1")
                + " h=" + H(bestC.x, bestC.y).ToString("F1"));

            // Crystal at the window centre; towers + panels in rows across it.
            Vector3 core = new Vector3(bestC.x, H(bestC.x, bestC.y), bestC.y);
            PlacePrefab("Assets/Models/Map/Prefabs/Crystal.prefab", root, "EnergyCore_Crystal",
                core, Quaternion.identity, Vector3.one * 4f);
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 8; i++)
                {
                    float x = bestC.x - 160f + i * 45f;
                    float z = bestC.y + 70f + row * 55f;
                    PlacePrefab("Assets/Models/Map/Prefabs/CoolingTower.prefab", root,
                        "CoolingTower_" + row + "_" + i,
                        new Vector3(x, H(x, z), z), Quaternion.Euler(0f, (i * 47) % 360, 0f),
                        Vector3.one * 15f);
                }
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 6; i++)
                {
                    float x = bestC.x - 100f + i * 40f;
                    float z = bestC.y - 90f - row * 45f;
                    PlacePrefab("Assets/Models/Map/Prefabs/SolarRig.prefab", root,
                        "SolarPanel_" + row + "_" + i,
                        new Vector3(x, H(x, z), z), Quaternion.Euler(0f, (i * 61) % 360, 0f),
                        Vector3.one);
                }
        }

        private static void FixBridge()
        {
            var root = FindRoot("Wuhai");
            if (root == null) return;
            DeleteChild(root, "Bridge_North");
            DeleteChild(root, "Bridge_South");

            // Canyon centre from the first pass: (-200, 2163), valley runs E-W.
            const float cx = -200f, cz = 2163f;
            float rimN = H(cx, cz + 75f);
            float rimS = H(cx, cz - 75f);
            Debug.Log("[FIXUP] canyon rims N=" + rimN.ToString("F1") + " at z=" + (cz + 75f)
                + " S=" + rimS.ToString("F1") + " at z=" + (cz - 75f));
            // Bridge length 30.7 m along local X. Yaw 90 deg maps local X to
            // world -Z; pitch is applied around local Z BEFORE the yaw, so the
            // sign below dips the inner (gap-facing) end.
            // North bridge: on the north rim, inner (south) end dips -> pitch -12.
            PlacePrefab("Assets/Models/Map/Prefabs/Bridge.prefab", root, "Bridge_North",
                new Vector3(cx, rimN - 2f, cz + 60f),
                Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(0f, 0f, -12f),
                Vector3.one);
            // South bridge: on the south rim, inner (north) end dips -> pitch +12.
            PlacePrefab("Assets/Models/Map/Prefabs/Bridge.prefab", root, "Bridge_South",
                new Vector3(cx, rimS - 2f, cz - 60f),
                Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(0f, 0f, 12f),
                Vector3.one);
        }
    }
}
