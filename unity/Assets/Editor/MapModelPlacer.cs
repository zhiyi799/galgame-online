// MapModelPlacer — one-shot Editor tool for the map-model placement.
// Imports the 18 CC0/CC-BY map models as prefabs, places them in the
// WorldMap scene per docs/地图模型摆放计划.md, applying the five user
// defaults (2026-11-28 "继续"): ruins-pack split into single pieces,
// bridge as two broken ends, sakura tree thickened + stone platform,
// README recolours (red pagoda -> white/silver, spires -> gold,
// cooling tower -> pale blue-grey), bonsai once as-is.
// Textures: the GLB->FBX conversion lost the texture links, so the
// embedded PNGs were extracted (Assets/Models/*/texture.png, pagoda red
// repainted white, temple terracotta repainted gold) and are re-attached
// here by cloning each model's imported material and setting mainTexture.
// Run: Unity -batchmode -quit -projectPath <proj>
//   -executeMethod GalgameOnline.Editor.MapModelPlacer.Place
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GalgameOnline.WorldMap;

namespace GalgameOnline.Editor
{
    public static class MapModelPlacer
    {
        private const string ModelRoot = "Assets/Models";
        private const string OutRoot = "Assets/Models/Map";
        private static readonly System.Random Rng = new System.Random(20261128);
        private static readonly List<string> PlacedLog = new List<string>();

        private struct ModelDef
        {
            public string key;      // short key, also prefab/material name
            public string rel;      // folder under Assets/Models
            public bool hasTexture; // texture.png present
        }

        private static readonly ModelDef[] Models =
        {
            new ModelDef { key = "PagodaRed5",    rel = "baita-tower/pagoda-red-5tier",    hasTexture = true },
            new ModelDef { key = "PagodaWhite3",  rel = "baita-tower/pagoda-white-3tier",  hasTexture = true },
            new ModelDef { key = "Column",        rel = "baita-tower/column",              hasTexture = true },
            new ModelDef { key = "BrokenPillar",  rel = "baita-tower/broken-pillar",       hasTexture = true },
            new ModelDef { key = "RuinsPack",     rel = "dressing/ruins-pack",             hasTexture = true },
            new ModelDef { key = "Crystal",       rel = "crystal-tower/crystal-cluster",   hasTexture = true },
            new ModelDef { key = "CoolingTower",  rel = "crystal-tower/cooling-tower",     hasTexture = false },
            new ModelDef { key = "SolarPanel",    rel = "crystal-tower/solar-panel",       hasTexture = true },
            new ModelDef { key = "Bridge",        rel = "wuhai-bridge/stone-arch-bridge",  hasTexture = true },
            new ModelDef { key = "CliffOutcrop",  rel = "wuhai-bridge/cliff-outcrop",      hasTexture = true },
            new ModelDef { key = "Rocks",         rel = "dressing/rocks",                 hasTexture = false },
            new ModelDef { key = "CherryTree",    rel = "sakura-tree/cherry-tree",         hasTexture = true },
            new ModelDef { key = "Bonsai",        rel = "sakura-tree/sakura-bonsai",       hasTexture = true },
            new ModelDef { key = "PinkCrystal",   rel = "sakura-tree/pink-crystal",        hasTexture = true },
            new ModelDef { key = "PineCluster",   rel = "dressing/pine-cluster",          hasTexture = true },
            new ModelDef { key = "PineSingle",    rel = "dressing/pine-single",           hasTexture = true },
            new ModelDef { key = "TempleSpires",  rel = "xinghuan-temple/temple-spires",   hasTexture = true },
            new ModelDef { key = "Fountain",      rel = "xinghuan-temple/plaza-fountain",  hasTexture = true },
        };

