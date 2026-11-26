using System.Collections.Generic;
using UnityEngine;

namespace TideSong
{
    /// <summary>把世界填满：树、岩石、营地、祭坛、锚点、宝箱、采集点、敌人。</summary>
    public class WorldBuilder : MonoBehaviour
    {
        GameManager gm;
        readonly List<Vector3> used = new List<Vector3>();

        // 树冠绿偏真绿（B < G），与地形草色一致；白天亮蓝环境光下仍读绿。
        static readonly Color[] TreeGreens = new Color[]
        {
            new Color(0.12f, 0.36f, 0.26f),
            new Color(0.16f, 0.44f, 0.30f),
            new Color(0.10f, 0.32f, 0.24f),
            new Color(0.18f, 0.48f, 0.32f),
        };

        public void Build(GameManager manager)
        {
            gm = manager;
            Random.InitState(20261118);
            BuildCamp();
            BuildAltar();
            BuildShrines();
            BuildTrees();
            BuildRocks();
            BuildChests();
            BuildGatherNodes();
            SpawnStartingEnemies();
        }

        // ---------- 通用 ----------

        Vector3 SafePoint(float x, float z, float minHeight = 1.6f, float maxHeight = 42f)
        {
            for (int ring = 0; ring < 26; ring++)
            {
                float r = ring * 3f;
                int seg = ring == 0 ? 1 : 8;
                for (int a = 0; a < seg; a++)
                {
                    float ang = a * Mathf.PI * 2f / seg + ring * 0.7f;
                    float px = x + Mathf.Cos(ang) * r;
                    float pz = z + Mathf.Sin(ang) * r;
                    if (Mathf.Abs(px) > WorldData.HalfSize - 18f || Mathf.Abs(pz) > WorldData.HalfSize - 18f) continue;
                    float h = WorldData.HeightAt(px, pz);
                    if (h > WorldData.WaterLevel + minHeight && h < maxHeight)
                    {
                        used.Add(new Vector3(px, h, pz));
                        return new Vector3(px, h, pz);
                    }
                }
            }
            return WorldData.PointAt(0f, 30f, 0f);
        }

        // ---------- 营地 ----------

