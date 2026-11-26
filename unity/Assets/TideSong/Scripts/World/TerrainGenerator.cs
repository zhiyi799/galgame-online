using System.Collections.Generic;
using UnityEngine;

namespace TideSong
{
    /// <summary>把 heightmap 直接烘成顶点色网格，无贴图依赖。</summary>
    public class TerrainGenerator : MonoBehaviour
    {
        public const int Resolution = 190;

        // 地图修复（2026-11-26，六款参考游戏研究）：湖底不再向沙色淡化
        // （以前 DeepWater→Sand ×0.35，湖面又暗又糊），改成浅青绿→饱和深蓝的
        // 长渐变；沙带加亮收窄，岸线处加一条湿沙带，3D 和小地图都变清晰。
        public static readonly Color DeepWater = new Color(0.08f, 0.30f, 0.72f);
        public static readonly Color ShallowWater = new Color(0.16f, 0.58f, 0.52f);
        public static readonly Color WetSand = new Color(0.62f, 0.55f, 0.38f);
        public static readonly Color Sand = new Color(0.88f, 0.83f, 0.64f);
        // 草色偏真绿（B < G）：白天环境光是亮蓝（0.45,0.58,0.85），原来偏青
        // 的草（B>G）在 3D 里被洗成灰蓝；改绿后实机仍读得出绿色。小地图
        // （无光照）本就正确，改色后两者一致。
        // 地图修复 v2（2026-11-26）：营地地面仍灰蓝的根因——不是 ShadeAt
        // 的颜色不对（顶点色在 3.34 m 处本就是草绿，小地图也显示绿色），
        // 而是三向环境光的亮蓝天光（0.45,0.58,0.85）把草色洗掉了：天光的
        // B/G=1.47，压过了旧草色的 G/B=1.40，环境光单独照出来 B 略胜 G，
        // 清晨低太阳（默认 07:41）直射又弱，营地就只剩灰蓝。治本是把草色
        // 的 G/B 拉到 2.2 以上（天光再蓝也压不住），而不是改全局环境光
        // （那会动整场群青氛围）或改地形高度。树冠不受影响（toon 有自己
        // 的环境光强度），岩石保持灰蓝（本就该是灰蓝）。
        public static readonly Color Grass = new Color(0.12f, 0.44f, 0.20f);
        public static readonly Color GrassLight = new Color(0.20f, 0.54f, 0.24f);
        public static readonly Color GrassDeep = new Color(0.07f, 0.35f, 0.15f);
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

            // 三角形绕序：顶点按 (x,z) 网格排布，(i, i+side, i+side+1) /
            // (i, i+side+1, i+1) 的叉积法线朝上（+y），正面朝上，相机
            // 从上方看得到；旧顺序 (i, i+side+1, i+side) 法线朝下，整片
            // 地形被背面剔除、根本不渲染——这才是营地地面一直灰蓝的根因：
            // 顶点色改了也看不见，镜头里其实是半透明水面 + 天空穹顶。
            // （实机诊断：顶点色已是草绿 (0.252,0.481,0.292)，但截图地面
            // 像素改色前后一模一样 (116,134,161)，且无 shader 的 ±5%
            // 抖动，证明 TerrainVC 根本没画出地面。）
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int i = z * side + x;
                    tris.Add(i); tris.Add(i + side); tris.Add(i + side + 1);
                    tris.Add(i); tris.Add(i + side + 1); tris.Add(i + 1);
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
                // 湖底：浅青绿 → 饱和深蓝的长渐变（6 m 拉满）。透过半透明水面看，
                // 近岸是青绿浅水，越深越蓝；旧做法是向沙色淡化 0.35，又暗又糊。
                float t = Mathf.Clamp01((h - (WorldData.WaterLevel - 6f)) / 6f);
                c = Color.Lerp(DeepWater, ShallowWater, t);
            }
            else if (h < WorldData.WaterLevel + 0.15f)
            {
                // 湿沙带：水线处一条深色带，岸线在 3D 和地图上都读得出来。
                c = WetSand;
            }
            else if (h < WorldData.WaterLevel + 0.5f)
            {
                // 干沙：加亮、收窄（旧 0.7 m），地图上是一圈 crisp 的亮沙环。
                c = Sand;
            }
            else if (h < 20f)
            {
                // 沙→草在 1.3 m 内完成（v2：原来 2 m，出生台地 3.4 m 处只有
                // 75% 草，剩下的沙色在蓝天光下也发灰；现在 3.0 m 以上全草，
                // 营地脚下不再有沙灰混色）：低地不再发白。
                float t = Mathf.Clamp01((h - (WorldData.WaterLevel + 0.5f)) / 1.3f);
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

        /// <summary>草色：细噪声 + 低频拼块两层变化，叠加罕见淡樱粉斑。
        /// 樱阈值 0.82–0.92、上限 0.18，只取 Fbm 顶部极小面积且混色浅，
        /// 不再把整片草地洗成灰粉（旧 0.68–0.80×0.55 曾让营地顶点色
        /// 变成 (0.447,0.548,0.453)，3D 里读成灰蓝）。</summary>
        static Color GrassColor(float x, float z, float n)
        {
            float n2 = NoiseUtil.Fbm(x * 0.02f + 7f, z * 0.02f + 3f, 2);
            float g = Mathf.Clamp01(n * 0.65f + n2 * 0.55f);
            Color grass = Color.Lerp(GrassDeep, GrassLight, g);
            float s = NoiseUtil.Fbm(x * 0.05f + 13f, z * 0.05f + 21f, 3);
            // 地图修复 v2 修正（2026-11-26）：旧阈值 SmoothStep(0.68, 0.80)×0.55
            // 把大片草地染成灰粉——实机诊断：营地顶点色 (0.447,0.548,0.453)，
            // 约 42% 樱粉混色，G/B 只剩 1.21，低于天光 B/G 1.47，3D 里被洗成
            // 灰蓝 (116,134,161)；Fbm 噪声是 20 m 级的团块，营地正好落在
            // 一团樱粉里，整片前景全灰。樱粉只留罕见淡斑：阈值提到 0.82–0.92、
            // 上限 0.18，草地 G/B 恒 ≥2.2，樱色交给樱树本身，不再由地面承担。
            float sakuraK = Mathf.SmoothStep(0.82f, 0.92f, s) * 0.18f;
            return Color.Lerp(grass, Sakura, sakuraK);
        }
    }
}