        // Ruins-pack pieces to split out (single pieces only, never the whole pack).
        private static readonly string[] RuinsPieces =
        {
            "Arch_Gothic", "Arch_Round", "Wall", "Wall_Broken", "Wall_Half",
            "Wall_Hole", "Column_Round", "Column_Square", "Stairs",
            "Doors_GothicArch_L", "Doors_GothicArch_R", "Floor_SquareLarge",
            "Statue_Fox", "Statue_Stag", "BridgeSection", "DeadTree_1",
            "Support_Tall", "Window_Open",
        };

        private static Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
        private static Dictionary<string, Material> _mats = new Dictionary<string, Material>();
        private static Dictionary<string, GameObject> _regionRoots = new Dictionary<string, GameObject>();

        public static void Place()
        {
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene("Assets/Scenes/WorldMap.unity");
            // Re-runs must not duplicate: remove the previous placement root.
            var old = GameObject.Find("MapModels");
            if (old != null) Object.DestroyImmediate(old);
            PlacedLog.Clear();
            BuildMaterials();
            BuildPrefabs();
            BuildRegionRoots();

            PlaceBaita();
            PlaceSkyvault();
            PlaceYongye();
            PlaceWuhai();
            PlaceXinghuan();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[PLACE] DONE — placed " + PlacedLog.Count + " instances");
            foreach (var line in PlacedLog) Debug.Log("[PLACE] " + line);
        }

