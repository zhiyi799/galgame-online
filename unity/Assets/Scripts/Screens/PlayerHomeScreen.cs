// PlayerHomeScreen — 玩家个人主页 (player personal homepage).
// Grid of the 11 menu entries from the user's reference image
// (docs/menu-screen-reference.png): 角色 / 队伍 / 图鉴 / 教程 / 任务 /
// 同行者 / 成就 / 画廊 / 地图 / 背包 / 开发日志, plus a bottom bar
// (logout, data-bank progress, USER ID, mail, settings).
// Layout authority is the reference image; style follows the existing
// ultramarine palette and the v4 gacha refinement (flat panels, hairline
// borders, pale-gold accents, no glass cards, no heavy glow).
//
// HONEST DATA POLICY (same as PullHistoryScreen): every number shown
// comes from real data — the inventory API, the local team store, or the
// static story bible. Entries with no real system yet open a page that
// says 即将开放 plainly; no count or progress is ever fabricated.
//  - 角色 / 图鉴： real inventory (online) — distinct owned item IDs.
//  - 队伍： real local team (TeamStore, PlayerPrefs).
//  - 任务 / 地图： static lists from the story bible (chapter / region
//    names only — no invented rewards or progress numbers).
//  - 教程 / 开发日志： static text about real mechanics / real version.
//  - 同行者 / 成就： no system exists — honest 即将开放 page.
//  - 背包： navigates to the existing Inventory screen.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using GalgameOnline.Api;
using GalgameOnline.Core;
using GalgameOnline.UI;

namespace GalgameOnline.Screens
{
    public class PlayerHomeScreen : MonoBehaviour
    {
        private enum Entry
        {
            Characters, Team, DataBank, Tutorials, Quests,
            Companions, Trophies, Gallery, Map, Backpack, DevLog
        }

        // --- Catalog (server seed 0004_seed.sql; display-only copy) ---
        private static readonly int[] AllItemIds =
            { 101, 102, 103, 104, 105, 201, 202, 203, 204, 205, 301, 302, 303, 304 };
        private const int CatalogCount = 14;

        private static readonly string[] ChapterNames =
        {
            "第一章 · 无星者", "第二章 · 学院的夜晚", "第三章 · 雾海列车",
            "第四章 · 白塔之下", "第五章 · 苍穹之城", "第六章 · 没有名字的人",
            "第七章 · 永夜之森", "第八章 · 星辰尽头"
        };

        private static readonly string[] RegionNames =
            { "星环城", "雾海边境", "白塔遗域", "苍穹工业区", "永夜之森" };
        private static readonly string[] RegionDescs =
        {
            "人类最大都市、学院、研究机构", "未知生物、失序区域",
            "古代文明、星律研究", "能源设施、机械都市", "古老生命、星樱遗迹"
        };

        private static readonly int[] GalleryCharacterIds =
            { 101, 102, 103, 104, 105, 201, 202, 203, 204, 205 };

        // --- Grid layout (1280 x 720) ---
        private const int Cols = 5;
        private const float CellW = 200f;
        private const float CellH = 148f;
        private const float GapX = 14f;
        private const float GapY = 16f;
        private const float GridLeft = 112f;
        private const float GridTop = 100f;

        // --- State (real data only) ---
        private InventoryItemDto[] _items;
        private bool _inventoryLoaded;
        private bool _inventoryFailed;
        private readonly HashSet<int> _ownedIds = new HashSet<int>();
        private readonly Dictionary<int, InventoryItemDto> _ownedById =
            new Dictionary<int, InventoryItemDto>();

        // --- UI refs ---
        private TextMeshProUGUI _charCountLabel;
        private TextMeshProUGUI _teamCountLabel;
        private TextMeshProUGUI _bankCountLabel;
        private TextMeshProUGUI _progressLabel;
        private RectTransform _progressFillRt;
        private const float ProgressBarWidth = 180f;

        private GameObject _subPage;
        private TextMeshProUGUI _subTitle;
        private RectTransform _subContent;
        private TextMeshProUGUI _subFooter;

        private static readonly Dictionary<string, Sprite> _spriteCache =
            new Dictionary<string, Sprite>();

        private void Start()
        {
            var canvas = UiFactory.SceneCanvas();
            AddShrineBackground(canvas.transform);
            var root = UiFactory.Panel(canvas.transform, "HomeRoot",
                Vector2.zero, Vector2.one, Vector2.zero, UiFactory.Glass(0.72f));

            BuildTopBar(root.transform);
            BuildGrid(root.transform);
            BuildHint(root.transform);
            BuildBottomBar(root.transform);
            BuildSubPage(root.transform);
            UpdateCounts();

            if (GameManager.Instance.IsLoggedIn)
            {
                StartCoroutine(GameManager.Instance.Api.GetInventory(r =>
                {
                    if (!r.Success || r.Data == null || r.Data.items == null)
                    {
                        if (r.Failure == ApiFailureKind.Unauthorized)
                        {
                            GameManager.Instance.HandleUnauthorized();
                            return;
                        }
                        _inventoryFailed = true;
                        UpdateCounts();
                        return;
                    }
                    _items = r.Data.items;
                    _ownedIds.Clear();
                    _ownedById.Clear();
                    foreach (var it in _items)
                    {
                        if (it == null || it.count <= 0) continue;
                        _ownedIds.Add(it.itemId);
                        _ownedById[it.itemId] = it;
                    }
                    _inventoryLoaded = true;
                    UpdateCounts();
                }));
            }

            if (GameManager.SmokeMode)
                SmokeTestRunner.Run(this);
        }

