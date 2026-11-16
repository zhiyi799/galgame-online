// ApiClient — UnityWebRequest wrapper for the Cloudflare Workers backend.
// Talks ONLY to the Worker (BaseUrl). No Supabase SDK, no direct DB access.
// Error mapping: network error -> readable message; 401 -> NeedsLogin (the
// caller forces a re-login); 429 -> rate limited; 5xx -> server error.
// All calls are coroutines with a result callback.
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace GalgameOnline.Api
{
    public enum ApiFailureKind
    {
        None,
        Network,      // unreachable / timeout / DNS
        Unauthorized, // 401 — token invalid or expired
        RateLimited,  // 429
        ClientError,  // other 4xx (400/404/409...) — message comes from the server
        ServerError,  // 5xx
        ParseError,   // response was not valid JSON
    }

    public class ApiResult<T>
    {
        public bool Success;
        public T Data;
        public string ErrorMessage;
        public ApiFailureKind Failure;
        public int StatusCode;

        public static ApiResult<T> Ok(T data) =>
            new ApiResult<T> { Success = true, Data = data };

        public static ApiResult<T> Fail(ApiFailureKind kind, string msg, int status = 0) =>
            new ApiResult<T> { Success = false, Failure = kind, ErrorMessage = msg, StatusCode = status };
    }

    public class ApiClient : MonoBehaviour
    {
        public const int TimeoutSeconds = 15;

        public string Token { get; set; }

        private string BaseUrl => Settings.ApiBaseUrl;

        private IEnumerator Send<T>(string method, string path, string jsonBody,
            Action<ApiResult<T>> callback, bool needsAuth) where T : class
        {
            string url = BaseUrl.TrimEnd('/') + path;
            // A malformed base URL makes `new UnityWebRequest(url)` throw
            // UriFormatException synchronously inside the coroutine — e.g. the
            // built-in "<your-subdomain>" placeholder before the Worker is
            // deployed. Fail gracefully like any other network error instead,
            // so callers always get their callback and a readable toast.
            if (Settings.IsPlaceholderUrl || !Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                callback(ApiResult<T>.Fail(ApiFailureKind.Network,
                    "服务器地址还没配置：Worker 尚未部署，请在设置里填写正确的 API 地址。"));
                yield break;
            }
            var req = new UnityWebRequest(url, method);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = TimeoutSeconds;
            req.SetRequestHeader("Accept", "application/json");
            if (!string.IsNullOrEmpty(jsonBody))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            if (needsAuth && !string.IsNullOrEmpty(Token))
                req.SetRequestHeader("Authorization", "Bearer " + Token);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.ConnectionError ||
                req.result == UnityWebRequest.Result.DataProcessingError)
            {
                callback(ApiResult<T>.Fail(ApiFailureKind.Network,
                    "无法连接服务器，请检查网络或 API 地址（设置里可改）。(" + req.error + ")"));
                yield break;
            }

            long status = req.responseCode;
            string text = req.downloadHandler.text ?? "";

            if (status == 401)
            {
                callback(ApiResult<T>.Fail(ApiFailureKind.Unauthorized, "登录已过期，请重新登录。", 401));
                yield break;
            }
            if (status == 429)
            {
                callback(ApiResult<T>.Fail(ApiFailureKind.RateLimited, "请求太频繁，请稍后再试。", 429));
                yield break;
            }
            if (status >= 500)
            {
                callback(ApiResult<T>.Fail(ApiFailureKind.ServerError,
                    "服务器出错（" + status + "），请稍后再试。", (int)status));
                yield break;
            }

            // 4xx / 2xx: parse the body. The Worker returns {error: "..."} on failure.
            T data = null;
            try
            {
                if (!string.IsNullOrEmpty(text))
                    data = JsonUtility.FromJson<T>(text);
            }
            catch (Exception e)
            {
                callback(ApiResult<T>.Fail(ApiFailureKind.ParseError, "服务器返回无法解析：" + e.Message, (int)status));
                yield break;
            }

            if (status >= 400)
            {
                string msg = null;
                try { msg = JsonUtility.FromJson<ErrorBody>(text)?.error; } catch { /* ignore */ }
                if (string.IsNullOrEmpty(msg))
                    msg = "请求失败（" + status + "）。";
                callback(ApiResult<T>.Fail(ApiFailureKind.ClientError, msg, (int)status));
                yield break;
            }

            if (data == null)
            {
                callback(ApiResult<T>.Fail(ApiFailureKind.ParseError, "服务器返回为空。", (int)status));
                yield break;
            }

            callback(ApiResult<T>.Ok(data));
        }

        private IEnumerator Get<T>(string path, Action<ApiResult<T>> cb, bool auth = true) where T : class
        {
            yield return Send<T>("GET", path, null, cb, auth);
        }

        private IEnumerator Post<T>(string path, object body, Action<ApiResult<T>> cb, bool auth = true) where T : class
        {
            string json = body == null ? null : JsonUtility.ToJson(body);
            yield return Send<T>("POST", path, json, cb, auth);
        }

        // --- Auth ---

        public IEnumerator Signup(string username, string password, Action<ApiResult<SignupResponse>> cb)
        {
            yield return Post("/api/sdk/signup", new AuthRequest { username = username, password = password }, cb, false);
        }

        public IEnumerator Login(string username, string password, Action<ApiResult<LoginResponse>> cb)
        {
            yield return Post("/api/sdk/login", new AuthRequest { username = username, password = password }, cb, false);
        }

        public IEnumerator CheckSession(Action<ApiResult<SessionResponse>> cb)
        {
            yield return Get("/api/sdk/session", cb, true);
        }

        // --- Game ---

        public IEnumerator GetBanners(Action<ApiResult<BannerListResponse>> cb)
        {
            yield return Get("/api/game/banners", cb);
        }

        public IEnumerator GetBannerDetail(int id, Action<ApiResult<BannerInfo>> cb)
        {
            yield return Get("/api/game/banner/" + id, cb);
        }

        public IEnumerator Pull(int bannerId, int count, Action<ApiResult<PullResponse>> cb)
        {
            yield return Post("/api/game/gacha/pull", new PullRequest { bannerId = bannerId, count = count }, cb);
        }

        public IEnumerator GetWallet(Action<ApiResult<WalletDto>> cb)
        {
            yield return Get("/api/game/wallet", cb);
        }

        public IEnumerator GetInventory(Action<ApiResult<InventoryResponse>> cb)
        {
            yield return Get("/api/game/inventory", cb);
        }

        public IEnumerator GetMail(Action<ApiResult<MailListResponse>> cb)
        {
            yield return Get("/api/game/mail", cb);
        }

        public IEnumerator ClaimMail(string mailId, Action<ApiResult<ClaimResponse>> cb)
        {
            yield return Post("/api/game/mail/" + mailId + "/claim", null, cb);
        }

        public IEnumerator UploadSave(int slot, string dataJson, Action<ApiResult<SaveResponse>> cb)
        {
            yield return Post("/api/game/save", new SaveUploadRequest { slot = slot, dataJson = dataJson }, cb);
        }

        public IEnumerator DownloadSave(int slot, Action<ApiResult<SaveResponse>> cb)
        {
            yield return Get("/api/game/save/" + slot, cb);
        }

        public IEnumerator GameStatus(Action<ApiResult<SessionResponse>> cb)
        {
            // /api/game/status returns {ok:true}; reuse SessionResponse shape (only .ok is read).
            yield return Get("/api/game/status", cb, false);
        }
    }
}
