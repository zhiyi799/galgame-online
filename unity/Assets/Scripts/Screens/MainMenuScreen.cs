// MainMenuScreen — HUD (wallet + username) and navigation buttons:
// Story, Gacha, Inventory, Mail (unread badge), Cloud Save, Settings, Logout.
// Refreshes the wallet and unread-mail count on entry (online only).
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GalgameOnline.Api;
using GalgameOnline.Core;
using GalgameOnline.UI;

namespace GalgameOnline.Screens
{
    public class MainMenuScreen : MonoBehaviour
    {
        private TextMeshProUGUI _walletLabel;
        private TextMeshProUGUI _userLabel;
        private GameObject _mailBadge;
        private TextMeshProUGUI _mailBadgeText;
        private GameObject _settingsPanel;
        private TMP_InputField _apiInput;

        private void Start()
        {
            var canvas = UiFactory.SceneCanvas();
            UiFactory.AddBackground(canvas.transform, "MainMenu", UiFactory.DeepUltramarine);
            var root = UiFactory.Panel(canvas.transform, "MenuRoot",
                Vector2.zero, Vector2.one, Vector2.zero, UiFactory.Glass(0.88f));

            var gm = GameManager.Instance;

            var title = UiFactory.Label(root.transform, "Title", "GalgameOnline", 40,
                TextAlignmentOptions.Center, UiFactory.MoonWhite, true);
            title.rectTransform.anchorMin = new Vector2(0.5f, 1);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1);
            UiFactory.Place(title.rectTransform, -300, -60, 600, 56);

            _userLabel = UiFactory.Label(root.transform, "User", "", 20,
                TextAlignmentOptions.Center, UiFactory.SilverWhite);
            _userLabel.rectTransform.anchorMin = new Vector2(0.5f, 1);
            _userLabel.rectTransform.anchorMax = new Vector2(0.5f, 1);
            UiFactory.Place(_userLabel.rectTransform, -300, -116, 600, 30);

            _walletLabel = UiFactory.Label(root.transform, "Wallet", "", 20,
                TextAlignmentOptions.Center, UiFactory.MoonlightBlue);
            _walletLabel.rectTransform.anchorMin = new Vector2(0.5f, 1);
            _walletLabel.rectTransform.anchorMax = new Vector2(0.5f, 1);
            UiFactory.Place(_walletLabel.rectTransform, -300, -148, 600, 30);

            float y = -210;
            AddMenuButton(root.transform, "开始游戏", () => gm.GoTo(GameManager.SceneStory), ref y);
            var gachaBtn = AddMenuButton(root.transform, "🎰 抽卡", () =>
            {
                if (gm.RequireOnline()) gm.GoTo(GameManager.SceneGacha);
            }, ref y);
            var invBtn = AddMenuButton(root.transform, "🎒 背包", () =>
            {
                if (gm.RequireOnline()) gm.GoTo(GameManager.SceneInventory);
            }, ref y);
            var mailBtn = AddMenuButton(root.transform, "✉️ 邮件", () =>
            {
                if (gm.RequireOnline()) gm.GoTo(GameManager.SceneMail);
            }, ref y);
            var saveBtn = AddMenuButton(root.transform, "☁️ 云存档", () =>
            {
                if (gm.RequireOnline()) gm.GoTo(GameManager.SceneCloudSave);
            }, ref y);
            AddMenuButton(root.transform, "⚙️ 设置", ToggleSettings, ref y, UiFactory.DeepUltramarine);
            AddMenuButton(root.transform, gm.IsOffline ? "返回登录" : "退出登录", () =>
            {
                gm.Logout();
            }, ref y, UiFactory.DeepRose);

            // Unread-mail badge on the mail button.
            _mailBadge = UiFactory.Panel(mailBtn.transform, "Badge",
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(34, 34), UiFactory.SakuraPink);
            var bRt = _mailBadge.GetComponent<RectTransform>();
            bRt.pivot = new Vector2(1, 1);
            bRt.anchoredPosition = new Vector2(6, 6);
            _mailBadgeText = UiFactory.Label(_mailBadge.transform, "BadgeText", "", 18,
                TextAlignmentOptions.Center, UiFactory.MoonWhite, true);
            UiFactory.Stretch(_mailBadgeText.rectTransform);
            _mailBadge.SetActive(false);

            BuildSettingsPanel(root.transform);

