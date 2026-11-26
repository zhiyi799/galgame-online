// SmokeTestRunner — automated in-player self-test, only active when the
// player is launched with "-smoke" (see Editor/BuildScript.cs).
// Walks every scene (Auth -> MainMenu -> Gacha -> Inventory -> Mail ->
// CloudSave -> Story), waits for each screen's network attempts to finish,
// and checks that nothing throws. The Worker is NOT deployed, so every
// online call must fail gracefully (readable toast, no exception).
// Logs "[SMOKE] ..." lines; ends with "[SMOKE] PASS" + exit(0), or
// "[SMOKE] FAIL: ..." + exit(1) if any Exception/Error is logged.
// Launch with "-smoke -gacha-shot" to capture only the Gacha screen to
// /tmp/gacha-screen-v2.png and exit (used to verify a gacha redesign).
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using GalgameOnline.Screens;

namespace GalgameOnline.Core
{
    public class SmokeTestRunner : MonoBehaviour
    {
        private static SmokeTestRunner _instance;
        private static bool _failed;
        private static string _failReason = "";

        private static readonly string[] Sequence = new[]
        {
            GameManager.SceneMainMenu,
            GameManager.ScenePlayerHome,
            GameManager.SceneProtagonistSelect,
            GameManager.SceneGacha,
            GameManager.SceneInventory,
            GameManager.SceneMail,
            GameManager.SceneCloudSave,
            GameManager.SceneStory,
            GameManager.SceneExploration,
            GameManager.SceneWorldMap,
        };

        public static void Run(MonoBehaviour owner)
        {
            if (_instance != null) return;
            var go = new GameObject("SmokeTestRunner");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SmokeTestRunner>();
            _instance.StartCoroutine(_instance.Drive());
        }

        private void OnEnable()
        {
            Application.logMessageReceived += OnLog;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= OnLog;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error)
            {
                if (!_failed)
                {
                    _failed = true;
                    _failReason = type + ": " + condition;
                    Debug.Log("[SMOKE] FAIL: " + _failReason);
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log("[SMOKE] scene loaded: " + scene.name);
        }

        private IEnumerator Drive()
        {
            // Protagonist persistence check (restart test, run 2): verify
            // the saved selection matches -protagonist-gender <g>. No scene
            // load — the value must survive a full process restart.
            if (HasArg("-protagonist-check"))
            {
                string expected = ArgValue("-protagonist-gender");
                ProtagonistManager.InvalidateCache();
                string saved = ProtagonistManager.SelectedGender;
                Debug.Log("[SMOKE] protagonist check: saved=" + (saved ?? "null") + " expected=" + expected);
                if (!string.IsNullOrEmpty(expected) && saved == expected)
                {
                    Debug.Log("[SMOKE] protagonist check PASS");
                    FlushAndExit(0);
                }
                else
                {
                    Debug.Log("[SMOKE] FAIL: protagonist check mismatch");
                    FlushAndExit(1);
                }
                yield break;
            }

            // Protagonist selection test (restart test, run 1): pick the
            // gender from -protagonist-gender, save it, verify read-back.
            if (HasArg("-protagonist-test"))
            {
                string gender = ArgValue("-protagonist-gender");
                Debug.Log("[SMOKE] protagonist-test mode, gender=" + gender);
                var testOp = SceneManager.LoadSceneAsync(GameManager.SceneProtagonistSelect);
                yield return testOp;
                yield return new WaitForSeconds(1.5f);
                var testScreen = FindObjectOfType<ProtagonistSelectScreen>();
                if (testScreen == null)
                {
                    Debug.Log("[SMOKE] FAIL: ProtagonistSelectScreen not found");
                    FlushAndExit(1);
                    yield break;
                }
                bool saved = testScreen.ConfirmForTest(gender);
                ProtagonistManager.InvalidateCache();
                string back = ProtagonistManager.SelectedGender;
                Debug.Log("[SMOKE] protagonist-test saved=" + saved + " readback=" + (back ?? "null"));
                if (saved && back == gender)
                {
                    Debug.Log("[SMOKE] protagonist-test PASS");
                    FlushAndExit(0);
                }
                else
                {
                    Debug.Log("[SMOKE] FAIL: protagonist-test save/readback mismatch");
                    FlushAndExit(1);
                }
                yield break;
            }

            // Protagonist screenshot mode: capture the select screen,
            // optionally with one gender pre-selected (-protagonist-gender).
            if (HasArg("-protagonist-shot"))
            {
                string gender = ArgValue("-protagonist-gender");
                Debug.Log("[SMOKE] protagonist-shot mode, gender=" + (gender ?? "none"));
                var shotOp = SceneManager.LoadSceneAsync(GameManager.SceneProtagonistSelect);
                yield return shotOp;
                yield return new WaitForSeconds(2f);
                var shotScreen = FindObjectOfType<ProtagonistSelectScreen>();
                if (shotScreen != null && !string.IsNullOrEmpty(gender))
                    shotScreen.PreviewGender(gender, false);
                yield return new WaitForSeconds(0.6f);
                string path = "/tmp/protagonist-select"
                    + (string.IsNullOrEmpty(gender) ? "" : "-" + gender) + ".png";
                ScreenCapture.CaptureScreenshot(path);
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.5f);
                Debug.Log("[SMOKE] protagonist screenshot saved: " + path);
                FlushAndExit(0);
                yield break;
            }

            // Auto-pull test: log in with -token, load Gacha, do ONE real
            // single pull through the real client (costs 160 on the test
            // account), then log the local history record count and
            // screenshot the result overlay. Verifies pull -> history file
            // -> result screen end to end.
            if (HasArg("-auto-pull-test"))
            {
                Debug.Log("[SMOKE] auto-pull-test mode");
                string token = ArgValue("-token");
                if (string.IsNullOrEmpty(token))
                {
                    Debug.Log("[SMOKE] FAIL: auto-pull-test needs -token <jwt>");
                    FlushAndExit(1);
                    yield break;
                }
                GameManager.Instance.SetLoggedIn("deploytest", token);
                Debug.Log("[SMOKE] auto-pull-test online (token supplied)");
                var pullOp = SceneManager.LoadSceneAsync(GameManager.SceneGacha);
                yield return pullOp;
                yield return new WaitForSeconds(18f);
                var gacha = FindObjectOfType<GachaScreen>();
                if (gacha == null)
                {
                    Debug.Log("[SMOKE] FAIL: GachaScreen not found");
                    FlushAndExit(1);
                    yield break;
                }
                gacha.AutoPullForTest();
                // Pull (<= 15 s timeout) + result reveal (~4 s) + margin.
                yield return new WaitForSeconds(25f);
                string histPath = GalgameOnline.Api.PullHistoryStore.GetFilePath("deploytest");
                var recs = GalgameOnline.Api.PullHistoryStore.Load("deploytest");
                Debug.Log("[SMOKE] auto-pull-test history file: " + histPath + " records=" + recs.Count);
                ScreenCapture.CaptureScreenshot("/tmp/gacha-result-p12.png");
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.5f);
                Debug.Log("[SMOKE] auto-pull-test result screenshot saved: /tmp/gacha-result-p12.png");
                FlushAndExit(0);
                yield break;
            }

