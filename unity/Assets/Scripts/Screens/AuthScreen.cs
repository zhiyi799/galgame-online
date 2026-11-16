// AuthScreen — startup scene: login / signup / play offline.
// On launch, if a token is stored, it is validated via GET /api/sdk/session;
// valid -> MainMenu, invalid/expired -> show the login form.
using System.Text.RegularExpressions;
using UnityEngine;
using TMPro;
using GalgameOnline.Api;
using GalgameOnline.Core;
using GalgameOnline.UI;

namespace GalgameOnline.Screens
{
    public class AuthScreen : MonoBehaviour
    {
        private TMP_InputField _userInput;
        private TMP_InputField _passInput;
        private TextMeshProUGUI _statusLabel;
        private TextMeshProUGUI _modeLabel;
        private UnityEngine.UI.Button _submitButton;
        private bool _signupMode;
        private bool _busy;

        private void Start()
        {
            var canvas = UiFactory.SceneCanvas();
            // Night shrine / sakura / moon art from Resources/Backgrounds/Auth
            // if the user has supplied it; otherwise deep ultramarine.
            UiFactory.AddBackground(canvas.transform, "Auth", UiFactory.DeepUltramarine);
            var root = UiFactory.Panel(canvas.transform, "AuthRoot",
                Vector2.zero, Vector2.one, Vector2.zero, UiFactory.Glass(0.88f));

            var title = UiFactory.Label(root.transform, "Title", "GalgameOnline", 44,
                TextAlignmentOptions.Center, UiFactory.MoonWhite, true);
            UiFactory.Place(title.rectTransform, -300, -90, 600, 60);
            title.rectTransform.anchorMin = new Vector2(0.5f, 1);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1);

            var sub = UiFactory.Label(root.transform, "Subtitle", "联网 Galgame · 抽卡 / 存档 / 邮件", 20,
                TextAlignmentOptions.Center, UiFactory.SilverWhite);
            UiFactory.Place(sub.rectTransform, -300, -140, 600, 34);
            sub.rectTransform.anchorMin = new Vector2(0.5f, 1);
            sub.rectTransform.anchorMax = new Vector2(0.5f, 1);

            _modeLabel = UiFactory.Label(root.transform, "Mode", "登录", 26,
                TextAlignmentOptions.Center, UiFactory.MoonWhite, true);
            UiFactory.Place(_modeLabel.rectTransform, -200, -200, 400, 40);
            _modeLabel.rectTransform.anchorMin = new Vector2(0.5f, 1);
            _modeLabel.rectTransform.anchorMax = new Vector2(0.5f, 1);

            _userInput = UiFactory.InputField(root.transform, "UserInput", "用户名（3-32位，字母/数字/下划线）",
                new Vector2(420, 48));
            var uRt = _userInput.GetComponent<RectTransform>();
            uRt.anchorMin = new Vector2(0.5f, 1); uRt.anchorMax = new Vector2(0.5f, 1);
            uRt.anchoredPosition = new Vector2(0, -260);

            _passInput = UiFactory.InputField(root.transform, "PassInput", "密码（至少 8 位）",
                new Vector2(420, 48), true);
            var pRt = _passInput.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.5f, 1); pRt.anchorMax = new Vector2(0.5f, 1);
            pRt.anchoredPosition = new Vector2(0, -322);

