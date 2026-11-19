// WorldMapBootstrap — scene setup for WorldMap.unity (runs on scene load).
// Registers the five terrains, sets render settings suited to a 6 km wide
// world (fog / shadow distance), and snaps the player onto the terrain at
// the Xinghuan central plaza spawn point.
using UnityEngine;

namespace GalgameOnline.WorldMap
{
    public class WorldMapBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            WorldMapTerrain.Register();

            // A 6 km continent needs a far shadow distance and fog that only
            // bites at real distance; the UI scenes use the defaults.
            QualitySettings.shadowDistance = 3000f;

            var playerObj = GameObject.Find("Player");
            if (playerObj != null)
            {
                Vector3 spawn = WorldMapTerrain.SpawnPoint;
                playerObj.transform.position = spawn;
                Debug.Log("[WorldMap] player spawn at " + spawn
                    + " ground=" + WorldMapTerrain.SampleHeight(spawn.x, spawn.z));

                // 建模重做 Item 1：玩家胶囊换 toon 材质（群青 + 描边）。
                // shader 未找到时保持原材质，绝不崩。
                var toon = Shader.Find("AstralBloom/Toon");
                var rend = playerObj.GetComponent<Renderer>();
                if (toon != null && rend != null)
                {
                    var m = new Material(toon);
                    m.SetColor("_Color", new Color(0.141f, 0.302f, 0.671f, 1f));
                    m.SetColor("_LightColor", new Color(0.90f, 0.93f, 1.0f, 1f));
                    m.SetColor("_ShadowColor", new Color(0.10f, 0.16f, 0.32f, 1f));
                    m.SetFloat("_ShadowThreshold", 0.5f);
                    m.SetFloat("_ShadowSmoothness", 0.05f);
                    m.SetFloat("_ShadowSteps", 3f);
                    m.SetFloat("_OutlineWidth", 0.02f);
                    m.SetColor("_RimColor", new Color(0.87f, 0.69f, 0.80f, 1f));
                    m.SetFloat("_RimStrength", 0.5f);
                    rend.sharedMaterial = m;
                }
            }

            // 建模重做 Item 2：水面换风格化水（shader/材质缺失时保持原材质，绝不崩）。
            ApplyStylizedWater();

            // Phase 1 / P1-C：星律工坊（运行时生成用地与设施，不重新生成场景）
            var wmObj = new GameObject("WorkshopManager");
            var wm = wmObj.AddComponent<AstralBloom.WorkshopManager>();
            var wmp = playerObj != null ? playerObj.GetComponent<WorldMapPlayer>() : null;
            wm.Init(wmp);

            foreach (var t in FindObjectsOfType<Terrain>())
            {
                Debug.Log("[WorldMap] terrain " + t.name + " at " + t.transform.position
                    + " size=" + t.terrainData.size);
            }
        }

        // 建模重做 Item 2：把 5 块 Standard 平面水面换成风格化水。
        // - 材质来自 Resources/StylizedWater（编辑器烘焙，StylizedWaterMaterialBuilder）；
        // - 网格换成 128×72 细分平面（原 Plane 原语只有 10×10 段，2000 m 地块上
        //   顶点波会被混叠掉；细分后约 15.6 m/顶点，波长 30–45 m 可见）；
        // - 相机开 DepthTextureMode.Depth，否则 shader 的 _CameraDepthTexture
        //   拿不到深度，深度染色与岸边泡沫会错。
        // 任何一步缺失都只跳过该块水面、保留原材质，绝不崩。
        private static Mesh _subdividedPlaneMesh;

        private void ApplyStylizedWater()
        {
            var cam = Camera.main;
            if (cam != null) cam.depthTextureMode = DepthTextureMode.Depth;

            foreach (var r in WorldMapTerrain.Regions)
            {
                var water = GameObject.Find("Water_" + r.key);
                if (water == null) continue;
                var rend = water.GetComponent<Renderer>();
                if (rend == null) continue;
                string matName = r.key == "wuhai" || r.key == "baita" ? "WaterIceStylized"
                    : r.key == "yongye" ? "WaterLakeStylized" : "WaterDeepStylized";
                var mat = Resources.Load<Material>("StylizedWater/" + matName);
                if (mat == null)
                {
                    Debug.LogWarning("[WorldMap] stylized water material not found: " + matName + " (keeping Standard water)");
                    continue;
                }
                rend.sharedMaterial = mat;
                var filter = water.GetComponent<MeshFilter>();
                if (filter != null)
                {
                    if (_subdividedPlaneMesh == null) _subdividedPlaneMesh = BuildSubdividedPlane(128, 72);
                    filter.sharedMesh = _subdividedPlaneMesh;
                }
                Debug.Log("[WorldMap] stylized water applied: Water_" + r.key + " -> " + matName);
            }
        }

        // 与 Unity Plane 原语同尺寸（10×10 m，中心原点，朝上），仅细分更多。
        private static Mesh BuildSubdividedPlane(int segX, int segZ)
        {
            int vx = segX + 1, vz = segZ + 1;
            var verts = new Vector3[vx * vz];
            var uvs = new Vector2[vx * vz];
            var normals = new Vector3[vx * vz];
            for (int z = 0; z < vz; z++)
                for (int x = 0; x < vx; x++)
                {
                    int i = z * vx + x;
                    verts[i] = new Vector3(x / (float)segX * 10f - 5f, 0f, z / (float)segZ * 10f - 5f);
                    uvs[i] = new Vector2(x / (float)segX, z / (float)segZ);
                    normals[i] = Vector3.up;
                }
            var tris = new int[segX * segZ * 6];
            int t = 0;
            for (int z = 0; z < segZ; z++)
                for (int x = 0; x < segX; x++)
                {
                    int a = z * vx + x, b = a + 1, c = a + vx, d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            var mesh = new Mesh { name = "SubdividedPlane128x72", vertices = verts, uv = uvs, normals = normals, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
