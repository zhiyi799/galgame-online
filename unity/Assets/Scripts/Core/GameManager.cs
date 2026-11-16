// GameManager — persistent singleton (DontDestroyOnLoad).
// Holds the ApiClient, the session (JWT + username), the offline flag, and a
// cached wallet. Created automatically on load via RuntimeInitializeOnLoadMethod,
// so every scene can use GameManager.Instance.
// Token is stored in PlayerPrefs — fine for now, NOT secure storage.
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using GalgameOnline.Api;

namespace GalgameOnline.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public const string SceneAuth = "Auth";
        public const string SceneMainMenu = "MainMenu";
        public const string SceneGacha = "Gacha";
        public const string SceneInventory = "Inventory";
        public const string SceneMail = "Mail";
        public const string SceneCloudSave = "CloudSave";
        public const string SceneStory = "Story";

        public ApiClient Api { get; private set; }

        public string Username { get; private set; }
        public bool IsOffline { get; private set; }
        public bool IsLoggedIn => !IsOffline && !string.IsNullOrEmpty(Api?.Token);
        public WalletDto Wallet { get; private set; }
        public int UnreadMailCount { get; set; }

        private const string PrefsToken = "gm_token";
        private const string PrefsUsername = "gm_username";

        // Smoke-test mode: launched with "-smoke" (see Editor/BuildScript.cs).
        // Runs an automated self-test in the player and quits with an exit code.
        public static bool SmokeMode { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            SmokeMode = HasArg("-smoke");
            var go = new GameObject("GameManager");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GameManager>();
            Instance.Api = go.AddComponent<ApiClient>();
            Instance.Api.Token = PlayerPrefs.GetString(PrefsToken, "");
            Instance.Username = PlayerPrefs.GetString(PrefsUsername, "");
            go.AddComponent<Toast>();
        }

        private static bool HasArg(string arg)
        {
            foreach (var a in Environment.GetCommandLineArgs())
                if (a == arg) return true;
            return false;
        }

        public void SetLoggedIn(string username, string token)
        {
            Username = username;
            IsOffline = false;
            Api.Token = token;
            PlayerPrefs.SetString(PrefsToken, token);
            PlayerPrefs.SetString(PrefsUsername, username);
            PlayerPrefs.Save();
        }

        public void SetOffline()
        {
            IsOffline = true;
            Username = "";
            Api.Token = "";
            PlayerPrefs.DeleteKey(PrefsToken);
            PlayerPrefs.DeleteKey(PrefsUsername);
            PlayerPrefs.Save();
        }

        public void Logout()
        {
            IsOffline = false;
            Username = "";
            Api.Token = "";
            Wallet = null;
            UnreadMailCount = 0;
            PlayerPrefs.DeleteKey(PrefsToken);
            PlayerPrefs.DeleteKey(PrefsUsername);
            PlayerPrefs.Save();
            SceneManager.LoadScene(SceneAuth);
        }

        public void GoTo(string scene) => SceneManager.LoadScene(scene);

        public void RefreshWallet(Action onDone = null)
        {
            if (!IsLoggedIn) { onDone?.Invoke(); return; }
            StartCoroutine(Api.GetWallet(r =>
            {
                if (r.Success) Wallet = r.Data;
                onDone?.Invoke();
            }));
        }

        /// <summary>Online features are disabled while offline; callers show a toast.</summary>
        public bool RequireOnline()
        {
            if (IsOffline)
            {
                Toast.Show("请先登录（当前为离线游玩）。");
                return false;
            }
            if (!IsLoggedIn)
            {
                Toast.Show("请先登录。");
                return false;
            }
            return true;
        }

        /// <summary>401 from any call: the token is dead — drop it and go back to login.</summary>
        public void HandleUnauthorized()
        {
            Api.Token = "";
            PlayerPrefs.DeleteKey(PrefsToken);
            PlayerPrefs.Save();
            Toast.Show("登录已过期，请重新登录。");
            SceneManager.LoadScene(SceneAuth);
        }
    }
}