            // Homepage screenshot modes: capture the PlayerHome grid, the
            // Characters page, or the DataBank page and exit. Same pattern
            // as -gacha-shot: a "-token <jwt>" logs in so the inventory
            // counts are real; without a token the pages show the honest
            // offline state.
            if (HasArg("-homepage-shot") || HasArg("-homepage-characters-shot")
                || HasArg("-homepage-databank-shot") || HasArg("-homepage-gallery-shot"))
            {
                bool chars = HasArg("-homepage-characters-shot");
                bool bank = HasArg("-homepage-databank-shot");
                bool gallery = HasArg("-homepage-gallery-shot");
                string mode = chars ? "characters" : (bank ? "databank" : (gallery ? "gallery" : "grid"));
                Debug.Log("[SMOKE] homepage-shot mode: " + mode);
                string token = ArgValue("-token");
                if (!string.IsNullOrEmpty(token))
                {
                    GameManager.Instance.SetLoggedIn("deploytest", token);
                    Debug.Log("[SMOKE] homepage-shot online (token supplied)");
                }
                else
                {
                    GameManager.Instance.SetOffline();
                }
                var homeOp = SceneManager.LoadSceneAsync(GameManager.ScenePlayerHome);
                yield return homeOp;
                yield return new WaitForSeconds(18f);
                var home = FindObjectOfType<PlayerHomeScreen>();
                if (home == null)
                {
                    Debug.Log("[SMOKE] FAIL: PlayerHomeScreen not found");
                    FlushAndExit(1);
                    yield break;
                }
                if (chars) home.OpenCharactersForShot();
                else if (bank) home.OpenDataBankForShot();
                else if (gallery) home.OpenGalleryForShot();
                yield return new WaitForSeconds(1.5f);
                string shotPath = "/tmp/player-home"
                    + (chars ? "-characters" : (bank ? "-databank" : (gallery ? "-gallery" : ""))) + ".png";
                ScreenCapture.CaptureScreenshot(shotPath);
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.5f);
                Debug.Log("[SMOKE] homepage screenshot saved: " + shotPath);
                FlushAndExit(0);
                yield break;
            }

