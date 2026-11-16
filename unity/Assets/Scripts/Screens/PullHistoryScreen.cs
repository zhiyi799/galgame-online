// PullHistoryScreen — local pull-history overlay (Gacha scene overlay).
//
// LOCAL-ONLY: the server has no pull-history endpoint and no pull_history
// table, so history lives only in the per-account JSON file written by
// PullHistoryStore. It is LOST on device change or app-data clear, and it
// cannot be recovered from the server. The overlay says so plainly (empty
// state + footer note) and never pretends the list came from the server.
//
// Self-contained overlay: built as a child of the Transform passed to
// Show(), destroyed on close. Does NOT depend on GachaScreen — the
// item->art map comes from the shared ItemNames.ArtForItem table.
//
// What a record does NOT store, the UI does NOT invent:
//  - No pity counter is stored, so no pity number is shown — only the
//    record's seq ("第 N 条记录") and its rarity.
//  - Single vs ten-pull is inferred by grouping records that share the
//    exact same `time` timestamp: 10 records on one timestamp = 十连,
//    anything else = 单抽. This relies on the pull flow writing one shared
//    `time` for all 10 records of a ten-pull (PullHistoryStore.Append only
//    fills `time` when it is unset); if a caller ever stamps each record
//    with its own time, ten-pulls will read as 单抽.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using GalgameOnline.Api;
using GalgameOnline.Core;
using GalgameOnline.UI;

namespace GalgameOnline.Screens
{
    public class PullHistoryScreen : MonoBehaviour
    {
        private const int PageSize = 20;

        // The two live banners (server data; see server/Services/Banners.cs).
        // They are always offered as filters, even before the player has
        // pulled on them; any other banner id found in the records is added.
        private const int KnownBanner1Id = 1;
        private const string KnownBanner1Name = "星夜邂逅";
        private const int KnownBanner2Id = 2;
        private const string KnownBanner2Name = "月下初见";

        private GameObject _root;
        private System.Action _onClose;
        private bool _closed;

        private List<PullRecord> _allRecords = new List<PullRecord>();
        private List<PullRecord> _filtered = new List<PullRecord>();
        // Records sharing one `time` timestamp: count == 10 means 十连.
        private Dictionary<string, int> _timeGroupCounts = new Dictionary<string, int>();

        private int _bannerFilter;      // 0 = 全部, otherwise bannerId
        private int _rarityFilter;      // 0 = 全部, otherwise 3 / 4 / 5
        private bool _onlyFeatured;
        private bool _newestFirst = true;
        private int _visibleCount = PageSize;

        private TextMeshProUGUI _countLabel;
        private RectTransform _content;
        private GameObject _bannerRow;
        private GameObject _rarityRow;
        private GameObject _toggleRow;
        private GameObject _detailPanel;
        private TextMeshProUGUI _detailName;
        private TextMeshProUGUI _detailStars;
        private TextMeshProUGUI _detailBanner;
        private TextMeshProUGUI _detailTime;
        private TextMeshProUGUI _detailPullType;
        private TextMeshProUGUI _detailKind;
        private TextMeshProUGUI _detailSeq;
        private TextMeshProUGUI _detailUp;
        private Image _detailArt;
        private TextMeshProUGUI _detailArtFallback;

        private static readonly Dictionary<string, Sprite> _spriteCache =
            new Dictionary<string, Sprite>();

        /// <summary>
        /// Builds the history overlay as a child of <paramref name="parent"/>
        /// and returns its root GameObject. Close (× / 返回) destroys the root
        /// and invokes <paramref name="onClose"/> exactly once.
        /// </summary>
        public static GameObject Show(Transform parent, System.Action onClose)
        {
            if (parent == null)
            {
                var canvas = UiFactory.SceneCanvas();
                if (canvas != null) parent = canvas.transform;
            }
            if (parent == null) return null;

            var root = new GameObject("PullHistoryOverlay");
            root.transform.SetParent(parent, false);
            var rootRt = root.AddComponent<RectTransform>();
            UiFactory.Stretch(rootRt);

            var screen = root.AddComponent<PullHistoryScreen>();
            screen.Build(onClose);
            return root;
        }

        // ---------- build ----------

