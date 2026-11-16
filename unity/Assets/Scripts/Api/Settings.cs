// Settings — API base URL resolution.
// Priority: PlayerPrefs override (set in the in-game Settings panel) >
// StreamingAssets/api-config.json (shipped with the build) > built-in default.
// The Worker is NOT deployed yet: the default is a placeholder and MUST be
// replaced with the real *.workers.dev URL after `wrangler deploy`.
// Local dev: http://127.0.0.1:8787 (wrangler dev).
using System.IO;
using UnityEngine;

namespace GalgameOnline.Api
{
    public static class Settings
    {
        public const string DefaultApiBaseUrl = "https://galgame-online-api.<your-subdomain>.workers.dev";
        public const string LocalDevApiBaseUrl = "http://127.0.0.1:8787";

        private const string PrefsKey = "api_base_url";
        private const string ConfigFileName = "api-config.json";

        [System.Serializable]
        private class ApiConfigFile
        {
            public string apiBaseUrl;
        }

        public static string ApiBaseUrl
        {
            get
            {
                // 1. In-game setting (PlayerPrefs).
                string fromPrefs = PlayerPrefs.GetString(PrefsKey, "");
                if (!string.IsNullOrEmpty(fromPrefs)) return fromPrefs.Trim();

                // 2. Shipped config file next to the build (StreamingAssets).
                string path = Path.Combine(Application.streamingAssetsPath, ConfigFileName);
                try
                {
                    if (File.Exists(path))
                    {
                        var cfg = JsonUtility.FromJson<ApiConfigFile>(File.ReadAllText(path));
                        if (cfg != null && !string.IsNullOrEmpty(cfg.apiBaseUrl))
                            return cfg.apiBaseUrl.Trim();
                    }
                }
                catch { /* fall through to default */ }

                // 3. Built-in placeholder.
                return DefaultApiBaseUrl;
            }
            set
            {
                PlayerPrefs.SetString(PrefsKey, (value ?? "").Trim());
                PlayerPrefs.Save();
            }
        }

        public static bool IsPlaceholderUrl =>
            ApiBaseUrl.Contains("<your-subdomain>");

        public static void UseLocalDev() => ApiBaseUrl = LocalDevApiBaseUrl;
        public static void ResetToDefault() => PlayerPrefs.DeleteKey(PrefsKey);
    }
}
