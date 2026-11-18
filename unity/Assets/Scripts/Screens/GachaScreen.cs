// GachaScreen — v3 redesign (user's detailed gacha brief, 2026-11-16)
// + v4 UI refinement (user's gacha UI refine prompt, 2026-11-17).
// Restrained commercial-game WISH UI: night shrine-courtyard scene
// background (Resources/Backgrounds/Gacha.png), 23% / 30% / 47% zones
// (left banner rows / center info column / right large character).
// v4 changes are VISUAL ONLY — layout, background, art, banner data
// and all functionality are unchanged: flat list rows (no heavy
// cards; selection = translucent tint + gold accent + hairline),
// flat rate-up frames with pale-gold stars, slim 6px pity bars,
// ten-pull as the clear primary (classic ultramarine + pale-gold
// hairline), single pull quiet deep blue + silver hairline, pink
// removed from coming-soon status, twinkle dots removed.
// API logic unchanged: GetBanners / GetBannerDetail / Pull / RefreshWallet.
//
// Concept slot -> backend banner mapping (the live backend only has
// 2 banners; the other two concept slots are "coming soon"):
//   slot 1 月下樱华 -> backend banner 1 (星夜邂逅, featured 101)
//   slot 2 苍穹之誓 -> backend banner 2 (月下初见, featured 105)
//   slot 3 春日絮语 -> coming soon (not pullable)
//   slot 4 星辰之约 -> coming soon (not pullable)
// Display names / subtitles / quotes come from the concept; remaining
// time for live slots is "长期开放" (the backend has no end date).
// "绫华" was the concept's display name for item 101's art (char_01).
// The featured 5★ splash is now 塞蕾斯蒂娅： her user-supplied
// illustration (docs/celestia-5star-illustration.png), cropped to a
// portrait splash (Resources/Characters/celestia_splash) with the
// "豆包 AI 生成" watermark removed. Item 101's art mapping in
// ItemNames.ArtForItem points at the same splash, so the UP frame,
// details, history and pull-result screens all show her.
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
    public class GachaScreen : MonoBehaviour
    {
        // Item -> art mapping lives in ItemNames.ArtForItem (single shared
        // copy; BannerDetailsScreen / PullHistoryScreen / PullResultScreen
        // use the same table). 3★ items (301-304) have no art.

        // Offline fallback: the two banners are static server data (see
        // server/Services/Banners.cs). Used only when the API is unreachable.
        private static readonly BannerDto[] FallbackBanners = new BannerDto[]
        {
            new BannerDto { id = 1, name = "星夜邂逅", featuredFiveStar = 101, costPerPull = 160, dailyLimit = 0 },
            new BannerDto { id = 2, name = "月下初见", featuredFiveStar = 105, costPerPull = 160, dailyLimit = 0 },
        };

        // One row of the left banner list. backendId 0 = no backend banner
        // (coming soon, not pullable). subtitle is the spaced English title
        // for the center column; cardSub is the compact version for the card.
        private class BannerSlot
        {
            public string name;
            public string subtitle;
            public string cardSub;
            public string quote;
            public string artName;      // Resources/Characters name, or null
            public string nameplate;    // name shown in the UP frame
            public int backendId;
            public bool comingSoon;
            public string badge;        // "限定" / "常驻" / null
            public string remain;       // display remaining-time text
            public int[] rateUpIds;
        }

        private static readonly BannerSlot[] Slots = new BannerSlot[]
        {
            new BannerSlot {
                name = "月下樱华", subtitle = "M O O N L I T   S A K U R A", cardSub = "MOONLIT SAKURA",
                quote = "「在这片星海与樱花之间……\n我们，终将再再次相遇。」",
                artName = "celestia_splash", nameplate = "塞蕾斯蒂娅",
                backendId = 1, comingSoon = false, badge = "限定",
                remain = "长期开放", rateUpIds = new int[] { 101, 102, 103 },
            },
            new BannerSlot {
                name = "苍穹之誓", subtitle = "O A T H   O F   T H E   S K Y", cardSub = "OATH OF THE SKY",
                quote = "「苍穹为誓，星光为证。」",
                artName = "char_105", nameplate = "月下·凛",
                backendId = 2, comingSoon = false, badge = null,
                remain = "长期开放", rateUpIds = new int[] { 105, 102, 103 },
            },
            new BannerSlot {
                name = "春日絮语", subtitle = "S P R I N G   W H I S P E R", cardSub = "SPRING WHISPER",
                quote = "", artName = "char_104", nameplate = "",
                backendId = 0, comingSoon = true, badge = null,
                remain = "即将开启", rateUpIds = new int[0],
            },
            new BannerSlot {
                name = "星辰之约", subtitle = "S T A R R Y   P R O M I S E", cardSub = "STARRY PROMISE",
                quote = "", artName = null, nameplate = "",
                backendId = 0, comingSoon = true, badge = "常驻",
                remain = "常驻祈愿 · 即将开启", rateUpIds = new int[0],
            },
        };

        // Pity bar width (center column).
        private const float PityBarWidth = 380f;

        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

        private Transform _root;
        private TextMeshProUGUI _freeLabel;
        private TextMeshProUGUI _ticketLabel;
        private TextMeshProUGUI _bannerTitle;
        private TextMeshProUGUI _bannerSubtitle;
        private TextMeshProUGUI _remainLabel;
        private TextMeshProUGUI _quoteLabel;
        private TextMeshProUGUI _badgeLabel;
        private TextMeshProUGUI _pityTotalLabel;
        private TextMeshProUGUI _fivePityLabel;
        private TextMeshProUGUI _fourPityLabel;
        private Image _centerArt;
        private RectTransform _centerArtRt;
        private Transform _rateUpRow;
        private RectTransform _fiveFill;
        private RectTransform _fourFill;
        private Button _pull1Btn;
        private Button _pull10Btn;
        private TextMeshProUGUI _pull1Text;
        private TextMeshProUGUI _pull10Text;
        private readonly List<Image> _cardBgs = new List<Image>();
        private readonly List<Button> _cardBtns = new List<Button>();
        private readonly List<GameObject> _cardAccents = new List<GameObject>();
        private readonly List<GameObject> _cardFrames = new List<GameObject>();
        private GameObject _overlay;
        private TextMeshProUGUI _overlayTitle;
        private RectTransform _overlayContent;
        private BannerDto[] _banners;
        private BannerDto _selected;
        private BannerSlot _selectedSlot;
        private BannerInfo _pity;
        private bool _offlineData;
        private bool _busy;
        // Owned item ids, snapshotted once when the page is entered (see
        // LoadOwnedItems). The server never returns isNew/isDuplicate, so
        // this snapshot is the only reliable NEW determination: an id not
        // in this set before the pull is NEW. Merged after every pull.
        // Caveat: a pull made on another device after this snapshot will
        // read as NEW here — accepted (see docs/gacha-phase1-2-plan.md §7.6).
        private readonly HashSet<int> _ownedItemIds = new HashSet<int>();
        // Soft moonlight glow behind the character (Update).
        private Image _charGlow;
        private float _time;
        // Wallet balance for pull-button gating; -1 = not loaded yet
        // (buttons stay enabled, as before, until the wallet arrives).
        private long _freeBalance = -1;

        private void Start()
        {
            // Scene Canvas (named "Canvas"), NOT the ToastCanvas.
            var canvas = UiFactory.SceneCanvas();
            if (canvas == null) { Debug.LogError("[Gacha] No Canvas found in scene."); return; }
            EnsureCanvasScaler(canvas);
            // Title left empty: the top bar is built by hand (logo + nav).
            var (root, topBar) = UiFactory.ScreenBase(canvas.transform, "",
                () => GameManager.Instance.GoTo(GameManager.SceneMainMenu));
            _root = root.transform;

            BuildBackdrop(root.transform);
            BuildTopBar(topBar);
            BuildCenterArt(root.transform);
            BuildCenterColumn(root.transform);
            BuildBottomButtons(root.transform);
            BuildOverlay(root.transform);

            LoadBanners();
            RefreshWallet();
            LoadOwnedItems();
        }

        // One GetInventory snapshot for NEW determination (see _ownedItemIds).
        // Silent on failure: an empty set just means every pulled item reads
        // as NEW until the next page visit. Never blocks the page.
        private void LoadOwnedItems()
        {
            if (!GameManager.Instance.IsLoggedIn) return;
            StartCoroutine(GameManager.Instance.Api.GetInventory(r =>
            {
                if (!r.Success || r.Data == null || r.Data.items == null) return;
                _ownedItemIds.Clear();
                foreach (var item in r.Data.items)
                {
                    if (item != null) _ownedItemIds.Add(item.itemId);
                }
            }));
        }

        // The scene Canvas has no CanvasScaler (constant pixel size), so at
        // 1920x1080 the 1280x720 layout would sit in the top-left corner.
        // Scale With Screen Size (reference 1280x720) keeps the design
        // proportional at any window size. Only the Gacha canvas is touched.
        private static void EnsureCanvasScaler(Canvas canvas)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) return;
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Gentle ambient motion: the moonlight glow breathes. Only Image
        // colour alpha is touched — no layout work. (v4 refinement: the
        // twinkling star dots are gone — decoration that adds nothing.)
        private void Update()
        {
            _time += Time.deltaTime;
            if (_charGlow != null)
            {
                var c = _charGlow.color;
                c.a = 0.09f + 0.02f * Mathf.Sin(_time * 0.6f);
                _charGlow.color = c;
            }
        }

        // ---------- top bar: logo + nav + currency + avatar + settings ----------
        // Simplified nav, no boxes: text items, WISH underlined as the
        // current page. The top bar itself is made more translucent than
        // ScreenBase's default so the scene's moon and sky show through it.

        private void BuildTopBar(GameObject topBar)
        {
            var barImg = topBar.GetComponent<Image>();
            if (barImg != null) barImg.color = UiFactory.Glass(0.62f);

            var logo = UiFactory.Label(topBar.transform, "Logo", "ASTRAL BLOOM", 20,
                TextAlignmentOptions.Left, UiFactory.MoonWhite, true);
            UiFactory.Place(logo.rectTransform, 130, -10, 230, 30);
            var logoSub = UiFactory.Label(topBar.transform, "LogoSub", "星樱物语", 13,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            UiFactory.Place(logoSub.rectTransform, 130, -38, 230, 20);

            MakeNavItem(topBar.transform, "HOME", 430, 70, false,
                () => GameManager.Instance.GoTo(GameManager.SceneMainMenu));
            MakeNavItem(topBar.transform, "WISH", 510, 70, true, null);
            MakeNavItem(topBar.transform, "CHARACTER", 600, 100, false,
                () => Toast.Show("角色界面即将开放。"));
            MakeNavItem(topBar.transform, "GALLERY", 705, 80, false,
                () => Toast.Show("图鉴界面即将开放。"));
            MakeNavItem(topBar.transform, "SETTINGS", 795, 90, false,
                () => Toast.Show("设置界面即将开放。"));

            // Currency: plain labels + small "+" buttons (no pills, no boxes).
            _freeLabel = UiFactory.Label(topBar.transform, "FreeLabel", "◆ —", 15,
                TextAlignmentOptions.Left, UiFactory.MoonWhite);
            var fRt = _freeLabel.rectTransform;
            fRt.anchorMin = new Vector2(0, 0.5f); fRt.anchorMax = new Vector2(0, 0.5f);
            fRt.pivot = new Vector2(0, 0.5f);
            fRt.anchoredPosition = new Vector2(930, 0);
            fRt.sizeDelta = new Vector2(96, 24);
            var freePlus = UiFactory.Button(topBar.transform, "FreePlus", "+",
                () => Toast.Show("货币购买暂未开放，敬请期待。"), new Vector2(24, 24), UiFactory.DeepUltramarine);
            var fpRt = freePlus.GetComponent<RectTransform>();
            fpRt.anchorMin = new Vector2(0, 0.5f); fpRt.anchorMax = new Vector2(0, 0.5f);
            fpRt.pivot = new Vector2(0, 0.5f);
            fpRt.anchoredPosition = new Vector2(1030, 0);

            _ticketLabel = UiFactory.Label(topBar.transform, "TicketLabel", "★ —", 15,
                TextAlignmentOptions.Left, UiFactory.MoonWhite);
            var tRt = _ticketLabel.rectTransform;
            tRt.anchorMin = new Vector2(0, 0.5f); tRt.anchorMax = new Vector2(0, 0.5f);
            tRt.pivot = new Vector2(0, 0.5f);
            tRt.anchoredPosition = new Vector2(1060, 0);
            tRt.sizeDelta = new Vector2(70, 24);
            var ticketPlus = UiFactory.Button(topBar.transform, "TicketPlus", "+",
                () => Toast.Show("货币购买暂未开放，敬请期待。"), new Vector2(24, 24), UiFactory.DeepUltramarine);
            var tpRt = ticketPlus.GetComponent<RectTransform>();
            tpRt.anchorMin = new Vector2(0, 0.5f); tpRt.anchorMax = new Vector2(0, 0.5f);
            tpRt.pivot = new Vector2(0, 0.5f);
            tpRt.anchoredPosition = new Vector2(1134, 0);

            // Avatar (expression sprite = face) + settings.
            var avatar = UiFactory.ArtImage(topBar.transform, "Avatar", LoadArt("expr_01_calm"));
            if (avatar != null)
            {
                var aRt = avatar.GetComponent<RectTransform>();
                aRt.anchorMin = new Vector2(0, 0.5f); aRt.anchorMax = new Vector2(0, 0.5f);
                aRt.pivot = new Vector2(0, 0.5f);
                aRt.anchoredPosition = new Vector2(1166, 0);
                aRt.sizeDelta = new Vector2(40, 40);
            }
            var settings = UiFactory.Button(topBar.transform, "SettingsBtn", "设置",
                () => Toast.Show("设置界面即将开放。"), new Vector2(52, 36));
            var sRt = settings.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0, 0.5f); sRt.anchorMax = new Vector2(0, 0.5f);
            sRt.pivot = new Vector2(0, 0.5f);
            sRt.anchoredPosition = new Vector2(1212, 0);
        }

        // Borderless text nav item: the label itself is the Button's
        // targetGraphic, so there is no box — hover just tints the text.
        // The selected item gets a thin underline instead of a highlight box.
        private Button MakeNavItem(Transform topBar, string text, float x, float width,
            bool selected, UnityAction onClick)
        {
            var label = UiFactory.Label(topBar, "Nav_" + text, text, 17,
                TextAlignmentOptions.Center,
                selected ? UiFactory.MoonWhite : UiFactory.SilverWhite, selected);
            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0, 0.5f); rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0);
            rt.sizeDelta = new Vector2(width, 28);
            var btn = label.gameObject.AddComponent<Button>();
            btn.targetGraphic = label;
            if (onClick != null) btn.onClick.AddListener(onClick);
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = UiFactory.MoonlightBlue;
            colors.pressedColor = UiFactory.SilverWhite;
            colors.disabledColor = Color.white;
            btn.colors = colors;
            if (selected)
            {
                var under = UiFactory.Panel(topBar, "NavUnderline",
                    new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                    new Vector2(width * 0.6f, 2), UiFactory.MoonlightBlue);
                var uRt = under.GetComponent<RectTransform>();
                uRt.pivot = new Vector2(0.5f, 0.5f);
                uRt.anchoredPosition = new Vector2(x, -18);
            }
            return btn;
        }

        // ---------- backdrop: shrine-courtyard scene + veil + soft glow ----------
        // The scene image is the backdrop (no gradient, no moon disc, no
        // halo stack — the painted scene already has its own moon). A thin
        // deep-ultramarine veil keeps the text readable; a low-alpha radial
        // glow behind the character blends the cut-out into the scene
        // instead of a white circle. The backdrop is the first child of the
        // screen root, so it draws behind the top bar and every panel.

        private void BuildBackdrop(Transform root)
        {
            var tex = Resources.Load<Texture2D>("Backgrounds/Gacha");
            if (tex != null)
            {
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                var bg = UiFactory.ArtImage(root, "BackdropScene", sprite, false);
                var bgRt = bg.GetComponent<RectTransform>();
                UiFactory.Stretch(bgRt);
                bg.transform.SetAsFirstSibling();
            }
            else
            {
                var gradSprite = UiFactory.GradientSprite(
                    new Color(0.11f, 0.23f, 0.55f, 1f), UiFactory.DeepUltramarine);
                var bg = UiFactory.ArtImage(root, "BackdropGradient", gradSprite, false);
                var bgRt = bg.GetComponent<RectTransform>();
                UiFactory.Stretch(bgRt);
                bg.transform.SetAsFirstSibling();
            }

            // Unifying veil (right after the backdrop, before everything else).
            var veil = UiFactory.Panel(root, "Veil",
                Vector2.zero, Vector2.one, Vector2.zero, UiFactory.Glass(0.22f));
            UiFactory.Stretch(veil.GetComponent<RectTransform>());
            veil.transform.SetSiblingIndex(1);

            // Soft moonlight glow centred on the character (right zone).
            // It blends the cut-out art into the scene's lighting — the
            // scene's own stars stay; no extra decorative dots are added.
            var glowSprite = UiFactory.RadialGlowSprite();
            _charGlow = AddGlow(root, glowSprite, new Vector2(0, 1), new Vector2(1128, -356),
                new Vector2(560, 560), new Color(0.47f, 0.59f, 0.86f, 0.10f));
        }

        private static Image AddGlow(Transform root, Sprite glowSprite, Vector2 anchor,
            Vector2 pos, Vector2 size, Color color)
        {
            var glow = UiFactory.ArtImage(root, "Glow", glowSprite);
            var gRt = glow.GetComponent<RectTransform>();
            gRt.anchorMin = anchor; gRt.anchorMax = anchor;
            gRt.pivot = new Vector2(0.5f, 0.5f);
            gRt.anchoredPosition = pos;
            gRt.sizeDelta = size;
            glow.color = color;
            return glow;
        }

        // ---------- right zone: large featured character (full body) ----------
        // Anchored top-right, 560px tall, feet at -636 — just above the
        // bottom button row (-640), so no UI ever overlaps the character.

        private void BuildCenterArt(Transform root)
        {
            _centerArt = UiFactory.ArtImage(root, "FeaturedArt", LoadArt("celestia_splash"));
            if (_centerArt != null)
            {
                _centerArtRt = _centerArt.GetComponent<RectTransform>();
                _centerArtRt.anchorMin = new Vector2(1, 1);
                _centerArtRt.anchorMax = new Vector2(1, 1);
                _centerArtRt.pivot = new Vector2(1, 1);
                _centerArtRt.anchoredPosition = new Vector2(-16, -76);
                SetCenterArtSize(_centerArt.sprite);
            }
        }

        private void SetCenterArtSize(Sprite sprite)
        {
            if (_centerArtRt == null || sprite == null || sprite.texture == null) return;
            float aspect = (float)sprite.texture.width / sprite.texture.height;
            _centerArtRt.sizeDelta = new Vector2(560f * aspect, 560f);
        }

        // ---------- center column (x 310, ~30%): badge, title, quote, UP, pity ----------
        // Generous whitespace between blocks; no boxes around the text.

        private void BuildCenterColumn(Transform root)
        {
            _badgeLabel = UiFactory.Label(root, "LimitedBadge", "★ 限定角色祈愿", 15,
                TextAlignmentOptions.Left, UiFactory.PaleGold);
            UiFactory.Place(_badgeLabel.rectTransform, 310, -84, 384, 22);

            _bannerTitle = UiFactory.Label(root, "BannerTitle", "", 46,
                TextAlignmentOptions.Left, UiFactory.MoonWhite, true);
            UiFactory.Place(_bannerTitle.rectTransform, 310, -110, 384, 58);

            _bannerSubtitle = UiFactory.Label(root, "BannerSubtitle", "", 15,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            UiFactory.Place(_bannerSubtitle.rectTransform, 310, -172, 384, 20);

            _remainLabel = UiFactory.Label(root, "BannerRemain", "剩余时间：长期开放", 14,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            UiFactory.Place(_remainLabel.rectTransform, 310, -196, 384, 20);

            _quoteLabel = UiFactory.Label(root, "BannerQuote", "", 16,
                TextAlignmentOptions.Left, UiFactory.MoonWhite);
            UiFactory.Place(_quoteLabel.rectTransform, 310, -222, 384, 48);

            // One thin hairline separating the info block from the UP block.
            var divider = UiFactory.Panel(root, "Divider",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(384, 1),
                new Color(0.788f, 0.831f, 0.910f, 0.35f));
            var dRt = divider.GetComponent<RectTransform>();
            dRt.pivot = new Vector2(0, 1);
            dRt.anchoredPosition = new Vector2(310, -286);

            var rateUpLabel = UiFactory.Label(root, "RateUpLabel", "★ 概率提升", 14,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            UiFactory.Place(rateUpLabel.rectTransform, 310, -298, 200, 18);

            var row = new GameObject("RateUpRow");
            row.transform.SetParent(root, false);
            var rowRt = row.AddComponent<RectTransform>();
            // NOTE: assign AFTER AddComponent<RectTransform> — adding a
            // RectTransform destroys the GameObject's original Transform,
            // so a reference taken before AddComponent becomes a dead
            // (fake-null) reference and ClearChildren throws NRE.
            _rateUpRow = rowRt;
            rowRt.anchorMin = new Vector2(0, 1); rowRt.anchorMax = new Vector2(0, 1);
            rowRt.pivot = new Vector2(0, 1);
            rowRt.anchoredPosition = new Vector2(310, -320);
            rowRt.sizeDelta = new Vector2(288, 140);

            // Pity block — all numbers come from the live BannerInfo (see
            // UpdatePityDisplay); nothing here is hardcoded. The total is
            // auxiliary (silver, smaller); the two pity lines carry the
            // numbers the player reads at a glance.
            _pityTotalLabel = UiFactory.Label(root, "PityTotal", "", 14,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            UiFactory.Place(_pityTotalLabel.rectTransform, 310, -472, 384, 20);

            _fivePityLabel = UiFactory.Label(root, "FivePity", "", 15,
                TextAlignmentOptions.Left, UiFactory.MoonWhite);
            UiFactory.Place(_fivePityLabel.rectTransform, 310, -496, 384, 18);
            // Slim 6px bars (v4 refinement): a flat hairline track and a
            // flat fill — a simple progress read, not a chunky gauge.
            var fiveBg = UiFactory.Panel(root, "FiveBarBg",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(PityBarWidth, 6), UiFactory.GlassWhite(0.12f));
            var fiveBgRt = fiveBg.GetComponent<RectTransform>();
            fiveBgRt.pivot = new Vector2(0, 1);
            fiveBgRt.anchoredPosition = new Vector2(310, -518);
            _fiveFill = UiFactory.Panel(root, "FiveBarFill",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 6), UiFactory.MoonlightBlue)
                .GetComponent<RectTransform>();
            _fiveFill.pivot = new Vector2(0, 1);
            _fiveFill.anchoredPosition = new Vector2(310, -518);

            _fourPityLabel = UiFactory.Label(root, "FourPity", "", 15,
                TextAlignmentOptions.Left, UiFactory.MoonWhite);
            UiFactory.Place(_fourPityLabel.rectTransform, 310, -536, 384, 18);
            var fourBg = UiFactory.Panel(root, "FourBarBg",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(PityBarWidth, 6), UiFactory.GlassWhite(0.12f));
            var fourBgRt = fourBg.GetComponent<RectTransform>();
            fourBgRt.pivot = new Vector2(0, 1);
            fourBgRt.anchoredPosition = new Vector2(310, -558);
            _fourFill = UiFactory.Panel(root, "FourBarFill",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 6), UiFactory.SilverWhite)
                .GetComponent<RectTransform>();
            _fourFill.pivot = new Vector2(0, 1);
            _fourFill.anchoredPosition = new Vector2(310, -558);

            var guarantee = UiFactory.Label(root, "Guarantee", "十连必出★4以上角色", 13,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            UiFactory.Place(guarantee.rectTransform, 310, -576, 384, 18);
        }

        // UP rate-up frames (unified size). The first frame is the featured
        // UP character: brighter fill, a pale-gold UP tag, and the concept
        // nameplate name (e.g. 绫华) instead of the placeholder catalog name.
        private void BuildRateUpCards(BannerSlot slot)
        {
            if (_rateUpRow == null) return;
            UiFactory.ClearChildren(_rateUpRow);
            for (int i = 0; i < slot.rateUpIds.Length; i++)
            {
                int itemId = slot.rateUpIds[i];
                bool featured = i == 0;
                // Flat frame (v4 refinement): sharp corners, hairline
                // border, subtle fill. The featured UP frame gets a
                // pale-gold hairline + a translucent classic-ultramarine
                // fill; the others stay quiet so the UP reads at a glance.
                var card = UiFactory.Panel(_rateUpRow, "RateUp" + itemId,
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(84, 140),
                    featured ? new Color(0.141f, 0.302f, 0.671f, 0.72f) : UiFactory.Glass(0.28f));
                var cRt = card.GetComponent<RectTransform>();
                cRt.pivot = new Vector2(0, 1);
                cRt.anchoredPosition = new Vector2(i * 96, 0);
                if (featured) UiFactory.AddBorder(cRt, UiFactory.PaleGold);
                else UiFactory.AddSilverBorder(cRt);

                var art = UiFactory.ArtImage(card.transform, "Art", LoadArt(ItemNames.ArtForItem(itemId)));
                if (art != null)
                {
                    var aRt = art.GetComponent<RectTransform>();
                    aRt.anchorMin = new Vector2(0, 1); aRt.anchorMax = new Vector2(0, 1);
                    aRt.pivot = new Vector2(0, 1);
                    aRt.anchoredPosition = Vector2.zero;
                    aRt.sizeDelta = new Vector2(84, 92);
                }

                string displayName = featured && !string.IsNullOrEmpty(slot.nameplate)
                    ? slot.nameplate : ItemNames.Get(itemId);
                var name = UiFactory.Label(card.transform, "Name", displayName, 12,
                    TextAlignmentOptions.Center, UiFactory.MoonWhite);
                UiFactory.Place(name.rectTransform, 0, -94, 84, 18);
                var stars = UiFactory.Label(card.transform, "Stars", ItemNames.Stars(5), 11,
                    TextAlignmentOptions.Center, UiFactory.PaleGold);
                UiFactory.Place(stars.rectTransform, 0, -114, 84, 16);

                if (featured)
                {
                    var up = UiFactory.Label(card.transform, "UpTag", "UP!", 11,
                        TextAlignmentOptions.Right, UiFactory.PaleGold, true);
                    UiFactory.Place(up.rectTransform, 44, -4, 36, 16);
                }
            }
        }

        // ---------- bottom: pull buttons (right) + 详情 / 历史记录 / 卡池说明 (left) ----------

        private void BuildBottomButtons(Transform root)
        {
            _pull1Btn = MakePullButton(root, "Pull1", 800, "祈愿一次",
                () => DoPull(1), UiFactory.DeepUltramarine, UiFactory.MoonWhite,
                UiFactory.StarryBlue, UiFactory.SilverBorder);
            _pull1Text = _pull1Btn.GetComponentInChildren<TextMeshProUGUI>();

            // Ten-pull is the primary action (v4 refinement): a stronger
            // classic-ultramarine fill with a pale-gold hairline, so it
            // clearly outweighs the single pull without any glow or
            // gradient. Single pull stays quiet deep blue + silver hairline.
            _pull10Btn = MakePullButton(root, "Pull10", 1022, "祈愿十次",
                () => DoPull(10), UiFactory.ClassicUltramarine, UiFactory.MoonWhite,
                UiFactory.StarryBlue, UiFactory.PaleGold);
            _pull10Text = _pull10Btn.GetComponentInChildren<TextMeshProUGUI>();

            MakeTextButton(root, "DetailsBtn", 16, "详情", ShowDetails);
            MakeTextButton(root, "HistoryBtn", 168, "历史记录", ShowHistory);
            MakeTextButton(root, "InfoBtn", 320, "卡池说明", ShowBannerInfo);
        }

        // Borderless text button with a thin underline: no box, no glass.
        private Button MakeTextButton(Transform root, string name, float x, string label,
            UnityAction onClick)
        {
            var txt = UiFactory.Label(root, name, label, 17,
                TextAlignmentOptions.Center, UiFactory.SilverWhite);
            var rt = txt.rectTransform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -640);
            rt.sizeDelta = new Vector2(140, 40);
            var btn = txt.gameObject.AddComponent<Button>();
            btn.targetGraphic = txt;
            btn.onClick.AddListener(onClick);
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = UiFactory.MoonWhite;
            colors.pressedColor = UiFactory.MoonlightBlue;
            btn.colors = colors;
            var under = UiFactory.Panel(root, name + "Line",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(140, 1),
                new Color(0.788f, 0.831f, 0.910f, 0.35f));
            var uRt = under.GetComponent<RectTransform>();
            uRt.pivot = new Vector2(0, 1);
            uRt.anchoredPosition = new Vector2(x, -684);
            return btn;
        }

        // Pull button: flat fill, thin hairline edge, hover/press
        // feedback. No glow panel behind it, no gradient — the colour
        // and the hairline carry the weight.
        private Button MakePullButton(Transform root, string name, float x, string label,
            UnityAction onClick, Color bg, Color textColor, Color highlight, Color borderColor)
        {
            var go = UiFactory.Panel(root, name,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(210, 64), bg);
            var rt = go.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -640);
            UiFactory.AddBorder(rt, borderColor);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            var colors = btn.colors;
            colors.normalColor = bg;
            colors.highlightedColor = highlight;
            colors.pressedColor = UiFactory.DeepUltramarine;
            colors.disabledColor = new Color(bg.r, bg.g, bg.b, 0.45f);
            btn.colors = colors;
            var txt = UiFactory.Label(go.transform, "Text", label, 22,
                TextAlignmentOptions.Center, textColor, true);
            UiFactory.Stretch(txt.rectTransform);
            return btn;
        }

        // ---------- overlay panel (details / results / history / info) ----------

        private void BuildOverlay(Transform root)
        {
            _overlay = UiFactory.Panel(root, "OverlayDim",
                Vector2.zero, Vector2.one, Vector2.zero, new Color(0f, 0f, 0.05f, 0.6f));

            var panel = UiFactory.RoundedPanel(_overlay.transform, "OverlayPanel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(560, 460), UiFactory.Glass(0.96f));
            var pRt = panel.GetComponent<RectTransform>();
            pRt.anchoredPosition = Vector2.zero;
            UiFactory.AddSilverBorder(pRt);

            _overlayTitle = UiFactory.Label(panel.transform, "OverlayTitle", "", 24,
                TextAlignmentOptions.Center, UiFactory.MoonWhite, true);
            UiFactory.Place(_overlayTitle.rectTransform, 0, -16, 560, 36);

            var (scroll, content) = UiFactory.ScrollView(panel.transform, "OverlayScroll",
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0));
            var sRt = scroll.GetComponent<RectTransform>();
            sRt.offsetMin = new Vector2(16, 64);
            sRt.offsetMax = new Vector2(-16, -60);
            _overlayContent = content;

            var close = UiFactory.Button(panel.transform, "OverlayClose", "关闭",
                () => _overlay.SetActive(false), new Vector2(160, 44));
            var cRt = close.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.5f, 0); cRt.anchorMax = new Vector2(0.5f, 0);
            cRt.pivot = new Vector2(0.5f, 0);
            cRt.anchoredPosition = new Vector2(0, 12);

            _overlay.SetActive(false);
        }

        private void ShowOverlay(string title, List<(string text, Color color)> lines)
        {
            _overlayTitle.text = title;
            UiFactory.ClearChildren(_overlayContent);
            foreach (var line in lines)
            {
                var l = UiFactory.Label(_overlayContent, "Line", line.text, 18,
                    TextAlignmentOptions.Left, line.color);
                l.rectTransform.sizeDelta = new Vector2(0, 30);
            }
            // Banner cards are built later (API callback) and would otherwise
            // draw above the overlay; force the overlay to the front.
            _overlay.transform.SetAsLastSibling();
            _overlay.SetActive(true);
        }

        // ---------- banners ----------

        private void LoadBanners()
        {
            StartCoroutine(GameManager.Instance.Api.GetBanners(r =>
            {
                if (!r.Success)
                {
                    if (r.Failure == ApiFailureKind.Unauthorized)
                    {
                        GameManager.Instance.HandleUnauthorized();
                        return;
                    }
                    Toast.Show("卡池信息加载失败，已显示本地卡池（离线）。");
                    _banners = FallbackBanners;
                    _offlineData = true;
                    BuildBannerCards();
                    SelectSlot(0);
                    return;
                }
                _banners = r.Data.banners ?? new BannerDto[0];
                _offlineData = false;
                if (_banners.Length == 0)
                {
                    _bannerTitle.text = "暂无卡池。";
                    BuildBannerCards();
                    return;
                }
                BuildBannerCards();
                SelectSlot(0);
            }));
        }

        // Slim banner rows (264x86): thumbnail, name, English sub, status.
        // No heavy boxes — a faint translucent fill; the selected row gets
        // a classic-ultramarine fill, a pale-gold left accent and a silver
        // hairline frame. Coming-soon rows are dimmed and show 即将开启.
        private void BuildBannerCards()
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                var slot = Slots[i];
                int idx = i;
                // Flat list row (v4 refinement): no rounded card, no heavy
                // fill — a near-transparent row with a hairline divider
                // below it. Selection is shown by a translucent classic-
                // ultramarine tint + the pale-gold accent and silver frame
                // (see UpdateCardHighlight), never by a solid block.
                var card = UiFactory.Panel(_root, "BannerCard" + idx,
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(264, 86),
                    UiFactory.Glass(slot.comingSoon ? 0.06f : 0.10f));
                var cRt = card.GetComponent<RectTransform>();
                cRt.pivot = new Vector2(0, 1);
                cRt.anchoredPosition = new Vector2(16, -84 - idx * 98);
                var btn = card.AddComponent<Button>();
                btn.onClick.AddListener(() => SelectSlot(idx));
                var cols = btn.colors;
                cols.normalColor = UiFactory.Glass(slot.comingSoon ? 0.06f : 0.10f);
                cols.highlightedColor = new Color(0.255f, 0.412f, 0.788f, 0.35f);
                cols.pressedColor = new Color(0.078f, 0.169f, 0.404f, 0.55f);
                btn.colors = cols;

                // Hairline list divider in the gap below the row.
                if (idx < Slots.Length - 1)
                {
                    var div = UiFactory.Panel(_root, "BannerDivider" + idx,
                        new Vector2(0, 1), new Vector2(0, 1), new Vector2(264, 1),
                        new Color(0.788f, 0.831f, 0.910f, 0.22f));
                    var dvRt = div.GetComponent<RectTransform>();
                    dvRt.pivot = new Vector2(0, 1);
                    dvRt.anchoredPosition = new Vector2(16, -84 - idx * 98 - 92);
                }
                _cardBtns.Add(btn);

                // Pale-gold accent bar on the left edge; visible only while
                // this card is selected (see UpdateCardHighlight).
                var accent = UiFactory.Panel(card.transform, "Accent",
                    new Vector2(0, 0), new Vector2(0, 1), new Vector2(3, -16), UiFactory.PaleGold);
                var aRt = accent.GetComponent<RectTransform>();
                aRt.anchorMin = new Vector2(0, 0); aRt.anchorMax = new Vector2(0, 1);
                aRt.pivot = new Vector2(0, 0.5f);
                aRt.anchoredPosition = new Vector2(0, 0);
                aRt.sizeDelta = new Vector2(3, -16);
                accent.SetActive(false);
                _cardAccents.Add(accent);

                // Silver hairline frame; visible only while selected.
                var frame = new GameObject("SelectedFrame");
                frame.transform.SetParent(card.transform, false);
                var fRt = frame.AddComponent<RectTransform>();
                UiFactory.Stretch(fRt);
                UiFactory.AddSilverBorder(fRt);
                frame.SetActive(false);
                _cardFrames.Add(frame);

                if (!string.IsNullOrEmpty(slot.artName))
                {
                    var art = UiFactory.ArtImage(card.transform, "Art", LoadArt(slot.artName));
                    if (art != null)
                    {
                        var aRt2 = art.GetComponent<RectTransform>();
                        aRt2.anchorMin = new Vector2(0, 0.5f); aRt2.anchorMax = new Vector2(0, 0.5f);
                        aRt2.pivot = new Vector2(0, 0.5f);
                        aRt2.anchoredPosition = new Vector2(8, 0);
                        aRt2.sizeDelta = new Vector2(64, 80);
                    }
                }
                else
                {
                    // Weapon banner: no art — a star glyph placeholder.
                    var glyph = UiFactory.Label(card.transform, "Glyph", "◆", 40,
                        TextAlignmentOptions.Center, UiFactory.MoonlightBlue);
                    UiFactory.Place(glyph.rectTransform, 8, -3, 64, 80);
                }

                Color nameColor = slot.comingSoon
                    ? new Color(0.898f, 0.918f, 0.969f, 0.55f) : UiFactory.MoonWhite;
                Color subColor = slot.comingSoon
                    ? new Color(0.788f, 0.831f, 0.910f, 0.5f) : UiFactory.SilverWhite;
                var name = UiFactory.Label(card.transform, "Name", slot.name, 18,
                    TextAlignmentOptions.Left, nameColor, true);
                UiFactory.Place(name.rectTransform, 80, -8, 176, 24);
                var sub = UiFactory.Label(card.transform, "Sub", slot.cardSub, 11,
                    TextAlignmentOptions.Left, subColor);
                UiFactory.Place(sub.rectTransform, 80, -34, 176, 16);
                var remain = UiFactory.Label(card.transform, "Remain",
                    slot.comingSoon ? "即将开启" : "剩余时间：" + slot.remain, 12,
                    TextAlignmentOptions.Left,
                    // Coming-soon status in dimmed silver, not pink: pink is
                    // reserved for the 5★ accent and must not dominate.
                    slot.comingSoon ? new Color(0.788f, 0.831f, 0.910f, 0.55f) : subColor);
                UiFactory.Place(remain.rectTransform, 80, -56, 176, 18);

                if (!string.IsNullOrEmpty(slot.badge))
                {
                    var badge = UiFactory.Label(card.transform, "Badge", slot.badge, 11,
                        TextAlignmentOptions.Right,
                        slot.badge == "限定" ? UiFactory.PaleGold : UiFactory.MoonlightBlue, true);
                    UiFactory.Place(badge.rectTransform, 216, -8, 40, 16);
                }

                _cardBgs.Add(card.GetComponent<Image>());
            }
        }

        private void UpdateCardHighlight(BannerSlot selected)
        {
            for (int i = 0; i < _cardBgs.Count; i++)
            {
                if (_cardBgs[i] == null) continue;
                bool isSel = Slots[i] == selected;
                bool soon = Slots[i].comingSoon;
                // Selected: translucent classic-ultramarine tint (the scene
                // shows through), not a solid block. Unselected rows stay
                // near-transparent so the list reads as a list, not cards.
                Color fill = isSel
                    ? new Color(0.141f, 0.302f, 0.671f, 0.55f)
                    : UiFactory.Glass(soon ? 0.06f : 0.10f);
                _cardBgs[i].color = fill;
                // The Button's colorBlock drives the Image colour on hover /
                // press, so its normal colour must track the selection too,
                // or a hover would silently drop the highlight.
                if (i < _cardBtns.Count && _cardBtns[i] != null)
                {
                    var c = _cardBtns[i].colors;
                    c.normalColor = fill;
                    _cardBtns[i].colors = c;
                }
                if (i < _cardAccents.Count && _cardAccents[i] != null)
                    _cardAccents[i].SetActive(isSel);
                if (i < _cardFrames.Count && _cardFrames[i] != null)
                    _cardFrames[i].SetActive(isSel);
            }
        }

        private void SelectSlot(int idx)
        {
            var slot = Slots[idx];
            // Selecting a card always updates the centre column (title, art,
            // quote, rate-up, pity) — for a coming-soon banner the pull
            // buttons are disabled instead of blocking the selection.
            _selectedSlot = slot;
            UpdateCardHighlight(slot);
            _bannerTitle.text = slot.name;
            _bannerSubtitle.text = slot.subtitle;
            _remainLabel.text = "剩余时间：" + slot.remain;
            _quoteLabel.text = slot.quote;
            _badgeLabel.text = slot.comingSoon
                ? (slot.badge == "常驻" ? "★ 常驻祈愿 · 即将开启" : "即将开启")
                : "★ 限定角色祈愿";
            UpdatePullButtons();

            var sprite = LoadArt(slot.artName);
            if (_centerArt != null)
            {
                if (sprite != null)
                {
                    _centerArt.sprite = sprite;
                    _centerArt.gameObject.SetActive(true);
                    SetCenterArtSize(sprite);
                }
                else
                {
                    _centerArt.gameObject.SetActive(false);
                }
            }

            BuildRateUpCards(slot);

            if (slot.comingSoon)
            {
                _selected = null;
                _pity = null;
                _pityTotalLabel.text = slot.name + "即将开启，敬请期待。";
                _fivePityLabel.text = "";
                _fourPityLabel.text = "";
                SetProgress(0f, 0f);
                UpdatePullButtonStates();
                return;
            }

            BannerDto dto = null;
            if (_banners != null)
            {
                for (int i = 0; i < _banners.Length; i++)
                {
                    if (_banners[i].id == slot.backendId) { dto = _banners[i]; break; }
                }
            }
            if (dto == null)
            {
                Toast.Show("卡池信息加载失败。");
                _selected = null;
                UpdatePullButtonStates();
                return;
            }
            _selected = dto;
            _pity = null;
            UpdatePullButtonStates();

            if (_offlineData)
            {
                _pityTotalLabel.text = "保底信息离线不可用";
                _fivePityLabel.text = "";
                _fourPityLabel.text = "";
                SetProgress(0f, 0f);
                return;
            }
            _pityTotalLabel.text = "加载保底信息…";
            StartCoroutine(GameManager.Instance.Api.GetBannerDetail(dto.id, r =>
            {
                if (_selected != dto) return; // user switched banners meanwhile
                if (!r.Success) { _pityTotalLabel.text = "保底信息加载失败。"; return; }
                _pity = r.Data;
                UpdatePityDisplay();
            }));
        }

        private void UpdatePullButtons()
        {
            int cost = _selected != null ? _selected.costPerPull : 160;
            if (_pull1Text != null) _pull1Text.text = "祈愿一次\n<size=14>◆ ×" + cost + "</size>";
            if (_pull10Text != null) _pull10Text.text = "祈愿十次\n<size=14>◆ ×" + (cost * 10) + "</size>";
            UpdatePullButtonStates();
        }

        // Pull buttons are only clickable when the selected banner is live,
        // no pull request is in flight (_busy), and the real wallet balance
        // covers the cost. While the wallet has not loaded yet
        // (_freeBalance < 0) the buttons stay enabled, as before — the server
        // still validates every pull.
        private void UpdatePullButtonStates()
        {
            if (_pull1Btn == null || _pull10Btn == null) return;
            if (_selectedSlot == null) return; // banners not loaded yet
            bool comingSoon = _selectedSlot.comingSoon;
            int cost = _selected != null ? _selected.costPerPull : 160;
            bool can1 = !_busy && !comingSoon && _selected != null && (_freeBalance < 0 || _freeBalance >= cost);
            bool can10 = !_busy && !comingSoon && _selected != null && (_freeBalance < 0 || _freeBalance >= cost * 10);
            _pull1Btn.interactable = can1;
            _pull10Btn.interactable = can10;
            if (_pull1Text != null)
                _pull1Text.color = can1 ? UiFactory.MoonWhite
                    : new Color(0.898f, 0.918f, 0.969f, 0.45f);
            if (_pull10Text != null)
                _pull10Text.color = can10 ? UiFactory.MoonWhite
                    : new Color(0.898f, 0.918f, 0.969f, 0.45f);
        }

        // All numbers come from the live BannerInfo — nothing is hardcoded.
        private void UpdatePityDisplay()
        {
            if (_pity == null) return;
            _pityTotalLabel.text = "已抽 " + _pity.totalCount + " 次";
            _fivePityLabel.text = "五星保底　还需 <color=#7897DB>" + _pity.fiveRemain + "</color> 抽";
            _fourPityLabel.text = "四星保底　还需 <color=#C9D4E8>" + _pity.fourRemain + "</color> 抽";
            SetProgress(1f - _pity.fiveRemain / (float)GachaRules.FiveStarHardPity,
                1f - _pity.fourRemain / (float)GachaRules.FourStarHardPity);
        }

        private void SetProgress(float five, float four)
        {
            if (_fiveFill != null)
                _fiveFill.sizeDelta = new Vector2(PityBarWidth * Mathf.Clamp01(five), 6f);
            if (_fourFill != null)
                _fourFill.sizeDelta = new Vector2(PityBarWidth * Mathf.Clamp01(four), 6f);
        }

        // ---------- wallet / pull ----------

        private void RefreshWallet()
        {
            GameManager.Instance.RefreshWallet(() =>
            {
                var w = GameManager.Instance.Wallet;
                if (w == null)
                {
                    _freeLabel.text = "◆ —";
                    _ticketLabel.text = "★ —";
                    _freeBalance = -1;
                }
                else
                {
                    _freeLabel.text = "◆ " + w.free.ToString("N0");
                    _ticketLabel.text = "★ " + w.tickets.ToString("N0");
                    _freeBalance = w.free;
                }
                UpdatePullButtonStates();
            });
        }

        // Full pull flow (docs/gacha-phase1-2-plan.md §6.5):
        // RequireOnline -> coming-soon guard -> _busy double-click guard
        // (both buttons disabled while the request is in flight) -> Api.Pull
        // -> on success: write history FIRST (one shared UTC timestamp for
        // the whole pull, so the history screen can group a ten-pull),
        // compute NEW from the pre-pull inventory snapshot, open the result
        // overlay, refresh the wallet (PullResponse has no wallet).
        // _busy is reset in EVERY path: failure callbacks, and the result
        // overlay's onClose (which PullResultScreen invokes BEFORE
        // onPullAgain, so 再次祈愿 re-enables first, then pulls again).
        // NOTE: the server has NO idempotency key — two separate requests
        // are two charges. The _busy guard is client-side only.
        private void DoPull(int count)
        {
            if (!GameManager.Instance.RequireOnline()) return;
            if (_selectedSlot != null && _selectedSlot.comingSoon)
            {
                Toast.Show("该卡池即将开启。");
                return;
            }
            if (_selected == null) return;
            if (_busy) return;
            if (_freeBalance >= 0 && _freeBalance < _selected.costPerPull * count)
            {
                Toast.Show("星愿结晶不足。");
                return;
            }
            // Capture the banner now: the player could switch banner cards
            // while the request is in flight; the callback must use the
            // banner that was actually pulled on.
            var banner = _selected;
            var slot = _selectedSlot;
            _busy = true;
            UpdatePullButtonStates();
            Toast.Show(count == 1 ? "抽卡中…" : "十连抽卡中…");
            StartCoroutine(GameManager.Instance.Api.Pull(banner.id, count, r =>
            {
                if (!r.Success)
                {
                    _busy = false;
                    UpdatePullButtonStates();
                    if (r.Failure == ApiFailureKind.Unauthorized) GameManager.Instance.HandleUnauthorized();
                    else Toast.Show(r.ErrorMessage);
                    return;
                }
                if (!r.Data.ok || r.Data.pulls == null || r.Data.pulls.Length == 0)
                {
                    _busy = false;
                    UpdatePullButtonStates();
                    Toast.Show(r.Data.error ?? "抽卡失败。");
                    return;
                }

                // 1) History FIRST, synchronously, before any UI — so a
                // crash after the pull cannot lose the record. One shared
                // UTC timestamp for all records of this pull.
                string now = System.DateTime.UtcNow.ToString("o");
                string bannerName = banner.name;
                var records = new List<PullRecord>();
                for (int i = 0; i < r.Data.pulls.Length; i++)
                {
                    var p = r.Data.pulls[i];
                    records.Add(new PullRecord
                    {
                        time = now,
                        bannerId = banner.id,
                        bannerName = bannerName,
                        itemId = p.itemId,
                        rarity = p.rarity,
                        isFeatured = p.isFeatured,
                        kind = p.kind,
                        pullNo = i + 1,
                    });
                }
                PullHistoryStore.Append(GameManager.Instance.Username, records);

                // 2) NEW = pulled ids NOT in the pre-pull snapshot; then merge.
                var newItemIds = new HashSet<int>();
                foreach (var p in r.Data.pulls)
                {
                    if (!_ownedItemIds.Contains(p.itemId)) newItemIds.Add(p.itemId);
                    _ownedItemIds.Add(p.itemId);
                }

                // 3) Result overlay. onClose resets _busy, re-enables the
                // buttons and refreshes pity from this pull's BannerInfo
                // (only if the player is still on this banner).
                var respBanner = r.Data.banner;
                string displayName = slot != null ? slot.name : banner.name;
                PullResultScreen.Show(OverlayParent(), r.Data.pulls, newItemIds, displayName,
                    onClose: () =>
                    {
                        _busy = false;
                        if (_selected == banner && respBanner != null)
                        {
                            _pity = respBanner;
                            UpdatePityDisplay();
                        }
                        UpdatePullButtonStates();
                    },
                    onPullAgain: () => DoPull(count));

                // 4) Wallet refresh (PullResponse carries no wallet).
                RefreshWallet();
            }));
        }

        // Parent for the self-contained overlays (Details / History /
        // Results): the scene Canvas, never the persistent ToastCanvas.
        // The overlay roots destroy themselves on close.
        private Transform OverlayParent()
        {
            var canvas = UiFactory.SceneCanvas();
            return canvas != null ? canvas.transform : null;
        }

        // Smoke-test hook (-auto-pull-test): one real single pull on the
        // currently selected banner (costs real currency on the test
        // account). Used to verify the pull -> history -> result flow
        // end to end through the real client.
        public void AutoPullForTest()
        {
            DoPull(1);
        }

        // Smoke-test hook (-details-shot): opens the details overlay for the
        // currently selected banner so the runner can screenshot it.
        public void OpenDetailsForShot()
        {
            ShowDetails();
        }

        private void ShowDetails()
        {
            if (_selectedSlot != null && _selectedSlot.comingSoon)
            {
                Toast.Show("该卡池即将开启。");
                return;
            }
            if (_selected == null)
            {
                Toast.Show("请先选择卡池。");
                return;
            }
            // _pity may be null if the banner detail has not loaded yet;
            // BannerDetailsScreen handles null info (static rules only).
            BannerDetailsScreen.Show(OverlayParent(), _selected, _pity, null);
        }

        private void ShowHistory()
        {
            // Local-only history (PullHistoryStore file). Works offline.
            PullHistoryScreen.Show(OverlayParent(), null);
        }

        private void ShowBannerInfo()
        {
            var lines = new List<(string text, Color color)>();
            lines.Add(("五星角色概率 0.8%（70抽起提升，90抽保底）", UiFactory.MoonWhite));
            lines.Add(("四星角色概率 5%（10抽保底）", UiFactory.MoonWhite));
            lines.Add(("十连必出★4以上角色", UiFactory.MoonWhite));
            lines.Add(("UP五星角色出现概率 50%（未抽中则下次必定为UP）", UiFactory.MoonWhite));
            lines.Add(("120抽必出当期UP五星角色", UiFactory.MoonWhite));
            lines.Add(("保底按卡池独立计算", UiFactory.SilverWhite));
            ShowOverlay("卡池说明", lines);
        }

        // ---------- art helpers ----------

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
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            _spriteCache[resourceName] = sprite;
            return sprite;
        }
    }
}
