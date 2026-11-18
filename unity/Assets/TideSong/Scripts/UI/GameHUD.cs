using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TideSong
{
    /// <summary>全部 UI 用 IMGUI 即时绘制，工程里不需要 Canvas / Prefab。</summary>
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance { get; private set; }

        GameManager gm;
        PlayerCharacter player;
        TouchControls touch;

        Font uiFont;
        GUIStyle sLabel, sSmall, sTitle, sHuge, sCenter, sBtn;
        Texture2D minimapTex;
        readonly List<DamagePop> pops = new List<DamagePop>();
        EnemyAI[] cachedEnemies = new EnemyAI[0];
        float enemyRefresh;

        string toastMsg = "";
        float toastT;
        float hitFlash;
        bool paused;
        bool mapOpen;
        bool victory;
        float victoryT;
        float uiScale = 1f;
        float introT = 5f;
        bool fontTried;

        class DamagePop
        {
            public Vector3 world;
            public string text;
            public Color color;
            public float t;
            public float life;
            public float size;
        }

        public void Init(GameManager manager)
        {
            Instance = this;
            gm = manager;
            player = manager.Player;
            touch = GetComponent<TouchControls>();
            fontTried = true;
            uiFont = LoadFont();
            BuildMinimap();
            Cursor.lockState = InputHub.Mobile ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = InputHub.Mobile;
        }

        // ---------------- 对外接口 ----------------

        public void Toast(string msg)
        {
            toastMsg = msg;
            toastT = 2.6f;
        }

        public void PopText(Vector3 world, string text, Color color)
        {
            pops.Add(new DamagePop { world = world, text = text, color = color, t = 1.35f, life = 1.35f, size = 26f });
        }

        public void PopDamage(Vector3 world, int dmg)
        {
            pops.Add(new DamagePop
            {
                world = world + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0f, 0.4f), Random.Range(-0.3f, 0.3f)),
                text = dmg.ToString(),
                color = new Color(1f, 0.92f, 0.6f),
                t = 0.9f,
                life = 0.9f,
                size = 34f
            });
        }

        public void FlashHit() { hitFlash = 1f; }

        public void TogglePause()
        {
            if (victory) return;
            paused = !paused;
            if (paused) mapOpen = false;
            gm.SetPaused(paused);
            Sfx.Play2D(SfxId.UI);
        }

        public void ToggleMap()
        {
            if (paused || victory) return;
            mapOpen = !mapOpen;
            Sfx.Play2D(SfxId.UI);
        }

        public void ShowVictory()
        {
            victory = true;
            victoryT = 0f;
            FX.Burst(player.transform.position + Vector3.up * 3f, new Color(0.75f, 1f, 1f), 120, 14f, 0.6f, 2f);
        }

        // ---------------- 每帧 ----------------

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (toastT > 0f) toastT -= dt;
            if (hitFlash > 0f) hitFlash -= dt * 2.2f;
            if (introT > 0f) introT -= dt;
            if (victory) victoryT += dt;

            enemyRefresh -= dt;
            if (enemyRefresh <= 0f)
            {
                enemyRefresh = 0.4f;
                cachedEnemies = Object.FindObjectsOfType<EnemyAI>();
            }

            for (int i = pops.Count - 1; i >= 0; i--)
            {
                pops[i].t -= dt;
                if (pops[i].t <= 0f) pops.RemoveAt(i);
            }
        }

        void OnGUI()
        {
            if (gm == null || player == null) return;
            uiScale = Mathf.Clamp(Screen.height / 900f, 0.68f, 2.4f);
            if (uiFont == null && !fontTried) { fontTried = true; uiFont = LoadFont(); }
            if (uiFont != null && GUI.skin.font != uiFont) GUI.skin.font = uiFont;
            EnsureStyles();

            if (!paused && !victory) DrawGameplayHUD();
            DrawEnemyBars();
            DrawPops();
            if (mapOpen) DrawFullMap();
            DrawToast();
            if (touch != null && touch.ShowButtons && !paused && !victory) touch.DrawGUI(uiScale);
            if (paused) DrawPauseMenu();
            if (victory) DrawVictory();
            if (introT > 0f) DrawIntro();
        }

        // ---------------- 绘制：游戏内 HUD ----------------

        void DrawGameplayHUD()
        {
            float s = uiScale;
            var white = ArtUtil.White;

            // ---- 左上：头像 + 血条 ----
            float pad = 22f * s;
            var barArea = new Rect(pad, pad, 320f * s, 92f * s);

            // 头像圆
            float av = 62f * s;
            var avRect = new Rect(barArea.x, barArea.y, av, av);
            Tint(new Color(0.05f, 0.10f, 0.16f, 0.85f));
            GUI.DrawTexture(avRect, ArtUtil.Dot);
            Tint(new Color(0.30f, 0.62f, 0.85f, 0.95f));
            GUI.DrawTexture(new Rect(avRect.x + 4f * s, avRect.y + 4f * s, av - 8f * s, av - 8f * s), ArtUtil.Dot);
            Tint(new Color(0.10f, 0.16f, 0.24f, 1f));
            GUI.DrawTexture(new Rect(avRect.x + 12f * s, avRect.y + 12f * s, av - 24f * s, av - 24f * s), ArtUtil.Dot);
            GUI.Label(new Rect(avRect.x, avRect.y + av * 0.28f, av, av * 0.5f), "Lv." + player.Level, Center(20f * s));

            // 名字 + 等级
            GUI.Label(new Rect(avRect.x + av + 12f * s, barArea.y - 2f * s, 220f * s, 24f * s), "无星者", sSmall);

            // 血条
            float bx = avRect.x + av + 12f * s;
            float by = barArea.y + 26f * s;
            var hpBg = new Rect(bx, by, 250f * s, 20f * s);
            DrawBarBg(hpBg, s);
            float hpK = Mathf.Clamp01(player.hp / player.maxHP);
            Tint(new Color(0.90f, 0.34f, 0.36f));
            GUI.DrawTexture(new Rect(hpBg.x + 2f * s, hpBg.y + 2f * s, (hpBg.width - 4f * s) * hpK, hpBg.height - 4f * s), white);
            Tint(new Color(1f, 0.62f, 0.58f, 0.55f));
            GUI.DrawTexture(new Rect(hpBg.x + 2f * s, hpBg.y + 2f * s, (hpBg.width - 4f * s) * hpK, (hpBg.height - 4f * s) * 0.45f), white);
            GUI.Label(new Rect(hpBg.x + 8f * s, hpBg.y - 1f * s, 200f * s, 22f * s), Mathf.CeilToInt(player.hp) + " / " + Mathf.CeilToInt(player.maxHP), sSmall);

            // 体力条
            var stBg = new Rect(bx, by + 28f * s, 210f * s, 11f * s);
            DrawBarBg(stBg, s);
            float stK = Mathf.Clamp01(player.stamina / player.maxStamina);
            Tint(new Color(0.42f, 0.86f, 0.62f));
            GUI.DrawTexture(new Rect(stBg.x + 2f * s, stBg.y + 2f * s, (stBg.width - 4f * s) * stK, stBg.height - 4f * s), white);

            // 星鸣能量
            var enBg = new Rect(bx, by + 44f * s, 210f * s, 8f * s);
            DrawBarBg(enBg, s);
            float enK = Mathf.Clamp01(player.BurstEnergy / 100f);
            Tint(player.BurstEnergy >= 100f ? new Color(0.75f, 1f, 1f) : new Color(0.35f, 0.72f, 0.95f));
            GUI.DrawTexture(new Rect(enBg.x + 2f * s, enBg.y + 2f * s, (enBg.width - 4f * s) * enK, enBg.height - 4f * s), white);

            // ---- 右上：小地图 ----
            float mapSize = 168f * s;
            var mapRect = new Rect(Screen.width - mapSize - pad, pad, mapSize, mapSize);
            Tint(new Color(0.04f, 0.08f, 0.12f, 0.78f));
            GUI.DrawTexture(new Rect(mapRect.x - 4f * s, mapRect.y - 4f * s, mapRect.width + 8f * s, mapRect.height + 8f * s), white);
            if (minimapTex != null)
            {
                Tint(new Color(1f, 1f, 1f, 0.92f));
                GUI.DrawTexture(mapRect, minimapTex, ScaleMode.StretchToFill);
            }
            DrawMapMarkers(mapRect, 1f);
            Tint(Color.white);
            // 边框
            Tint(new Color(0.45f, 0.82f, 1f, 0.55f));
            GUI.DrawTexture(new Rect(mapRect.x - 4f * s, mapRect.y - 4f * s, mapRect.width + 8f * s, 2f * s), white);
            GUI.DrawTexture(new Rect(mapRect.x - 4f * s, mapRect.y + mapRect.height + 2f * s, mapRect.width + 8f * s, 2f * s), white);
            GUI.DrawTexture(new Rect(mapRect.x - 4f * s, mapRect.y - 4f * s, 2f * s, mapRect.height + 8f * s), white);
            GUI.DrawTexture(new Rect(mapRect.x + mapRect.width + 2f * s, mapRect.y - 4f * s, 2f * s, mapRect.height + 8f * s), white);

            // 时间
            string clock = gm.DayNight != null ? gm.DayNight.ClockText() : "--:--";
            GUI.Label(new Rect(mapRect.x, mapRect.y + mapRect.height + 8f * s, mapRect.width, 22f * s), clock + "   Tab 地图", Right(18f * s));

            // ---- 左下：目标 ----
            float oy = Screen.height - 138f * s;
            Tint(new Color(0.05f, 0.09f, 0.14f, 0.62f));
            GUI.DrawTexture(new Rect(pad - 8f * s, oy - 14f * s, 330f * s, 112f * s), white);
            Tint(new Color(0.55f, 0.9f, 1f));
            GUI.DrawTexture(new Rect(pad - 8f * s, oy - 14f * s, 3f * s, 112f * s), white);
            GUI.Label(new Rect(pad, oy - 8f * s, 320f * s, 24f * s), "星樱回响", sLabel);
            GUI.Label(new Rect(pad, oy + 16f * s, 320f * s, 22f * s), "星律锚点  " + player.ShrinesLit + " / 3", sSmall);
            GUI.Label(new Rect(pad, oy + 40f * s, 320f * s, 22f * s), "星律结晶  " + player.CrystalCount + " / 15", sSmall);
            GUI.Label(new Rect(pad, oy + 64f * s, 320f * s, 22f * s), "宝箱  " + player.ChestOpened + "   等级 " + player.Level, sSmall);

            // ---- 连击 ----
            if (player.comboCount > 1)
            {
                Tint(new Color(0.55f, 0.95f, 1f, Mathf.Clamp01(player.comboTimer)));
                GUI.Label(new Rect(Screen.width * 0.5f - 100f * s, 84f * s, 200f * s, 60f * s), player.comboCount + " HIT", Center(42f * s));
            }
            Tint(Color.white);

            // ---- 交互提示 ----
            var near = player.Nearest;
            if (near != null)
            {
                float w = 380f * s, h = 46f * s;
                var r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height - 176f * s, w, h);
                Tint(new Color(0.05f, 0.10f, 0.16f, 0.82f));
                GUI.DrawTexture(r, white);
                Tint(new Color(0.4f, 0.85f, 1f, 0.9f));
                GUI.DrawTexture(new Rect(r.x, r.y, 3f * s, r.height), white);
                GUI.Label(new Rect(r.x, r.y + 4f * s, r.width - 70f * s, r.height), "  " + near.Prompt, sLabel);
                GUI.Label(new Rect(r.x + r.width - 74f * s, r.y + 8f * s, 68f * s, r.height), "F", Center(24f * s));
            }

            // ---- PC 技能提示 ----
            if (!InputHub.Mobile)
            {
                float w = 470f * s, h = 54f * s;
                var r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height - 70f * s, w, h);
                Tint(new Color(0.05f, 0.09f, 0.14f, 0.55f));
                GUI.DrawTexture(r, white);
                GUI.Label(new Rect(r.x + 10f * s, r.y + 4f * s, w, h),
                    "左键 攻击   右键 闪避   E 技能   Q 星鸣   空格 跳跃/滑翔   Shift 冲刺", sSmall);
            }
            Tint(Color.white);

            // ---- 受伤红屏 ----
            if (hitFlash > 0f)
            {
                Tint(new Color(0.8f, 0.05f, 0.08f, Mathf.Clamp01(hitFlash) * 0.28f));
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
                Tint(Color.white);
            }
        }

        void DrawBarBg(Rect r, float s)
        {
            Tint(new Color(0.02f, 0.04f, 0.07f, 0.85f));
            GUI.DrawTexture(r, ArtUtil.White);
            Tint(new Color(1f, 1f, 1f, 0.10f));
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), ArtUtil.White);
            Tint(Color.white);
        }

        void DrawMapMarkers(Rect mapRect, float scale)
        {
            var white = ArtUtil.White;
            // 玩家
            Vector2 pp = WorldToMap(player.transform.position, mapRect);
            Tint(new Color(0.5f, 1f, 1f));
            GUI.DrawTexture(new Rect(pp.x - 4f, pp.y - 4f, 8f, 8f), ArtUtil.Dot);
            // 朝向
            Vector3 f = player.transform.forward;
            Tint(new Color(0.5f, 1f, 1f, 0.85f));
            GUI.DrawTexture(new Rect(pp.x - 1f + f.x * 5f, pp.y - 1f - f.z * 5f, 3f, 3f), white);

            if (gm.Interactables != null)
            {
                for (int i = 0; i < gm.Interactables.Count; i++)
                {
                    var it = gm.Interactables[i];
                    if (it == null) continue;
                    Vector2 p = WorldToMap(it.transform.position, mapRect);
                    Color c = Color.white;
                    float sz = 5f;
                    if (it is TideShrine) { var sh = (TideShrine)it; c = sh.activated ? new Color(0.35f, 0.9f, 1f) : new Color(0.55f, 0.75f, 0.85f, 0.75f); sz = 7f; }
                    else if (it is TreasureChest) { var ch = (TreasureChest)it; if (ch.opened) continue; c = new Color(1f, 0.85f, 0.35f); }
                    else if (it is GatherNode) { c = new Color(0.5f, 1f, 0.6f, 0.75f); sz = 3.5f; }
                    else if (it is TideAltar) { c = new Color(0.7f, 1f, 1f); sz = 9f; }
                    Tint(c);
                    GUI.DrawTexture(new Rect(p.x - sz * 0.5f, p.y - sz * 0.5f, sz, sz), white);
                }
            }
            Tint(Color.white);
        }

        Vector2 WorldToMap(Vector3 world, Rect mapRect)
        {
            float u = (world.x + WorldData.HalfSize) / WorldData.Size;
            float v = (world.z + WorldData.HalfSize) / WorldData.Size;
            return new Vector2(mapRect.x + Mathf.Clamp01(u) * mapRect.width,
                               mapRect.y + (1f - Mathf.Clamp01(v)) * mapRect.height);
        }

        void DrawEnemyBars()
        {
            if (cachedEnemies == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            float s = uiScale;
            foreach (var e in cachedEnemies)
            {
                if (e == null || e.Dead) continue;
                Vector3 sp = cam.WorldToScreenPoint(e.transform.position + Vector3.up * 1.9f);
                if (sp.z < 0f) continue;
                float dist = Vector3.Distance(cam.transform.position, e.transform.position);
                if (dist > 46f) continue;
                float w = Mathf.Lerp(84f, 42f, Mathf.Clamp01(dist / 46f)) * s;
                float h = 7f * s;
                float x = sp.x - w * 0.5f;
                float y = Screen.height - sp.y;
                Tint(new Color(0.03f, 0.03f, 0.06f, 0.85f));
                GUI.DrawTexture(new Rect(x, y, w, h), ArtUtil.White);
                Tint(new Color(0.85f, 0.25f, 0.42f));
                GUI.DrawTexture(new Rect(x + 1f, y + 1f, (w - 2f) * e.HpRatio, h - 2f), ArtUtil.White);
                Tint(Color.white);
            }
        }

        void DrawPops()
        {
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var p in pops)
            {
                Vector3 sp = cam.WorldToScreenPoint(p.world + Vector3.up * (1f - p.t / p.life) * 1.2f);
                if (sp.z < 0f) continue;
                float k = p.t / p.life;
                var c = p.color;
                c.a = Mathf.Clamp01(k * 1.6f);
                Tint(c);
                GUI.Label(new Rect(sp.x - 90f, Screen.height - sp.y, 180f, 50f), p.text, Center(p.size * uiScale));
            }
            Tint(Color.white);
        }

        void DrawToast()
        {
            if (toastT <= 0f) return;
            float a = Mathf.Clamp01(toastT / 0.6f);
            float s = uiScale;
            float w = Mathf.Min(Screen.width * 0.8f, 620f * s);
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.26f, w, 52f * s);
            Tint(new Color(0.04f, 0.09f, 0.15f, 0.78f * a));
            GUI.DrawTexture(r, ArtUtil.White);
            Tint(new Color(0.5f, 0.9f, 1f, a));
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), ArtUtil.White);
            Tint(new Color(1f, 1f, 1f, a));
            GUI.Label(r, toastMsg, Center(24f * s));
            Tint(Color.white);
        }

        void DrawIntro()
        {
            float a = Mathf.Clamp01(introT / 2f);
            float s = uiScale;
            Tint(new Color(1f, 1f, 1f, a));
            GUI.Label(new Rect(0, Screen.height * 0.36f, Screen.width, 90f * s), "星 樱 物 语", Center(46f * s));
            Tint(new Color(0.6f, 0.85f, 1f, a * 0.9f));
            GUI.Label(new Rect(0, Screen.height * 0.36f + 70f * s, Screen.width, 40f * s), "ASTRAL BLOOM — 星樱物语", Center(18f * s));
            Tint(Color.white);
        }

        // ---------------- 地图 ----------------

        void DrawFullMap()
        {
            var white = ArtUtil.White;
            Tint(new Color(0.01f, 0.03f, 0.06f, 0.88f));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);

            float size = Mathf.Min(Screen.width, Screen.height) * 0.78f;
            var r = new Rect((Screen.width - size) * 0.5f, (Screen.height - size) * 0.5f, size, size);
            Tint(new Color(1f, 1f, 1f, 0.95f));
            if (minimapTex != null) GUI.DrawTexture(r, minimapTex, ScaleMode.StretchToFill);
            DrawMapMarkers(r, 1.6f);

            // 网格
            Tint(new Color(0.4f, 0.8f, 1f, 0.12f));
            for (int i = 1; i < 8; i++)
            {
                GUI.DrawTexture(new Rect(r.x + r.width * i / 8f, r.y, 1f, r.height), white);
                GUI.DrawTexture(new Rect(r.x, r.y + r.height * i / 8f, r.width, 1f), white);
            }
            Tint(Color.white);
            GUI.Label(new Rect(0, r.y - 54f, Screen.width, 40f), "星樱地图 · Tab 关闭", Center(26f * uiScale));
        }

        // ---------------- 暂停 / 胜利 ----------------

        void DrawPauseMenu()
        {
            var white = ArtUtil.White;
            Tint(new Color(0.02f, 0.04f, 0.07f, 0.86f));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);

            float s = uiScale;
            float w = 420f * s, h = 480f * s;
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f - h * 0.5f, w, h);
            Tint(new Color(0.06f, 0.11f, 0.18f, 0.96f));
            GUI.DrawTexture(r, white);
            Tint(new Color(0.4f, 0.85f, 1f, 0.8f));
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), white);

            Tint(Color.white);
            GUI.Label(new Rect(r.x, r.y + 28f * s, r.width, 50f * s), "暂停", Center(38f * s));
            GUI.Label(new Rect(r.x, r.y + 92f * s, r.width, 30f * s), "游戏时间 " + Mathf.FloorToInt(gm.PlayTime / 60f) + " 分 " + Mathf.FloorToInt(gm.PlayTime % 60f) + " 秒", Center(20f * s));

            float bw = 260f * s, bh = 52f * s, bx = r.x + (r.width - bw) * 0.5f, by = r.y + 148f * s;
            if (GUI.Button(new Rect(bx, by, bw, bh), "继续游戏", sBtn)) TogglePause();
            if (GUI.Button(new Rect(bx, by + 66f * s, bw, bh), "回到营地", sBtn)) { gm.Respawn(); TogglePause(); }
            if (GUI.Button(new Rect(bx, by + 132f * s, bw, bh), "保存进度", sBtn)) { SaveSystem.SaveFrom(gm); Toast("进度已保存"); }
            if (GUI.Button(new Rect(bx, by + 198f * s, bw, bh), "退出游戏", sBtn))
            {
                SaveSystem.SaveFrom(gm);
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            if (GUI.Button(new Rect(bx, by + 264f * s, bw, bh), "返回主菜单", sBtn))
            {
                // Back to the ASTRAL BLOOM main menu (UGUI): save first,
                // unpause, and make sure the cursor stays unlocked for the
                // menu — TideSong locks it while exploring.
                SaveSystem.SaveFrom(gm);
                gm.SetPaused(false);
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                SceneManager.LoadScene("MainMenu");
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            Tint(Color.white);
        }

        void DrawVictory()
        {
            var white = ArtUtil.White;
            float a = Mathf.Clamp01(victoryT / 1.2f);
            Tint(new Color(0.02f, 0.06f, 0.10f, 0.72f * a));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
            float s = uiScale;
            Tint(new Color(1f, 1f, 1f, a));
            GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 90f * s), "星 樱 回 响", Center(56f * s));
            Tint(new Color(0.6f, 0.9f, 1f, a));
            GUI.Label(new Rect(0, Screen.height * 0.3f + 84f * s, Screen.width, 44f * s), "星律祭坛已唤醒，暗星退去", Center(24f * s));
            Tint(new Color(1f, 1f, 1f, a * 0.8f));
            GUI.Label(new Rect(0, Screen.height * 0.3f + 140f * s, Screen.width, 40f * s),
                "用时 " + Mathf.FloorToInt(gm.PlayTime) + " 秒 · 等级 " + player.Level + " · 结晶 " + player.CrystalCount, Center(20f * s));

            float bw = 240f * s, bh = 52f * s;
            if (victoryT > 1.6f && GUI.Button(new Rect(Screen.width * 0.5f - bw * 0.5f, Screen.height * 0.58f, bw, bh), "继续探索", sBtn))
            {
                victory = false;
            }
            Tint(Color.white);
        }

        // ---------------- 工具 ----------------

        void Tint(Color c) { GUI.color = c; }

        static GUIStyle MakeStyle(int size, TextAnchor anchor, Color color)
        {
            var st = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                alignment = anchor,
                richText = false
            };
            st.normal.textColor = color;
            st.wordWrap = false;
            return st;
        }

        GUIStyle Center(float size) { return sCenterWith(size); }

        GUIStyle sCenterCache;
        int sCenterSize = -1;

        GUIStyle sCenterWith(float size)
        {
            int px = Mathf.RoundToInt(size);
            if (sCenterCache == null || sCenterSize != px)
            {
                sCenterCache = MakeStyle(px, TextAnchor.MiddleCenter, Color.white);
                sCenterSize = px;
            }
            sCenterCache.normal.textColor = Color.white;
            return sCenterCache;
        }

        GUIStyle Right(float size)
        {
            var st = new GUIStyle(sSmall) { fontSize = Mathf.RoundToInt(size), alignment = TextAnchor.MiddleRight };
            st.normal.textColor = new Color(0.75f, 0.88f, 1f, 0.9f);
            return st;
        }

        void EnsureStyles()
        {
            if (sLabel != null) return;
            sLabel = MakeStyle(Mathf.RoundToInt(21f * uiScale), TextAnchor.MiddleLeft, new Color(0.94f, 0.97f, 1f));
            sSmall = MakeStyle(Mathf.RoundToInt(15f * uiScale), TextAnchor.MiddleLeft, new Color(0.80f, 0.89f, 1f));
            sTitle = MakeStyle(Mathf.RoundToInt(30f * uiScale), TextAnchor.MiddleLeft, Color.white);
            sHuge = MakeStyle(Mathf.RoundToInt(48f * uiScale), TextAnchor.MiddleCenter, Color.white);
            sCenter = MakeStyle(Mathf.RoundToInt(24f * uiScale), TextAnchor.MiddleCenter, Color.white);
            sBtn = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(20f * uiScale),
                alignment = TextAnchor.MiddleCenter
            };
            sBtn.normal.textColor = new Color(0.9f, 0.96f, 1f);
            sBtn.hover.textColor = Color.white;
            if (uiFont != null)
            {
                sLabel.font = uiFont; sSmall.font = uiFont; sTitle.font = uiFont;
                sHuge.font = uiFont; sCenter.font = uiFont; sBtn.font = uiFont;
            }
        }

        Font LoadFont()
        {
            // Prefer the bundled Noto CJK font (same file the TMP font asset
            // uses), so Chinese renders even on machines with no CJK system
            // font installed (e.g. the Linux sandbox player).
            var bundled = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            if (bundled != null) return bundled;

            string[] candidates = new string[]
            {
                "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "SimSun",
                "PingFang SC", "Hiragino Sans GB", "Heiti SC", "STHeiti",
                "Noto Sans CJK SC", "Noto Sans SC", "Source Han Sans SC",
                "Droid Sans Fallback", "WenQuanYi Micro Hei", "Arial Unicode MS"
            };

            var installed = Font.GetOSInstalledFontNames();
            if (installed != null)
            {
                for (int c = 0; c < candidates.Length; c++)
                {
                    for (int i = 0; i < installed.Length; i++)
                    {
                        if (string.Equals(installed[i], candidates[c], System.StringComparison.OrdinalIgnoreCase))
                            return Font.CreateDynamicFontFromOSFont(installed[i], 28);
                    }
                }
                for (int c = 0; c < candidates.Length; c++)
                {
                    for (int i = 0; i < installed.Length; i++)
                    {
                        if (installed[i] != null && installed[i].IndexOf(candidates[c], System.StringComparison.OrdinalIgnoreCase) >= 0)
                            return Font.CreateDynamicFontFromOSFont(installed[i], 28);
                    }
                }
                if (installed.Length > 0)
                {
                    for (int i = 0; i < installed.Length; i++)
                    {
                        if (installed[i] != null && installed[i].IndexOf("CJK", System.StringComparison.OrdinalIgnoreCase) >= 0)
                            return Font.CreateDynamicFontFromOSFont(installed[i], 28);
                    }
                }
            }
            return null;
        }

        void BuildMinimap()
        {
            int size = 160;
            minimapTex = new Texture2D(size, size, TextureFormat.RGB24, false);
            minimapTex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float wx = (x / (float)(size - 1) - 0.5f) * WorldData.Size;
                    float wz = (y / (float)(size - 1) - 0.5f) * WorldData.Size;
                    float h = WorldData.HeightAt(wx, wz);
                    Color c;
                    if (h < WorldData.WaterLevel) c = new Color(0.06f, 0.16f, 0.40f);
                    else if (h < WorldData.WaterLevel + 1.6f) c = new Color(0.62f, 0.58f, 0.42f);
                    else if (h < 20f) c = Color.Lerp(new Color(0.18f, 0.34f, 0.38f), new Color(0.22f, 0.40f, 0.42f), Random.value * 0.4f);
                    else if (h < 34f) c = new Color(0.34f, 0.34f, 0.34f);
                    else c = new Color(0.72f, 0.76f, 0.82f);
                    px[y * size + x] = c;
                }
            }
            minimapTex.SetPixels32(px);
            minimapTex.Apply();
        }
    }
}