        // ---------- materials ----------
        private static void BuildMaterials()
        {
            System.IO.Directory.CreateDirectory(OutRoot + "/Materials");
            foreach (var m in Models)
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelRoot + "/" + m.rel + "/model.fbx");
                if (fbx == null) { Debug.LogError("[PLACE] FBX missing: " + m.rel); continue; }
                var tmp = Object.Instantiate(fbx);
                var tex = m.hasTexture
                    ? AssetDatabase.LoadAssetAtPath<Texture2D>(ModelRoot + "/" + m.rel + "/texture.png")
                    : null;
                if (m.hasTexture && tex == null)
                    Debug.LogWarning("[PLACE] texture.png not imported for " + m.rel);
                foreach (var r in tmp.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var src in r.sharedMaterials)
                    {
                        if (src == null) continue;
                        // Key by model+material: several models share the
                        // imported material name "Material_0" but need
                        // different textures (wrong texture if keyed by name).
                        string dictKey = m.key + "|" + src.name;
                        if (_mats.ContainsKey(dictKey)) continue;
                        string assetPath = OutRoot + "/Materials/" + m.key + "_" + Sanitize(src.name) + ".mat";
                        var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                        if (existing != null) { _mats[dictKey] = existing; continue; }
                        var mat = new Material(src);
                        if (tex != null) mat.mainTexture = tex;
                        // Recolour overrides (no texture to repaint for these two).
                        if (m.key == "CoolingTower")
                        {
                            mat.mainTexture = null;
                            mat.color = new Color(0.62f, 0.72f, 0.82f); // pale blue-grey
                        }
                        if (m.key == "Rocks")
                        {
                            mat.mainTexture = null;
                            mat.color = new Color(0.40f, 0.42f, 0.46f); // readable grey
                        }
                        if (m.key == "Crystal")
                        {
                            mat.EnableKeyword("_EMISSION");
                            mat.SetColor("_EmissionColor", new Color(0.10f, 0.45f, 1.0f));
                        }
                        AssetDatabase.CreateAsset(mat, assetPath);
                        _mats[dictKey] = mat;
                    }
                }
                Object.DestroyImmediate(tmp);
            }
            // Helper materials for primitives (trunk / platform / legs).
            MakeSimpleMat("StoneGrey", new Color(0.45f, 0.45f, 0.48f), 0.25f);
            MakeSimpleMat("TrunkBrown", new Color(0.23f, 0.15f, 0.10f), 0.1f);
            AssetDatabase.SaveAssets();
            Debug.Log("[PLACE] materials built: " + _mats.Count);
        }

        private static void MakeSimpleMat(string name, Color col, float smooth)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = col;
            mat.SetFloat("_Smoothness", smooth);
            AssetDatabase.CreateAsset(mat, OutRoot + "/Materials/" + name + ".mat");
            _mats[name] = mat;
        }

        private static string Sanitize(string s)
        {
            foreach (char c in new[] { ' ', '.', '/', '\\', ':' }) s = s.Replace(c, '_');
            return s;
        }

        // ---------- prefabs ----------
        private static void BuildPrefabs()
        {
            System.IO.Directory.CreateDirectory(OutRoot + "/Prefabs");
            foreach (var m in Models)
            {
                if (m.key == "RuinsPack") continue; // split below, never as a whole
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelRoot + "/" + m.rel + "/model.fbx");
                var inst = Object.Instantiate(fbx);
                ApplyMats(inst, m.key);
                // The FBX import stores tiny (cm-scale) vertices and
                // compensates with a root scale of 100; placing with a plain
                // localScale would shrink the model 100x. Bake the world
                // scale into cloned metre-scale meshes instead.
                NormalizeScale(inst, m.key);
                RecenterToOrigin(inst);
                string path = OutRoot + "/Prefabs/" + m.key + ".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(inst, path);
                _prefabs[m.key] = prefab;
                Object.DestroyImmediate(inst);
            }
            BuildRuinsPiecePrefabs();
            // Solar panel rig: panel tilted on two legs.
            BuildSolarRigPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[PLACE] prefabs built: " + _prefabs.Count);
        }

        private static void ApplyMats(GameObject root, string modelKey)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    if (shared[i] != null && _mats.TryGetValue(modelKey + "|" + shared[i].name, out var nm))
                        shared[i] = nm;
                }
                r.sharedMaterials = shared;
            }
        }

        // Bake each mesh's world (lossy) scale into cloned metre-scale
        // vertices, then reset all transform scales to 1 so the prefab
        // renders at true size with a plain localScale. lossyScale (not a
        // bounds ratio) is the reliable measure: the mesh is stored in
        // Blender Z-up orientation while the world is Y-up, so a bounds
        // ratio compares mismatched axes. The same Z-up storage means the
        // mesh height sits on local Z; bake a -90 deg X rotation into the
        // vertices ((x,y,z) -> (x,z,-y)) so height lands on world Y, and
        // reset transform rotations to identity (the import carries no
        // Y-up conversion rotation).
        private static void NormalizeScale(GameObject root, string modelKey)
        {
            System.IO.Directory.CreateDirectory(OutRoot + "/Meshes");
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                Vector3 s = mf.transform.lossyScale;
                if (s.x <= 1e-6f || s.y <= 1e-6f || s.z <= 1e-6f)
                {
                    Debug.LogWarning("[PLACE] bad lossyScale on " + mf.name + ": " + s);
                    continue;
                }
                var nm = Object.Instantiate(mesh);
                nm.name = mesh.name + "_m";
                var verts = nm.vertices;
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 v = verts[i];
                    // Scale (Blender axes) then Z-up -> Y-up rotation.
                    verts[i] = new Vector3(v.x * s.x, v.z * s.z, -v.y * s.y);
                }
                nm.vertices = verts;
                // Rotate normals/tangents with the same Z-up -> Y-up turn.
                var norms = nm.normals;
                if (norms != null && norms.Length == verts.Length)
                {
                    for (int i = 0; i < norms.Length; i++)
                    {
                        Vector3 n = norms[i];
                        norms[i] = new Vector3(n.x, n.z, -n.y).normalized;
                    }
                    nm.normals = norms;
                }
                var tans = nm.tangents;
                if (tans != null && tans.Length == verts.Length)
                {
                    for (int i = 0; i < tans.Length; i++)
                    {
                        Vector4 t = tans[i];
                        tans[i] = new Vector4(t.x, t.z, -t.y, t.w);
                    }
                    nm.tangents = tans;
                }
                nm.RecalculateBounds();
                string mpath = OutRoot + "/Meshes/" + modelKey + "_" + Sanitize(mesh.name) + ".asset";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(mpath) != null)
                    AssetDatabase.DeleteAsset(mpath);
                AssetDatabase.CreateAsset(nm, mpath);
                mf.sharedMesh = nm;
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                t.localScale = Vector3.one;
                t.localRotation = Quaternion.identity;
            }
            Debug.Log("[PLACE] normalized " + root.name);
        }

        private static void RecenterToOrigin(GameObject root)
        {
            var b = CombinedBounds(root);
            root.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
        }

        private static Bounds CombinedBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var b = new Bounds(root.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in renderers)
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private static void BuildRuinsPiecePrefabs()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelRoot + "/dressing/ruins-pack/model.fbx");
            var root = Object.Instantiate(fbx);
            var byName = new Dictionary<string, Transform>();
            foreach (Transform ch in root.transform) byName[ch.name] = ch;
            foreach (var name in RuinsPieces)
            {
                if (!byName.TryGetValue(name, out var child))
                {
                    Debug.LogWarning("[PLACE] ruins piece not found: " + name);
                    continue;
                }
                // Normalize in place (still a child of the FBX root, so
                // lossyScale includes the root import scale), then extract.
                NormalizeScale(child.gameObject, "Ruins_" + name);
                var piece = Object.Instantiate(child.gameObject);
                piece.name = name;
                ApplyMats(piece, "RuinsPack");
                RecenterToOrigin(piece);
                string path = OutRoot + "/Prefabs/Ruins_" + name + ".prefab";
                _prefabs["Ruins_" + name] = PrefabUtility.SaveAsPrefabAsset(piece, path);
                Object.DestroyImmediate(piece);
            }
            Object.DestroyImmediate(root);
        }

        private static void BuildSolarRigPrefab()
        {
            var rig = new GameObject("SolarPanelRig");
            var panel = Object.Instantiate(_prefabs["SolarPanel"]);
            panel.name = "Panel";
            panel.transform.SetParent(rig.transform, false);
            panel.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            panel.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            var legMat = _mats["StoneGrey"];
            foreach (float lx in new[] { -0.9f, 0.9f })
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "Leg";
                leg.transform.SetParent(rig.transform, false);
                leg.transform.localPosition = new Vector3(lx, 0.55f, 0f);
                leg.transform.localScale = new Vector3(0.12f, 1.1f, 0.12f);
                leg.GetComponent<Renderer>().sharedMaterial = legMat;
            }
            _prefabs["SolarRig"] = PrefabUtility.SaveAsPrefabAsset(rig, OutRoot + "/Prefabs/SolarRig.prefab");
            Object.DestroyImmediate(rig);
        }

        private static void BuildRegionRoots()
        {
            var root = new GameObject("MapModels");
            foreach (var name in new[] { "Xinghuan", "Baita", "Wuhai", "Yongye", "Skyvault" })
            {
                var g = new GameObject("MapModels_" + name);
                g.transform.SetParent(root.transform, false);
                _regionRoots[name] = g;
            }
        }

        // ---------- placement helpers ----------
        private static float H(float x, float z) { return WorldMapTerrain.SampleHeight(x, z); }

        private static GameObject Place(string prefabKey, string region, string name,
            float x, float z, float scale, float rotY, float yOff = 0f,
            float tiltX = 0f, float tiltZ = 0f)
        {
            var prefab = _prefabs[prefabKey];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetParent(_regionRoots[region].transform, false);
            go.transform.position = new Vector3(x, H(x, z) + yOff, z);
            go.transform.rotation = Quaternion.Euler(tiltX, rotY, tiltZ);
            go.transform.localScale = Vector3.one * scale;
            PlacedLog.Add(string.Format("{0} {1} pos=({2:F0},{3:F1},{4:F0}) scale={5} rotY={6}",
                region, name, x, go.transform.position.y, z, scale, rotY));
            return go;
        }

        private static Vector2 FindMaxInBox(float x0, float z0, float x1, float z1,
            float step, Vector2 exclude, float excludeR)
        {
            float best = float.MinValue; Vector2 bp = new Vector2((x0 + x1) / 2, (z0 + z1) / 2);
            for (float x = x0; x <= x1; x += step)
                for (float z = z0; z <= z1; z += step)
                {
                    if (excludeR > 0 && Vector2.Distance(new Vector2(x, z), exclude) < excludeR) continue;
                    float h = H(x, z);
                    if (h > best) { best = h; bp = new Vector2(x, z); }
                }
            Debug.Log("[PLACE] max in box (" + x0 + "," + z0 + ")-(" + x1 + "," + z1
                + ") = " + best.ToString("F1") + " at (" + bp.x.ToString("F0") + "," + bp.y.ToString("F0") + ")");
            return bp;
        }

        private static Vector2 RandDry(float x0, float z0, float x1, float z1,
            float minH, float maxH, Vector2 avoid, float avoidR, int tries = 60)
        {
            for (int i = 0; i < tries; i++)
            {
                float x = x0 + (float)Rng.NextDouble() * (x1 - x0);
                float z = z0 + (float)Rng.NextDouble() * (z1 - z0);
                float h = H(x, z);
                if (h < minH || h > maxH) continue;
                if (avoidR > 0 && Vector2.Distance(new Vector2(x, z), avoid) < avoidR) continue;
                return new Vector2(x, z);
            }
            return new Vector2((x0 + x1) / 2, (z0 + z1) / 2);
        }

        private static GameObject Primitive(string region, string name, PrimitiveType type,
            Vector3 pos, Vector3 scale, string matKey)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(_regionRoots[region].transform, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = _mats[matKey];
            PlacedLog.Add(string.Format("{0} {1} pos=({2:F0},{3:F1},{4:F0}) scale=({5:F1},{6:F1},{7:F1})",
                region, name, pos.x, pos.y, pos.z, scale.x, scale.y, scale.z));
            return go;
        }

        // ---------- regions ----------
        private static void PlaceBaita()
        {
            // Giant white pagoda on the highest ground (inset from tile edges).
            Vector2 top = FindMaxInBox(1300f, 1300f, 2900f, 2200f, 25f, Vector2.zero, 0f);
            Place("PagodaRed5", "Baita", "WhiteTower_Giant", top.x, top.y, 1f, 0f);
            // Secondary cream pagoda: highest point well away from the giant.
            Vector2 second = FindMaxInBox(1100f, 1200f, 2900f, 2200f, 25f, top, 400f);
            Place("PagodaWhite3", "Baita", "Pagoda_Secondary", second.x, second.y, 3f, 40f);
            // Ruin field around the giant pagoda base.
            for (int i = 0; i < 8; i++)
            {
                Vector2 p = RandDry(top.x - 180f, top.y - 180f, top.x + 180f, top.y + 180f,
                    5f, 400f, top, 45f);
                Place("Column", "Baita", "Column_Ruins_" + i, p.x, p.y, 2f, Rng.Next(0, 360));
            }
            for (int i = 0; i < 6; i++)
            {
                Vector2 p = RandDry(top.x - 220f, top.y - 220f, top.x + 220f, top.y + 220f,
                    5f, 400f, top, 60f);
                Place("BrokenPillar", "Baita", "BrokenPillar_" + i, p.x, p.y, 2f, Rng.Next(0, 360));
            }
            string[] ruins = { "Arch_Gothic", "Wall_Broken", "Wall_Half", "Column_Round", "Statue_Fox", "Stairs" };
            for (int i = 0; i < ruins.Length; i++)
            {
                Vector2 p = RandDry(top.x - 200f, top.y - 200f, top.x + 200f, top.y + 200f,
                    5f, 400f, top, 55f);
                Place("Ruins_" + ruins[i], "Baita", "Ruins_" + ruins[i], p.x, p.y, 1.5f, Rng.Next(0, 360));
            }
            for (int i = 0; i < 6; i++)
            {
                Vector2 p = RandDry(1100f, 1200f, 2900f, 2200f, 5f, 400f, top, 120f);
                Place("Rocks", "Baita", "Rock_" + i, p.x, p.y, 15f, Rng.Next(0, 360));
            }
        }

        private static void PlaceSkyvault()
        {
            // Energy core: prominent high spot near the tile centre.
            Vector2 core = FindMaxInBox(2700f, -850f, 3300f, -300f, 25f, Vector2.zero, 0f);
            Place("Crystal", "Skyvault", "EnergyCore_Crystal", core.x, core.y, 4f, 0f);
            // Cooling towers: two rows of 8 beside the core.
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 8; i++)
                {
                    float x = core.x - 160f + i * 45f;
                    float z = core.y + 70f + row * 55f;
                    Place("CoolingTower", "Skyvault", "CoolingTower_" + row + "_" + i,
                        x, z, 15f, Rng.Next(0, 360));
                }
            // Solar panels: two rows of 6 on the far side of the core.
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 6; i++)
                {
                    float x = core.x - 100f + i * 40f;
                    float z = core.y - 90f - row * 45f;
                    Place("SolarRig", "Skyvault", "SolarPanel_" + row + "_" + i,
                        x, z, 1f, Rng.Next(0, 360));
                }
        }

        private static void PlaceYongye()
        {
            // Giant sakura: dry ground near the lake (h<=2.5), 60-250 m from shore.
            Vector2 tree = FindLakeShoreSpot();
            float th = H(tree.x, tree.y);
            // Stone ruin platform + thick trunk cylinder, then the tree on top.
            Primitive("Yongye", "Sakura_Platform", PrimitiveType.Cylinder,
                new Vector3(tree.x, th + 0.6f, tree.y), new Vector3(32f, 1.2f, 32f), "StoneGrey");
            Primitive("Yongye", "Sakura_Trunk", PrimitiveType.Cylinder,
                new Vector3(tree.x, th + 10f, tree.y), new Vector3(6f, 20f, 6f), "TrunkBrown");
            Place("CherryTree", "Yongye", "Sakura_AncientTree", tree.x, tree.y, 4f, 0f);
            // Bonsai feature tree once, ~45 m away.
            Vector2 bonsai = RandDry(tree.x - 80f, tree.y - 80f, tree.x + 80f, tree.y + 80f,
                5f, 200f, tree, 40f);
            Place("Bonsai", "Yongye", "Sakura_Bonsai", bonsai.x, bonsai.y, 4f, 0f);
            // Pink crystals near the tree.
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = RandDry(tree.x - 100f, tree.y - 100f, tree.x + 100f, tree.y + 100f,
                    5f, 200f, tree, 25f);
                Place("PinkCrystal", "Yongye", "PinkCrystal_" + i, p.x, p.y, 4f, Rng.Next(0, 360));
            }
            // Forest mass: pine clusters + singles.
            for (int i = 0; i < 5; i++)
            {
                Vector2 p = RandDry(100f, -1050f, 1900f, -150f, 10f, 250f, tree, 70f);
                Place("PineCluster", "Yongye", "PineCluster_" + i, p.x, p.y, 5f, Rng.Next(0, 360));
            }
            for (int i = 0; i < 8; i++)
            {
                Vector2 p = RandDry(100f, -1050f, 1900f, -150f, 10f, 250f, tree, 60f);
                Place("PineSingle", "Yongye", "PineSingle_" + i, p.x, p.y, 1.5f, Rng.Next(0, 360));
            }
            // Forest ruins near the tree.
            string[] ruins = { "Arch_Round", "Wall_Hole", "Column_Square", "Statue_Stag" };
            for (int i = 0; i < ruins.Length; i++)
            {
                Vector2 p = RandDry(tree.x - 130f, tree.y - 130f, tree.x + 130f, tree.y + 130f,
                    5f, 200f, tree, 35f);
                Place("Ruins_" + ruins[i], "Yongye", "Ruins_" + ruins[i], p.x, p.y, 1.5f, Rng.Next(0, 360));
            }
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = RandDry(tree.x - 120f, tree.y - 120f, tree.x + 120f, tree.y + 120f,
                    5f, 200f, tree, 30f);
                Place("BrokenPillar", "Yongye", "BrokenPillar_" + i, p.x, p.y, 2f, Rng.Next(0, 360));
            }
        }

        private static Vector2 FindLakeShoreSpot()
        {
            // Lake cells: h <= 2.5. Pick a dry cell (5..150 m) 60-250 m from the
            // nearest lake cell, with the flattest 50 m neighbourhood.
            const float step = 25f;
            var lake = new List<Vector2>();
            for (float x = 100f; x <= 1900f; x += step)
                for (float z = -1050f; z <= -150f; z += step)
                    if (H(x, z) <= 2.5f) lake.Add(new Vector2(x, z));
            Debug.Log("[PLACE] yongye lake cells: " + lake.Count);
            float bestScore = float.MaxValue; Vector2 best = new Vector2(1400f, -700f);
            for (float x = 600f; x <= 1800f; x += step)
                for (float z = -1000f; z <= -400f; z += step)
                {
                    float h = H(x, z);
                    if (h < 5f || h > 150f) continue;
                    float dMin = float.MaxValue;
                    foreach (var l in lake)
                    {
                        float d = Vector2.Distance(new Vector2(x, z), l);
                        if (d < dMin) dMin = d;
                    }
                    if (dMin < 60f || dMin > 250f) continue;
                    float h00 = H(x - 25f, z), h10 = H(x + 25f, z);
                    float h01 = H(x, z - 25f), h11 = H(x, z + 25f);
                    float slope = Mathf.Max(Mathf.Abs(h00 - h), Mathf.Abs(h10 - h),
                        Mathf.Abs(h01 - h), Mathf.Abs(h11 - h));
                    float score = slope * 10f + Mathf.Abs(dMin - 150f);
                    if (score < bestScore) { bestScore = score; best = new Vector2(x, z); }
                }
            Debug.Log("[PLACE] sakura spot (" + best.x.ToString("F0") + "," + best.y.ToString("F0")
                + ") h=" + H(best.x, best.y).ToString("F1") + " score=" + bestScore.ToString("F1"));
            return best;
        }

        private static void PlaceWuhai()
        {
            // Canyon: deepest cell with steep walls on both sides (either axis).
            Vector2 canyon; bool spanX;
            FindCanyon(out canyon, out spanX);
            Debug.Log("[PLACE] canyon at (" + canyon.x.ToString("F0") + "," + canyon.y.ToString("F0")
                + ") h=" + H(canyon.x, canyon.y).ToString("F1") + " spanX=" + spanX);
            // Two broken ends: each bridge anchored on one side, inner end
            // tilted down toward the gap and sunk 1.5 m.
            if (spanX)
            {
                // Valley runs N-S; bridges span E-W (bridge length along X).
                Place("Bridge", "Wuhai", "Bridge_West", canyon.x - 22f, canyon.y, 1f, 0f, -1.5f, 0f, -12f);
                Place("Bridge", "Wuhai", "Bridge_East", canyon.x + 22f, canyon.y, 1f, 0f, -1.5f, 0f, 12f);
            }
            else
            {
                // Valley runs E-W; bridges span N-S (bridge length along Z).
                Place("Bridge", "Wuhai", "Bridge_North", canyon.x, canyon.y + 22f, 1f, 90f, -1.5f, 12f, 0f);
                Place("Bridge", "Wuhai", "Bridge_South", canyon.x, canyon.y - 22f, 1f, 90f, -1.5f, -12f, 0f);
            }
            // Cliff outcrops along the canyon walls.
            for (int i = 0; i < 8; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                float x = spanX ? canyon.x + side * (45f + Rng.Next(0, 60)) : canyon.x + (Rng.Next(-90, 90));
                float z = spanX ? canyon.y + (Rng.Next(-90, 90)) : canyon.y + side * (45f + Rng.Next(0, 60));
                Place("CliffOutcrop", "Wuhai", "Cliff_" + i, x, z, 8f, Rng.Next(0, 360));
            }
            for (int i = 0; i < 6; i++)
            {
                Vector2 p = RandDry(canyon.x - 150f, canyon.y - 150f, canyon.x + 150f, canyon.y + 150f,
                    5f, 500f, canyon, 40f);
                Place("Rocks", "Wuhai", "Rock_" + i, p.x, p.y, 15f, Rng.Next(0, 360));
            }
            // Cliff-settlement remnants: ruins wall fragments near the canyon.
            string[] use = { "Wall_Half", "Wall_Broken", "Column_Round" };
            for (int i = 0; i < use.Length; i++)
            {
                Vector2 p = RandDry(canyon.x - 120f, canyon.y - 120f, canyon.x + 120f, canyon.y + 120f,
                    5f, 500f, canyon, 45f);
                Place("Ruins_" + use[i], "Wuhai", "Ruins_" + use[i], p.x, p.y, 1.5f, Rng.Next(0, 360));
            }
        }

        private static void FindCanyon(out Vector2 centre, out bool spanX)
        {
            // Wuhai tile: x -1000..1000, z 1138..2276. Scan interior (inset 100 m).
            float bestDepth = 0f; centre = new Vector2(800f, 1241f); spanX = true;
            for (float x = -900f; x <= 900f; x += 25f)
                for (float z = 1238f; z <= 2176f; z += 25f)
                {
                    float h = H(x, z);
                    // Valley running N-S: low along X, high walls E + W.
                    float wL = H(x - 75f, z), wR = H(x + 75f, z);
                    float depthX = Mathf.Min(wL, wR) - h;
                    if (depthX > 60f && depthX > bestDepth)
                    { bestDepth = depthX; centre = new Vector2(x, z); spanX = true; }
                    // Valley running E-W: low along Z, high walls N + S.
                    float wN = H(x, z + 75f), wS = H(x, z - 75f);
                    float depthZ = Mathf.Min(wN, wS) - h;
                    if (depthZ > 60f && depthZ > bestDepth)
                    { bestDepth = depthZ; centre = new Vector2(x, z); spanX = false; }
                }
            Debug.Log("[PLACE] canyon depth=" + bestDepth.ToString("F1"));
        }

        private static void PlaceXinghuan()
        {
            // Golden spire landmark near the ring-city centre, clear of spawn (1000,569).
            Place("TempleSpires", "Xinghuan", "GoldenSpire", 1060f, 700f, 9f, 0f);
            // Plaza fountain: plaza centre, south of spawn.
            Place("Fountain", "Xinghuan", "PlazaFountain", 940f, 470f, 2.5f, 0f);
            // Colonnade ring around the plaza centre.
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                float x = 1000f + Mathf.Cos(a) * 110f;
                float z = 569f + Mathf.Sin(a) * 110f;
                Place("Column", "Xinghuan", "Colonnade_" + i, x, z, 2f, 0f);
            }
            // Plaza ruins: wall + floor pieces.
            string[] ruins = { "Wall_Hole", "Floor_SquareLarge", "Arch_Round" };
            for (int i = 0; i < ruins.Length; i++)
            {
                float a = (30f + i * 120f) * Mathf.Deg2Rad;
                float x = 1000f + Mathf.Cos(a) * 150f;
                float z = 569f + Mathf.Sin(a) * 150f;
                Place("Ruins_" + ruins[i], "Xinghuan", "Ruins_" + ruins[i], x, z, 1.5f, i * 120f);
            }
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = RandDry(850f, 420f, 1150f, 720f, 5f, 120f, new Vector2(1000f, 569f), 40f);
                Place("Rocks", "Xinghuan", "Rock_" + i, p.x, p.y, 12f, Rng.Next(0, 360));
            }
        }
    }
}