            if (gm.IsOffline)
            {
                _userLabel.text = "离线游玩";
                _walletLabel.text = "离线模式：钱包、邮件、云存档不可用";
            }
            else
            {
                _userLabel.text = "玩家：" + gm.Username;
                _walletLabel.text = "正在加载钱包…";
                gm.RefreshWallet(() =>
                {
                    _walletLabel.text = gm.Wallet == null
                        ? "钱包加载失败"
                        : $"高级货币 {gm.Wallet.premium} · 免费货币 {gm.Wallet.free} · 抽卡券 {gm.Wallet.tickets}";
                });
                StartCoroutine(gm.Api.GetMail(r =>
                {
                    if (!r.Success) return;
                    int unread = 0;
                    foreach (var m in r.Data.mail)
                        if (!m.read) unread++;
                    gm.UnreadMailCount = unread;
                    if (unread > 0)
                    {
                        _mailBadge.SetActive(true);
                        _mailBadgeText.text = unread.ToString();
                    }
                }));
            }

            if (GameManager.SmokeMode)
                SmokeTestRunner.Run(this);
        }

        private Button AddMenuButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick,
            ref float y, Color? bg = null)
        {
            var btn = UiFactory.Button(parent, label, label, onClick, new Vector2(420, 52), bg);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1);
            rt.anchorMax = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, y);
            y -= 64;
            return btn;
        }

        private void BuildSettingsPanel(Transform root)
        {
            _settingsPanel = UiFactory.Panel(root.transform, "SettingsPanel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(760, 420),
                UiFactory.Glass(0.92f));
            var pRt = _settingsPanel.GetComponent<RectTransform>();
            pRt.anchoredPosition = Vector2.zero;

            var title = UiFactory.Label(_settingsPanel.transform, "Title", "设置 · API 地址", 24,
                TextAlignmentOptions.Center, UiFactory.MoonWhite, true);
            UiFactory.Place(title.rectTransform, -380, -20, 760, 36);
            title.rectTransform.anchorMin = new Vector2(0.5f, 1);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1);

            var hint = UiFactory.Label(_settingsPanel.transform, "Hint",
                "Worker 部署后把 *.workers.dev 地址填在这里；本地开发用 127.0.0.1:8787。\n改完需要重新登录。", 16,
                TextAlignmentOptions.Center, UiFactory.SilverWhite);
            UiFactory.Place(hint.rectTransform, -370, -58, 740, 48);
            hint.rectTransform.anchorMin = new Vector2(0.5f, 1);
            hint.rectTransform.anchorMax = new Vector2(0.5f, 1);

            _apiInput = UiFactory.InputField(_settingsPanel.transform, "ApiInput", "https://…workers.dev",
                new Vector2(680, 48));
            var iRt = _apiInput.GetComponent<RectTransform>();
            iRt.anchorMin = new Vector2(0.5f, 1); iRt.anchorMax = new Vector2(0.5f, 1);
            iRt.anchoredPosition = new Vector2(0, -140);
            _apiInput.text = Settings.ApiBaseUrl;

            var saveBtn = UiFactory.Button(_settingsPanel.transform, "SaveBtn", "保存", () =>
            {
                Settings.ApiBaseUrl = _apiInput.text;
                Toast.Show("API 地址已保存，请重新登录。");
                ToggleSettings();
            }, new Vector2(200, 48));
            var sRt = saveBtn.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0.5f, 1); sRt.anchorMax = new Vector2(0.5f, 1);
            sRt.anchoredPosition = new Vector2(-110, -210);

            var localBtn = UiFactory.Button(_settingsPanel.transform, "LocalBtn", "本地开发地址", () =>
            {
                _apiInput.text = Settings.LocalDevApiBaseUrl;
            }, new Vector2(200, 48), UiFactory.DeepUltramarine);
            var lRt = localBtn.GetComponent<RectTransform>();
            lRt.anchorMin = new Vector2(0.5f, 1); lRt.anchorMax = new Vector2(0.5f, 1);
            lRt.anchoredPosition = new Vector2(110, -210);

            var resetBtn = UiFactory.Button(_settingsPanel.transform, "ResetBtn", "恢复默认", () =>
            {
                Settings.ResetToDefault();
                _apiInput.text = Settings.ApiBaseUrl;
            }, new Vector2(200, 48), UiFactory.DeepUltramarine);
            var rRt = resetBtn.GetComponent<RectTransform>();
            rRt.anchorMin = new Vector2(0.5f, 1); rRt.anchorMax = new Vector2(0.5f, 1);
            rRt.anchoredPosition = new Vector2(-110, -270);

            var closeBtn = UiFactory.Button(_settingsPanel.transform, "CloseBtn", "关闭", ToggleSettings,
                new Vector2(200, 48), UiFactory.DeepUltramarine);
            var cRt = closeBtn.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.5f, 1); cRt.anchorMax = new Vector2(0.5f, 1);
            cRt.anchoredPosition = new Vector2(110, -270);

            _settingsPanel.SetActive(false);
        }

        private void ToggleSettings()
        {
            bool show = !_settingsPanel.activeSelf;
            _settingsPanel.SetActive(show);
            if (show) _apiInput.text = Settings.ApiBaseUrl;
        }
    }
}