            _submitButton = UiFactory.Button(root.transform, "SubmitButton", "登录", OnSubmit, new Vector2(420, 52));
            var sRt = _submitButton.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0.5f, 1); sRt.anchorMax = new Vector2(0.5f, 1);
            sRt.anchoredPosition = new Vector2(0, -392);

            var switchBtn = UiFactory.Button(root.transform, "SwitchButton", "没有账号？去注册", OnSwitchMode,
                new Vector2(420, 40), UiFactory.DeepUltramarine);
            var swRt = switchBtn.GetComponent<RectTransform>();
            swRt.anchorMin = new Vector2(0.5f, 1); swRt.anchorMax = new Vector2(0.5f, 1);
            swRt.anchoredPosition = new Vector2(0, -448);

            var offlineBtn = UiFactory.Button(root.transform, "OfflineButton", "离线游玩（无需登录）", OnOffline,
                new Vector2(420, 40), UiFactory.DeepUltramarine);
            var oRt = offlineBtn.GetComponent<RectTransform>();
            oRt.anchorMin = new Vector2(0.5f, 1); oRt.anchorMax = new Vector2(0.5f, 1);
            oRt.anchoredPosition = new Vector2(0, -496);

            _statusLabel = UiFactory.Label(root.transform, "Status", "", 18,
                TextAlignmentOptions.Center, UiFactory.SakuraPink);
            UiFactory.Place(_statusLabel.rectTransform, -350, -540, 700, 30);
            _statusLabel.rectTransform.anchorMin = new Vector2(0.5f, 1);
            _statusLabel.rectTransform.anchorMax = new Vector2(0.5f, 1);

            var apiLabel = UiFactory.Label(root.transform, "ApiLabel",
                "API: " + Settings.ApiBaseUrl + (Settings.IsPlaceholderUrl ? "\n（服务器未部署：请在主菜单设置里填入部署后的 Workers 地址）" : ""),
                15, TextAlignmentOptions.Center, UiFactory.SilverWhite);
            UiFactory.Place(apiLabel.rectTransform, -450, -580, 900, 44);
            apiLabel.rectTransform.anchorMin = new Vector2(0.5f, 1);
            apiLabel.rectTransform.anchorMax = new Vector2(0.5f, 1);

            // Restore the saved token if there is one.
            var gm = GameManager.Instance;
            if (!string.IsNullOrEmpty(gm.Api.Token) && !string.IsNullOrEmpty(gm.Username))
            {
                _statusLabel.text = "正在验证登录…";
                StartCoroutine(gm.Api.CheckSession(r =>
                {
                    if (r.Success && r.Data.ok)
                    {
                        gm.GoTo(GameManager.SceneMainMenu);
                    }
                    else
                    {
                        _statusLabel.text = "登录已过期，请重新登录。";
                    }
                }));
            }

            if (GameManager.SmokeMode)
                SmokeTestRunner.Run(this);
        }

        private void OnSwitchMode()
        {
            _signupMode = !_signupMode;
            _modeLabel.text = _signupMode ? "注册" : "登录";
            _submitButton.GetComponentInChildren<TextMeshProUGUI>().text = _signupMode ? "注册" : "登录";
            _statusLabel.text = "";
        }

        private void OnSubmit()
        {
            if (_busy) return;
            string user = _userInput.text.Trim();
            string pass = _passInput.text;
            if (!Regex.IsMatch(user, @"^[A-Za-z0-9_]{3,32}$"))
            {
                _statusLabel.text = "用户名 3-32 位：字母、数字、下划线。";
                return;
            }
            if (pass.Length < 8)
            {
                _statusLabel.text = "密码至少 8 位。";
                return;
            }
            if (Settings.IsPlaceholderUrl)
            {
                _statusLabel.text = "服务器未部署：先在主菜单设置里填入 Workers 地址，或用离线游玩。";
                // Still attempt the request — it will fail with a readable network error.
            }
            _busy = true;
            _statusLabel.text = _signupMode ? "注册中…" : "登录中…";
            var gm = GameManager.Instance;
            if (_signupMode)
            {
                StartCoroutine(gm.Api.Signup(user, pass, r =>
                {
                    _busy = false;
                    if (r.Success && r.Data.ok)
                    {
                        _statusLabel.text = "注册成功，请登录。";
                        OnSwitchMode();
                    }
                    else
                    {
                        _statusLabel.text = r.Success ? (r.Data.error ?? "注册失败。") : r.ErrorMessage;
                    }
                }));
            }
            else
            {
                StartCoroutine(gm.Api.Login(user, pass, r =>
                {
                    _busy = false;
                    if (r.Success && r.Data.ok && !string.IsNullOrEmpty(r.Data.token))
                    {
                        gm.SetLoggedIn(user, r.Data.token);
                        gm.GoTo(GameManager.SceneMainMenu);
                    }
                    else
                    {
                        _statusLabel.text = r.Success ? (r.Data.error ?? "登录失败。") : r.ErrorMessage;
                    }
                }));
            }
        }

        private void OnOffline()
        {
            GameManager.Instance.SetOffline();
            Toast.Show("离线游玩：抽卡、邮件、云存档不可用。");
            GameManager.Instance.GoTo(GameManager.SceneMainMenu);
        }
    }
}