        private void Build(System.Action onClose)
        {
            _root = gameObject;
            _onClose = onClose;

            // Dimmed full-screen backdrop (clicks behind the overlay are
            // absorbed by this panel's Image).
            var dim = UiFactory.Panel(_root.transform, "Dim",
                Vector2.zero, Vector2.one, Vector2.zero,
                new Color(0.02f, 0.04f, 0.12f, 0.72f));

            // Centered glass panel: 1120 x 640 on a 1280 x 720 screen.
            var panel = UiFactory.RoundedPanel(_root.transform, "Panel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(1120, 640), UiFactory.Glass(0.96f));
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchoredPosition = Vector2.zero;
            UiFactory.AddSilverBorder(panelRt);

            // Title bar.
            var title = UiFactory.Label(panel.transform, "Title", "抽卡记录", 26,
                TextAlignmentOptions.Left, UiFactory.MoonWhite, true);
            UiFactory.Place(title.rectTransform, 20, -14, 300, 40);

            _countLabel = UiFactory.Label(panel.transform, "Count", "", 18,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            UiFactory.Place(_countLabel.rectTransform, 200, -20, 500, 30);

            var closeBtn = UiFactory.Button(panel.transform, "CloseButton", "×",
                new UnityAction(Close), new Vector2(44, 40), UiFactory.ClassicUltramarine);
            PlaceTopLeft(closeBtn.GetComponent<RectTransform>(), 1056, 10, 44, 40);

            // Filter rows (buttons are rebuilt once records are loaded).
            var bannerLabel = UiFactory.Label(panel.transform, "BannerLabel", "卡池", 18,
                TextAlignmentOptions.Left, UiFactory.SilverWhite, true);
            UiFactory.Place(bannerLabel.rectTransform, 20, -62, 60, 30);
            _bannerRow = RowContainer(panel.transform, "BannerRow", 80, 54);

            var rarityLabel = UiFactory.Label(panel.transform, "RarityLabel", "星级", 18,
                TextAlignmentOptions.Left, UiFactory.SilverWhite, true);
            UiFactory.Place(rarityLabel.rectTransform, 20, -110, 60, 30);
            _rarityRow = RowContainer(panel.transform, "RarityRow", 80, 102);

            _toggleRow = RowContainer(panel.transform, "ToggleRow", 20, 150);

            var hint = UiFactory.Label(panel.transform, "Hint", "点击一条记录查看详情", 16,
                TextAlignmentOptions.Right, UiFactory.SilverWhite);
            UiFactory.Place(hint.rectTransform, 700, -158, 380, 30);

            // Scrollable record list.
            var (scroll, content) = UiFactory.ScrollView(panel.transform, "List",
                Vector2.zero, Vector2.one, Vector2.zero);
            var sRt = scroll.GetComponent<RectTransform>();
            sRt.offsetMin = new Vector2(16, 56);
            sRt.offsetMax = new Vector2(-16, -196);
            _content = content;

            // Footer: local-only note + 返回 button.
            var note = UiFactory.Label(panel.transform, "FooterNote",
                "抽卡记录仅保存在本机，换设备或清除应用数据后会丢失。", 16,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            note.rectTransform.anchorMin = new Vector2(0, 0);
            note.rectTransform.anchorMax = new Vector2(0, 0);
            note.rectTransform.pivot = new Vector2(0, 0);
            note.rectTransform.anchoredPosition = new Vector2(20, 14);
            note.rectTransform.sizeDelta = new Vector2(900, 26);

            var backBtn = UiFactory.Button(panel.transform, "BackButton", "返回",
                new UnityAction(Close), new Vector2(110, 40), UiFactory.ClassicUltramarine);
            var backRt = backBtn.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(1, 0);
            backRt.anchorMax = new Vector2(1, 0);
            backRt.pivot = new Vector2(1, 0);
            backRt.anchoredPosition = new Vector2(-16, 10);

            BuildDetailPanel(panel.transform);
            RenderFilterButtons();

            // Brief loading state, then the local read (fast, but the brief
            // requires a visible loading state on open).
            var loading = UiFactory.Label(_content, "Loading", "正在加载…", 20,
                TextAlignmentOptions.Center, UiFactory.SilverWhite);
            loading.rectTransform.sizeDelta = new Vector2(0, 40);
            StartCoroutine(LoadRoutine());
        }

        private static GameObject RowContainer(Transform parent, string name, float x, float yFromTop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -yFromTop);
            rt.sizeDelta = new Vector2(1020, 36);
            return go;
        }

        private static void PlaceTopLeft(RectTransform rt, float x, float yFromTop, float w, float h)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -yFromTop);
            rt.sizeDelta = new Vector2(w, h);
        }

