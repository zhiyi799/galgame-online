using UnityEngine;

namespace TideSong
{
    /// <summary>程序化生成贴图与粒子，零美术资源。</summary>
    public static class ArtUtil
    {
        static Texture2D _dot;
        static Texture2D _ring;
        static Material _particleMat;

        public static Texture2D Dot
        {
            get
            {
                if (_dot == null) _dot = MakeDot(64, 0.65f);
                return _dot;
            }
        }

        public static Texture2D Ring
        {
            get
            {
                if (_ring == null) _ring = MakeRing(128, 0.62f, 0.92f);
                return _ring;
            }
        }

        public static Texture2D MakeDot(int size, float hardness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - Mathf.Pow(d, 1f / Mathf.Max(0.05f, 1f - hardness)));
                    a = a * a;
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        public static Texture2D MakeRing(int size, float inner, float outer)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - (inner + outer) * 0.5f) / ((outer - inner) * 0.5f));
                    px[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>1x1 纯白，用于 IMGUI 自绘。</summary>
        static Texture2D _white;
        public static Texture2D White
        {
            get
            {
                if (_white == null)
                {
                    _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    _white.SetPixel(0, 0, Color.white);
                    _white.Apply();
                }
                return _white;
            }
        }

        public static Material ParticleMat
        {
            get
            {
                if (_particleMat == null)
                {
                    var sh = Shader.Find("Sprites/Default");
                    if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
                    if (sh == null) sh = Shader.Find("Standard");
                    _particleMat = new Material(sh);
                    if (_particleMat.HasProperty("_MainTex")) _particleMat.mainTexture = Dot;
                    _particleMat.renderQueue = 3100;
                }
                return _particleMat;
            }
        }

        public static Material MakeSpriteMat(Color color)
        {
            var m = new Material(ParticleMat);
            m.color = color;
            if (m.HasProperty("_MainTex")) m.mainTexture = Dot;
            m.renderQueue = 3100;
            return m;
        }
    }

    /// <summary>一次性特效：爆点、冲击波、刀光。</summary>
    public static class FX
    {
        public static ParticleSystem Burst(Vector3 pos, Color color, int count = 26, float speed = 6f, float size = 0.35f, float life = 0.6f)
        {
            var go = new GameObject("fx_burst");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.6f));
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.35f;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.material = ArtUtil.MakeSpriteMat(Color.white);
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
            Object.Destroy(go, life + 0.6f);
            return ps;
        }

        public static void Shockwave(Vector3 pos, Color color, float radius = 4f, float duration = 0.5f)
        {
            var go = new GameObject("fx_shock");
            go.transform.position = pos + Vector3.up * 0.2f;
            var mr = go.AddComponent<MeshRenderer>();
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = RingMesh();
            var mat = ArtUtil.MakeSpriteMat(color);
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var sw = go.AddComponent<ShockwaveDriver>();
            sw.duration = duration;
            sw.maxRadius = radius;
            sw.mat = mat;
        }

        static Mesh _ringMesh;
        public static Mesh RingMesh()
        {
            if (_ringMesh != null) return _ringMesh;
            var mesh = new Mesh { name = "ring" };
            int seg = 48;
            var verts = new Vector3[seg + 1];
            var uvs = new Vector2[seg + 1];
            var tris = new int[seg * 3];
            verts[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                verts[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                uvs[i + 1] = new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f);
            }
            for (int i = 0; i < seg; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = (i + 1) % seg + 1;
            }
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            _ringMesh = mesh;
            return mesh;
        }

        /// <summary>刀光：一段弧形拖尾。</summary>
        public static void SlashArc(Transform origin, Color color, float radius = 2.2f, float angle = 120f, float duration = 0.22f)
        {
            var go = new GameObject("fx_slash");
            go.transform.SetParent(origin, false);
            go.transform.localPosition = new Vector3(0f, 1.1f, 0.5f);
            go.transform.localRotation = Quaternion.Euler(0f, -angle * 0.5f + 90f, 0f);
            var mr = go.AddComponent<MeshRenderer>();
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = ArcMesh(radius, angle);
            var mat = ArtUtil.MakeSpriteMat(color);
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var sw = go.AddComponent<SlashDriver>();
            sw.duration = duration;
            sw.mat = mat;
        }

        static Mesh ArcMesh(float radius, float angle)
        {
            var mesh = new Mesh();
            int seg = 24;
            var verts = new Vector3[seg * 2];
            var uvs = new Vector2[seg * 2];
            var tris = new int[(seg - 1) * 6];
            for (int i = 0; i < seg; i++)
            {
                float t = i / (float)(seg - 1);
                float a = Mathf.Lerp(-angle * 0.5f, angle * 0.5f, t) * Mathf.Deg2Rad;
                float r = radius * (0.72f + 0.28f * Mathf.Sin(t * Mathf.PI));
                verts[i * 2] = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                verts[i * 2 + 1] = new Vector3(Mathf.Sin(a) * (r * 0.55f), 0f, Mathf.Cos(a) * (r * 0.55f));
                uvs[i * 2] = new Vector2(t, 1f);
                uvs[i * 2 + 1] = new Vector2(t, 0f);
            }
            for (int i = 0; i < seg - 1; i++)
            {
                int b = i * 2;
                tris[i * 6] = b; tris[i * 6 + 1] = b + 1; tris[i * 6 + 2] = b + 2;
                tris[i * 6 + 3] = b + 1; tris[i * 6 + 4] = b + 3; tris[i * 6 + 5] = b + 2;
            }
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }
    }

    public class ShockwaveDriver : MonoBehaviour
    {
        public float duration = 0.5f;
        public float maxRadius = 4f;
        public Material mat;
        float t;

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float r = Mathf.Lerp(0.4f, maxRadius, k);
            transform.localScale = new Vector3(r, 1f, r);
            if (mat != null)
            {
                var c = mat.color;
                c.a = 1f - k;
                mat.color = c;
            }
            if (k >= 1f) Destroy(gameObject);
        }
    }

    public class SlashDriver : MonoBehaviour
    {
        public float duration = 0.22f;
        public Material mat;
        float t;

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            if (mat != null)
            {
                var c = mat.color;
                c.a = (1f - k) * 0.9f;
                mat.color = c;
            }
            transform.localScale = new Vector3(1f, 1f, Mathf.Lerp(0.6f, 1.35f, k));
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
