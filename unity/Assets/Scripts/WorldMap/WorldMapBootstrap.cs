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
    }
}