        private IEnumerator LoadRoutine()
        {
            yield return null;
            yield return new WaitForSeconds(0.2f);
            if (_closed) yield break;

            string username = GameManager.Instance != null ? GameManager.Instance.Username : "";
            _allRecords = PullHistoryStore.Load(username) ?? new List<PullRecord>();

            // Group by exact timestamp for the 单抽 / 十连 inference.
            _timeGroupCounts.Clear();
            foreach (var r in _allRecords)
            {
                if (r == null || string.IsNullOrEmpty(r.time)) continue;
                if (_timeGroupCounts.TryGetValue(r.time, out int c))
                    _timeGroupCounts[r.time] = c + 1;
                else
                    _timeGroupCounts[r.time] = 1;
            }

            RenderFilterButtons();
            RenderList();
        }

        // ---------- filters ----------

        private void RenderFilterButtons()
        {
            // Banner buttons: 全部 + the two known live banners + any other
            // banner id present in the records (name from the first record).
            UiFactory.ClearChildren(_bannerRow.transform);
            var bannerNames = new Dictionary<int, string>
            {
                { KnownBanner1Id, KnownBanner1Name },
                { KnownBanner2Id, KnownBanner2Name },
            };
            foreach (var r in _allRecords)
            {
                if (r == null) continue;
                if (!bannerNames.ContainsKey(r.bannerId))
                    bannerNames[r.bannerId] = string.IsNullOrEmpty(r.bannerName)
                        ? "卡池#" + r.bannerId : r.bannerName;
            }
            var bannerIds = new List<int>(bannerNames.Keys);
            bannerIds.Sort();

            float x = 0;
            x = AddFilterButton(_bannerRow.transform, x, "全部", _bannerFilter == 0,
                () => SetBannerFilter(0), 90);
            foreach (int id in bannerIds)
            {
                int captured = id;
                x = AddFilterButton(_bannerRow.transform, x, bannerNames[id],
                    _bannerFilter == id, () => SetBannerFilter(captured), 130);
            }

            // Rarity buttons: 全部 / 5★ / 4★ / 3★.
            UiFactory.ClearChildren(_rarityRow.transform);
            x = 0;
            x = AddFilterButton(_rarityRow.transform, x, "全部", _rarityFilter == 0,
                () => SetRarityFilter(0), 90);
            x = AddFilterButton(_rarityRow.transform, x, "5★", _rarityFilter == 5,
                () => SetRarityFilter(5), 90);
            x = AddFilterButton(_rarityRow.transform, x, "4★", _rarityFilter == 4,
                () => SetRarityFilter(4), 90);
            x = AddFilterButton(_rarityRow.transform, x, "3★", _rarityFilter == 3,
                () => SetRarityFilter(3), 90);

            // 只看 UP toggle + sort toggle.
            UiFactory.ClearChildren(_toggleRow.transform);
            x = 0;
            x = AddFilterButton(_toggleRow.transform, x,
                _onlyFeatured ? "只看 UP：开" : "只看 UP：关", _onlyFeatured,
                () => { _onlyFeatured = !_onlyFeatured; _visibleCount = PageSize; RenderFilterButtons(); RenderList(); },
                150);
            x = AddFilterButton(_toggleRow.transform, x,
                _newestFirst ? "排序：最新" : "排序：最早", false,
                () => { _newestFirst = !_newestFirst; _visibleCount = PageSize; RenderFilterButtons(); RenderList(); },
                150);
        }

        private float AddFilterButton(Transform row, float x, string label, bool selected,
            System.Action onClick, float width)
        {
            var btn = UiFactory.Button(row, "Filter_" + label, label,
                new UnityAction(onClick), new Vector2(width, 36),
                selected ? UiFactory.StarryBlue : UiFactory.ClassicUltramarine);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0);
            return x + width + 8;
        }

        private void SetBannerFilter(int bannerId)
        {
            _bannerFilter = bannerId;
            _visibleCount = PageSize;
            RenderFilterButtons();
            RenderList();
        }

        private void SetRarityFilter(int rarity)
        {
            _rarityFilter = rarity;
            _visibleCount = PageSize;
            RenderFilterButtons();
            RenderList();
        }

        // ---------- list ----------