        void BuildCamp()
        {
            var camp = new GameObject("Camp").transform;
            camp.SetParent(transform, false);
            Vector3 c = WorldData.PointAt(0f, 34f, 0f);
            camp.position = c;

            // 地面石圈
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f;
                MaterialLibrary.Prim(PrimitiveType.Sphere, camp, MaterialLibrary.Stone,
                    new Vector3(Mathf.Cos(a) * 2.4f, 0.2f, Mathf.Sin(a) * 2.4f),
                    new Vector3(0.6f, 0.4f, 0.6f), Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), 0f));
            }

            // 篝火
            var fire = MaterialLibrary.Prim(PrimitiveType.Cylinder, camp, MaterialLibrary.Wood,
                new Vector3(0f, 0.25f, 0f), new Vector3(1.1f, 0.12f, 1.1f));
            var flame = MaterialLibrary.Prim(PrimitiveType.Sphere, camp, MaterialLibrary.Glow,
                new Vector3(0f, 0.8f, 0f), new Vector3(0.75f, 1.05f, 0.75f));
            flame.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flame.AddComponent<FlameWobble>();

            var fireLight = new GameObject("FireLight").AddComponent<Light>();
            fireLight.transform.SetParent(camp, false);
            fireLight.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            fireLight.type = LightType.Point;
            fireLight.range = 18f;
            fireLight.intensity = 3.2f;
            fireLight.color = new Color(1f, 0.62f, 0.32f);
            fireLight.shadows = LightShadows.None;

            // 帐篷：两块斜面（建模重做 Item 1 调色：原砖红与群青世界不搭，改深群青帆布）
            var tentA = MaterialLibrary.Prim(PrimitiveType.Cube, camp, MaterialLibrary.Make(new Color(0.16f, 0.28f, 0.55f)),
                new Vector3(-4.2f, 1.2f, 1.5f), new Vector3(3.4f, 0.14f, 2.6f), Quaternion.Euler(0f, 0f, 38f));
            var tentB = MaterialLibrary.Prim(PrimitiveType.Cube, camp, MaterialLibrary.Make(new Color(0.12f, 0.22f, 0.45f)),
                new Vector3(-2.1f, 1.2f, 1.5f), new Vector3(3.4f, 0.14f, 2.6f), Quaternion.Euler(0f, 0f, -38f));

            // 木桩围栏
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f + 0.3f;
                MaterialLibrary.Prim(PrimitiveType.Cylinder, camp, MaterialLibrary.Wood,
                    new Vector3(Mathf.Cos(a) * 7.5f, 0.6f, Mathf.Sin(a) * 7.5f),
                    new Vector3(0.16f, 0.65f, 0.16f), Quaternion.Euler(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f)));
            }
        }

        // ---------- 祭坛 ----------

        void BuildAltar()
        {
            var root = new GameObject("Altar").transform;
            root.SetParent(transform, false);
            Vector3 c = WorldData.PointAt(0f, 0f, 0f);
            root.position = c;

            MaterialLibrary.Prim(PrimitiveType.Cylinder, root, MaterialLibrary.Stone,
                new Vector3(0f, 0.25f, 0f), new Vector3(9.5f, 0.25f, 9.5f));
            MaterialLibrary.Prim(PrimitiveType.Cylinder, root, MaterialLibrary.Stone,
                new Vector3(0f, 0.75f, 0f), new Vector3(7.2f, 0.28f, 7.2f));
            MaterialLibrary.Prim(PrimitiveType.Cylinder, root, MaterialLibrary.Make(new Color(0.32f, 0.36f, 0.44f)),
                new Vector3(0f, 1.15f, 0f), new Vector3(5.0f, 0.2f, 5.0f));

            // 符文环
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                MaterialLibrary.Prim(PrimitiveType.Cube, root, MaterialLibrary.Glow,
                    new Vector3(Mathf.Cos(a) * 3.0f, 1.25f, Mathf.Sin(a) * 3.0f),
                    new Vector3(0.5f, 0.03f, 0.14f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f));
            }

            // 三根石柱
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f;
                var pillar = MaterialLibrary.Prim(PrimitiveType.Cube, root, MaterialLibrary.Stone,
                    new Vector3(Mathf.Cos(a) * 6.4f, 3.2f, Mathf.Sin(a) * 6.4f),
                    new Vector3(0.9f, 6.4f, 0.9f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f));
                MaterialLibrary.Prim(PrimitiveType.Cube, root, MaterialLibrary.Glow,
                    new Vector3(Mathf.Cos(a) * 6.4f, 5.4f, Mathf.Sin(a) * 6.4f),
                    new Vector3(1.0f, 0.12f, 1.0f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f));
            }

            // 中央水晶
            var crystal = MaterialLibrary.Prim(PrimitiveType.Cube, root, MaterialLibrary.Anchor,
                new Vector3(0f, 2.6f, 0f), new Vector3(1.1f, 1.1f, 1.1f), Quaternion.Euler(45f, 0f, 45f));

            var lightGo = new GameObject("AltarLight");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, 3.0f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 26f;
            l.intensity = 2.2f;
            l.color = new Color(0.35f, 0.60f, 1f);
            l.shadows = LightShadows.None;

            var altar = root.gameObject.AddComponent<TideAltar>();
            altar.core = crystal.transform;
            altar.glow = l;
        }

        // ---------- 锚点 ----------

        void BuildShrines()
        {
            Vector2[] spots = new Vector2[]
            {
                new Vector2(96f, 18f),
                new Vector2(-72f, 74f),
                new Vector2(-38f, -92f),
            };

            for (int i = 0; i < spots.Length; i++)
            {
                Vector3 p = SafePoint(spots[i].x, spots[i].y, 2.2f, 30f);
                var root = new GameObject("Shrine" + i).transform;
                root.SetParent(transform, false);
                root.position = p;

                MaterialLibrary.Prim(PrimitiveType.Cylinder, root, MaterialLibrary.Stone,
                    new Vector3(0f, 0.3f, 0f), new Vector3(4.2f, 0.3f, 4.2f));
                for (int k = 0; k < 3; k++)
                {
                    float a = k / 3f * Mathf.PI * 2f + 0.5f;
                    MaterialLibrary.Prim(PrimitiveType.Cube, root, MaterialLibrary.Stone,
                        new Vector3(Mathf.Cos(a) * 2.2f, 1.5f, Mathf.Sin(a) * 2.2f),
                        new Vector3(0.45f, 2.6f, 0.45f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f));
                }

                var crystal = MaterialLibrary.Prim(PrimitiveType.Cube, root, MaterialLibrary.Make(new Color(0.85f, 0.95f, 1f), 0.4f, 0.9f),
                    new Vector3(0f, 2.1f, 0f), new Vector3(0.8f, 1.5f, 0.8f), Quaternion.Euler(0f, 45f, 0f));
                var glowMat = MaterialLibrary.Make(new Color(0.35f, 0.60f, 1f), 0.2f, 0.9f);
                MaterialLibrary.ApplyGlow(glowMat, new Color(0.25f, 0.55f, 1f) * 1.2f);
                crystal.GetComponent<Renderer>().sharedMaterial = glowMat;

                var ring = MaterialLibrary.Prim(PrimitiveType.Cylinder, root, MaterialLibrary.MakeTransparent(new Color(0.4f, 0.9f, 1f, 0.55f), 0f, 0.6f),
                    new Vector3(0f, 0.9f, 0f), new Vector3(2.6f, 0.02f, 2.6f));
                ring.AddComponent<RingSpin>();

                var lightGo = new GameObject("ShrineLight");
                lightGo.transform.SetParent(root, false);
                lightGo.transform.localPosition = new Vector3(0f, 2.4f, 0f);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 16f;
                l.intensity = 0.9f;
                l.color = new Color(0.35f, 0.60f, 1f);
                l.shadows = LightShadows.None;

                var sh = root.gameObject.AddComponent<TideShrine>();
                sh.crystal = crystal.transform;
                sh.glow = l;
                sh.index = i;
            }
        }

        // ---------- 植被 ----------

        void BuildTrees()
        {
            var root = new GameObject("Trees").transform;
            root.SetParent(transform, false);
            int placed = 0;
            int guard = 0;
            while (placed < 210 && guard < 4000)
            {
                guard++;
                float x = Random.Range(-WorldData.HalfSize + 24f, WorldData.HalfSize - 24f);
                float z = Random.Range(-WorldData.HalfSize + 24f, WorldData.HalfSize - 24f);
                float h = WorldData.HeightAt(x, z);
                if (h < WorldData.WaterLevel + 1.8f || h > 36f) continue;
                if (new Vector2(x, z).magnitude < 26f) continue;   // 给营地/祭坛留空地
                if (Vector3.Distance(new Vector3(x, 0, z), new Vector3(0, 0, 34)) < 14f) continue;

                float slope = 1f - WorldData.NormalAt(x, z).y;
                if (slope > 0.24f) continue;
                MakeTree(root, new Vector3(x, h, z), Random.Range(0.75f, 1.45f));
                placed++;
            }
        }

        // 探索地基（2026-11-26）：树浮空修复。原来树干直径仅 0.34×scale，
        // 远处亚像素不可见，树冠成漂浮绿点；且树基按解析高度放，网格顶点间
        // 线性插值下沉时树基悬空。改法：树干加粗、根部膨大、整树下沉、
        // 首层树冠下移加大，与树干重叠。
        void MakeTree(Transform parent, Vector3 pos, float scale)
        {
            var t = new GameObject("Tree").transform;
            t.SetParent(parent, false);
            // 整树下沉 0.3×scale：抵消网格插值造成的基部悬空。
            t.position = pos + Vector3.down * (0.3f * scale);
            t.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            float trunkH = 2.0f * scale;
            // 树干加粗：0.34 → 0.60×scale（直径 0.45–0.87 m，远处仍可见）。
            MaterialLibrary.Prim(PrimitiveType.Cylinder, t, MaterialLibrary.Wood,
                new Vector3(0f, trunkH * 0.5f, 0f),
                new Vector3(0.60f * scale, trunkH * 0.5f, 0.60f * scale));
            // 根部膨大：压扁球，底部沉入地下 0.175×scale，树像长在地里。
            MaterialLibrary.Prim(PrimitiveType.Sphere, t, MaterialLibrary.Wood,
                new Vector3(0f, 0.05f * scale, 0f),
                new Vector3(0.95f * scale, 0.45f * scale, 0.95f * scale));

            var green = TreeGreens[Random.Range(0, TreeGreens.Length)];
            int blobs = Random.Range(2, 4);
            for (int i = 0; i < blobs; i++)
            {
                float bs = (1.7f - i * 0.28f) * scale;
                // 首层树冠下移（0.55 → 0.35×scale），与树干顶重叠 0.33×scale。
                float by = trunkH + (0.35f + i * 0.62f) * scale;
                // 树冠用 toon（环境光 0.85）而不用 Standard：Standard 背光面
                // 在亮蓝环境光下仍近黑，远处看成黑盘；toon 环境光保证背光面
                // 仍读得出绿色（与 MaterialLibrary.Foliage 同一思路）。
                var crownMat = MaterialLibrary.MakeToon(green * (0.9f + i * 0.06f),
                    outline: 0.006f, shadowColor: new Color(0.08f, 0.22f, 0.16f), ambientStrength: 0.85f);
                MaterialLibrary.Prim(PrimitiveType.Sphere, t, crownMat,
                    new Vector3(Random.Range(-0.25f, 0.25f) * scale, by, Random.Range(-0.25f, 0.25f) * scale),
                    new Vector3(bs, bs * 0.85f, bs));
            }
        }

        void BuildRocks()
        {
            var root = new GameObject("Rocks").transform;
            root.SetParent(transform, false);
            int placed = 0, guard = 0;
            while (placed < 90 && guard < 2500)
            {
                guard++;
                float x = Random.Range(-WorldData.HalfSize + 20f, WorldData.HalfSize - 20f);
                float z = Random.Range(-WorldData.HalfSize + 20f, WorldData.HalfSize - 20f);
                float h = WorldData.HeightAt(x, z);
                if (h < WorldData.WaterLevel - 1.5f || h > 52f) continue;
                if (new Vector2(x, z).magnitude < 12f) continue;
                float s = Random.Range(0.5f, 2.2f);
                var r = MaterialLibrary.Prim(PrimitiveType.Sphere, root, MaterialLibrary.Stone,
                    new Vector3(x, h + s * 0.25f, z),
                    new Vector3(s * Random.Range(0.9f, 1.6f), s * Random.Range(0.5f, 0.9f), s * Random.Range(0.9f, 1.6f)),
                    Quaternion.Euler(Random.Range(-14f, 14f), Random.Range(0f, 360f), Random.Range(-14f, 14f)));
                placed++;
            }
        }

        // ---------- 宝箱 / 采集 ----------

        void BuildChests()
        {
            var root = new GameObject("Chests").transform;
            root.SetParent(transform, false);
            int placed = 0, guard = 0;
            while (placed < 14 && guard < 2000)
            {
                guard++;
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(34f, 130f);
                float x = Mathf.Cos(a) * r;
                float z = Mathf.Sin(a) * r;
                float h = WorldData.HeightAt(x, z);
                if (h < WorldData.WaterLevel + 1.5f || h > 40f) continue;
                MakeChest(root, new Vector3(x, h, z));
                placed++;
            }
        }

        void MakeChest(Transform parent, Vector3 pos)
        {
            var c = new GameObject("Chest").transform;
            c.SetParent(parent, false);
            c.position = pos;
            c.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            MaterialLibrary.Prim(PrimitiveType.Cube, c, MaterialLibrary.Chest, new Vector3(0f, 0.45f, 0f), new Vector3(1.3f, 0.9f, 0.95f));
            MaterialLibrary.Prim(PrimitiveType.Cube, c, MaterialLibrary.Gold, new Vector3(0f, 0.45f, 0f), new Vector3(1.36f, 0.14f, 1.0f));

            var lidPivot = new GameObject("LidPivot").transform;
            lidPivot.SetParent(c, false);
            lidPivot.localPosition = new Vector3(0f, 0.9f, -0.47f);
            var lid = MaterialLibrary.Prim(PrimitiveType.Cube, lidPivot, MaterialLibrary.Chest,
                new Vector3(0f, 0.0f, 0.47f), new Vector3(1.32f, 0.22f, 0.98f));
            MaterialLibrary.Prim(PrimitiveType.Cube, lidPivot, MaterialLibrary.Gold,
                new Vector3(0f, 0.06f, 0.47f), new Vector3(1.38f, 0.06f, 0.3f));

            var glow = MaterialLibrary.Prim(PrimitiveType.Cube, c, MaterialLibrary.Glow,
                new Vector3(0f, 0.5f, 0.49f), new Vector3(0.26f, 0.26f, 0.05f));
            glow.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var chest = c.gameObject.AddComponent<TreasureChest>();
            chest.lid = lidPivot;
        }

        void BuildGatherNodes()
        {
            var root = new GameObject("Gather").transform;
            root.SetParent(transform, false);
            int placed = 0, guard = 0;
            while (placed < 30 && guard < 2000)
            {
                guard++;
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(16f, 120f);
                float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                float h = WorldData.HeightAt(x, z);
                if (h < WorldData.WaterLevel + 1f || h > 36f) continue;

                var g = new GameObject("Node").transform;
                g.SetParent(root, false);
                g.position = new Vector3(x, h, z);

                var holder = new GameObject("Visual").transform;
                holder.SetParent(g, false);
                // 发光花：三片小叶子 + 花心
                for (int i = 0; i < 3; i++)
                {
                    float ang = i * 120f * Mathf.Deg2Rad;
                    MaterialLibrary.Prim(PrimitiveType.Cube, holder, MaterialLibrary.Foliage,
                        new Vector3(Mathf.Cos(ang) * 0.22f, 0.22f, Mathf.Sin(ang) * 0.22f),
                        new Vector3(0.12f, 0.42f, 0.36f), Quaternion.Euler(24f, -i * 120f, 0f));
                }
                MaterialLibrary.Prim(PrimitiveType.Sphere, holder, MaterialLibrary.Glow,
                    new Vector3(0f, 0.55f, 0f), new Vector3(0.32f, 0.32f, 0.32f));

                var node = g.gameObject.AddComponent<GatherNode>();
                node.visual = holder.gameObject;
                placed++;
            }
        }

        // ---------- 敌人 ----------

        void SpawnStartingEnemies()
        {
            var root = new GameObject("Enemies").transform;
            root.SetParent(transform, false);
            int placed = 0, guard = 0;
            while (placed < 16 && guard < 2000)
            {
                guard++;
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(34f, 135f);
                float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                float h = WorldData.HeightAt(x, z);
                if (h < WorldData.WaterLevel + 0.5f || h > 44f) continue;
                if (Vector3.Distance(new Vector3(x, 0, z), new Vector3(0, 0, 34)) < 26f) continue;
                EnemyAI.Spawn(new Vector3(x, h, z), Random.Range(0.85f, 1.35f), Random.Range(0.9f, 1.15f), root);
                placed++;
            }
        }

        public Transform EnemyRoot
        {
            get
            {
                var t = transform.Find("Enemies");
                return t != null ? t : transform;
            }
        }
    }

    /// <summary>简单循环动画组件。</summary>
    public class RingSpin : MonoBehaviour
    {
        public float speed = 40f;
        void Update() { transform.Rotate(Vector3.up, speed * Time.deltaTime, Space.Self); }
    }

    public class FlameWobble : MonoBehaviour
    {
        Vector3 baseScale;
        void Start() { baseScale = transform.localScale; }
        void Update()
        {
            float s = 1f + Mathf.Sin(Time.time * 7f) * 0.12f + Mathf.Sin(Time.time * 13f) * 0.06f;
            transform.localScale = new Vector3(baseScale.x * (2f - s), baseScale.y * s, baseScale.z * (2f - s));
            transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.Self);
        }
    }
}
