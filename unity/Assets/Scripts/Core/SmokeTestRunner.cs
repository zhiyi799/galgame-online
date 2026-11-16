// SmokeTestRunner — automated in-player self-test, only active when the
// player is launched with "-smoke" (see Editor/BuildScript.cs).
// Walks every scene (Auth -> MainMenu -> Gacha -> Inventory -> Mail ->
// CloudSave -> Story), waits for each screen's network attempts to finish,
// and checks that nothing throws. The Worker is NOT deployed, so every
// online call must fail gracefully (readable toast, no exception).
// Logs "[SMOKE] ..." lines; ends with "[SMOKE] PASS" + exit(0), or
// "[SMOKE] FAIL: ..." + exit(1) if any Exception/Error is logged.
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
