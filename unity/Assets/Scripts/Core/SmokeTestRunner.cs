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
            GameManager.SceneGacha,
            GameManager.SceneInventory,
            GameManager.SceneMail,
            GameManager.SceneCloudSave,
            GameManager.SceneStory,
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
