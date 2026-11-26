using UnityEngine;

namespace TideSong
{
    /// <summary>所有材质在运行时生成，工程里不带任何美术资源。</summary>
    public static class MaterialLibrary
    {
        static Material _terrain;
        static Material _water;
        static Material _foliage;
        static Material _wood;
        static Material _stone;
        static Material _skin;
        static Material _hair;
        static Material _cloth;
        static Material _metal;
        static Material _glow;
        static Material _enemy;
        static Material _enemyDark;
        static Material _chest;
        static Material _gold;
        static Material _anchor;

        static Shader Lit
        {
            get
            {
                var s = Shader.Find("Standard");
                if (s == null) s = Shader.Find("Diffuse");
                return s;
            }
        }

        // AstralBloom/Toon（Assets/Shaders/AstralToon.shader，手写 Built-in 卡通 shader）。
        // 找不到时返回 null，调用方回退到 Standard，游戏绝不因此崩。
        static Shader _toon;
        static bool _toonChecked;
        public static Shader ToonShader
        {
            get
            {
                if (!_toonChecked)
                {
                    _toonChecked = true;
                    _toon = Shader.Find("AstralBloom/Toon");
                    if (_toon == null) Debug.LogWarning("[MaterialLibrary] AstralBloom/Toon not found, falling back to Standard.");
                }
                return _toon;
            }
        }

        /// <summary>toon 材质：分阶阴影 + 描边 + 边缘光。shader 缺失时回退到 Standard。
        /// shadowColor 默认深群青；布景类传各自色调的暗色，避免树冠/岩石整面变黑。</summary>
        public static Material MakeToon(Color color, float outline = 0.02f, float specStrength = 0.6f,
            float gloss = 24f, float hairStrength = 0f, float rimStrength = 0.5f,
            Color? shadowColor = null, float ambientStrength = 0.45f)
        {
            var sh = ToonShader;
            if (sh == null) return Make(color, 0f, 0.35f);
            var m = new Material(sh);
            m.SetColor("_Color", color);
            m.SetColor("_LightColor", new Color(0.90f, 0.93f, 1.0f, 1f));
            m.SetColor("_ShadowColor", shadowColor ?? new Color(0.10f, 0.16f, 0.32f, 1f));
            m.SetFloat("_ShadowThreshold", 0.5f);
            m.SetFloat("_ShadowSmoothness", 0.05f);
            m.SetFloat("_ShadowSteps", 3f);
            m.SetColor("_SpecularColor", Color.white);
            m.SetFloat("_Gloss", gloss);
            m.SetFloat("_SpecularStrength", specStrength);
            m.SetFloat("_SpecularThreshold", 0.75f);
            m.SetFloat("_SpecularSmoothness", 0.08f);
            m.SetFloat("_SpecularType", 0f);
            m.SetColor("_RimColor", new Color(0.87f, 0.69f, 0.80f, 1f));
            m.SetFloat("_RimStrength", rimStrength);
            m.SetFloat("_RimThreshold", 0.35f);
            m.SetFloat("_RimSmoothness", 0.15f);
            m.SetColor("_HairSpecularColor", new Color(1f, 0.85f, 0.65f, 1f));
            m.SetFloat("_HairSpecularStrength", hairStrength);
            m.SetFloat("_AmbientStrength", ambientStrength);
            m.SetColor("_OutlineColor", new Color(0.05f, 0.07f, 0.16f, 1f));
            m.SetFloat("_OutlineWidth", outline);
            m.SetFloat("_Cutoff", 0.5f);
            return m;
        }

        public static Material Terrain
        {
            get
            {
                if (_terrain == null)
                {
                    var sh = Resources.Load<Shader>("Shaders/TerrainVC");
                    _terrain = sh != null ? new Material(sh) : new Material(Lit);
                    if (sh == null) _terrain.color = new Color(0.36f, 0.55f, 0.34f);
                    _terrain.name = "TerrainMat";
                }
                return _terrain;
            }
        }

        public static Material Water
        {
            get
            {
                if (_water == null)
                {
                    // 地图修复（2026-11-26）：水面更透明（alpha 0.62→0.42）+ 更亮的
                    // 青蓝，浅水处能看到下面的湖底渐变（青绿→深蓝），深水仍读成饱和蓝。
                    _water = MakeTransparent(new Color(0.12f, 0.42f, 0.68f, 0.42f), 0.28f, 0.85f);
                    _water.name = "WaterMat";
                }
                return _water;
            }
        }