        private void RenderList()
        {
            _filtered.Clear();
            foreach (var r in _allRecords)
            {
                if (r == null) continue;
                if (_bannerFilter != 0 && r.bannerId != _bannerFilter) continue;
                if (_rarityFilter != 0 && r.rarity != _rarityFilter) continue;
                if (_onlyFeatured && !r.isFeatured) continue;
                _filtered.Add(r);
            }
            _filtered.Sort(CompareRecords);

            if (_countLabel != null)
            {
                _countLabel.text = _filtered.Count == _allRecords.Count
                    ? "共 " + _allRecords.Count + " 条"
                    : "共 " + _filtered.Count + " 条（全部 " + _allRecords.Count + " 条）";
            }

            UiFactory.ClearChildren(_content);

            if (_allRecords.Count == 0)
            {
                AddEmptyLabel("暂无抽卡记录（记录仅保存在本机）");
                return;
            }
            if (_filtered.Count == 0)
            {
                AddEmptyLabel("没有符合筛选的记录");
                return;
            }

            int shown = Math.Min(_visibleCount, _filtered.Count);
            for (int i = 0; i < shown; i++)
                AddRow(_filtered[i]);

            if (_filtered.Count > shown)
            {
                var more = UiFactory.Button(_content, "LoadMore",
                    "加载更多（还剩 " + (_filtered.Count - shown) + " 条）",
                    new UnityAction(() => { _visibleCount += PageSize; RenderList(); }),
                    new Vector2(0, 44), UiFactory.ClassicUltramarine);
                more.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            }
        }

        private void AddEmptyLabel(string text)
        {
            var e = UiFactory.Label(_content, "Empty", text, 20,
                TextAlignmentOptions.Center, UiFactory.SilverWhite);
            e.rectTransform.sizeDelta = new Vector2(0, 40);
        }

        private int CompareRecords(PullRecord a, PullRecord b)
        {
            DateTime ta = ParseTime(a.time);
            DateTime tb = ParseTime(b.time);
            int byTime = _newestFirst
                ? tb.CompareTo(ta)
                : ta.CompareTo(tb);
            if (byTime != 0) return byTime;
            return _newestFirst ? b.seq.CompareTo(a.seq) : a.seq.CompareTo(b.seq);
        }

        private void AddRow(PullRecord record)
        {
            var row = new GameObject("Row_" + record.seq);
            row.transform.SetParent(_content, false);
            var rowRt = row.AddComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0, 1);
            rowRt.anchorMax = new Vector2(0, 1);
            rowRt.pivot = new Vector2(0, 1);
            rowRt.sizeDelta = new Vector2(0, 64);
            var bg = row.AddComponent<Image>();
            bg.color = UiFactory.GlassWhite(0.06f);
            bg.material = UiFactory.UiMaterial;
            row.AddComponent<LayoutElement>().preferredHeight = 64;
            var btn = row.AddComponent<Button>();
            PullRecord captured = record;
            btn.onClick.AddListener(new UnityAction(() => ShowDetail(captured)));

            // Icon: character art where one exists, else a coloured rarity
            // badge with the item name (3★ items have no art).
            var art = LoadArt(ItemNames.ArtForItem(record.itemId));
            if (art != null)
            {
                var img = UiFactory.ArtImage(row.transform, "Icon", art);
                PlaceInRow(img.rectTransform, 8, 52, 52);
            }
            else
            {
                var badge = UiFactory.Panel(row.transform, "IconBadge",
                    new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(52, 52),
                    WithAlpha(RarityColor(record.rarity), 0.35f));
                var bRt = badge.GetComponent<RectTransform>();
                bRt.pivot = new Vector2(0, 0.5f);
                bRt.anchoredPosition = new Vector2(8, 0);
                var stars = UiFactory.Label(badge.transform, "BadgeStars",
                    ItemNames.Stars(record.rarity), 13,
                    TextAlignmentOptions.Center, UiFactory.MoonWhite);
                UiFactory.Stretch(stars.rectTransform);
            }

            Color rarityColor = RarityColor(record.rarity);
            var name = UiFactory.Label(row.transform, "Name", ItemNames.Get(record.itemId), 20,
                TextAlignmentOptions.Left, UiFactory.MoonWhite, true);
            PlaceInRow(name.rectTransform, 72, 190, 30);

            var starsLabel = UiFactory.Label(row.transform, "Stars", ItemNames.Stars(record.rarity), 20,
                TextAlignmentOptions.Left, rarityColor);
            PlaceInRow(starsLabel.rectTransform, 270, 120, 30);