            // Exploration screenshot mode: load the 3D world, wait for
            // terrain generation, capture the world view, then open the full
            // map and capture the HUD view. No login needed (local world).
            if (HasArg("-exploration-shot"))
            {
                Debug.Log("[SMOKE] exploration-shot mode");
                var exOp = SceneManager.LoadSceneAsync(GameManager.SceneExploration);
                yield return exOp;
                yield return new WaitForSeconds(5f);
                var gm3d = TideSong.GameManager.Instance;
                if (gm3d == null || gm3d.Player == null)
                {
                    Debug.Log("[SMOKE] FAIL: TideSong GameManager/Player not found");
                    FlushAndExit(1);
                    yield break;
                }
                Debug.Log("[SMOKE] exploration world ready, player at "
                    + gm3d.Player.transform.position);
                ScreenCapture.CaptureScreenshot("/tmp/exploration-world.png");
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.5f);
                var hud = TideSong.GameHUD.Instance;
                if (hud != null) hud.ToggleMap();
                yield return new WaitForSeconds(0.8f);
                ScreenCapture.CaptureScreenshot("/tmp/exploration-hud.png");
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.5f);
                Debug.Log("[SMOKE] exploration screenshots saved");
                FlushAndExit(0);
                yield break;
            }

            // Map-fix screenshot mode (2026-11-26): three shots for the map
            // fixes — (a) spawn view on the dry west-shore camp facing the
            // altar, (b) lake view facing west (new water gradient +
            // shoreline), (c) Tab map (new water/sand colours, arrow player
            // marker, camp + island labels). Also asserts the player stands
            // on dry land (ground > water level). Saves into the project docs.
            if (HasArg("-mapfix-shot"))
            {
                Debug.Log("[SMOKE] mapfix-shot mode");
                var exOp = SceneManager.LoadSceneAsync(GameManager.SceneExploration);
                yield return exOp;
                yield return new WaitForSeconds(5f);
                var gm3d = TideSong.GameManager.Instance;
                if (gm3d == null || gm3d.Player == null)
                {
                    Debug.Log("[SMOKE] FAIL: TideSong GameManager/Player not found");
                    FlushAndExit(1);
                    yield break;
                }
                Vector3 pp = gm3d.Player.transform.position;
                float ground = TideSong.WorldData.HeightAt(pp.x, pp.z);
                Debug.Log("[SMOKE] mapfix player at " + pp + " ground=" + ground
                    + " water=" + TideSong.WorldData.WaterLevel
                    + " camp=" + gm3d.CampCenter);
                if (ground <= TideSong.WorldData.WaterLevel)
                {
                    Debug.Log("[SMOKE] FAIL: spawn point is underwater (ground " + ground + ")");
                    FlushAndExit(1);
                    yield break;
                }
                // 地图修复 v2 诊断：营地地面在截图里仍是灰蓝 (116,134,161)，
                // 与 ShadeAt 的草绿顶点色不符。直接在运行时查：地形材质用的
                // 哪个 shader、Resources.Load 能不能拿到 TerrainVC、网格里
                // 营地附近的顶点色到底是什么——定位是 shader 回退还是顶点色丢失。
                var terrainGo = GameObject.Find("Terrain");
                if (terrainGo != null)
                {
                    var mr = terrainGo.GetComponent<MeshRenderer>();
                    if (mr != null && mr.sharedMaterial != null)
                        Debug.Log("[SMOKE] mapfix terrain material shader="
                            + mr.sharedMaterial.shader.name);
                    var mf = terrainGo.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var verts = mf.sharedMesh.vertices;
                        var cols = mf.sharedMesh.colors;
                        float best = float.MaxValue; int bi = -1;
                        for (int i = 0; i < verts.Length; i++)
                        {
                            float d = (verts[i].x + 18f) * (verts[i].x + 18f)
                                + verts[i].z * verts[i].z;
                            if (d < best) { best = d; bi = i; }
                        }
                        if (bi >= 0)
                            Debug.Log("[SMOKE] mapfix terrain vertex near camp ("
                                + verts[bi].x.ToString("F1") + "," + verts[bi].z.ToString("F1")
                                + ") color=" + cols[bi] + " (mesh colors=" + cols.Length + ")");
                    }
                }
                var sh = Resources.Load<Shader>("Shaders/TerrainVC");
                Debug.Log("[SMOKE] mapfix Resources.Load TerrainVC "
                    + (sh == null ? "NULL (fallback Standard!)" : "OK"));
                Debug.Log("[SMOKE] mapfix ShadeAt camp expected="
                    + TideSong.TerrainGenerator.ShadeAt(-18f, 0f, 3.34f));
                if (gm3d.Interactables != null)
                {
                    foreach (var it in gm3d.Interactables)
                    {
                        if (it == null) continue;
                        Vector3 p = it.transform.position;
                        Debug.Log("[SMOKE] mapfix interactable " + it.GetType().Name
                            + " at (" + p.x.ToString("F1") + "," + p.z.ToString("F1")
                            + ") h=" + p.y.ToString("F1"));
                    }
                }
                const string docs = "/home/hatch/workspace/galgame-online/docs/";
                ScreenCapture.CaptureScreenshot(docs + "exploration-spawn-west-shore-v2-2026-11-26.png");
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.5f);
                // (b) 湖面：相机朝西（yaw -90°），看沙滩 + 渐变水面 + 岸线。
                var rig = TideSong.CameraRig.Instance;
                if (rig != null) rig.yaw = -90f;
                yield return new WaitForSeconds(1f);
                ScreenCapture.CaptureScreenshot(docs + "exploration-lake-v2-2026-11-26.png");
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.5f);
                if (rig != null) rig.yaw = 90f;
                yield return new WaitForSeconds(0.5f);
                var hud = TideSong.GameHUD.Instance;
                if (hud != null) hud.ToggleMap();
                yield return new WaitForSeconds(0.8f);
                ScreenCapture.CaptureScreenshot(docs + "exploration-map-v2-2026-11-26.png");
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.5f);
                Debug.Log("[SMOKE] mapfix screenshots saved");
                FlushAndExit(0);
                yield break;
            }

            // Phase 1 / P1-A + P1-B 小队测试：加载 Exploration，验证 4 人同屏、
            // 切换、失衡打满（连携就绪）、回响掉落/装备/主动技能，各截一张图。
            if (HasArg("-squad-shot"))
            {
                Debug.Log("[SMOKE] squad-shot mode");
                var exOp = SceneManager.LoadSceneAsync(GameManager.SceneExploration);
                yield return exOp;
                yield return new WaitForSeconds(5f);
                var sq = AstralBloom.SquadManager.Instance;
                var gm3d = TideSong.GameManager.Instance;
                if (sq == null || gm3d == null || gm3d.Player == null)
                {
                    Debug.Log("[SMOKE] FAIL: SquadManager/GameManager/Player not found");
                    FlushAndExit(1);
                    yield break;
                }
                var player = gm3d.Player;
                var mates = FindObjectsOfType<AstralBloom.SquadCompanion>();
                Debug.Log("[SMOKE] squad companions=" + mates.Length + " active=" + sq.ActiveIndex);
                if (mates.Length != 3)
                {
                    Debug.Log("[SMOKE] FAIL: expected 3 companions, found " + mates.Length);
                    FlushAndExit(1);
                    yield break;
                }
                // 4 人同屏：3 名队友排到玩家前方 3 m（一字排开，<4 m 不触发跟随）
                Vector3 fwd = player.transform.forward; fwd.y = 0f; fwd.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                for (int i = 0; i < mates.Length; i++)
                {
                    Vector3 p = player.transform.position + fwd * 3f + right * (i - 1) * 2.5f;
                    p.y = TideSong.WorldData.HeightAt(p.x, p.z);
                    mates[i].transform.position = p;
                }
                ScreenCapture.CaptureScreenshot("/home/hatch/workspace/galgame-online/docs/phase1-squad-4.png");
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.5f);
                Debug.Log("[SMOKE] squad 4-person screenshot saved");
                // 切换到队员 2（星夜·雪，炽）
                sq.SwitchTo(1);
                yield return new WaitForSeconds(0.5f);
                Debug.Log("[SMOKE] squad switched to index=" + sq.ActiveIndex + " (expect 1)");
                if (sq.ActiveIndex != 1)
                {
                    Debug.Log("[SMOKE] FAIL: switch did not change active member");
                    FlushAndExit(1);
                    yield break;
                }
                ScreenCapture.CaptureScreenshot("/home/hatch/workspace/galgame-online/docs/phase1-squad-switch.png");
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.5f);
                // 失衡打满：附近无敌人时生成一个
                TideSong.EnemyAI enemy = null;
                float bestD = 30f;
                foreach (var e in FindObjectsOfType<TideSong.EnemyAI>())
                {
                    if (e == null || e.Dead) continue;
                    float d = Vector3.Distance(player.transform.position, e.transform.position);
                    if (d < bestD) { bestD = d; enemy = e; }
                }
                if (enemy == null)
                {
                    Vector3 ep = player.transform.position + fwd * 6f;
                    enemy = TideSong.EnemyAI.Spawn(ep);
                    yield return new WaitForSeconds(0.5f);
                }
                bool filled = sq.TestFillStagger();
                Debug.Log("[SMOKE] squad stagger filled=" + filled + " comboReady=" + sq.ComboReady);
                if (!filled || !sq.ComboReady)
                {
                    Debug.Log("[SMOKE] FAIL: stagger/combo not ready");
                    FlushAndExit(1);
                    yield break;
                }
                ScreenCapture.CaptureScreenshot("/home/hatch/workspace/galgame-online/docs/phase1-squad-stagger.png");
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.5f);
                // 回响：发 3 个回响，装备攻击回响到槽位 1，释放主动技能
                var em = AstralBloom.EchoManager.Instance;
                int uid0 = em.TestGrantEcho(0);
                em.TestGrantEcho(1);
                em.TestGrantEcho(2);
                bool equipped = em.Equip(uid0, sq.ActiveIndex, 0);
                Debug.Log("[SMOKE] echo granted=" + em.InventoryCount + " equipped=" + equipped);
                bool fired = em.TryUseEchoSkill();
                Debug.Log("[SMOKE] echo skill fired=" + fired);
                if (!equipped || !fired)
                {
                    Debug.Log("[SMOKE] FAIL: echo equip/skill failed");
                    FlushAndExit(1);
                    yield break;
                }
                ScreenCapture.CaptureScreenshot("/home/hatch/workspace/galgame-online/docs/phase1-squad-echo.png");
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.5f);
                Debug.Log("[SMOKE] squad-shot done");
                FlushAndExit(0);
                yield break;
            }

            // Phase 1 / P1-B 回响面板测试：加载 MainMenu（无需登录），发 3 个
            // 回响并装备 2 件套，打开面板截图。
            if (HasArg("-echo-shot"))
            {
                Debug.Log("[SMOKE] echo-shot mode");
                var mmOp = SceneManager.LoadSceneAsync(GameManager.SceneMainMenu);
                yield return mmOp;
                yield return new WaitForSeconds(2f);
                if (GalgameOnline.UI.UiFactory.SceneCanvas() == null)
                {
                    Debug.Log("[SMOKE] FAIL: no scene Canvas in MainMenu");
                    FlushAndExit(1);
                    yield break;
                }
                var em = AstralBloom.EchoManager.Instance;
                if (em == null)
                {
                    var go = new GameObject("EchoManager");
                    em = go.AddComponent<AstralBloom.EchoManager>();
                    yield return new WaitForSeconds(0.2f);
                }
                int u0 = em.TestGrantEcho(0);
                int u1 = em.TestGrantEcho(1);
                em.TestGrantEcho(2);
                em.Equip(u0, 0, 0);
                em.Equip(u1, 0, 1);
                em.AddPlates(10);
                AstralBloom.EchoPanel.Open();
                yield return new WaitForSeconds(0.8f);
                ScreenCapture.CaptureScreenshot("/home/hatch/workspace/galgame-online/docs/phase1-echo-panel.png");
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.5f);
                Debug.Log("[SMOKE] echo panel: inventory=" + em.InventoryCount
                    + " plates=" + em.StarPlates + " twoSet=" + em.HasTwoSet(0));
                FlushAndExit(0);
                yield break;
            }

            // Phase 1 / P1-C 工坊测试：加载 WorldMap，传送玩家到工坊用地，
            // 快进生产验证矿→板，超载验证，离线结算验证，各截一张图。
            if (HasArg("-workshop-shot"))
            {
                Debug.Log("[SMOKE] workshop-shot mode");
                var wmOp = SceneManager.LoadSceneAsync(GameManager.SceneWorldMap);
                yield return wmOp;
                yield return new WaitForSeconds(3f);
                var wm = AstralBloom.WorkshopManager.Instance;
                var wplayer = FindObjectOfType<GalgameOnline.WorldMap.WorldMapPlayer>();
                if (wm == null || wplayer == null)
                {
                    Debug.Log("[SMOKE] FAIL: WorkshopManager/WorldMapPlayer not found");
                    FlushAndExit(1);
                    yield break;
                }
                Debug.Log("[SMOKE] workshop facilities=" + wm.Facilities.Count +
                    " power=" + wm.PowerUsed + " complete=" + wm.LineComplete +
                    " running=" + wm.Running);
                // 先重置为标准线（防之前失败运行残留的额外设施污染存档）
                wm.TestPlaceStandardLine();
                Debug.Log("[SMOKE] workshop reset to standard line, facilities=" +
                    wm.Facilities.Count + " power=" + wm.PowerUsed);
                if (!wm.LineComplete || wm.Overloaded)
                {
                    Debug.Log("[SMOKE] FAIL: standard line not complete/powered");
                    FlushAndExit(1);
                    yield break;
                }
                // 传送到工坊用地旁，俯视视角
                Vector3 plot = AstralBloom.WorkshopManager.PlotCenter;
                wplayer.transform.position = new Vector3(plot.x, plot.y + 2f, plot.z + 55f);
                yield return new WaitForSeconds(0.5f);
                wplayer.SetExternalView(
                    new Vector3(plot.x, plot.y + 90f, plot.z + 55f), plot);
                // 快进生产 60 秒：应产出约 10 板（1 板/6s）
                int produced = wm.TestFastForward(60f);
                Debug.Log("[SMOKE] workshop fast-forward 60s produced=" + produced +
                    " warehouse=" + wm.WarehousePlates);
                if (produced < 8)
                {
                    Debug.Log("[SMOKE] FAIL: production too low (expect ~10, got " + produced + ")");
                    FlushAndExit(1);
                    yield break;
                }
                yield return new WaitForSeconds(0.5f);
                ScreenCapture.CaptureScreenshot("/home/hatch/workspace/galgame-online/docs/phase1-workshop-plot.png");
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.5f);
                // 超载：再加 2 个加工站（+80，65→145）
                // v1：超载不再停机，生产以 45% 速度继续（Running 仍为真）。
                wm.TestForceOverload();
                yield return new WaitForSeconds(0.5f);
                Debug.Log("[SMOKE] workshop overload power=" + wm.PowerUsed +
                    " overloaded=" + wm.Overloaded + " running=" + wm.Running +
                    " speed=" + wm.SpeedFactor);
                if (!wm.Overloaded || !wm.Running)
                {
                    Debug.Log("[SMOKE] FAIL: overload not triggered or production stopped");
                    FlushAndExit(1);
                    yield break;
                }
                int beforeOverload = wm.WarehousePlates;
                int producedOverload = wm.TestFastForward(30f);
                Debug.Log("[SMOKE] workshop overload fast-forward 30s produced=" +
                    producedOverload + " (expect ~2 at 45% speed, full speed ~5)");
                if (producedOverload < 1 || producedOverload > 3)
                {
                    Debug.Log("[SMOKE] FAIL: overload production out of range (expect 1-3, got " + producedOverload + ")");
                    FlushAndExit(1);
                    yield break;
                }
                ScreenCapture.CaptureScreenshot("/home/hatch/workspace/galgame-online/docs/phase1-workshop-overload.png");
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.5f);
                // 离线结算：先恢复（拆除超载的 2 个加工站），再设离线 4 小时
                var extras = new System.Collections.Generic.List<AstralBloom.WorkshopManager.Facility>();
                foreach (var f in wm.Facilities)
                    if (f.type == AstralBloom.WorkshopManager.TypeProcessor && f.pos.z > -310f)
                        extras.Add(f);
                foreach (var f in extras) wm.RemoveFacility(f);
                Debug.Log("[SMOKE] workshop overload cleared, power=" + wm.PowerUsed +
                    " running=" + wm.Running);
                int offline = wm.TestSetOfflineHours(4f);
                Debug.Log("[SMOKE] workshop offline 4h granted=" + offline +
                    " warehouse=" + wm.WarehousePlates);
                // 4h × 6/h = 24 板，但仓库上限 50，当前仓库已有快进的板
                if (offline < 0)
                {
                    Debug.Log("[SMOKE] FAIL: offline settlement negative");
                    FlushAndExit(1);
                    yield break;
                }
                // 收取：仓库板 → 星律板
                int collected = wm.CollectPlates();
                Debug.Log("[SMOKE] workshop collected=" + collected +
                    " starPlates=" + wm.StarPlates);
                if (collected <= 0)
                {
                    Debug.Log("[SMOKE] FAIL: nothing to collect");
                    FlushAndExit(1);
                    yield break;
                }
                Debug.Log("[SMOKE] workshop-shot done");
                FlushAndExit(0);
                yield break;
            }

            // Phase 1 / P1-D WorldMap 移动验证：4 条主干道（星环城中心 →
            // 四大区域中心）每 25 m 采样：是否在地形上、坡度、碰撞体、
            // 玩家实地站立；边界接缝用 ±1/±5 m 外推法量台阶；每条路
            // 在边界处截一张实机行走图。注意：地形上没有道路模型，
            // 本测试验证的是“主干道方向上的地形可走性”，不是道路网格。
            if (HasArg("-worldmap-walk"))
            {
                Debug.Log("[SMOKE] worldmap-walk mode");
                var wmOp = SceneManager.LoadSceneAsync(GameManager.SceneWorldMap);
                yield return wmOp;
                yield return new WaitForSeconds(3f);
                var wplayer = FindObjectOfType<GalgameOnline.WorldMap.WorldMapPlayer>();
                if (wplayer == null)
                {
                    Debug.Log("[SMOKE] FAIL: WorldMapPlayer not found");
                    FlushAndExit(1);
                    yield break;
                }
                var routes = new System.Collections.Generic.List<(string, Vector3[], Vector3, Vector3)>();
                // (name, waypoints, boundary crossing point, travel direction there)
                // 注意：东线不走直线穿三地交角点 (2000,0)——该点三块地形高差
                // 331 m（实测，见小结），真实道路应先南下穿永夜再东进苍穹。
                routes.Add(("north-baita", new[] { new Vector3(1000f, 0f, 569f), new Vector3(2000f, 0f, 1707f) }, new Vector3(1500f, 0f, 1138f), new Vector3(0.660f, 0f, 0.751f)));
                routes.Add(("west-wuhai", new[] { new Vector3(1000f, 0f, 569f), new Vector3(0f, 0f, 1707f) }, new Vector3(500f, 0f, 1138f), new Vector3(-0.660f, 0f, 0.751f)));
                routes.Add(("south-yongye", new[] { new Vector3(1000f, 0f, 569f), new Vector3(1000f, 0f, -569f) }, new Vector3(1000f, 0f, 0f), new Vector3(0f, 0f, -1f)));
                routes.Add(("east-skyvault", new[] { new Vector3(1000f, 0f, 569f), new Vector3(1000f, 0f, -569f), new Vector3(3000f, 0f, -569f) }, new Vector3(2000f, 0f, -569f), new Vector3(1f, 0f, 0f)));
                bool anyFail = false;
                foreach (var route in routes)
                {
                    // 沿折线每 25 m 采样
                    var samples = new System.Collections.Generic.List<Vector3>();
                    for (int s = 0; s < route.Item2.Length - 1; s++)
                    {
                        Vector3 segA = route.Item2[s];
                        Vector3 segB = route.Item2[s + 1];
                        float segLen = Vector3.Distance(segA, segB);
                        int segN = Mathf.CeilToInt(segLen / 25f);
                        for (int i = 0; i < segN; i++)
                            samples.Add(Vector3.Lerp(segA, segB, (float)i / segN));
                    }
                    samples.Add(route.Item2[route.Item2.Length - 1]);
                    float maxGrade = 0f;
                    string maxGradeAt = "";
                    int offTerrain = 0;
                    int colliderMiss = 0;
                    int standFail = 0;
                    Vector3 prev = samples[0];
                    float prevH = GalgameOnline.WorldMap.WorldMapTerrain.SampleHeight(prev.x, prev.z);
                    for (int i = 0; i < samples.Count; i++)
                    {
                        Vector3 p = samples[i];
                        float h = GalgameOnline.WorldMap.WorldMapTerrain.SampleHeight(p.x, p.z);
                        bool on = GalgameOnline.WorldMap.WorldMapTerrain.IsOnTerrain(p.x, p.z);
                        if (!on) offTerrain++;
                        float segLen = Vector3.Distance(p, prev);
                        if (segLen > 0.01f)
                        {
                            float g = Mathf.Atan2(Mathf.Abs(h - prevH), segLen) * Mathf.Rad2Deg;
                            if (g > maxGrade)
                            {
                                maxGrade = g;
                                maxGradeAt = string.Format("({0:F0},{1:F0})", p.x, p.z);
                            }
                        }
                        // 碰撞体：从上方 50 m 射线打下来，应命中地形且高度吻合。
                        // 先把玩家挪到远处——玩家自身的胶囊会挡住射线（2026-11-18
                        // 误报：首采样点 (1000,569) 命中了玩家胶囊）。注意：改
                        // transform.position 后必须 Physics.SyncTransforms()，
                        // 否则 Raycast 仍看到旧位置的碰撞体（第二次误报即因此）。
                        wplayer.transform.position = new Vector3(p.x + 500f, h + 100f, p.z + 500f);
                        Physics.SyncTransforms();
                        RaycastHit hit;
                        if (Physics.Raycast(new Vector3(p.x, h + 50f, p.z), Vector3.down, out hit, 100f))
                        {
                            if (hit.collider is TerrainCollider)
                            {
                                if (Mathf.Abs(hit.point.y - h) > 1f)
                                {
                                    colliderMiss++;
                                    Debug.Log(string.Format(
                                        "[SMOKE] walk collider deviation at ({0:F0},{1:F0}): sampleH={2:F1} hitY={3:F1} collider={4}",
                                        p.x, p.z, h, hit.point.y, hit.collider.name));
                                }
                            }
                            else
                            {
                                colliderMiss++;
                                Debug.Log(string.Format(
                                    "[SMOKE] walk collider wrong-hit at ({0:F0},{1:F0}): collider={2}",
                                    p.x, p.z, hit.collider.name));
                            }
                        }
                        else
                        {
                            colliderMiss++;
                            Debug.Log(string.Format(
                                "[SMOKE] walk collider MISS at ({0:F0},{1:F0}): no hit, sampleH={2:F1}",
                                p.x, p.z, h));
                        }
                        // 实地站立：传送玩家到采样点，看是否贴地
                        wplayer.transform.position = new Vector3(p.x, h + 1.2f, p.z);
                        yield return new WaitForSeconds(0.08f);
                        float py = wplayer.transform.position.y;
                        if (on && Mathf.Abs(py - h) > 2.5f) standFail++;
                        prev = p;
                        prevH = h;
                    }
                    // 边界接缝：沿路线方向 ±1/±5 m 外推，量台阶
                    Vector3 dir = route.Item4;
                    Vector3 b = route.Item3;
                    float hM5 = GalgameOnline.WorldMap.WorldMapTerrain.SampleHeight(b.x - dir.x * 5f, b.z - dir.z * 5f);
                    float hM1 = GalgameOnline.WorldMap.WorldMapTerrain.SampleHeight(b.x - dir.x * 1f, b.z - dir.z * 1f);
                    float hP1 = GalgameOnline.WorldMap.WorldMapTerrain.SampleHeight(b.x + dir.x * 1f, b.z + dir.z * 1f);
                    float hP5 = GalgameOnline.WorldMap.WorldMapTerrain.SampleHeight(b.x + dir.x * 5f, b.z + dir.z * 5f);
                    float slopeBefore = (hM1 - hM5) / 4f;
                    float slopeAfter = (hP5 - hP1) / 4f;
                    float seam = Mathf.Abs((hM1 + slopeBefore * 1f) - (hP1 - slopeAfter * 1f));
                    bool routeFail = offTerrain > 0 || colliderMiss > 0 || standFail > 0 || seam > 3f;
                    if (routeFail) anyFail = true;
                    Debug.Log(string.Format(
                        "[SMOKE] walk {0}: samples={1} offTerrain={2} colliderMiss={3} standFail={4} maxGrade={5:F1}° at {6} seam={7:F2}m {8}",
                        route.Item1, samples.Count, offTerrain, colliderMiss, standFail, maxGrade, maxGradeAt, seam,
                        routeFail ? "FAIL" : "OK"));
                    // 边界处实机行走截图
                    float mh = GalgameOnline.WorldMap.WorldMapTerrain.SampleHeight(b.x, b.z);
                    wplayer.transform.position = new Vector3(b.x, mh + 1.2f, b.z);
                    yield return new WaitForSeconds(0.3f);
                    wplayer.SetExternalView(
                        new Vector3(b.x, mh + 60f, b.z + 70f), new Vector3(b.x, mh, b.z));
                    yield return new WaitForSeconds(0.8f);
                    ScreenCapture.CaptureScreenshot("/home/hatch/workspace/galgame-online/docs/phase1-walk-" + route.Item1 + ".png");
                    yield return new WaitForEndOfFrame();
                    yield return new WaitForSeconds(0.5f);
                    wplayer.ClearExternalView();
                    Debug.Log("[SMOKE] walk screenshot saved: phase1-walk-" + route.Item1);
                }
                if (anyFail)
                {
                    Debug.Log("[SMOKE] FAIL: one or more walk routes failed");
                    FlushAndExit(1);
                    yield break;
                }
                Debug.Log("[SMOKE] worldmap-walk done");
                FlushAndExit(0);
                yield break;
            }

            // WorldMap screenshot mode: load the five-region terrain scene,
            // park the camera at a series of viewpoints (one high overview +
            // one overview + one ground view per region), capture each to
            // docs/worldmap-*.png, then exit. No login needed (local world).
            if (HasArg("-worldmap-shot"))
            {
                Debug.Log("[SMOKE] worldmap-shot mode");
                var wmOp = SceneManager.LoadSceneAsync(GameManager.SceneWorldMap);
                yield return wmOp;
                yield return new WaitForSeconds(3f);
                var player = FindObjectOfType<GalgameOnline.WorldMap.WorldMapPlayer>();
                if (player == null)
                {
                    Debug.Log("[SMOKE] FAIL: WorldMapPlayer not found");
                    FlushAndExit(1);
                    yield break;
                }
                var terrains = FindObjectsOfType<Terrain>();
                Debug.Log("[SMOKE] worldmap terrains=" + terrains.Length
                    + " player at " + player.transform.position);
                if (terrains.Length != 5)
                {
                    Debug.Log("[SMOKE] FAIL: expected 5 terrains, found " + terrains.Length);
                    FlushAndExit(1);
                    yield break;
                }
                foreach (var t in terrains)
                    Debug.Log("[SMOKE] worldmap terrain " + t.name + " at "
                        + t.transform.position + " size=" + t.terrainData.size);
                float px = player.transform.position.x;
                float pz = player.transform.position.z;
                float ground = GalgameOnline.WorldMap.WorldMapTerrain.SampleHeight(px, pz);
                Debug.Log("[SMOKE] worldmap spawn check: player y="
                    + player.transform.position.y + " ground=" + ground);
                if (Mathf.Abs(player.transform.position.y - ground) > 2.5f)
                {
                    Debug.Log("[SMOKE] FAIL: player not standing on terrain at spawn");
                    FlushAndExit(1);
                    yield break;
                }
                // Viewpoints: (file, camera pos, lookAt). Ground-view heights
                // are sampled from the terrain at capture time.
                // NOTE (2026-11-18): terrain tiles are positioned by their
                // CORNER, not centre — region centres are xinghuan (1000,569),
                // wuhai (0,1707), baita (2000,1707), yongye (1000,-569),
                // skyvault (3000,-569). The first shot list used the corner
                // coords as centres, so wuhai/baita overviews and ground
                // shots were framed half on empty void. Fixed here.
                var shots = new System.Collections.Generic.List<(string, Vector3, Vector3)>();
                shots.Add(("worldmap-overview", new Vector3(1500f, 3400f, -2800f), new Vector3(1500f, 100f, 569f)));
                shots.Add(("worldmap-xinghuan-overview", new Vector3(1000f, 1100f, -500f), new Vector3(1000f, 40f, 569f)));
                shots.Add(("worldmap-wuhai-overview", new Vector3(0f, 1500f, 700f), new Vector3(0f, 250f, 1707f)));
                shots.Add(("worldmap-baita-overview", new Vector3(2000f, 1400f, 700f), new Vector3(2000f, 200f, 1707f)));
                shots.Add(("worldmap-skyvault-overview", new Vector3(3000f, 2400f, -1700f), new Vector3(3000f, 700f, -569f)));
                shots.Add(("worldmap-yongye-overview", new Vector3(1000f, 1200f, -1600f), new Vector3(1000f, 120f, -569f)));
                foreach (var shot in shots)
                {
                    player.SetExternalView(shot.Item2, shot.Item3);
                    yield return new WaitForSeconds(1.5f);
                    ScreenCapture.CaptureScreenshot("/home/hatch/workspace/galgame-online/docs/" + shot.Item1 + ".png");
                    yield return new WaitForEndOfFrame();
                    yield return new WaitForSeconds(0.5f);
                    Debug.Log("[SMOKE] worldmap screenshot saved: " + shot.Item1);
                }
                // Ground views: camera a few metres above the terrain, looking
                // toward each region's landmark / centre.
                // (file, camX, camZ, lookX, lookZ, camHeightAboveGround)
                // NOTE (2026-11-18): skyvault ground shot stands in the
                // valley floor south of the mesa (3010,-900, ~622 m) looking
                // north up at the energy-tower platform (1100 m, ~38°).
                // Three earlier positions failed: the slope camera (3000,-150)
                // faced into the shadowed cliff face 16 m ahead (blank
                // dark-blue wall); the platform-top camera (3010,-291)
                // looked down across a flat 1100 m top filling the frame;
                // the close valley camera (3010,-550) looked up at 71° and
                // showed only cliff + sky. The far valley floor gives the
                // mesa face, valley and sky in one frame.
                var groundShots = new System.Collections.Generic.List<(string, float, float, float, float, float)>();
                groundShots.Add(("worldmap-xinghuan-ground", 1000f, 300f, 1000f, 569f, 5f));
                groundShots.Add(("worldmap-wuhai-ground", 0f, 1300f, 0f, 2000f, 5f));
                groundShots.Add(("worldmap-baita-ground", 2000f, 1300f, 2000f, 2100f, 5f));
                groundShots.Add(("worldmap-skyvault-ground", 3010f, -900f, 3010f, -291f, 5f));
                groundShots.Add(("worldmap-yongye-ground", 1000f, -200f, 1000f, -800f, 5f));
                // 建模重做 Item 2：永夜湖近景。旧机位 (1330,-850) 高 8 m 正处水面
                // 上 6 m，却朝南看 (1330,-1050)——该点地面 53 m，正南方 -988/
                // -1000 m 处是 103–134 m 山脊：机位贴水面掠角看水，水面只占
                // 画面底部一条，山占满屏。RAW 实测：湖最长连续水面在 z≈-926
                // 一线，x 600–1300（约 700 m），西岸 x<600 陡升至 ~89 m。
                // 新机位沿湖长轴向西看：湖面东端上空 22 m，看 600 m 外西岸，
                // 视线全程水面无遮挡（8 m 掠角看不见水面，故抬到 22 m）。
                groundShots.Add(("worldmap-yongye-lake", 1250f, -926f, 700f, -926f, 22f));
                foreach (var g in groundShots)
                {
                    float ch = GalgameOnline.WorldMap.WorldMapTerrain.SampleHeight(g.Item2, g.Item3);
                    float lh = GalgameOnline.WorldMap.WorldMapTerrain.SampleHeight(g.Item4, g.Item5);
                    player.SetExternalView(
                        new Vector3(g.Item2, ch + g.Item6, g.Item3),
                        new Vector3(g.Item4, lh + 10f, g.Item5));
                    yield return new WaitForSeconds(1.5f);
                    ScreenCapture.CaptureScreenshot("/home/hatch/workspace/galgame-online/docs/" + g.Item1 + ".png");
                    yield return new WaitForEndOfFrame();
                    yield return new WaitForSeconds(0.5f);
                    Debug.Log("[SMOKE] worldmap screenshot saved: " + g.Item1);
                }
                Debug.Log("[SMOKE] worldmap screenshots done");
                FlushAndExit(0);
                yield break;
            }

            // Screenshot mode: capture the Gacha screen and exit.
            // The wait is long on purpose: an unreachable API takes the
            // full 15s request timeout before the offline fallback renders.
            if (HasArg("-gacha-shot"))
            {
                Debug.Log("[SMOKE] gacha-shot mode");
                // Online shot: if a "-token <jwt>" argument is supplied, log in
                // with it instead of going offline. Offline Gacha redirects to
                // Auth on 401 (the Worker answers unauthenticated /api/game/*
                // calls with 401 when reachable), so an offline shot can only
                // ever capture the login screen. The token is NOT logged.
                string token = ArgValue("-token");
                if (!string.IsNullOrEmpty(token))
                {
                    GameManager.Instance.SetLoggedIn("deploytest", token);
                    Debug.Log("[SMOKE] gacha-shot online (token supplied)");
                }
                else
                {
                    GameManager.Instance.SetOffline();
                }
                var shotOp = SceneManager.LoadSceneAsync(GameManager.SceneGacha);
                yield return shotOp;
                yield return new WaitForSeconds(18f);
                // Details shot: open the banner-details overlay for the
                // selected banner and capture it instead of the plain page.
                if (HasArg("-details-shot"))
                {
                    var gacha = FindObjectOfType<GachaScreen>();
                    if (gacha != null) gacha.OpenDetailsForShot();
                    yield return new WaitForSeconds(1.5f);
                    // Diagnostic: dump the details overlay layout so an
                    // empty content area can be diagnosed from the log.
                    var overlay = GameObject.Find("BannerDetailsOverlay");
                    if (overlay != null)
                    {
                        var content = overlay.transform.Find("Panel/Content/Viewport/Content");
                        if (content != null)
                        {
                            var cRt = content.GetComponent<RectTransform>();
                            Debug.Log("[SMOKE] details content children=" + content.childCount
                                + " sizeDelta=" + cRt.sizeDelta + " anchoredPos=" + cRt.anchoredPosition);
                            var ch0 = content.GetChild(0);
                            var tmp0 = ch0.GetComponent<TMPro.TextMeshProUGUI>();
                            Debug.Log("[SMOKE] details child0 text='" + (tmp0 != null ? tmp0.text : "NO TMP")
                                + "' color=" + (tmp0 != null ? tmp0.color.ToString() : "?")
                                + " enabled=" + (tmp0 != null ? tmp0.enabled.ToString() : "?")
                                + " active=" + ch0.gameObject.activeInHierarchy
                                + " fontNull=" + (tmp0 != null ? (tmp0.font == null).ToString() : "?")
                                + " rect=" + ch0.GetComponent<RectTransform>().rect);
                            var title = overlay.transform.Find("Panel/TitleBar/Title");
                            var tmpT = title != null ? title.GetComponent<TMPro.TextMeshProUGUI>() : null;
                            Debug.Log("[SMOKE] details title text='" + (tmpT != null ? tmpT.text : "NO TMP")
                                + "' fontNull=" + (tmpT != null ? (tmpT.font == null).ToString() : "?"));
                        }
                        else Debug.Log("[SMOKE] details content rect NOT FOUND");
                    }
                    else Debug.Log("[SMOKE] details overlay NOT FOUND");
                    ScreenCapture.CaptureScreenshot("/tmp/gacha-details-p12.png");
                    yield return new WaitForEndOfFrame();
                    yield return new WaitForSeconds(0.5f);
                    Debug.Log("[SMOKE] gacha details screenshot saved: /tmp/gacha-details-p12.png");
                    FlushAndExit(0);
                    yield break;
                }
                ScreenCapture.CaptureScreenshot("/tmp/gacha-screen-p12.png");
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.5f);
                Debug.Log("[SMOKE] gacha screenshot saved: /tmp/gacha-screen-p12.png");
                FlushAndExit(0);
                yield break;
            }

            Debug.Log("[SMOKE] start — auth screen ok (this scene loaded)");
            yield return new WaitForSeconds(1.5f);

            // Offline play: no Worker, no login — the client must still work.
            GameManager.Instance.SetOffline();
            Debug.Log("[SMOKE] offline mode set");

            foreach (var scene in Sequence)
            {
                if (_failed) break;
                Debug.Log("[SMOKE] loading " + scene);
                var op = SceneManager.LoadSceneAsync(scene);
                yield return op;
                yield return new WaitForSeconds(2.0f);

                if (scene == GameManager.SceneStory)
                {
                    var story = FindObjectOfType<StoryScreen>();
                    if (story != null)
                    {
                        story.Advance();
                        story.Advance();
                        Debug.Log("[SMOKE] story advanced 2 lines ok");
                    }
                    else
                    {
                        _failed = true;
                        _failReason = "StoryScreen not found in Story scene";
                    }
                }

                if (scene == GameManager.SceneMainMenu)
                {
                    // Offline guard: online screens must refuse with a toast, not crash.
                    var gm = GameManager.Instance;
                    bool allowed = gm.RequireOnline();
                    Debug.Log("[SMOKE] offline guard (RequireOnline should be false): " + allowed);
                    if (allowed)
                    {
                        _failed = true;
                        _failReason = "offline guard did not block RequireOnline";
                    }
                }

                if (scene == GameManager.SceneWorldMap)
                {
                    // WorldMap checks: 5 terrains exist, player stands on the
                    // terrain at the Xinghuan plaza spawn (not falling).
                    var terrains = FindObjectsOfType<Terrain>();
                    Debug.Log("[SMOKE] worldmap terrains=" + terrains.Length);
                    if (terrains.Length != 5)
                    {
                        _failed = true;
                        _failReason = "WorldMap: expected 5 terrains, found " + terrains.Length;
                    }
                    else
                    {
                        foreach (var t in terrains)
                            Debug.Log("[SMOKE] worldmap terrain " + t.name + " at "
                                + t.transform.position + " size=" + t.terrainData.size);
                    }
                    var wmPlayer = FindObjectOfType<GalgameOnline.WorldMap.WorldMapPlayer>();
                    if (wmPlayer == null)
                    {
                        _failed = true;
                        _failReason = "WorldMapPlayer not found in WorldMap scene";
                    }
                    else
                    {
                        Vector3 pp = wmPlayer.transform.position;
                        float g = GalgameOnline.WorldMap.WorldMapTerrain.SampleHeight(pp.x, pp.z);
                        Debug.Log("[SMOKE] worldmap player at " + pp + " ground=" + g
                            + " region=" + GalgameOnline.WorldMap.WorldMapTerrain.RegionAt(pp.x, pp.z));
                        if (Mathf.Abs(pp.y - g) > 2.5f)
                        {
                            _failed = true;
                            _failReason = "WorldMap: player not standing on terrain (y=" + pp.y + " ground=" + g + ")";
                        }
                    }
                }
            }

            if (_failed)
            {
                Debug.Log("[SMOKE] FAIL: " + _failReason);
                FlushAndExit(1);
            }
            else
            {
                Debug.Log("[SMOKE] PASS — all scenes loaded, no exceptions, offline flow works");
                FlushAndExit(0);
            }
        }

        private static bool HasArg(string arg)
        {
            foreach (var a in Environment.GetCommandLineArgs())
                if (a == arg) return true;
            return false;
        }

        private static string ArgValue(string arg)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == arg) return args[i + 1];
            return null;
        }

        private void FlushAndExit(int code)
        {
            // Give the log a moment to flush, then exit with a real exit code.
            StartCoroutine(ExitAfter(0.5f, code));
        }

        private IEnumerator ExitAfter(float seconds, int code)
        {
            yield return new WaitForSeconds(seconds);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            System.Environment.Exit(code);
#endif
        }
    }
}