        // 卡通渲染：角色与布景（叶/木/石/敌）用 toon；金属/发光/透明保持 Standard。
        // 布景阴影色用各自色调的暗色（叶=深青绿、木=深棕、石=深灰蓝），避免整面变黑。
        // 布景描边收窄到 0.006：描边按 clip w 缩放、屏幕宽度恒定，小物件
        // （石/木/叶）用 0.02 会被黑盘吞掉（实测 0.02 的黑环约 31 px，石头
        // 本体才 32 px）。角色材质描边不动（体量大，描边比例正常）。
        public static Material Foliage { get { if (_foliage == null) _foliage = MakeToon(new Color(0.18f, 0.47f, 0.32f), outline: 0.006f, shadowColor: new Color(0.08f, 0.22f, 0.16f), ambientStrength: 0.85f); return _foliage; } }
        public static Material Wood { get { if (_wood == null) _wood = MakeToon(new Color(0.35f, 0.26f, 0.19f), outline: 0.006f, shadowColor: new Color(0.16f, 0.11f, 0.08f)); return _wood; } }
        public static Material Stone { get { if (_stone == null) _stone = MakeToon(new Color(0.47f, 0.47f, 0.50f), outline: 0.006f, shadowColor: new Color(0.20f, 0.21f, 0.26f)); return _stone; } }
        public static Material Skin { get { if (_skin == null) _skin = MakeToon(new Color(0.99f, 0.86f, 0.78f), outline: 0.014f, specStrength: 0.35f); return _skin; } }
        public static Material Hair { get { if (_hair == null) _hair = MakeToon(new Color(0.92f, 0.95f, 1.0f), specStrength: 0.9f, gloss: 32f, hairStrength: 1.2f); return _hair; } }
        public static Material Cloth { get { if (_cloth == null) _cloth = MakeToon(new Color(0.14f, 0.30f, 0.67f)); return _cloth; } }
        public static Material ClothLight { get { if (_clothLight == null) _clothLight = MakeToon(new Color(0.86f, 0.93f, 0.99f)); return _clothLight; } }
        static Material _clothLight;
        public static Material Metal { get { if (_metal == null) _metal = Make(new Color(0.78f, 0.82f, 0.88f), 0.9f, 0.85f); return _metal; } }
        public static Material Gold { get { if (_gold == null) { _gold = Make(new Color(0.95f, 0.76f, 0.30f), 0.85f, 0.8f); ApplyGlow(_gold, new Color(0.6f, 0.42f, 0.08f)); } return _gold; } }
        public static Material Chest { get { if (_chest == null) _chest = Make(new Color(0.44f, 0.29f, 0.18f), 0f, 0.2f); return _chest; } }
        public static Material Enemy { get { if (_enemy == null) { _enemy = MakeToon(new Color(0.08f, 0.10f, 0.32f), outline: 0.024f, rimStrength: 0.9f); } return _enemy; } }
        public static Material EnemyDark { get { if (_enemyDark == null) _enemyDark = MakeToon(new Color(0.03f, 0.04f, 0.12f), outline: 0.024f, rimStrength: 0.9f); return _enemyDark; } }

        public static Material Glow
        {
            get
            {
                if (_glow == null)
                {
                    _glow = Make(new Color(0.25f, 0.41f, 0.79f), 0f, 0.6f);
                    GlowLight(_glow, new Color(0.25f, 0.55f, 1f) * 2.2f);
                }
                return _glow;
            }
        }

        public static Material Anchor
        {
            get
            {
                if (_anchor == null)
                {
                    _anchor = Make(new Color(0.35f, 0.60f, 1f), 0.2f, 0.7f);
                    GlowLight(_anchor, new Color(0.25f, 0.55f, 1f) * 3.5f);
                }
                return _anchor;
            }
        }

        public static Material Make(Color color, float metallic = 0f, float smoothness = 0.25f)
        {
            var m = new Material(Lit);
            m.color = color;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            return m;
        }

        public static void ApplyGlow(Material m, Color emission)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        public static void GlowLight(Material m, Color emission)
        {
            ApplyGlow(m, emission);
        }

        public static Material MakeTransparent(Color color, float metallic, float smoothness)
        {
            var m = new Material(Lit);
            m.color = color;
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            return m;
        }

        /// <summary>给物体快速套一个几何体（用于世界装饰）。</summary>
        public static GameObject Prim(PrimitiveType type, Transform parent, Material mat, Vector3 localPos, Vector3 localScale, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (rot.HasValue) go.transform.localRotation = rot.Value;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var r = go.GetComponent<Renderer>();
            if (r != null) { r.sharedMaterial = mat; }
            return go;
        }
    }
}