            var banner = UiFactory.Label(row.transform, "Banner",
                string.IsNullOrEmpty(record.bannerName) ? "卡池#" + record.bannerId : record.bannerName,
                18, TextAlignmentOptions.Left, UiFactory.SilverWhite);
            PlaceInRow(banner.rectTransform, 400, 140, 30);

            var time = UiFactory.Label(row.transform, "Time", FormatTime(record.time), 18,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            PlaceInRow(time.rectTransform, 550, 180, 30);

            var pullType = UiFactory.Label(row.transform, "PullType", PullTypeOf(record), 18,
                TextAlignmentOptions.Left, UiFactory.MoonlightBlue);
            PlaceInRow(pullType.rectTransform, 740, 70, 30);

            var seq = UiFactory.Label(row.transform, "Seq", "第 " + record.seq + " 条", 18,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            PlaceInRow(seq.rectTransform, 820, 110, 30);

            if (record.isFeatured)
            {
                var up = UiFactory.Label(row.transform, "Up", "UP", 18,
                    TextAlignmentOptions.Left, UiFactory.SakuraPink, true);
                PlaceInRow(up.rectTransform, 940, 80, 30);
            }
        }

        private static void PlaceInRow(RectTransform rt, float x, float w, float h)
        {
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0);
            rt.sizeDelta = new Vector2(w, h);
        }

        // ---------- detail panel (inside this overlay) ----------