        // ---------- background ----------
        // The shrine art imports as Texture2D (not a Sprite), so
        // UiFactory.AddBackground's Resources.Load<Sprite> misses it and
        // falls back to a solid colour. Load it the same way GachaScreen
        // does (Texture2D -> Sprite.Create) and darken it so the menu
        // text stays readable over the scene.
        private void AddShrineBackground(Transform canvas)
        {
            var tex = Resources.Load<Texture2D>("Backgrounds/Gacha");
            if (tex != null)
            {
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f));
                var bg = UiFactory.ArtImage(canvas, "Background", sprite, false);
                var bgRt = bg.rectTransform;
                UiFactory.Stretch(bgRt);
                var dim = UiFactory.Panel(canvas, "BackgroundDim",
                    Vector2.zero, Vector2.one, Vector2.zero,
                    new Color(0.02f, 0.04f, 0.12f, 0.55f));
            }
            else
            {
                UiFactory.AddBackground(canvas, "Gacha", UiFactory.DeepUltramarine);
            }
        }

        // ---------- top bar / grid / bottom bar ----------

        private void BuildTopBar(Transform root)
        {
            var top = UiFactory.Panel(root, "TopBar",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 64),
                UiFactory.Glass(0.92f));
            var topRt = top.GetComponent<RectTransform>();
            topRt.pivot = new Vector2(0.5f, 1);
            topRt.anchoredPosition = Vector2.zero;

            var back = UiFactory.Button(top.transform, "BackButton", "← 返回",
                new UnityAction(() => GameManager.Instance.GoTo(GameManager.SceneMainMenu)),
                new Vector2(110, 44));
            var bRt = back.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0, 0.5f);
            bRt.anchorMax = new Vector2(0, 0.5f);
            bRt.pivot = new Vector2(0, 0.5f);
            bRt.anchoredPosition = new Vector2(12, 0);

            var title = UiFactory.Label(top.transform, "Title",
                "玩家个人主页 · PLAYER HOME", 26,
                TextAlignmentOptions.Left, UiFactory.MoonWhite, true);
            UiFactory.Place(title.rectTransform, 140, -14, 600, 40);

            string protag = ProtagonistManager.HasSelection
                ? "主角：无星者（" + ProtagonistManager.SelectedGenderLabel + "）"
                : "主角：无星者（未选择）";
            var protagLabel = UiFactory.Label(top.transform, "Protagonist", protag, 16,
                TextAlignmentOptions.Right, UiFactory.MoonlightBlue);
            protagLabel.rectTransform.anchorMin = new Vector2(1, 1);
            protagLabel.rectTransform.anchorMax = new Vector2(1, 1);
            protagLabel.rectTransform.pivot = new Vector2(1, 1);
            protagLabel.rectTransform.anchoredPosition = new Vector2(-16, -18);
            protagLabel.rectTransform.sizeDelta = new Vector2(420, 30);
        }

        private void BuildGrid(Transform root)
        {
            // Order matches the reference image, row by row.
            AddEntry(root, Entry.Characters, "角色", "CHARACTERS", "人", 0, 0);
            AddEntry(root, Entry.Team, "队伍", "TEAM", "队", 1, 0);
            AddEntry(root, Entry.DataBank, "图鉴", "DATA BANK", "鉴", 2, 0);
            AddEntry(root, Entry.Tutorials, "教程", "TUTORIALS", "教", 3, 0);
            AddEntry(root, Entry.Quests, "任务", "QUESTS", "任", 4, 0);
            AddEntry(root, Entry.Companions, "同行者", "COMPANIONS", "伴", 0, 1);
            AddEntry(root, Entry.Trophies, "成就", "TROPHIES", "奖", 1, 1);
            AddEntry(root, Entry.Gallery, "画廊", "GALLERY", "画", 2, 1);
            AddEntry(root, Entry.Map, "地图", "MAP", "图", 3, 1);
            AddEntry(root, Entry.Backpack, "背包", "BACKPACK", "包", 4, 1);
            AddEntry(root, Entry.DevLog, "开发日志", "FEEDBACK", "志", 0, 2);
        }

        private void AddEntry(Transform root, Entry entry, string cn, string en,
            string icon, int col, int row)
        {
            float x = GridLeft + col * (CellW + GapX);
            float y = GridTop + row * (CellH + GapY);
            Entry captured = entry;
            var btn = UiFactory.Button(root, "Entry_" + entry, "",
                new UnityAction(() => OpenEntry(captured)),
                new Vector2(CellW, CellH), UiFactory.GlassWhite(0.06f));
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);

            var iconLabel = UiFactory.Label(btn.transform, "Icon", icon, 34,
                TextAlignmentOptions.Center, UiFactory.MoonWhite, true);
            UiFactory.Place(iconLabel.rectTransform, 0, -10, CellW, 48);

            var cnLabel = UiFactory.Label(btn.transform, "Name", cn, 20,
                TextAlignmentOptions.Center, UiFactory.MoonWhite, true);
            UiFactory.Place(cnLabel.rectTransform, 0, -62, CellW, 30);

            var enLabel = UiFactory.Label(btn.transform, "En", en, 11,
                TextAlignmentOptions.Center, UiFactory.SilverWhite);
            UiFactory.Place(enLabel.rectTransform, 0, -92, CellW, 20);

            // Count line: only 角色 / 队伍 / 图鉴 have real counts.
            if (entry == Entry.Characters || entry == Entry.Team || entry == Entry.DataBank)
            {
                var count = UiFactory.Label(btn.transform, "Count", "加载中…", 13,
                    TextAlignmentOptions.Center, UiFactory.PaleGold);
                UiFactory.Place(count.rectTransform, 0, -116, CellW, 22);
                if (entry == Entry.Characters) _charCountLabel = count;
                else if (entry == Entry.Team) _teamCountLabel = count;
                else _bankCountLabel = count;
            }
        }

        private void BuildHint(Transform root)
        {
            var hint = UiFactory.Label(root, "Hint",
                "点击进入各个界面 · 数据来自服务器与本地存档", 14,
                TextAlignmentOptions.Center, UiFactory.SilverWhite);
            hint.rectTransform.anchorMin = new Vector2(0, 1);
            hint.rectTransform.anchorMax = new Vector2(0, 1);
            hint.rectTransform.pivot = new Vector2(0, 1);
            UiFactory.Place(hint.rectTransform, 0, -596, 1280, 24);
        }

        private void BuildBottomBar(Transform root)
        {
            var bar = UiFactory.Panel(root, "BottomBar",
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 72),
                UiFactory.Glass(0.92f));
            var barRt = bar.GetComponent<RectTransform>();
            barRt.pivot = new Vector2(0.5f, 0f);
            barRt.anchoredPosition = Vector2.zero;
            // Hairline divider on top of the bar.
            var divider = UiFactory.Panel(bar.transform, "Divider",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                UiFactory.SilverBorder);
            var dRt = divider.GetComponent<RectTransform>();
            dRt.pivot = new Vector2(0.5f, 1);
            dRt.anchoredPosition = Vector2.zero;

            var logout = UiFactory.Button(bar.transform, "Logout", "退出登录",
                new UnityAction(() => GameManager.Instance.Logout()),
                new Vector2(120, 44), UiFactory.DeepRose);
            var lRt = logout.GetComponent<RectTransform>();
            lRt.anchorMin = new Vector2(0, 0.5f);
            lRt.anchorMax = new Vector2(0, 0.5f);
            lRt.pivot = new Vector2(0, 0.5f);
            lRt.anchoredPosition = new Vector2(16, 0);

            var progLabel = UiFactory.Label(bar.transform, "ProgressLabel", "图鉴进度", 16,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            progLabel.rectTransform.anchorMin = new Vector2(0, 0.5f);
            progLabel.rectTransform.anchorMax = new Vector2(0, 0.5f);
            progLabel.rectTransform.pivot = new Vector2(0, 0.5f);
            UiFactory.Place(progLabel.rectTransform, 160, -15, 80, 30);

            var track = UiFactory.Panel(bar.transform, "ProgressTrack",
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(ProgressBarWidth, 6),
                UiFactory.GlassWhite(0.12f));
            var tRt = track.GetComponent<RectTransform>();
            tRt.pivot = new Vector2(0, 0.5f);
            tRt.anchoredPosition = new Vector2(244, 0);
            var fill = UiFactory.Panel(track.transform, "ProgressFill",
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 6),
                UiFactory.ClassicUltramarine);
            var fRt = fill.GetComponent<RectTransform>();
            fRt.pivot = new Vector2(0, 0.5f);
            fRt.anchoredPosition = Vector2.zero;
            _progressFillRt = fRt;

            _progressLabel = UiFactory.Label(bar.transform, "ProgressText", "— / 14", 16,
                TextAlignmentOptions.Left, UiFactory.PaleGold);
            _progressLabel.rectTransform.anchorMin = new Vector2(0, 0.5f);
            _progressLabel.rectTransform.anchorMax = new Vector2(0, 0.5f);
            _progressLabel.rectTransform.pivot = new Vector2(0, 0.5f);
            UiFactory.Place(_progressLabel.rectTransform, 436, -15, 100, 30);

            var mailBtn = UiFactory.Button(bar.transform, "MailBtn", "邮件",
                new UnityAction(() =>
                {
                    var gm = GameManager.Instance;
                    if (gm.RequireOnline()) gm.GoTo(GameManager.SceneMail);
                }), new Vector2(100, 44));
            var mRt = mailBtn.GetComponent<RectTransform>();
            mRt.anchorMin = new Vector2(1, 0.5f);
            mRt.anchorMax = new Vector2(1, 0.5f);
            mRt.pivot = new Vector2(1, 0.5f);
            mRt.anchoredPosition = new Vector2(-420, 0);

            var settingsBtn = UiFactory.Button(bar.transform, "SettingsBtn", "设置",
                new UnityAction(() => Toast.Show("设置即将开放。")),
                new Vector2(100, 44));
            var sRt = settingsBtn.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(1, 0.5f);
            sRt.anchorMax = new Vector2(1, 0.5f);
            sRt.pivot = new Vector2(1, 0.5f);
            sRt.anchoredPosition = new Vector2(-308, 0);

            var gm2 = GameManager.Instance;
            string userText = gm2.IsOffline || string.IsNullOrEmpty(gm2.Username)
                ? "USER ID: 离线游玩"
                : "USER ID: " + gm2.Username;
            var userLabel = UiFactory.Label(bar.transform, "UserId", userText, 16,
                TextAlignmentOptions.Right, UiFactory.SilverWhite);
            userLabel.rectTransform.anchorMin = new Vector2(1, 0.5f);
            userLabel.rectTransform.anchorMax = new Vector2(1, 0.5f);
            userLabel.rectTransform.pivot = new Vector2(1, 0.5f);
            userLabel.rectTransform.anchoredPosition = new Vector2(-16, -15);
            userLabel.rectTransform.sizeDelta = new Vector2(280, 30);
        }

        // ---------- counts (real data only) ----------

        private void UpdateCounts()
        {
            int ownedChars = 0;
            if (_inventoryLoaded)
            {
                foreach (int id in _ownedIds)
                    if (IsCharacter(id)) ownedChars++;
            }
            int teamCount = TeamStore.Load().Count;
            int bankOwned = 0;
            if (_inventoryLoaded)
            {
                foreach (int id in AllItemIds)
                    if (_ownedIds.Contains(id)) bankOwned++;
            }

            if (_charCountLabel != null)
                _charCountLabel.text = !_inventoryLoaded
                    ? (_inventoryFailed ? "加载失败" : (GameManager.Instance.IsLoggedIn ? "加载中…" : "离线"))
                    : "已拥有 " + ownedChars;
            if (_teamCountLabel != null)
                _teamCountLabel.text = "已编队 " + teamCount + " / " + TeamStore.MaxTeamSize;
            if (_bankCountLabel != null)
                _bankCountLabel.text = !_inventoryLoaded
                    ? (_inventoryFailed ? "加载失败" : (GameManager.Instance.IsLoggedIn ? "加载中…" : "离线"))
                    : "全部 14 · 已拥有 " + bankOwned;
            if (_progressLabel != null)
                _progressLabel.text = _inventoryLoaded ? bankOwned + " / " + CatalogCount : "— / 14";
            if (_progressFillRt != null)
                _progressFillRt.sizeDelta = new Vector2(
                    _inventoryLoaded ? ProgressBarWidth * bankOwned / CatalogCount : 0f, 6f);
        }

        // ---------- sub-page overlay ----------

        private void BuildSubPage(Transform root)
        {
            _subPage = new GameObject("SubPage");
            _subPage.transform.SetParent(root, false);
            var spRt = _subPage.AddComponent<RectTransform>();
            UiFactory.Stretch(spRt);

            var dim = UiFactory.Panel(_subPage.transform, "Dim",
                Vector2.zero, Vector2.one, Vector2.zero,
                new Color(0.02f, 0.04f, 0.12f, 0.72f));

            var panel = UiFactory.Panel(_subPage.transform, "Panel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(1120, 640), UiFactory.Glass(0.96f));
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchoredPosition = Vector2.zero;
            UiFactory.AddSilverBorder(panelRt);

            _subTitle = UiFactory.Label(panel.transform, "Title", "", 26,
                TextAlignmentOptions.Left, UiFactory.MoonWhite, true);
            UiFactory.Place(_subTitle.rectTransform, 20, -14, 700, 40);

            var closeBtn = UiFactory.Button(panel.transform, "CloseButton", "×",
                new UnityAction(CloseSubPage), new Vector2(44, 40));
            var cRt = closeBtn.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0, 1);
            cRt.anchorMax = new Vector2(0, 1);
            cRt.pivot = new Vector2(0, 1);
            cRt.anchoredPosition = new Vector2(1056, -10);
            cRt.sizeDelta = new Vector2(44, 40);

            var (scroll, content) = UiFactory.ScrollView(panel.transform, "List",
                Vector2.zero, Vector2.one, Vector2.zero);
            var sRt = scroll.GetComponent<RectTransform>();
            sRt.offsetMin = new Vector2(16, 64);
            sRt.offsetMax = new Vector2(-16, -56);
            _subContent = content;

            _subFooter = UiFactory.Label(panel.transform, "FooterNote", "", 16,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            _subFooter.rectTransform.anchorMin = new Vector2(0, 0);
            _subFooter.rectTransform.anchorMax = new Vector2(0, 0);
            _subFooter.rectTransform.pivot = new Vector2(0, 0);
            _subFooter.rectTransform.anchoredPosition = new Vector2(20, 14);
            _subFooter.rectTransform.sizeDelta = new Vector2(900, 26);

            var backBtn = UiFactory.Button(panel.transform, "BackButton", "返回",
                new UnityAction(CloseSubPage), new Vector2(110, 40));
            var backRt = backBtn.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(1, 0);
            backRt.anchorMax = new Vector2(1, 0);
            backRt.pivot = new Vector2(1, 0);
            backRt.anchoredPosition = new Vector2(-16, 10);

            _subPage.SetActive(false);
        }

        private void OpenEntry(Entry entry)
        {
            if (entry == Entry.Backpack)
            {
                var gm = GameManager.Instance;
                if (gm.RequireOnline()) gm.GoTo(GameManager.SceneInventory);
                return;
            }
            if (_subPage == null) return;
            UiFactory.ClearChildren(_subContent);
            switch (entry)
            {
                case Entry.Characters:
                    _subTitle.text = "角色 · CHARACTERS";
                    _subFooter.text = "角色来自抽卡与邮件；名字为占位名。";
                    BuildCharacters();
                    break;
                case Entry.Team:
                    _subTitle.text = "队伍 · TEAM";
                    _subFooter.text = "编队仅保存在本机，最多 3 名角色。";
                    BuildTeam();
                    break;
                case Entry.DataBank:
                    _subTitle.text = "图鉴 · DATA BANK";
                    _subFooter.text = "图鉴数据来自服务器物品表与本地背包。";
                    BuildDataBank();
                    break;
                case Entry.Tutorials:
                    _subTitle.text = "教程 · TUTORIALS";
                    _subFooter.text = "教程内容以游戏内实际机制为准。";
                    BuildTutorials();
                    break;
                case Entry.Quests:
                    _subTitle.text = "任务 · QUESTS";
                    _subFooter.text = "任务列表来自主线第一部《失序的星环》。";
                    BuildQuests();
                    break;
                case Entry.Companions:
                    _subTitle.text = "同行者 · COMPANIONS";
                    _subFooter.text = "系统正在开发中。";
                    BuildPlaceholder("同行者系统即将开放。",
                        "同行者是与主角并肩作战的伙伴，系统正在开发中，敬请期待。");
                    break;
                case Entry.Trophies:
                    _subTitle.text = "成就 · TROPHIES";
                    _subFooter.text = "系统正在开发中。";
                    BuildPlaceholder("成就系统即将开放。",
                        "成就与奖励系统正在开发中，敬请期待。");
                    break;
                case Entry.Gallery:
                    _subTitle.text = "画廊 · GALLERY";
                    _subFooter.text = "仅展示项目中已有的美术资源。";
                    BuildGallery();
                    break;
                case Entry.Map:
                    _subTitle.text = "地图 · MAP";
                    _subFooter.text = "3D 探索已开放。";
                    BuildMap();
                    break;
                case Entry.DevLog:
                    _subTitle.text = "开发日志 · FEEDBACK";
                    _subFooter.text = "开发日志将持续更新。";
                    BuildDevLog();
                    break;
            }
            _subPage.SetActive(true);
        }

        private void CloseSubPage()
        {
            if (_subPage != null) _subPage.SetActive(false);
        }

        // Shot hooks for the smoke runner (same pattern as -gacha-shot).
        public void OpenCharactersForShot() => OpenEntry(Entry.Characters);
        public void OpenDataBankForShot() => OpenEntry(Entry.DataBank);
        public void OpenGalleryForShot() => OpenEntry(Entry.Gallery);

        // ---------- sub-page builders ----------

        private void BuildCharacters()
        {
            if (!_inventoryLoaded)
            {
                AddEmptyLabel(GameManager.Instance.IsLoggedIn
                    ? (_inventoryFailed ? "角色加载失败，请稍后重试。" : "正在加载角色…")
                    : "离线模式：登录后查看角色。");
                return;
            }
            var chars = new List<InventoryItemDto>();
            foreach (var it in _items)
                if (it != null && it.kind == "character" && it.count > 0) chars.Add(it);
            chars.Sort((a, b) =>
            {
                int r = Rarity(b.itemId).CompareTo(Rarity(a.itemId));
                return r != 0 ? r : a.itemId.CompareTo(b.itemId);
            });
            AddSectionHeader("已拥有 " + chars.Count + " 名角色");
            if (chars.Count == 0)
            {
                AddEmptyLabel("暂无角色，去抽卡吧。");
                return;
            }
            foreach (var it in chars)
                AddItemRow(it.itemId, "Lv." + it.level + " · x" + it.count, null);
        }

        private void BuildTeam()
        {
            var team = TeamStore.Load();
            AddSectionHeader("当前编队（" + team.Count + " / " + TeamStore.MaxTeamSize + "）");
            for (int i = 0; i < TeamStore.MaxTeamSize; i++)
            {
                if (i < team.Count)
                {
                    int id = team[i];
                    AddItemRow(id, "已编队", null);
                }
                else
                {
                    AddSlotRow("空槽位", "未编入角色");
                }
            }

            AddSectionHeader("选择角色");
            if (!_inventoryLoaded)
            {
                AddEmptyLabel(GameManager.Instance.IsLoggedIn
                    ? (_inventoryFailed ? "角色加载失败，请稍后重试。" : "正在加载角色…")
                    : "离线模式：登录后编队。");
                return;
            }
            var chars = new List<InventoryItemDto>();
            foreach (var it in _items)
                if (it != null && it.kind == "character" && it.count > 0) chars.Add(it);
            chars.Sort((a, b) =>
            {
                int r = Rarity(b.itemId).CompareTo(Rarity(a.itemId));
                return r != 0 ? r : a.itemId.CompareTo(b.itemId);
            });
            if (chars.Count == 0)
            {
                AddEmptyLabel("暂无角色，去抽卡吧。");
                return;
            }
            foreach (var it in chars)
            {
                int id = it.itemId;
                bool inTeam = team.Contains(id);
                AddItemRow(id, inTeam ? "已编队" : "未编入",
                    new RowButton(inTeam ? "移出" : "加入", () =>
                    {
                        if (!TeamStore.Toggle(id))
                        {
                            Toast.Show("队伍已满（最多 3 名）。");
                            return;
                        }
                        Toast.Show("编队已保存。");
                        UpdateCounts();
                        OpenEntry(Entry.Team);
                    }));
            }
        }

        private void BuildDataBank()
        {
            int owned = 0;
            foreach (int id in AllItemIds)
                if (_ownedIds.Contains(id)) owned++;
            AddSectionHeader(_inventoryLoaded
                ? "全部 14 · 已拥有 " + owned + " · 含未拥有"
                : "全部 14 · 含未拥有");
            if (!_inventoryLoaded && GameManager.Instance.IsLoggedIn && !_inventoryFailed)
            {
                AddEmptyLabel("正在加载图鉴…");
                return;
            }
            var ids = new List<int>(AllItemIds);
            ids.Sort((a, b) =>
            {
                int r = Rarity(b).CompareTo(Rarity(a));
                return r != 0 ? r : a.CompareTo(b);
            });
            foreach (int id in ids)
            {
                bool has = _ownedIds.Contains(id);
                InventoryItemDto dto = null;
                if (has) _ownedById.TryGetValue(id, out dto);
                AddCatalogRow(id, has, dto);
            }
        }

        private void BuildTutorials()
        {
            AddSectionHeader("登录与账号");
            AddTextRow("使用账号登录后可使用抽卡、背包、邮件、云存档。");
            AddTextRow("也可以离线游玩，但在线功能不可用。");
            AddSectionHeader("抽卡与保底");
            AddTextRow("单抽 160 货币，十连 1600。");
            AddTextRow("5★ 概率 0.8%，4★ 概率 5%。");
            AddTextRow("5★ 保底 90 抽，4★ 保底 10 抽；70 抽起 5★ 概率提升。");
            AddSectionHeader("主角选择");
            AddTextRow("首次进入游戏时选择男主角或女主角（均为无星者）。");
            AddTextRow("选择后直接进入主菜单。");
            AddSectionHeader("存档");
            AddTextRow("剧情进度自动保存到本地槽 1。");
            AddTextRow("可在云存档页面上传到云端槽 1–3。");
            AddSectionHeader("背包与邮件");
            AddTextRow("抽到的角色与道具在背包查看。");
            AddTextRow("邮件附件在邮件中心领取。");
        }

        private void BuildQuests()
        {
            AddSectionHeader("主线第一部《失序的星环》共 8 章");
            for (int i = 0; i < ChapterNames.Length; i++)
            {
                bool current = i == 0;
                AddStatusRow(ChapterNames[i], current ? "进行中" : "未开启",
                    current ? UiFactory.PaleGold : UiFactory.SilverWhite);
            }
            AddTextRow("后续章节随主线推进开启。");
        }

        private void BuildPlaceholder(string title, string body)
        {
            var t = UiFactory.Label(_subContent, "PlaceholderTitle", title, 24,
                TextAlignmentOptions.Center, UiFactory.MoonWhite, true);
            t.rectTransform.sizeDelta = new Vector2(0, 44);
            var b = UiFactory.Label(_subContent, "PlaceholderBody", body, 18,
                TextAlignmentOptions.Center, UiFactory.SilverWhite);
            b.rectTransform.sizeDelta = new Vector2(0, 34);
        }

        private void BuildGallery()
        {
            AddSectionHeader("角色立绘");
            foreach (int id in GalleryCharacterIds)
            {
                bool has = _ownedIds.Contains(id);
                if (has)
                {
                    InventoryItemDto dto = null;
                    _ownedById.TryGetValue(id, out dto);
                    AddItemRow(id, "已拥有", null);
                }
                else
                {
                    AddLockedRow(id);
                }
            }
            AddSectionHeader("CG");
            var row = new GameObject("Row_CG");
            row.transform.SetParent(_subContent, false);
            var rowRt = row.AddComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0, 1);
            rowRt.anchorMax = new Vector2(0, 1);
            rowRt.pivot = new Vector2(0, 1);
            rowRt.sizeDelta = new Vector2(0, 150);
            var bg = row.AddComponent<Image>();
            bg.color = UiFactory.GlassWhite(0.06f);
            bg.material = UiFactory.UiMaterial;
            row.AddComponent<LayoutElement>().preferredHeight = 150;
            var art = LoadArtPath("CG/cg_01_ballroom_gown");
            if (art != null)
            {
                var img = UiFactory.ArtImage(row.transform, "CgArt", art);
                var iRt = img.rectTransform;
                iRt.anchorMin = new Vector2(0, 0.5f);
                iRt.anchorMax = new Vector2(0, 0.5f);
                iRt.pivot = new Vector2(0, 0.5f);
                iRt.anchoredPosition = new Vector2(8, 0);
                iRt.sizeDelta = new Vector2(220, 134);
            }
            var name = UiFactory.Label(row.transform, "CgName", "CG · 舞会礼服", 20,
                TextAlignmentOptions.Left, UiFactory.MoonWhite, true);
            PlaceInRow(name.rectTransform, 244, 300, 30);
            var note = UiFactory.Label(row.transform, "CgNote", "本地美术资源", 16,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            PlaceInRow(note.rectTransform, 244, 300, 26);
            var status = UiFactory.Label(row.transform, "CgStatus", "已解锁", 18,
                TextAlignmentOptions.Left, UiFactory.PaleGold);
            PlaceInRow(status.rectTransform, 560, 120, 30);
        }

        private void BuildMap()
        {
            AddSectionHeader("艾斯特拉大陆共 5 个区域");
            AddTextRow("3D 探索世界已开放，点击下方按钮进入。");
            // Entry to the 3D exploration world (TideSong, re-skinned).
            // The five story regions below are not yet mapped to it.
            var enterBtn = UiFactory.Button(_subContent, "EnterExploration", "进入探索",
                new UnityAction(() => GameManager.Instance.GoTo(GameManager.SceneExploration)),
                new Vector2(240, 52));
            enterBtn.gameObject.AddComponent<LayoutElement>().preferredHeight = 52;
            AddTextRow("区域随主线推进开启。");
            for (int i = 0; i < RegionNames.Length; i++)
            {
                var row = new GameObject("Row_Region" + i);
                row.transform.SetParent(_subContent, false);
                var rowRt = row.AddComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0, 1);
                rowRt.anchorMax = new Vector2(0, 1);
                rowRt.pivot = new Vector2(0, 1);
                rowRt.sizeDelta = new Vector2(0, 64);
                var bg = row.AddComponent<Image>();
                bg.color = UiFactory.GlassWhite(0.06f);
                bg.material = UiFactory.UiMaterial;
                row.AddComponent<LayoutElement>().preferredHeight = 64;
                var name = UiFactory.Label(row.transform, "RegionName", RegionNames[i], 20,
                    TextAlignmentOptions.Left, UiFactory.MoonWhite, true);
                PlaceInRow(name.rectTransform, 12, 200, 30);
                var desc = UiFactory.Label(row.transform, "RegionDesc", RegionDescs[i], 16,
                    TextAlignmentOptions.Left, UiFactory.SilverWhite);
                PlaceInRow(desc.rectTransform, 220, 500, 30);
                var status = UiFactory.Label(row.transform, "RegionStatus", "尚未开放探索", 16,
                    TextAlignmentOptions.Left, UiFactory.SilverWhite);
                PlaceInRow(status.rectTransform, 760, 160, 30);
            }
        }

        private void BuildDevLog()
        {
            AddSectionHeader("当前版本");
            AddTextRow("版本：v1.0.0（Windows / Linux 客户端）。");
            AddTextRow("后端：Cloudflare Workers + Supabase（已部署）。");
            AddSectionHeader("更新日志");
            AddTextRow("· 玩家个人主页（角色 / 队伍 / 图鉴 / 画廊 / 地图等）。");
            AddTextRow("· 抽卡界面 v4 精修。");
            AddTextRow("· 双性别主角选择。");
            AddTextRow("· 登录 / 抽卡 / 背包 / 邮件 / 云存档 / 主线剧情。");
        }

        // ---------- row helpers (PullHistoryScreen pattern) ----------

        private class RowButton
        {
            public string Label;
            public System.Action OnClick;
            public RowButton(string label, System.Action onClick)
            {
                Label = label;
                OnClick = onClick;
            }
        }

        private void AddSectionHeader(string text)
        {
            var h = UiFactory.Label(_subContent, "Section", "—— " + text + " ——", 20,
                TextAlignmentOptions.Left, UiFactory.SilverWhite, true);
            h.rectTransform.sizeDelta = new Vector2(0, 32);
        }

        private void AddTextRow(string text)
        {
            var l = UiFactory.Label(_subContent, "Text", text, 18,
                TextAlignmentOptions.Left, UiFactory.MoonWhite);
            l.rectTransform.sizeDelta = new Vector2(0, 30);
        }

        private void AddEmptyLabel(string text)
        {
            var e = UiFactory.Label(_subContent, "Empty", text, 20,
                TextAlignmentOptions.Center, UiFactory.SilverWhite);
            e.rectTransform.sizeDelta = new Vector2(0, 40);
        }

        private void AddStatusRow(string text, string status, Color statusColor)
        {
            var row = new GameObject("Row_Status");
            row.transform.SetParent(_subContent, false);
            var rowRt = row.AddComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0, 1);
            rowRt.anchorMax = new Vector2(0, 1);
            rowRt.pivot = new Vector2(0, 1);
            rowRt.sizeDelta = new Vector2(0, 48);
            var bg = row.AddComponent<Image>();
            bg.color = UiFactory.GlassWhite(0.06f);
            bg.material = UiFactory.UiMaterial;
            row.AddComponent<LayoutElement>().preferredHeight = 48;
            var name = UiFactory.Label(row.transform, "Name", text, 20,
                TextAlignmentOptions.Left, UiFactory.MoonWhite, true);
            PlaceInRow(name.rectTransform, 12, 500, 30);
            var st = UiFactory.Label(row.transform, "Status", status, 18,
                TextAlignmentOptions.Left, statusColor);
            PlaceInRow(st.rectTransform, 760, 160, 30);
        }

        private void AddSlotRow(string title, string note)
        {
            var row = new GameObject("Row_Slot");
            row.transform.SetParent(_subContent, false);
            var rowRt = row.AddComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0, 1);
            rowRt.anchorMax = new Vector2(0, 1);
            rowRt.pivot = new Vector2(0, 1);
            rowRt.sizeDelta = new Vector2(0, 64);
            var bg = row.AddComponent<Image>();
            bg.color = UiFactory.GlassWhite(0.03f);
            bg.material = UiFactory.UiMaterial;
            row.AddComponent<LayoutElement>().preferredHeight = 64;
            var name = UiFactory.Label(row.transform, "Name", title, 20,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            PlaceInRow(name.rectTransform, 72, 300, 30);
            var n = UiFactory.Label(row.transform, "Note", note, 16,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            PlaceInRow(n.rectTransform, 400, 300, 30);
        }

        /// <summary>One owned item row: art (or stars badge), name, rarity
        /// stars, right-hand text, optional action button.</summary>
        private void AddItemRow(int itemId, string rightText, RowButton action)
        {
            var row = new GameObject("Row_" + itemId);
            row.transform.SetParent(_subContent, false);
            var rowRt = row.AddComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0, 1);
            rowRt.anchorMax = new Vector2(0, 1);
            rowRt.pivot = new Vector2(0, 1);
            rowRt.sizeDelta = new Vector2(0, 64);
            var bg = row.AddComponent<Image>();
            bg.color = UiFactory.GlassWhite(0.06f);
            bg.material = UiFactory.UiMaterial;
            row.AddComponent<LayoutElement>().preferredHeight = 64;

            var art = LoadArtPath("Characters/" + ItemNames.ArtForItem(itemId));
            if (art != null)
            {
                var img = UiFactory.ArtImage(row.transform, "Icon", art);
                PlaceInRow(img.rectTransform, 8, 52, 52);
            }
            else
            {
                var badge = UiFactory.Panel(row.transform, "IconBadge",
                    new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(52, 52),
                    WithAlpha(RarityColor(Rarity(itemId)), 0.35f));
                var bRt = badge.GetComponent<RectTransform>();
                bRt.pivot = new Vector2(0, 0.5f);
                bRt.anchoredPosition = new Vector2(8, 0);
                var stars = UiFactory.Label(badge.transform, "BadgeStars",
                    ItemNames.Stars(Rarity(itemId)), 13,
                    TextAlignmentOptions.Center, UiFactory.MoonWhite);
                UiFactory.Stretch(stars.rectTransform);
            }

            var name = UiFactory.Label(row.transform, "Name", ItemNames.Get(itemId), 20,
                TextAlignmentOptions.Left, UiFactory.MoonWhite, true);
            PlaceInRow(name.rectTransform, 72, 220, 30);

            var starsLabel = UiFactory.Label(row.transform, "Stars",
                ItemNames.Stars(Rarity(itemId)), 20,
                TextAlignmentOptions.Left, RarityColor(Rarity(itemId)));
            PlaceInRow(starsLabel.rectTransform, 300, 130, 30);

            float rightX = action != null ? 760 : 760;
            var right = UiFactory.Label(row.transform, "Right", rightText ?? "", 18,
                TextAlignmentOptions.Left, UiFactory.MoonlightBlue);
            PlaceInRow(right.rectTransform, rightX, 160, 30);

            if (action != null)
            {
                var btn = UiFactory.Button(row.transform, "Action", action.Label,
                    new UnityAction(action.OnClick), new Vector2(100, 36));
                var btnRt = btn.GetComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(0, 0.5f);
                btnRt.anchorMax = new Vector2(0, 0.5f);
                btnRt.pivot = new Vector2(0, 0.5f);
                btnRt.anchoredPosition = new Vector2(940, 0);
            }
        }

        /// <summary>One data-bank row for an item the player does NOT own:
        /// dimmed "?" box instead of the art — the art stays locked.</summary>
        private void AddLockedRow(int itemId)
        {
            var row = new GameObject("Row_Locked" + itemId);
            row.transform.SetParent(_subContent, false);
            var rowRt = row.AddComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0, 1);
            rowRt.anchorMax = new Vector2(0, 1);
            rowRt.pivot = new Vector2(0, 1);
            rowRt.sizeDelta = new Vector2(0, 64);
            var bg = row.AddComponent<Image>();
            bg.color = UiFactory.GlassWhite(0.03f);
            bg.material = UiFactory.UiMaterial;
            row.AddComponent<LayoutElement>().preferredHeight = 64;

            var badge = UiFactory.Panel(row.transform, "IconBadge",
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(52, 52),
                UiFactory.GlassWhite(0.08f));
            var bRt = badge.GetComponent<RectTransform>();
            bRt.pivot = new Vector2(0, 0.5f);
            bRt.anchoredPosition = new Vector2(8, 0);
            var q = UiFactory.Label(badge.transform, "BadgeQ", "?", 24,
                TextAlignmentOptions.Center, UiFactory.SilverWhite);
            UiFactory.Stretch(q.rectTransform);

            var name = UiFactory.Label(row.transform, "Name", ItemNames.Get(itemId), 20,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            PlaceInRow(name.rectTransform, 72, 220, 30);

            var starsLabel = UiFactory.Label(row.transform, "Stars",
                ItemNames.Stars(Rarity(itemId)), 20,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            PlaceInRow(starsLabel.rectTransform, 300, 130, 30);

            var right = UiFactory.Label(row.transform, "Right", "未拥有", 18,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            PlaceInRow(right.rectTransform, 760, 160, 30);
        }

        /// <summary>One data-bank row: owned (art + 已拥有 xN) or not
        /// (dimmed "?" + 未拥有).</summary>
        private void AddCatalogRow(int itemId, bool owned, InventoryItemDto dto)
        {
            if (owned)
            {
                AddItemRow(itemId, "已拥有 x" + (dto != null ? dto.count : 1), null);
                return;
            }
            AddLockedRow(itemId);
        }

        private static void PlaceInRow(RectTransform rt, float x, float w, float h)
        {
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0);
            rt.sizeDelta = new Vector2(w, h);
        }

        // ---------- data helpers ----------

        private static int Rarity(int itemId)
        {
            if (itemId <= 105) return 5;
            if (itemId <= 205) return 4;
            return 3;
        }

        private static bool IsCharacter(int itemId) => itemId < 300;

        private static Color RarityColor(int rarity)
        {
            switch (rarity)
            {
                case 5: return UiFactory.PaleGold;
                case 4: return UiFactory.MoonlightBlue;
                default: return UiFactory.SilverWhite;
            }
        }

        private static Color WithAlpha(Color c, float alpha)
        {
            return new Color(c.r, c.g, c.b, alpha);
        }

        // Same loading path as GachaScreen.LoadArt: the Resources PNGs/JPGs
        // import as Texture2D, so the Sprite is created at runtime.
        private static Sprite LoadArtPath(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            if (_spriteCache.TryGetValue(resourcePath, out var cached)) return cached;
            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null)
            {
                _spriteCache[resourcePath] = null;
                return null;
            }
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f));
            _spriteCache[resourcePath] = sprite;
            return sprite;
        }
    }
}
