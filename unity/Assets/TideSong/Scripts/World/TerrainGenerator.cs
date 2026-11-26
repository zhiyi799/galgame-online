using System.Collections.Generic;
using UnityEngine;

namespace TideSong
{
    /// <summary>把 heightmap 直接烘成顶点色网格，无贴图依赖。</summary>
    public class TerrainGenerator : MonoBehaviour
    {
        public const int Resolution = 190;

        public static readonly Color DeepWater = new Color(0.06f, 0.14f, 0.34f);
        public static readonly Color Sand = new Color(0.84f, 0.78f, 0.58f);
        // 草色偏真绿（B < G）：白天环境光是亮蓝（0.45,0.58,0.85），原来偏青
        // 的草（B>G）在 3D 里被洗成灰蓝；改绿后实机仍读得出绿色。小地图
        // （无光照）本就正确，改色后两者一致。
        public static readonly Color Grass = new Color(0.16f, 0.42f, 0.30f);
        public static readonly Color GrassLight = new Color(0.28f, 0.52f, 0.34f);
        public static readonly Color GrassDeep = new Color(0.10f, 0.34f, 0.24f);
        public static readonly Color Sakura = new Color(0.87f, 0.69f, 0.80f);
        public static readonly Color Rock = new Color(0.36f, 0.38f, 0.46f);
        public static readonly Color Snow = new Color(0.93f, 0.95f, 0.98f);

        public void Generate()
        {
            var mesh = new Mesh { name = "TideSongTerrain" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            int res = Resolution;
            int side = res + 1;
            var verts = new List<Vector3>(side * side);
            var colors = new List<Color>(side * side);
            var uvs = new List<Vector2>(side * side);
            var tris = new List<int>(res * res * 6);

            for (int z = 0; z <= res; z++)
            {
                for (int x = 0; x <= res; x++)
                {
                    float wx = (x / (float)res - 0.5f) * WorldData.Size;
                    float wz = (z / (float)res - 0.5f) * WorldData.Size;
                    float h = WorldData.HeightAt(wx, wz);

                    verts.Add(new Vector3(wx, h, wz));
                    uvs.Add(new Vector2(x / (float)res, z / (float)res));
                    colors.Add(ShadeAt(wx, wz, h));
                }
            }

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int i = z * side + x;
                    tris.Add(i); tris.Add(i + side + 1); tris.Add(i + side);
                    tris.Add(i); tris.Add(i + 1); tris.Add(i + side + 1);
                }
            }

            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var mf = GetComponent<MeshFilter>();
            if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = GetComponent<MeshRenderer>();
            if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialLibrary.Terrain;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            var col = GetComponent<MeshCollider>();
            if (col == null) col = gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            col.convex = false;
        }

        /// <summary>按高度 + 陡峭程度 + 一点噪声给地表上色。
        /// 探索地基（2026-11-26）：低地以草色为主，沙只留在水边一圈薄滩；
        /// 草加两层噪声拼块 + 稀疏樱粉点缀。布局/高程不动，只改配色。</summary>
        public static Color ShadeAt(float x, float z, float h)
        {
            float n = NoiseUtil.Fbm(x * 0.09f, z * 0.09f, 3);
            Vector3 nrm = WorldData.NormalAt(x, z);
            float slope = 1f - Mathf.Clamp01(nrm.y);   // 0 平地 / 1 峭壁

            Color c;
            if (h < WorldData.WaterLevel - 0.6f)
            {
                c = Color.Lerp(DeepWater, Sand, Mathf.Clamp01((h - (WorldData.WaterLevel - 6f)) / 6f) * 0.35f);
            }
            else if (h < WorldData.WaterLevel + 0.7f)
            {
                // 薄滩：只在水边 0.7 m 一圈是沙（原来 1.6 m）。
                c = Sand;
            }
            else if (h < 20f)
            {
                // 沙→草在 2 m 内完成（原来 16 m）：出生台地 3.4 m 处 75% 草，
                // 3.9 m 以上全草，低地不再发白。
                float t = Mathf.Clamp01((h - (WorldData.WaterLevel + 0.7f)) / 2.0f);
                c = Color.Lerp(Sand, GrassColor(x, z, n), t);
            }
            else if (h < 34f)
            {
                c = Color.Lerp(GrassLight, Rock, Mathf.Clamp01((h - 20f) / 14f));
            }
            else if (h < 46f)
            {
                c = Color.Lerp(Rock, Snow, Mathf.Clamp01((h - 34f) / 12f));
            }
            else
            {
                c = Snow;
            }

            // 陡坡露岩石
            c = Color.Lerp(c, Rock * (0.85f + n * 0.3f), Mathf.Clamp01(slope * 2.4f) * 0.75f);

            // 细节明暗
            float v = 0.92f + n * 0.16f;
            c *= v;
            c.a = 1f;
            return c;
        }

        /// <summary>草色：细噪声 + 低频拼块两层变化，叠加稀疏樱粉点缀。
        /// Fbm(3 oct) 均值约 0.47，樱阈值 0.68–0.80 只取顶部约 4% 面积。</summary>
        static Color GrassColor(float x, float z, float n)
        {
            float n2 = NoiseUtil.Fbm(x * 0.02f + 7f, z * 0.02f + 3f, 2);
            float g = Mathf.Clamp01(n * 0.65f + n2 * 0.55f);
            Color grass = Color.Lerp(GrassDeep, GrassLight, g);
            float s = NoiseUtil.Fbm(x * 0.05f + 13f, z * 0.05f + 21f, 3);
            float sakuraK = Mathf.SmoothStep(0.68f, 0.80f, s) * 0.55f;
            return Color.Lerp(grass, Sakura, sakuraK);
        }
    }
}
