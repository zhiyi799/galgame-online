// WorldMapBuilder — Editor tool that builds the WorldMap scene from the
// pre-generated 513x513 RAW heightmaps (stage 1-2 output, see
// docs/world-map-final/高度图生成记录.md). Run once from the menu
// (GalgameOnline/Build WorldMap Scene) or in batchmode:
//   -executeMethod GalgameOnline.Editor.WorldMapBuilder.BuildWorldMapScene
//
// Pipeline (no re-doing stage 1-2):
//   1. Read each *_513.raw (uint16 LE, row 0 = south = z=0) into metres.
//   2. Blend shared tile edges toward the mean of the two edge profiles
//      over a ramp band, so neighbouring tiles meet with no cliff.
//      Layout is the contiguous half-offset layout (see WorldMapTerrain):
//      xinghuan (0,0), wuhai (-1000,1138), baita (1000,1138),
//      yongye (0,-1138), skyvault (2000,-1138).
//   3. Create TerrainData assets (513 heightmap, 2000 x maxH x 1138 m),
//      paint splatmaps by height / slope / region rules.
//   4. Create terrain GameObjects, water planes, world base plane, sun,
//      camera, player capsule, HUD + bootstrap; save WorldMap.unity.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GalgameOnline.Editor
{
    public static class WorldMapBuilder
    {
        private const int N = 513;               // heightmap grid
        private const float TileW = 2000f;       // metres (x)
        private const float TileD = 1138f;       // metres (z), aspect 1.757
        private const int AlphaRes = 256;

        private struct RegionSpec
        {
            public string key, name, rawFile;
            public float maxHeight, originX, originZ, waterLevel;
            // rawMaxHeight: the max the RAW heightmap was encoded against
            // (metres). MUST stay equal to the heightmap-generation max_m,
            // otherwise the decoded metre heights change. maxHeight is the
            // TerrainData vertical scale and may be larger than rawMaxHeight
            // to give seam-blend headroom (see P1-D fix 2026-11-18: the
            // yongye/skyvault blend target reaches 570 m but yongye's RAW
            // max is 300 m — using maxHeight to decode doubled the terrain).
            public float rawMaxHeight;
            public Color waterColor;
            public float snowLine;      // normalised height where snow starts
            public int baseLayer;       // index into LayerDefs
            public float accentU, accentV, accentRadius; // local 0..1, metres; -1 = none
            public int accentLayer;
            public float shoreH;        // below this height paint sand; -1 = none
        }

        // Layers: 0 Snow, 1 Rock, 2 Grass, 3 Forest, 4 Stone, 5 Industrial, 6 Sand, 7 Sakura
        private static readonly (string name, Color color)[] LayerDefs = new[]
        {
            ("Snow",       new Color(0.914f, 0.929f, 0.961f)),
            ("Rock",       new Color(0.494f, 0.533f, 0.600f)),
            ("Grass",      new Color(0.361f, 0.478f, 0.329f)),
            ("Forest",     new Color(0.239f, 0.353f, 0.243f)),
            ("Stone",      new Color(0.604f, 0.647f, 0.722f)),
            ("Industrial", new Color(0.420f, 0.463f, 0.525f)),
            ("Sand",       new Color(0.710f, 0.675f, 0.576f)),
            ("Sakura",     new Color(0.867f, 0.686f, 0.796f)),
        };

        private static readonly RegionSpec[] Regions = new[]
        {
            new RegionSpec { key = "xinghuan", name = "星环城", rawFile = "xinghuan_513.raw",
                maxHeight = 200f, rawMaxHeight = 100f, originX = 0f, originZ = 0f, waterLevel = 2f,
                waterColor = new Color(0.10f, 0.22f, 0.45f, 0.62f),
                snowLine = 0.475f, baseLayer = 2, accentU = 0.5f, accentV = 0.5f, accentRadius = 260f, accentLayer = 4, shoreH = 3f },
            new RegionSpec { key = "wuhai", name = "雾海边境", rawFile = "wuhai_513.raw",
                maxHeight = 600f, rawMaxHeight = 600f, originX = -1000f, originZ = 1138f, waterLevel = 2f,
                waterColor = new Color(0.62f, 0.74f, 0.82f, 0.72f),
                snowLine = 0.62f, baseLayer = 2, accentU = -1f, accentV = -1f, accentRadius = 0f, accentLayer = 4, shoreH = 4f },
            new RegionSpec { key = "baita", name = "白塔遗域", rawFile = "baita_513.raw",
                maxHeight = 420f, rawMaxHeight = 420f, originX = 1000f, originZ = 1138f, waterLevel = 2f,
                waterColor = new Color(0.62f, 0.74f, 0.82f, 0.72f),
                snowLine = 0.55f, baseLayer = 2, accentU = 0.5f, accentV = 0.5f, accentRadius = 220f, accentLayer = 4, shoreH = 4f },
            new RegionSpec { key = "skyvault", name = "苍穹工业区", rawFile = "skyvault_513.raw",
                maxHeight = 1400f, rawMaxHeight = 1400f, originX = 2000f, originZ = -1138f, waterLevel = 315f,
                waterColor = new Color(0.10f, 0.22f, 0.45f, 0.62f),
                snowLine = 0.80f, baseLayer = 5, accentU = 0.505f, accentV = 0.744f, accentRadius = 160f, accentLayer = 4, shoreH = -1f },
            new RegionSpec { key = "yongye", name = "永夜之森", rawFile = "yongye_513.raw",
                maxHeight = 600f, rawMaxHeight = 300f, originX = 0f, originZ = -1138f, waterLevel = 2f,
                waterColor = new Color(0.08f, 0.20f, 0.32f, 0.62f),
                snowLine = 0.475f, baseLayer = 3, accentU = 0.35f, accentV = 0.55f, accentRadius = 180f, accentLayer = 7, shoreH = 4f },
        };

        [MenuItem("GalgameOnline/Build WorldMap Scene")]
        public static void BuildWorldMapScene()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string rawDir = Path.GetFullPath(Path.Combine(projectRoot, "..", "tools", "heightmap", "out"));
            if (!Directory.Exists(rawDir))
                rawDir = Path.Combine(projectRoot, "tools", "heightmap", "out");
            Debug.Log("[WorldMap] RAW dir: " + rawDir);

            // 1. Load all RAWs into metres. heights[v,u]: v = z row (0 = south),
            //    u = x col (0 = west). RAW row 0 = south, so read rows directly.
            var grids = new Dictionary<string, float[,]>();
            foreach (var r in Regions)
            {
                string path = Path.Combine(rawDir, r.rawFile);
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length != N * N * 2)
                    throw new Exception("Unexpected RAW size for " + r.rawFile + ": " + bytes.Length);
                var g = new float[N, N];
                for (int v = 0; v < N; v++)
                    for (int u = 0; u < N; u++)
                    {
                        int i = (v * N + u) * 2;
                        int val = bytes[i] | (bytes[i + 1] << 8);
                        g[v, u] = val / 65535f * r.rawMaxHeight;
                    }
                grids[r.key] = g;
                Debug.Log($"[WorldMap] loaded {r.rawFile}: min={Min(g):F1} max={Max(g):F1} m");
            }

            // 2. Seam blending. Each seam blends both tiles' outer bands toward
            //    the mean of the two edge profiles, so the shared edge is exact.
            BlendSeam(grids["xinghuan"], "N", 0, grids["wuhai"], "S", 256, 257, 48);   // west half
            BlendSeam(grids["xinghuan"], "N", 256, grids["baita"], "S", 0, 257, 48);   // east half
            BlendSeam(grids["xinghuan"], "S", 0, grids["yongye"], "N", 0, 513, 48);
            BlendSeam(grids["yongye"], "E", 0, grids["skyvault"], "W", 0, 513, 96);    // wide: 120m vs 794m gap
            BlendSeam(grids["wuhai"], "E", 0, grids["baita"], "W", 0, 513, 48);

            // Triple junction (1000, 1138): Xinghuan NE corner of the north
            // edge meets Wuhai SE corner and Baita SW corner. Average the
            // three corner samples so the point is exact.
            float t = (grids["xinghuan"][N - 1, 256] + grids["wuhai"][0, N - 1] + grids["baita"][0, 0]) / 3f;
            grids["xinghuan"][N - 1, 256] = t;
            grids["wuhai"][0, N - 1] = t;
            grids["baita"][0, 0] = t;
            Debug.Log($"[WorldMap] triple junction (1000,1138) fixed to {t:F1} m");

            // Triple junction (2000, 0): Xinghuan SE corner meets Yongye NE
            // corner and Skyvault NW corner. Without this the pairwise seam
            // blends leave a ~340 m step here (verified 2026-11-18, P1-D);
            // average the three corner samples so the point is exact.
            float t2 = (grids["xinghuan"][0, N - 1] + grids["yongye"][N - 1, N - 1] + grids["skyvault"][N - 1, 0]) / 3f;
            grids["xinghuan"][0, N - 1] = t2;
            grids["yongye"][N - 1, N - 1] = t2;
            grids["skyvault"][N - 1, 0] = t2;
            Debug.Log($"[WorldMap] triple junction (2000,0) fixed to {t2:F1} m");

            foreach (var r in Regions)
                LogEdgeMeans(r.key, grids[r.key]);

            // 3. Terrain layers (shared).
            TerrainLayer[] layers = BuildLayers();

            // 4. TerrainData assets + splatmaps.
            var terrainDatas = new Dictionary<string, TerrainData>();
            foreach (var r in Regions)
            {
                var td = new TerrainData();
                td.heightmapResolution = N;
                td.size = new Vector3(TileW, r.maxHeight, TileD);
                var norm = new float[N, N];
                var g = grids[r.key];
                for (int v = 0; v < N; v++)
                    for (int u = 0; u < N; u++)
                        norm[v, u] = g[v, u] / r.maxHeight;
                td.SetHeights(0, 0, norm);
                td.alphamapResolution = AlphaRes;
                td.terrainLayers = layers;
                var alphas = PaintSplatmap(r, g);
                td.SetAlphamaps(0, 0, alphas);
                string assetPath = "Assets/WorldMap/TerrainData/" + r.key + ".asset";
                Directory.CreateDirectory("Assets/WorldMap/TerrainData");
                var existing = AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath);
                if (existing != null) AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.CreateAsset(td, assetPath);
                // NOTE (2026-11-18): SetAlphamaps on a fresh (not yet saved)
                // TerrainData does NOT survive AssetDatabase.CreateAsset —
                // the saved asset comes back with the default all-layer-0
                // alphamap, so every terrain rendered all-snow (verified by
                // editor dump: L0=4096/4096 sampled cells on all 5 regions;
                // screenshots showed 0% green pixels). Re-apply after the
                // asset exists and save explicitly; verified to persist.
                AssetDatabase.SaveAssets();
                td.SetAlphamaps(0, 0, alphas);
                EditorUtility.SetDirty(td);
                AssetDatabase.SaveAssets();
                terrainDatas[r.key] = td;
                Debug.Log("[WorldMap] TerrainData saved: " + assetPath);
            }

            // 5. Materials.
            Material waterDeep = BuildWaterMaterial("WaterDeep", new Color(0.10f, 0.22f, 0.45f, 0.62f));
            Material waterIce = BuildWaterMaterial("WaterIce", new Color(0.62f, 0.74f, 0.82f, 0.72f));
            Material waterLake = BuildWaterMaterial("WaterLake", new Color(0.08f, 0.20f, 0.32f, 0.62f));
            Material baseMat = BuildOpaqueMaterial("WorldBase", new Color(0.055f, 0.118f, 0.271f));
            // 建模重做 Item 1：玩家胶囊用 toon 材质（群青 + 描边）；shader 缺失时回退 Standard。
            Material playerMat = BuildToonMaterial("WorldMapPlayer", new Color(0.141f, 0.302f, 0.671f));
            if (playerMat == null)
            {
                playerMat = BuildOpaqueMaterial("WorldMapPlayer", new Color(0.141f, 0.302f, 0.671f));
                playerMat.SetFloat("_Glossiness", 0.4f);
            }

            // Terrain material: the built player in this project does not
            // include the default terrain shader (Nature/Terrain/Standard),
            // so terrains render solid magenta without it — same class of
            // bug as the 2026-11-16 UI magenta issue (UI/Default missing).
            // Bake an explicit material referencing the Editor's built-in
            // terrain shader and assign it as materialTemplate, so the
            // build includes the shader.
            Material terrainMat = BuildTerrainMaterial();

            // 6. Scene.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "WorldMap";

            // Render settings: night-ultramarine world, linear fog.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 600f;
            RenderSettings.fogEndDistance = 5000f;
            RenderSettings.fogColor = new Color(0.141f, 0.212f, 0.373f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.165f, 0.247f, 0.431f);
            RenderSettings.ambientEquatorColor = new Color(0.102f, 0.173f, 0.322f);
            RenderSettings.ambientGroundColor = new Color(0.055f, 0.102f, 0.204f);
            RenderSettings.ambientIntensity = 1f;

            var bootstrap = new GameObject("Bootstrap");
            bootstrap.AddComponent<GalgameOnline.Core.ScreenBootstrap>();
            var worldRoot = new GameObject("WorldMap");
            worldRoot.AddComponent<GalgameOnline.WorldMap.WorldMapBootstrap>();

            // Sun (moonlight).
            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.87f, 0.90f, 0.96f);
            light.intensity = 1.05f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Camera.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.086f, 0.161f, 0.310f);
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 12000f;
            camGo.AddComponent<AudioListener>();

            // Player capsule at the Xinghuan central plaza (height snapped
            // again at runtime by WorldMapBootstrap).
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(1000f, 100f, 569f);
            player.GetComponent<Renderer>().sharedMaterial = playerMat;
            player.AddComponent<GalgameOnline.WorldMap.WorldMapPlayer>();

            var hud = new GameObject("HUD");
            hud.AddComponent<GalgameOnline.WorldMap.WorldMapHud>();

            // Terrains.
            foreach (var r in Regions)
            {
                var go = new GameObject("Terrain_" + r.key);
                go.transform.position = new Vector3(r.originX, 0f, r.originZ);
                var terrain = go.AddComponent<Terrain>();
                terrain.terrainData = terrainDatas[r.key];
                if (terrainMat != null) terrain.materialTemplate = terrainMat;
                terrain.heightmapPixelError = 5f;
                terrain.basemapDistance = 1500f;
                var col = go.AddComponent<TerrainCollider>();
                col.terrainData = terrainDatas[r.key];
            }

            // Water planes (one per tile, at the region's low-water level;
            // the plane is hidden wherever terrain rises above it).
            foreach (var r in Regions)
            {
                var w = GameObject.CreatePrimitive(PrimitiveType.Plane);
                w.name = "Water_" + r.key;
                w.transform.position = new Vector3(r.originX + TileW / 2f, r.waterLevel, r.originZ + TileD / 2f);
                w.transform.localScale = new Vector3(TileW / 10f, 1f, TileD / 10f);
                Material m = r.key == "wuhai" || r.key == "baita" ? waterIce
                    : r.key == "yongye" ? waterLake : waterDeep;
                w.GetComponent<Renderer>().sharedMaterial = m;
                // Water needs no collider.
                UnityEngine.Object.DestroyImmediate(w.GetComponent<Collider>());
            }

            // World base plane under everything (y = -10) so the void never shows.
            var baseGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            baseGo.name = "WorldBase";
            baseGo.transform.position = new Vector3(1000f, -10f, 569f);
            baseGo.transform.localScale = new Vector3(900f, 1f, 600f);
            baseGo.GetComponent<Renderer>().sharedMaterial = baseMat;
            UnityEngine.Object.DestroyImmediate(baseGo.GetComponent<Collider>());

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/WorldMap.unity");
            Debug.Log("[WorldMap] scene saved: Assets/Scenes/WorldMap.unity");

            // Register the scene in the build settings.
            var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            string scenePath = "Assets/Scenes/WorldMap.unity";
            bool found = false;
            foreach (var s in buildScenes) if (s.path == scenePath) found = true;
            if (!found)
            {
                buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[WorldMap] BUILD COMPLETE — 5 terrains, 5 water planes, player, HUD.");
        }

        // ---- seam blending ----

        // Blends the outer `band` rows/cols of two tiles sharing an edge
        // toward the mean of their edge profiles. Edge ids: N (v=512), S
        // (v=0), E (u=512), W (u=0). The shared run maps aStart+i on tile A
        // to bStart+i on tile B (i = 0..count-1).
        private static void BlendSeam(float[,] a, string edgeA, int aStart,
            float[,] b, string edgeB, int bStart, int count, int band)
        {
            float[] pa = new float[count];
            float[] pb = new float[count];
            for (int i = 0; i < count; i++)
            {
                pa[i] = EdgeValue(a, edgeA, aStart + i);
                pb[i] = EdgeValue(b, edgeB, bStart + i);
            }
            float[] target = new float[count];
            for (int i = 0; i < count; i++) target[i] = (pa[i] + pb[i]) * 0.5f;
            // Box-blur the target profile so a segment junction (e.g. where
            // Xinghuan's north edge switches from Wuhai to Baita) has no kink.
            target = BoxBlur(target, 6);

            ApplyBlend(a, edgeA, aStart, count, band, target);
            ApplyBlend(b, edgeB, bStart, count, band, target);
            Debug.Log($"[WorldMap] seam {edgeA}/{edgeB} blended: count={count} band={band} " +
                $"target range {Min(target):F1}–{Max(target):F1} m");
        }

        private static float EdgeValue(float[,] g, string edge, int i)
        {
            switch (edge)
            {
                case "N": return g[N - 1, i];
                case "S": return g[0, i];
                case "E": return g[i, N - 1];
                default: return g[i, 0]; // W
            }
        }

        private static void ApplyBlend(float[,] g, string edge, int start, int count, int band, float[] target)
        {
            for (int i = 0; i < count; i++)
            {
                int idx = start + i;
                for (int r = 0; r < band; r++)
                {
                    // t = 1 at the edge, 0 at the inner end of the band.
                    float t = 1f - Smoothstep(0f, 1f, r / (float)band);
                    switch (edge)
                    {
                        case "N": g[N - 1 - r, idx] = Mathf.Lerp(g[N - 1 - r, idx], target[i], t); break;
                        case "S": g[r, idx] = Mathf.Lerp(g[r, idx], target[i], t); break;
                        case "E": g[idx, N - 1 - r] = Mathf.Lerp(g[idx, N - 1 - r], target[i], t); break;
                        default: g[idx, r] = Mathf.Lerp(g[idx, r], target[i], t); break; // W
                    }
                }
            }
        }

        private static float[] BoxBlur(float[] v, int radius)
        {
            int n = v.Length;
            var outV = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0f; int c = 0;
                for (int j = Mathf.Max(0, i - radius); j <= Mathf.Min(n - 1, i + radius); j++) { sum += v[j]; c++; }
                outV[i] = sum / c;
            }
            return outV;
        }

        private static float Smoothstep(float a, float b, float x)
        {
            x = Mathf.Clamp01((x - a) / (b - a));
            return x * x * (3f - 2f * x);
        }

        // ---- splatmap painting ----

        private static float[,,] PaintSplatmap(RegionSpec r, float[,] g)
        {
            int L = LayerDefs.Length;
            var alpha = new float[AlphaRes, AlphaRes, L];
            for (int az = 0; az < AlphaRes; az++)
            {
                for (int ax = 0; ax < AlphaRes; ax++)
                {
                    // Alphamap [row=z, col=x], row 0 = south, like heights.
                    float gx = ax / (float)(AlphaRes - 1) * (N - 1);
                    float gz = az / (float)(AlphaRes - 1) * (N - 1);
                    float h = SampleBilinear(g, gx, gz);
                    float h01 = h / r.maxHeight;

                    // Slope from finite differences (2 cells each way).
                    float hx1 = SampleBilinear(g, Mathf.Clamp(gx - 2, 0, N - 1), gz);
                    float hx2 = SampleBilinear(g, Mathf.Clamp(gx + 2, 0, N - 1), gz);
                    float hz1 = SampleBilinear(g, gx, Mathf.Clamp(gz - 2, 0, N - 1));
                    float hz2 = SampleBilinear(g, gx, Mathf.Clamp(gz + 2, 0, N - 1));
                    float cellX = TileW / (N - 1);   // ~3.91 m
                    float cellZ = TileD / (N - 1);   // ~2.22 m
                    float sx = (hx2 - hx1) / (4f * cellX);
                    float sz = (hz2 - hz1) / (4f * cellZ);
                    float slopeDeg = Mathf.Atan(Mathf.Sqrt(sx * sx + sz * sz)) * Mathf.Rad2Deg;

                    float[] w = new float[L];
                    w[r.baseLayer] = 1f;
                    w[1] = Smoothstep(35f, 55f, slopeDeg) * 2f;                    // rock on steep
                    w[0] = Smoothstep(r.snowLine, r.snowLine + 0.12f, h01) * 2f;   // snow up high
                    if (r.accentU >= 0f)
                    {
                        float dx = (ax / (float)(AlphaRes - 1) - r.accentU) * TileW;
                        float dz = (az / (float)(AlphaRes - 1) - r.accentV) * TileD;
                        float d = Mathf.Sqrt(dx * dx + dz * dz);
                        w[r.accentLayer] = (1f - Smoothstep(r.accentRadius * 0.5f, r.accentRadius, d)) * 2f;
                    }
                    if (r.shoreH >= 0f)
                        w[6] = (1f - Smoothstep(r.shoreH * 0.3f, r.shoreH, h)) * 1.5f; // sand at waterline

                    float sum = 0f;
                    for (int k = 0; k < L; k++) sum += w[k];
                    if (sum < 0.001f) sum = 1f;
                    for (int k = 0; k < L; k++) alpha[az, ax, k] = w[k] / sum;
                }
            }
            return alpha;
        }

        private static float SampleBilinear(float[,] g, float gx, float gz)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(gx), 0, N - 2);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(gz), 0, N - 2);
            float fx = Mathf.Clamp01(gx - x0);
            float fz = Mathf.Clamp01(gz - z0);
            float a = Mathf.Lerp(g[z0, x0], g[z0, x0 + 1], fx);
            float b = Mathf.Lerp(g[z0 + 1, x0], g[z0 + 1, x0 + 1], fx);
            return Mathf.Lerp(a, b, fz);
        }

        // ---- layers / materials ----

        private static TerrainLayer[] BuildLayers()
        {
            Directory.CreateDirectory("Assets/WorldMap/Textures");
            Directory.CreateDirectory("Assets/WorldMap/Layers");
            var layers = new TerrainLayer[LayerDefs.Length];
            var rng = new System.Random(1234);
            for (int i = 0; i < LayerDefs.Length; i++)
            {
                // 128x128 noise texture: base colour +/-6% value + speckles.
                var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                Color c = LayerDefs[i].color;
                for (int y = 0; y < 128; y++)
                    for (int x = 0; x < 128; x++)
                    {
                        float n = 0.94f + (float)rng.NextDouble() * 0.12f;
                        if (rng.NextDouble() < 0.02f) n *= 0.82f; // speckle
                        tex.SetPixel(x, y, new Color(c.r * n, c.g * n, c.b * n, 1f));
                    }
                tex.Apply();
                string texPath = "Assets/WorldMap/Textures/layer_" + LayerDefs[i].name.ToLower() + ".png";
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(texPath);
                var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

                var layer = new TerrainLayer
                {
                    diffuseTexture = imported,
                    tileSize = new Vector2(48f, 48f),
                    tileOffset = Vector2.zero,
                };
                string layerPath = "Assets/WorldMap/Layers/" + LayerDefs[i].name + ".terrainlayer";
                var existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
                if (existing != null) AssetDatabase.DeleteAsset(layerPath);
                AssetDatabase.CreateAsset(layer, layerPath);
                layers[i] = layer;
            }
            AssetDatabase.SaveAssets();
            return layers;
        }

        // 建模重做 Item 1：toon 玩家材质（与 ToonMaterialBuilder 同款默认值）。
        // shader 缺失时返回 null，调用方回退 Standard。
        private static Material BuildToonMaterial(string name, Color color)
        {
            var shader = Shader.Find("AstralBloom/Toon");
            if (shader == null) return null;
            Directory.CreateDirectory("Assets/WorldMap/Materials");
            var mat = new Material(shader);
            mat.SetColor("_Color", color);
            mat.SetColor("_LightColor", new Color(0.90f, 0.93f, 1.0f, 1f));
            mat.SetColor("_ShadowColor", new Color(0.10f, 0.16f, 0.32f, 1f));
            mat.SetFloat("_ShadowThreshold", 0.5f);
            mat.SetFloat("_ShadowSmoothness", 0.05f);
            mat.SetFloat("_ShadowSteps", 3f);
            mat.SetFloat("_OutlineWidth", 0.02f);
            mat.SetColor("_RimColor", new Color(0.87f, 0.69f, 0.80f, 1f));
            mat.SetFloat("_RimStrength", 0.5f);
            string path = "Assets/WorldMap/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material BuildWaterMaterial(string name, Color color)
        {
            Directory.CreateDirectory("Assets/WorldMap/Materials");
            var mat = new Material(Shader.Find("Standard"));
            mat.SetFloat("_Mode", 3f); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            mat.color = color;
            mat.SetFloat("_Glossiness", 0.1f);
            mat.SetFloat("_Metallic", 0f);
            string path = "Assets/WorldMap/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material BuildTerrainMaterial()
        {
            Directory.CreateDirectory("Assets/WorldMap/Materials");
            var shader = Shader.Find("Nature/Terrain/Standard");
            if (shader == null)
            {
                Debug.LogError("[WorldMap] Nature/Terrain/Standard shader not found in Editor.");
                return null;
            }
            var mat = new Material(shader);
            string path = "Assets/WorldMap/Materials/TerrainStandard.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            Debug.Log("[WorldMap] terrain material saved: " + path);
            return mat;
        }

        private static Material BuildOpaqueMaterial(string name, Color color)
        {
            Directory.CreateDirectory("Assets/WorldMap/Materials");
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetFloat("_Glossiness", 0.2f);
            mat.SetFloat("_Metallic", 0f);
            string path = "Assets/WorldMap/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ---- diagnostics ----

        private static float Min(float[,] g)
        {
            float m = float.MaxValue;
            foreach (float v in g) if (v < m) m = v;
            return m;
        }

        private static float Max(float[,] g)
        {
            float m = float.MinValue;
            foreach (float v in g) if (v > m) m = v;
            return m;
        }

        private static float Min(float[] v)
        {
            float m = float.MaxValue;
            foreach (float x in v) if (x < m) m = x;
            return m;
        }

        private static float Max(float[] v)
        {
            float m = float.MinValue;
            foreach (float x in v) if (x > m) m = x;
            return m;
        }

        private static void LogEdgeMeans(string key, float[,] g)
        {
            float n = 0, s = 0, w = 0, e = 0;
            for (int i = 0; i < N; i++)
            {
                n += g[N - 1, i]; s += g[0, i]; w += g[i, 0]; e += g[i, N - 1];
            }
            Debug.Log($"[WorldMap] {key} edges post-blend (m): N={n / N:F1} S={s / N:F1} W={w / N:F1} E={e / N:F1}");
        }
    }
}
