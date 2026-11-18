using UnityEngine;

namespace TideSong
{
    /// <summary>
    /// 世界形状的唯一权威：所有物体落点、角色贴地都走这里，保证地形与逻辑一致。
    /// </summary>
    public static class WorldData
    {
        public const float Size = 420f;          // 世界边长
        public const float HalfSize = Size * 0.5f;
        public const float WaterLevel = 1.2f;    // 水面高度
        public const float SpawnHeight = 3.4f;   // 出生台地高度

        /// <summary>地形高度采样。</summary>
        public static float HeightAt(float x, float z)
        {
            float d = Mathf.Sqrt(x * x + z * z);

            // 基础起伏
            float baseNoise = NoiseUtil.Fbm(x * 0.0055f + 11.3f, z * 0.0055f + 4.7f, 4);
            float h = (baseNoise - 0.5f) * 52f;

            // 中央台地压平（出生点、祭坛都在这里）
            float flat = NoiseUtil.SmoothStep(16f, 78f, d);
            h = Mathf.Lerp(SpawnHeight, h, flat);

            // 环形山脉把世界围起来
            float rim = NoiseUtil.SmoothStep(138f, 206f, d);
            h += rim * 62f * (0.55f + 0.45f * NoiseUtil.Fbm(x * 0.021f + 5f, z * 0.021f + 9f, 3));

            // 洼地变湖
            float lake = NoiseUtil.Fbm(x * 0.011f + 31f, z * 0.011f + 17f, 3);
            if (lake < 0.44f) h -= (0.44f - lake) * 46f;

            // 靠近边界的海
            float sea = NoiseUtil.SmoothStep(150f, 200f, d);
            h -= sea * 26f * NoiseUtil.SmoothStep(0.5f, 0.9f, NoiseUtil.Fbm(x * 0.03f, z * 0.03f, 2));

            return h;
        }

        public static Vector3 PointAt(float x, float z, float yOffset = 0f)
        {
            return new Vector3(x, HeightAt(x, z) + yOffset, z);
        }

        public static bool IsUnderwater(Vector3 p) { return p.y < WaterLevel - 0.05f; }

        /// <summary>地表法线（用于种树、贴花时避免倾斜）。</summary>
        public static Vector3 NormalAt(float x, float z)
        {
            float e = 1.2f;
            float hL = HeightAt(x - e, z), hR = HeightAt(x + e, z);
            float hD = HeightAt(x, z - e), hU = HeightAt(x, z + e);
            return new Vector3(hL - hR, 2f * e, hD - hU).normalized;
        }

        /// <summary>把玩家夹在世界边界内。</summary>
        public static Vector3 Clamp(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, -HalfSize + 14f, HalfSize - 14f);
            p.z = Mathf.Clamp(p.z, -HalfSize + 14f, HalfSize - 14f);
            return p;
        }
    }
}
