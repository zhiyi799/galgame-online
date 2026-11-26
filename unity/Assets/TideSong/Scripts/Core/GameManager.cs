using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TideSong
{
    /// <summary>世界总装：地形、水面、天空、玩家、相机、物件、昼夜、存档。</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public bool Paused;
        public bool GodMode;
        public PlayerCharacter Player;
        public DayNightCycle DayNight;
        public WorldBuilder World;
        public List<Interactable> Interactables = new List<Interactable>();

        public Vector3 SpawnPoint;
        public float PlayTime;

        Transform worldRoot;
        GameObject waterGo;
        WaterSurface water;
        bool frozen;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            InputHub.Mobile = Application.isMobilePlatform;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadowDistance = 90f;
            QualitySettings.shadowCascades = 2;
        }

        void Start()
        {
            Sfx.Init(transform);
            BuildWorld();
            GameHUD.Instance?.Toast("ASTRAL BLOOM · 星樱物语");
            StartCoroutine(IntroRoutine());
        }

        IEnumerator IntroRoutine()
        {
            yield return new WaitForSeconds(1.2f);
            GameHUD.Instance?.Toast("按 F 与世界互动 · 鼠标左键攻击");
        }

        void BuildWorld()
        {
            worldRoot = new GameObject("World").transform;

            // ---------- 地形 ----------
            var terrainGo = new GameObject("Terrain");
            terrainGo.transform.SetParent(worldRoot, false);
            terrainGo.AddComponent<TerrainGenerator>().Generate();

            // ---------- 水面 ----------
            waterGo = new GameObject("Water");
            waterGo.transform.SetParent(worldRoot, false);
            water = waterGo.AddComponent<WaterSurface>();
            water.Build(WorldData.Size, 44);

            // ---------- 天空 ----------
            var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sky.name = "SkyDome";
            Destroy(sky.GetComponent<Collider>());
            sky.transform.SetParent(worldRoot, false);
            sky.transform.localScale = Vector3.one * 1500f;
            var skyShader = Resources.Load<Shader>("Shaders/Sky");
            Material skyMat;
            if (skyShader != null) skyMat = new Material(skyShader);
            else
            {
                var fallback = Shader.Find("Skybox/Procedural");
                skyMat = fallback != null ? new Material(fallback) : new Material(Shader.Find("Standard"));
            }
            var skyRend = sky.GetComponent<MeshRenderer>();
            skyRend.sharedMaterial = skyMat;
            skyRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            skyRend.receiveShadows = false;

            // ---------- 光照 ----------
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(worldRoot, false);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sunGo.transform.rotation = Quaternion.Euler(48f, 34f, 0f);

            var moonGo = new GameObject("Fill");
            moonGo.transform.SetParent(worldRoot, false);
            var fill = moonGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.28f;
            fill.color = new Color(0.55f, 0.72f, 1f);
            fill.shadows = LightShadows.None;
            moonGo.transform.rotation = Quaternion.Euler(-30f, -140f, 0f);

            DayNight = gameObject.AddComponent<DayNightCycle>();
            DayNight.Setup(sun, fill, skyMat, sunGo.transform);

            // ---------- 玩家 ----------
            SpawnPoint = WorldData.PointAt(0f, 30f, 1.2f);
            var playerGo = new GameObject("Player");
            playerGo.transform.position = SpawnPoint;
            Player = playerGo.AddComponent<PlayerCharacter>();
            Player.SetupVisual();

            // ---------- 相机 ----------
            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 58f;
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 1600f;
            camGo.AddComponent<AudioListener>();
            var rig = camGo.AddComponent<CameraRig>();
            rig.Setup(Player.transform);

            // ---------- 世界物件 ----------
            World = new GameObject("Props").AddComponent<WorldBuilder>();
            World.transform.SetParent(worldRoot, false);
            World.Build(this);

            // ---------- HUD ----------
            var touch = gameObject.AddComponent<TouchControls>();
            touch.Init(this);

            var hud = gameObject.AddComponent<GameHUD>();
            hud.Init(this);

            // ---------- 星律小队（Phase 1 / P1-A + P1-B） ----------
            // 2026-11-25 决定：v2.0（TeamManager / EchoSystem）是本场景的实时战斗/回响层；
            // Phase 1 只保留养成数据与 AI 队友/入场退场技/连携等机制，运行时不再叠加
            // 回响属性（ApplyRuntimeBuffs = false → EchoManager.RecalculateStats 与
            // SquadManager 切换时的回响 HP 上限均不生效）。
            AstralBloom.EchoManager.ApplyRuntimeBuffs = false;
            gameObject.AddComponent<AstralBloom.EchoManager>();
            var squad = gameObject.AddComponent<AstralBloom.SquadManager>();
            squad.Init(this);

            // ---------- v2.0 声骸 + 共鸣队伍（TideSong） ----------
            // v2.0 为本场景唯一实时层：战技只收 v2.0 费用（20 体力 + 冷却，不再收 Phase 1 SP）；
            // 回响属性只由 EchoSystem 提供（攻击/生命/体力/暴击/防御/属性加成）；
            // 伤害统一走 CombatUtil，Phase 1 的 DamageMultiplier 不参与结算（且无任何读处）。
            var echoSys = gameObject.AddComponent<EchoSystem>();
            var team = gameObject.AddComponent<TeamManager>();
            team.Initialize(Player);
            echoSys.Recalc();
            echoSys.GrantStarterIfEmpty();

            // ---------- 共鸣面板与 HUD ----------
            var teamPanel = gameObject.AddComponent<TeamPanel>();
            teamPanel.Init(this);
            var echoPanel = gameObject.AddComponent<EchoPanel>();
            echoPanel.Init(this);
            var resHud = gameObject.AddComponent<ResonanceHUD>();
            resHud.Init(this, teamPanel, echoPanel);

            // ---------- 存档 ----------
            SaveSystem.LoadInto(this);
        }

        void Update()
        {
            if (!Paused) PlayTime += Time.deltaTime;

            if (Input.GetKeyDown(KeyCode.Escape) && !ResonanceHUD.PanelOpenStatic) GameHUD.Instance?.TogglePause();
            if (Input.GetKeyDown(KeyCode.Tab) && !ResonanceHUD.PanelOpenStatic) GameHUD.Instance?.ToggleMap();
            if (Input.GetKeyDown(KeyCode.F5)) SaveSystem.SaveFrom(this);

            // 快捷战斗输入转交给玩家
            if (!Paused && Player != null)
            {
                if (InputHub.ConsumeAttack()) Player.OnAttackPressed();
                if (InputHub.ConsumeDodge()) Player.Dodge();
                // 战技 E：唯一费用模型为 v2.0（20 体力 + 角色冷却，见 TeamManager.UseSkill）；
                // 不再先过 Phase 1 SquadManager 的 SP 门槛（2026-11-25 决定，避免双重扣费）。
                if (InputHub.ConsumeSkill()) Player.UseSkill();
                if (InputHub.ConsumeBurst()) Player.TryBurst();
            }
        }

        public Interactable FindNearestInteractable(Vector3 pos, float range = 3.6f)
        {
            Interactable best = null;
            float bestD = range;
            for (int i = 0; i < Interactables.Count; i++)
            {
                var it = Interactables[i];
                if (it == null || !it.CanInteract) continue;
                float d = Vector3.Distance(pos, it.transform.position);
                if (d < bestD) { bestD = d; best = it; }
            }
            return best;
        }

        public void Register(Interactable it)
        {
            if (!Interactables.Contains(it)) Interactables.Add(it);
        }

        float hitStopCooldown;

        public void HitStop(float duration)
        {
            if (Paused || frozen) return;
            if (Time.unscaledTime - hitStopCooldown < 0.02f) return;
            hitStopCooldown = Time.unscaledTime;
            StartCoroutine(HitStopRoutine(duration));
        }

        IEnumerator HitStopRoutine(float d)
        {
            frozen = true;
            Time.timeScale = 0.04f;
            yield return new WaitForSecondsRealtime(d);
            if (!Paused) Time.timeScale = 1f;
            frozen = false;
        }

        public void OnPlayerDied()
        {
            GameHUD.Instance?.Toast("被暗星吞没 · 在营地苏醒");
            Player.Teleport(SpawnPoint);
            Player.hp = Player.maxHP;
            Player.stamina = Player.maxStamina;
            Player.shield = 0f;
            if (TeamManager.Instance != null && TeamManager.Instance.Active != null)
                TeamManager.Instance.Active.hp = TeamManager.Instance.Active.maxHp;
        }

        public void Respawn()
        {
            Player.Teleport(SpawnPoint);
        }

        public void SetPaused(bool p)
        {
            Paused = p;
            Time.timeScale = p ? 0f : 1f;
            Cursor.lockState = p ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = p;
        }


        public void SpawnWave(Vector3 center, int count)
        {
            StartCoroutine(SpawnWaveRoutine(center, count));
        }

        IEnumerator SpawnWaveRoutine(Vector3 center, int count)
        {
            var root = World != null ? World.EnemyRoot : transform;
            for (int i = 0; i < count; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(9f, 16f);
                Vector3 p = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                p.y = WorldData.HeightAt(p.x, p.z);
                if (p.y < WorldData.WaterLevel) p.y = WorldData.WaterLevel + 1f;
                EnemyAI.Spawn(p, 1.15f, 1.05f, root);
                yield return new WaitForSeconds(0.35f);
            }
            GameHUD.Instance?.Toast("星鸣涌动 · 暗星靠近");
        }

        void OnApplicationQuit() { SaveSystem.SaveFrom(this); }
    }

    /// <summary>水面：顶点波动 + 半透明。</summary>
    public class WaterSurface : MonoBehaviour
    {
        Mesh mesh;
        Vector3[] baseVerts;
        Vector3[] verts;
        public float waveAmp = 0.35f;
        public float waveSpeed = 1.1f;

        public void Build(float size, int res)
        {
            mesh = new Mesh { name = "WaterMesh" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            int side = res + 1;
            verts = new Vector3[side * side];
            var uvs = new Vector2[side * side];
            var tris = new int[res * res * 6];

            for (int z = 0; z <= res; z++)
            {
                for (int x = 0; x <= res; x++)
                {
                    int i = z * side + x;
                    float wx = (x / (float)res - 0.5f) * size;
                    float wz = (z / (float)res - 0.5f) * size;
                    verts[i] = new Vector3(wx, 0f, wz);
                    uvs[i] = new Vector2(x / (float)res * 6f, z / (float)res * 6f);
                }
            }
            int t = 0;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int i = z * side + x;
                    tris[t++] = i; tris[t++] = i + side + 1; tris[t++] = i + side;
                    tris[t++] = i; tris[t++] = i + 1; tris[t++] = i + side + 1;
                }
            }

            baseVerts = (Vector3[])verts.Clone();
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();

            var mf = gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialLibrary.Water;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            transform.position = new Vector3(0f, WorldData.WaterLevel, 0f);
        }

        void Update()
        {
            if (mesh == null) return;
            float t = Time.time * waveSpeed;
            for (int i = 0; i < verts.Length; i++)
            {
                var b = baseVerts[i];
                verts[i].y = Mathf.Sin(b.x * 0.22f + t) * waveAmp
                           + Mathf.Cos(b.z * 0.19f + t * 0.8f) * waveAmp * 0.8f;
            }
            mesh.vertices = verts;
            mesh.RecalculateNormals();
        }
    }
}