        private void BuildDetailPanel(Transform panel)
        {
            _detailPanel = UiFactory.RoundedPanel(panel, "DetailPanel",
                Vector2.zero, Vector2.one, Vector2.zero, UiFactory.Glass(0.97f));
            var dRt = _detailPanel.GetComponent<RectTransform>();
            dRt.offsetMin = new Vector2(16, 16);
            dRt.offsetMax = new Vector2(-16, -16);
            UiFactory.AddSilverBorder(dRt);

            var title = UiFactory.Label(_detailPanel.transform, "DetailTitle", "记录详情", 24,
                TextAlignmentOptions.Left, UiFactory.MoonWhite, true);
            UiFactory.Place(title.rectTransform, 24, -16, 300, 40);

            var closeBtn = UiFactory.Button(_detailPanel.transform, "DetailClose", "×",
                new UnityAction(HideDetail), new Vector2(44, 40), UiFactory.ClassicUltramarine);
            PlaceTopLeft(closeBtn.GetComponent<RectTransform>(), 1028, 12, 44, 40);

            // Art area (left): sprite when one exists, stars fallback otherwise.
            var artGo = new GameObject("DetailArt");
            artGo.transform.SetParent(_detailPanel.transform, false);
            _detailArt = artGo.AddComponent<Image>();
            _detailArt.preserveAspect = true;
            _detailArt.color = Color.white;
            _detailArt.material = UiFactory.UiMaterial;
            var artRt = artGo.GetComponent<RectTransform>();
            artRt.anchorMin = new Vector2(0, 1);
            artRt.anchorMax = new Vector2(0, 1);
            artRt.pivot = new Vector2(0, 1);
            artRt.anchoredPosition = new Vector2(24, -70);
            artRt.sizeDelta = new Vector2(220, 340);

            _detailArtFallback = UiFactory.Label(_detailPanel.transform, "DetailArtFallback",
                "", 40, TextAlignmentOptions.Center, UiFactory.SilverWhite);
            _detailArtFallback.rectTransform.anchorMin = new Vector2(0, 1);
            _detailArtFallback.rectTransform.anchorMax = new Vector2(0, 1);
            _detailArtFallback.rectTransform.pivot = new Vector2(0, 1);
            _detailArtFallback.rectTransform.anchoredPosition = new Vector2(24, -190);
            _detailArtFallback.rectTransform.sizeDelta = new Vector2(220, 80);

            float y = 80;
            _detailName = DetailField("DetailName", y, 28, UiFactory.MoonWhite, true); y += 48;
            _detailStars = DetailField("DetailStars", y, 22, UiFactory.MoonWhite); y += 44;
            _detailBanner = DetailField("DetailBanner", y, 20, UiFactory.SilverWhite); y += 44;
            _detailTime = DetailField("DetailTime", y, 20, UiFactory.SilverWhite); y += 44;
            _detailPullType = DetailField("DetailPullType", y, 20, UiFactory.MoonlightBlue); y += 44;
            _detailKind = DetailField("DetailKind", y, 20, UiFactory.SilverWhite); y += 44;
            _detailSeq = DetailField("DetailSeq", y, 20, UiFactory.SilverWhite); y += 44;
            _detailUp = DetailField("DetailUp", y, 20, UiFactory.SakuraPink, true); y += 44;

            var note = UiFactory.Label(_detailPanel.transform, "DetailNote",
                "记录仅保存在本机。", 16,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            UiFactory.Place(note.rectTransform, 280, -y, 600, 30);

            var backBtn = UiFactory.Button(_detailPanel.transform, "DetailBack", "返回列表",
                new UnityAction(HideDetail), new Vector2(120, 40), UiFactory.ClassicUltramarine);
            PlaceTopLeft(backBtn.GetComponent<RectTransform>(), 952, 552, 120, 40);

            _detailPanel.SetActive(false);
        }

        private TextMeshProUGUI DetailField(string name, float yFromTop, int fontSize, Color color, bool bold = false)
        {
            var l = UiFactory.Label(_detailPanel.transform, name, "", fontSize,
                TextAlignmentOptions.Left, color, bold);
            UiFactory.Place(l.rectTransform, 280, -yFromTop, 720, 36);
            return l;
        }

        private void ShowDetail(PullRecord record)
        {
            if (record == null || _detailPanel == null) return;
            _detailName.text = ItemNames.Get(record.itemId);
            _detailStars.text = ItemNames.Stars(record.rarity);
            _detailStars.color = RarityColor(record.rarity);
            _detailBanner.text = "卡池：" + (string.IsNullOrEmpty(record.bannerName)
                ? "卡池#" + record.bannerId : record.bannerName);
            _detailTime.text = "时间：" + FormatTime(record.time) + "（本地时间）";
            string pullType = PullTypeOf(record);
            _detailPullType.text = pullType == "十连"
                ? "抽取方式：十连（第 " + record.pullNo + " 抽）"
                : "抽取方式：单抽";
            _detailKind.text = "类型：" + (record.kind == "character" ? "角色" : "道具");
            // No pity counter is stored on the record: show only its seq.
            // Never invent a pity number.
            _detailSeq.text = "第 " + record.seq + " 条记录";
            _detailUp.text = record.isFeatured ? "★ UP 角色" : "非 UP";
            _detailUp.color = record.isFeatured ? UiFactory.SakuraPink : UiFactory.SilverWhite;

            var art = LoadArt(ItemNames.ArtForItem(record.itemId));
            _detailArt.gameObject.SetActive(art != null);
            if (art != null) _detailArt.sprite = art;
            _detailArtFallback.gameObject.SetActive(art == null);
            if (art == null) _detailArtFallback.text = ItemNames.Stars(record.rarity);

            _detailPanel.SetActive(true);
        }

        private void HideDetail()
        {
            if (_detailPanel != null) _detailPanel.SetActive(false);
        }

        // ---------- close ----------

        private void Close()
        {
            if (_closed) return;
            _closed = true;
            System.Action cb = _onClose;
            _onClose = null;
            if (_root != null) Destroy(_root);
            cb?.Invoke();
        }

        // ---------- helpers ----------

        /// <summary>十连 vs 单抽：10 records sharing one `time` timestamp = 十连.</summary>
        private string PullTypeOf(PullRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.time)) return "单抽";
            if (_timeGroupCounts.TryGetValue(record.time, out int count) && count == 10)
                return "十连";
            return "单抽";
        }

        private static DateTime ParseTime(string iso)
        {
            if (!string.IsNullOrEmpty(iso) &&
                DateTime.TryParse(iso, null, DateTimeStyles.RoundtripKind, out DateTime dt))
                return dt;
            return DateTime.MinValue;
        }

        private static string FormatTime(string iso)
        {
            DateTime dt = ParseTime(iso);
            if (dt == DateTime.MinValue) return string.IsNullOrEmpty(iso) ? "—" : iso;
            // Stored as UTC; display in local time.
            return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

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

        // Item -> Resources/Characters art comes from the single shared
        // table ItemNames.ArtForItem. 3★ items (301-304) have no art.

        // Same loading path as GachaScreen.LoadArt: the Resources/Characters
        // PNGs import as Texture2D, so the Sprite is created at runtime.
        private static Sprite LoadArt(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName)) return null;
            if (_spriteCache.TryGetValue(resourceName, out var cached)) return cached;
            var tex = Resources.Load<Texture2D>("Characters/" + resourceName);
            if (tex == null)
            {
                _spriteCache[resourceName] = null;
                return null;
            }
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f));
            _spriteCache[resourceName] = sprite;
            return sprite;
        }
    }
}
