using UnityEngine;

namespace TideSong
{
    /// <summary>轻量程序化噪声（value noise + fbm），不依赖任何外部资源。</summary>
    public static class NoiseUtil
    {
        static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h = h ^ (h >> 16);
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static float Smooth(float t) { return t * t * (3f - 2f * t); }

        public static float Value(float x, float y)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float xf = x - xi;
            float yf = y - yi;
            float v00 = Hash(xi, yi);
            float v10 = Hash(xi + 1, yi);
            float v01 = Hash(xi, yi + 1);
            float v11 = Hash(xi + 1, yi + 1);
            float u = Smooth(xf);
            float v = Smooth(yf);
            return Mathf.Lerp(Mathf.Lerp(v00, v10, u), Mathf.Lerp(v01, v11, u), v);
        }

        public static float Fbm(float x, float y, int octaves = 4)
        {
            float sum = 0f, amp = 0.5f, freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(x * freq, y * freq) * amp;
                freq *= 2f;
                amp *= 0.5f;
            }
            return sum;
        }

        public static float SmoothStep(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }
    }
}
