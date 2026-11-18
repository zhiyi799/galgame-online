// NavMeshCheck — P1-D：WorldMap 逐块 NavMesh 烘焙覆盖检查（编辑器批处理用）。
// 用法（batchmode）：-executeMethod GalgameOnline.Editor.NavMeshCheck.BakeAndReport
// 只报告，不保存任何资产。5 块地形各烘一块（整块大陆一次烘焙的体素量
// 不现实）；agentRadius 1.0 的粗粒度，用于“哪些区域可走”的覆盖判断，
// 不是最终寻路精度。烘焙超时由 shell 的 timeout 兜底。
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using GalgameOnline.WorldMap;

namespace GalgameOnline.Editor
{
    public static class NavMeshCheck
    {
        [MenuItem("GalgameOnline/NavMesh Check (WorldMap)")]
        public static void BakeAndReport()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/WorldMap.unity");
            WorldMapTerrain.Register();
            var terrains = Object.FindObjectsOfType<Terrain>();
            Debug.Log("[NavMesh] terrains=" + terrains.Length);
            if (terrains.Length != 5)
            {
                Debug.Log("[NavMesh] FAIL: expected 5 terrains");
                return;
            }

            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 1f;
            settings.agentHeight = 2f;
            settings.agentSlope = 45f;
            settings.agentClimb = 0.9f;

            var instances = new List<NavMeshDataInstance>();
            foreach (var t in terrains)
            {
                var src = new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Terrain,
                    sourceObject = t.terrainData,
                    transform = t.transform.localToWorldMatrix,
                    area = 0
                };
                Vector3 size = t.terrainData.size;
                var bounds = new Bounds(
                    t.transform.position + new Vector3(size.x * 0.5f, size.y * 0.5f, size.z * 0.5f),
                    new Vector3(size.x, size.y + 20f, size.z));
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var data = NavMeshBuilder.BuildNavMeshData(settings,
                    new List<NavMeshBuildSource> { src }, bounds,
                    Vector3.zero, Quaternion.identity);
                sw.Stop();
                if (data == null)
                {
                    Debug.Log("[NavMesh] FAIL: bake returned null for " + t.name);
                    continue;
                }
                instances.Add(NavMesh.AddNavMeshData(data));
                Debug.Log("[NavMesh] baked " + t.name + " in " +
                    sw.Elapsed.TotalSeconds.ToString("F1") + "s");
            }

            // 路线覆盖率：4 条主干道每 25 m 采样，SamplePosition 5 m 内有否
            var routes = new (string, Vector3, Vector3)[]
            {
                ("north-baita", new Vector3(1000f, 0f, 569f), new Vector3(2000f, 0f, 1707f)),
                ("west-wuhai", new Vector3(1000f, 0f, 569f), new Vector3(0f, 0f, 1707f)),
                ("south-yongye", new Vector3(1000f, 0f, 569f), new Vector3(1000f, 0f, -569f)),
                ("east-skyvault", new Vector3(1000f, 0f, 569f), new Vector3(3000f, 0f, -569f)),
            };
            foreach (var route in routes)
            {
                float len = Vector3.Distance(route.Item2, route.Item3);
                int n = Mathf.CeilToInt(len / 25f);
                int covered = 0;
                for (int i = 0; i <= n; i++)
                {
                    Vector3 p = Vector3.Lerp(route.Item2, route.Item3, (float)i / n);
                    float h = WorldMapTerrain.SampleHeight(p.x, p.z);
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(new Vector3(p.x, h + 1f, p.z), out hit, 5f, NavMesh.AllAreas))
                        covered++;
                }
                Debug.Log("[NavMesh] coverage " + route.Item1 + ": " + covered + "/" + (n + 1) +
                    " (" + (100f * covered / (n + 1)).ToString("F0") + "%)");
            }

            foreach (var inst in instances) inst.Remove();
            Debug.Log("[NavMesh] done");
        }
    }
}
