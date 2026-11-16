// PullResultScreen — pull-result overlay (single pull / ten-pull).
//
// Self-contained: builds its own overlay under the `parent` Transform passed
// to Show(); it does NOT depend on GachaScreen (which is being rewritten) and
// does NOT call the API, show a wallet, or write pull history.
//
// CRITICAL RULE: results come from the SERVER. This screen NEVER decides
// rarity or items locally — it only receives and reveals the PullOutcome[]
// passed to Show(). The reveal animation is presentation only: it reveals
// already-known results. If the animation is skipped, interrupted, or the
// overlay is closed mid-reveal, the results shown are still exactly the
// passed-in outcomes (cards are all built up-front; the reveal only fades
// them in, and SkipAll()/Close() never changes their content).
//
// NEW vs 重复: the caller computes `newItemIds` from an inventory snapshot
// taken BEFORE the pull (see docs/gacha-phase1-2-plan.md §3 — the server
// does not return isNew). This screen only reads the set to label cards.
// If it is null, every item is shown as 重复/unknown and nothing crashes.
//
// 5★ non-UP: if a 5★ has isFeatured == false it is a standard-pool 5★; the
// UI adds the note "下个 5★ 必定 UP" (valid inference: losing the 50/50 sets
// `guaranteed` on the server, so the next 5★ is guaranteed UP).
//
// Reduced motion: PlayerPrefs key `gm_reduce_motion` (0/1). When on, Show()
// skips the reveal entirely and displays all results immediately. The toggle
// on the overlay writes the same key so the gacha page can read it later via
// PullResultScreen.IsReduceMotionEnabled().
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GalgameOnline.Api;
using GalgameOnline.UI;

namespace GalgameOnline.Screens
{
    public static class PullResultScreen
    {
        /// <summary>PlayerPrefs key for reduced motion (0 = full reveal, 1 = skip reveal).</summary>
        public const string ReduceMotionKey = "gm_reduce_motion";

        /// <summary>Read by the gacha page (and anything else) to honour the same toggle.</summary>
        public static bool IsReduceMotionEnabled()
        {
            return PlayerPrefs.GetInt(ReduceMotionKey, 0) == 1;
        }

        // Item -> art mapping lives in the single shared table
        // ItemNames.ArtForItem. User-generated placeholder art in
        // Resources/Characters/. 3★ items (301-304) have no art; they
        // appear as a text fallback.

        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

        internal static Sprite LoadArt(string resourceName)
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

        // ---- Reveal timing (seconds) ----
        // Full ten-pull reveal: prep 0.6 + 10 x 0.32 + last fade 0.03 = ~3.8 s.
        // The 5★ flourish runs concurrently with its card's reveal and adds
        // no extra time.
        internal const float PrepBeatSeconds = 0.6f;
        internal const float CardIntervalSeconds = 0.32f;
        internal const float CardFadeSeconds = 0.35f;
        internal const float SingleFadeSeconds = 0.5f;
        internal const float FiveStarFlourishSeconds = 0.9f;

        /// <summary>
        /// Builds the results overlay as a child of <paramref name="parent"/>
        /// and returns the root GameObject. The caller owns the parent (the
        /// scene Canvas); this screen never uses FindObjectOfType&lt;Canvas&gt;().
        /// <paramref name="pulls"/> is the server-returned order and is never
        /// re-sorted. <paramref name="newItemIds"/> may be null.
        /// Closing destroys the root and then invokes <paramref name="onClose"/>.
        /// 再次祈愿 destroys the root, invokes <paramref name="onClose"/>, then
        /// invokes <paramref name="onPullAgain"/>.
        /// </summary>
        public static GameObject Show(Transform parent, PullOutcome[] pulls,
            HashSet<int> newItemIds, string bannerName,
            System.Action onClose, System.Action onPullAgain)
        {
            var outcomes = pulls ?? new PullOutcome[0];
            if (parent == null)
            {
                var canvas = UiFactory.SceneCanvas();
                if (canvas != null) parent = canvas.transform;
            }
            var root = new GameObject("PullResultOverlay");
            if (parent != null) root.transform.SetParent(parent, false);
            var rootRt = root.AddComponent<RectTransform>();
            UiFactory.Stretch(rootRt);

            var controller = root.AddComponent<PullResultController>();
            controller.Init(outcomes, newItemIds, bannerName, onClose, onPullAgain);
            return root;
        }

        // Rarity accent colours (presentation only — rarity itself is server data).
        internal static Color StarsColor(int rarity)
        {
            switch (rarity)
            {
                case 5: return UiFactory.PaleGold;
                case 4: return UiFactory.MoonlightBlue;
                default: return UiFactory.SilverWhite;
            }
        }

        internal static Color CardColor(int rarity)
        {
            switch (rarity)
            {
                case 5: return new Color(0.20f, 0.30f, 0.60f, 0.92f); // gold-tinted ultramarine
                case 4: return new Color(0.13f, 0.27f, 0.58f, 0.90f); // moonlight blue glass
                default: return UiFactory.Glass(0.88f);
            }
        }

        internal static Color AccentColor(int rarity)
        {
            switch (rarity)
            {
                case 5: return UiFactory.PaleGold;
                case 4: return UiFactory.MoonlightBlue;
                default: return UiFactory.SilverWhite;
            }
        }

        internal static string KindName(string kind)
        {
            switch (kind)
            {
                case "character": return "角色";
                case "item": return "道具";
                default: return string.IsNullOrEmpty(kind) ? "—" : kind;
            }
        }

        internal static bool IsNew(HashSet<int> newItemIds, int itemId)
        {
            return newItemIds != null && newItemIds.Contains(itemId);
        }
    }

    /// <summary>
    /// Drives one overlay instance: builds the UI, runs the reveal coroutine,
    /// handles skip / detail / close. Attached to the overlay root; destroying
    /// the root destroys this with it, so a mid-reveal close can never leave a
    /// half-revealed state behind.
    ///
    /// Layout (panel is 1200 x 660, centred in 1280 x 720; all y values are
    /// distances down from the panel's top edge):
    ///   title -8..-48 | guarantee note -48..-72 (ten-pull with non-UP 5★ only)
    ///   single card 70..510 | grid 64..478 (82..496 when the note shows)
    ///   detail strip -508..-596 | buttons -612..-648
    /// </summary>
    internal class PullResultController : MonoBehaviour
    {
        private PullOutcome[] _pulls;
        private HashSet<int> _newItemIds;
        private System.Action _onClose;
        private System.Action _onPullAgain;

        private bool _closed;
        private bool _revealing;
        private bool _skipRequested;
        private bool _anyOffBannerFive;
        private Coroutine _revealRoutine;

        private readonly List<CanvasGroup> _cardGroups = new List<CanvasGroup>();
        private readonly List<GameObject> _cardRoots = new List<GameObject>();
        private readonly List<GameObject> _cardGlows = new List<GameObject>();

        private GameObject _prepLayer;
        private GameObject _prepText;
        private GameObject _skipButton;
        private GameObject _detailPanel;
        private TextMeshProUGUI _detailTitle;
        private TextMeshProUGUI _detailLines;
        private TextMeshProUGUI _detailNote;
        private TextMeshProUGUI _motionLabel;
        private int _selectedIndex = -1;

        public void Init(PullOutcome[] pulls, HashSet<int> newItemIds, string bannerName,
            System.Action onClose, System.Action onPullAgain)
        {
            _pulls = pulls;
            _newItemIds = newItemIds;
            _onClose = onClose;
            _onPullAgain = onPullAgain;
            foreach (var p in _pulls)
                if (p.rarity == 5 && !p.isFeatured) { _anyOffBannerFive = true; break; }
            Build(bannerName);
        }

        // ---------------- Build ----------------

        private void Build(string bannerName)
        {
            var root = transform;

            // Dim backdrop (first child, so it renders behind everything).
            // Tapping it during the reveal skips the reveal; after the reveal
            // it does nothing (only the buttons close the overlay).
            var dim = new GameObject("Dim");
            dim.transform.SetParent(root, false);
            var dimRt = dim.AddComponent<RectTransform>();
            UiFactory.Stretch(dimRt);
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0.05f, 0.09f, 0.25f, 0.82f);
            var dimBtn = dim.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(() => { if (_revealing) SkipAll(); });

            // Centered glass panel holding everything.
            var panel = UiFactory.RoundedPanel(root, "Panel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(1200, 660), UiFactory.Glass(0.94f));
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            UiFactory.AddSilverBorder(panelRt);

            // Tapping empty panel space during the reveal also skips it.
            // (Child buttons/cards sit above this and keep their own clicks;
            // the panel Button only catches taps that land on bare panel.)
            var panelBtn = panel.AddComponent<Button>();
            panelBtn.transition = Selectable.Transition.None;
            panelBtn.onClick.AddListener(() => { if (_revealing) SkipAll(); });

            // Title.
            string title = string.IsNullOrEmpty(bannerName)
                ? "祈愿结果" : bannerName + " · 祈愿结果";
            var titleLabel = UiFactory.Label(panel.transform, "Title", title, 26,
                TextAlignmentOptions.Center, UiFactory.MoonWhite, true);
            UiFactory.Place(titleLabel.rectTransform, 300, -8, 600, 40);

            // Reduced-motion toggle (top-right of the panel).
            var motionBtn = UiFactory.Button(panel.transform, "ReduceMotionButton",
                "", () => ToggleReduceMotion(), new Vector2(150, 36),
                UiFactory.GlassWhite(0.12f));
            var mRt = motionBtn.GetComponent<RectTransform>();
            mRt.anchorMin = new Vector2(1, 1);
            mRt.anchorMax = new Vector2(1, 1);
            mRt.pivot = new Vector2(1, 1);
            mRt.anchoredPosition = new Vector2(-16, -12);
            _motionLabel = motionBtn.GetComponentInChildren<TextMeshProUGUI>();
            _motionLabel.fontSize = 18;
            RefreshMotionLabel();

            // "下个 5★ 必定 UP" note (ten-pull only; single pull carries it in
            // the detail panel). Sits under the title; the grid shifts down
            // when it is shown so nothing overlaps.
            if (_anyOffBannerFive && _pulls.Length > 1)
            {
                var note = UiFactory.Label(panel.transform, "GuaranteeNote",
                    "本次出现非 UP 5★：下个 5★ 必定 UP", 18,
                    TextAlignmentOptions.Center, UiFactory.PaleGold);
                UiFactory.Place(note.rectTransform, 200, -48, 800, 24);
            }

            // Summon-preparation layer: a moon disc + soft glow + a few stars.
            // A short, quiet beat — not a full screen of particles.
            _prepLayer = new GameObject("PrepLayer");
            _prepLayer.transform.SetParent(panel.transform, false);
            var prepRt = _prepLayer.AddComponent<RectTransform>();
            prepRt.anchorMin = new Vector2(0.5f, 0.5f);
            prepRt.anchorMax = new Vector2(0.5f, 0.5f);
            prepRt.pivot = new Vector2(0.5f, 0.5f);
            prepRt.anchoredPosition = new Vector2(0, 40);
            prepRt.sizeDelta = new Vector2(500, 300);

            var moon = new GameObject("Moon");
            moon.transform.SetParent(_prepLayer.transform, false);
            var moonImg = moon.AddComponent<Image>();
            moonImg.sprite = UiFactory.MoonDiscSprite();
            moonImg.color = new Color(0.93f, 0.95f, 1f, 0.95f);
            var moonRt = moon.GetComponent<RectTransform>();
            moonRt.anchorMin = new Vector2(0.5f, 0.5f);
            moonRt.anchorMax = new Vector2(0.5f, 0.5f);
            moonRt.pivot = new Vector2(0.5f, 0.5f);
            moonRt.anchoredPosition = Vector2.zero;
            moonRt.sizeDelta = new Vector2(150, 150);

            var glow = new GameObject("Glow");
            glow.transform.SetParent(_prepLayer.transform, false);
            var glowImg = glow.AddComponent<Image>();
            glowImg.sprite = UiFactory.RadialGlowSprite();
            glowImg.color = new Color(0.47f, 0.59f, 0.86f, 0.35f);
            var glowRt = glow.GetComponent<RectTransform>();
            glowRt.anchorMin = new Vector2(0.5f, 0.5f);
            glowRt.anchorMax = new Vector2(0.5f, 0.5f);
            glowRt.pivot = new Vector2(0.5f, 0.5f);
            glowRt.anchoredPosition = Vector2.zero;
            glowRt.sizeDelta = new Vector2(420, 280);

            string[] starGlyphs = { "✦", "✧", "✦", "✧", "✦" };
            Vector2[] starPos = {
                new Vector2(-190, 90), new Vector2(-120, -70),
                new Vector2(140, 100), new Vector2(200, -60), new Vector2(0, 130)
            };
            for (int i = 0; i < starGlyphs.Length; i++)
            {
                var s = UiFactory.Label(_prepLayer.transform, "Star" + i, starGlyphs[i], 26,
                    TextAlignmentOptions.Center, UiFactory.MoonWhite);
                s.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                s.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                s.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                UiFactory.Place(s.rectTransform, starPos[i].x - 15, starPos[i].y - 15, 30, 30);
            }

            _prepText = UiFactory.Label(panel.transform, "PrepText", "星光正在汇聚……", 20,
                TextAlignmentOptions.Center, UiFactory.SilverWhite).gameObject;
            UiFactory.Place(_prepText.GetComponent<RectTransform>(), 300, -560, 600, 30);
            _prepLayer.SetActive(false);
            _prepText.SetActive(false);

            if (_pulls.Length == 0)
            {
                var empty = UiFactory.Label(panel.transform, "Empty", "本次没有抽到结果。", 22,
                    TextAlignmentOptions.Center, UiFactory.SilverWhite);
                UiFactory.Place(empty.rectTransform, 300, -280, 600, 40);
            }
            else if (_pulls.Length == 1)
            {
                BuildSingle(panel.transform);
            }
            else
            {
                BuildGrid(panel.transform);
            }

            BuildDetailPanel(panel.transform);
            BuildButtons(panel.transform);

            // Reveal: skipped entirely when reduced motion is on.
            if (_pulls.Length > 0 && !PullResultScreen.IsReduceMotionEnabled())
            {
                _revealing = true;
                _skipButton.SetActive(true);
                _revealRoutine = StartCoroutine(RevealRoutine());
            }
            else
            {
                RevealAllInstant();
            }
        }

        private void BuildSingle(Transform panel)
        {
            // One large card, centred in the panel. 420 x 440, art on top,
            // name/stars/badges below. Occupies y 70..510.
            var card = BuildCard(panel, "SingleCard", _pulls[0], 0,
                new Vector2(390, 70), new Vector2(420, 440), true);
            // Tapping the single card opens its detail too.
            var btn = card.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(() => ShowDetail(0));
        }

        private void BuildGrid(Transform panel)
        {
            // 5 columns x 2 rows for ten-pull (other counts flow into more rows).
            const int cols = 5;
            const float cw = 200f, ch = 200f, gapX = 16f, gapY = 14f;
            int rows = (_pulls.Length + cols - 1) / cols;
            float gridW = cols * cw + (cols - 1) * gapX;
            float startX = (1200f - gridW) / 2f;
            float startY = _anyOffBannerFive ? 82f : 64f;
            for (int i = 0; i < _pulls.Length; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float x = startX + col * (cw + gapX);
                float y = startY + row * (ch + gapY);
                var card = BuildCard(panel, "Card" + i, _pulls[i], i,
                    new Vector2(x, y), new Vector2(cw, ch), false);
                int index = i; // capture for the listener
                var btn = card.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(() => ShowDetail(index));
            }
            // Keep rows referenced so the layout math stays honest if a future
            // caller passes a non-10 count.
            _ = rows;
        }

        /// <summary>Builds one result card. Hidden (alpha 0) until the reveal shows it.</summary>
        private GameObject BuildCard(Transform panel, string name, PullOutcome pull, int index,
            Vector2 topLeft, Vector2 size, bool large)
        {
            var card = UiFactory.RoundedPanel(panel, name,
                new Vector2(0, 1), new Vector2(0, 1), size,
                PullResultScreen.CardColor(pull.rarity));
            var cRt = card.GetComponent<RectTransform>();
            cRt.pivot = new Vector2(0, 1);
            UiFactory.Place(cRt, topLeft.x, -topLeft.y, size.x, size.y);
            UiFactory.AddSilverBorder(cRt);

            // Rarity accent strip across the top of the card.
            var strip = UiFactory.Panel(card.transform, "Accent",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 5),
                PullResultScreen.AccentColor(pull.rarity));
            var sRt = strip.GetComponent<RectTransform>();
            sRt.pivot = new Vector2(0.5f, 1);
            sRt.anchoredPosition = Vector2.zero;

            // 5★ glow behind the card (soft radial glow, gold — restrained,
            // not a full-screen effect). Hidden until this card is revealed.
            GameObject glowObj = null;
            if (pull.rarity == 5)
            {
                glowObj = new GameObject("FiveStarGlow");
                glowObj.transform.SetParent(card.transform, false);
                var gImg = glowObj.AddComponent<Image>();
                gImg.sprite = UiFactory.RadialGlowSprite();
                gImg.color = new Color(0.85f, 0.80f, 0.67f, 0.30f);
                var gRt = glowObj.GetComponent<RectTransform>();
                gRt.anchorMin = new Vector2(0.5f, 0.5f);
                gRt.anchorMax = new Vector2(0.5f, 0.5f);
                gRt.pivot = new Vector2(0.5f, 0.5f);
                gRt.anchoredPosition = Vector2.zero;
                gRt.sizeDelta = new Vector2(size.x + 70, size.y + 70);
                glowObj.transform.SetAsFirstSibling();
                glowObj.SetActive(false);
                _cardGlows.Add(glowObj);
            }
            else
            {
                _cardGlows.Add(null);
            }

            // Art (full character/item art) or a text fallback for 3★ items
            // that have no art (301-304).
            float artH = large ? 322f : size.y - 96f;
            var sprite = PullResultScreen.LoadArt(ItemNames.ArtForItem(pull.itemId));
            if (sprite != null)
            {
                var art = UiFactory.ArtImage(card.transform, "Art", sprite);
                var aRt = art.GetComponent<RectTransform>();
                aRt.anchorMin = new Vector2(0, 1);
                aRt.anchorMax = new Vector2(1, 1);
                aRt.pivot = new Vector2(0.5f, 1);
                aRt.anchoredPosition = new Vector2(0, -8);
                aRt.sizeDelta = new Vector2(size.x - 16, artH);
            }
            else
            {
                var fb = UiFactory.Label(card.transform, "ArtFallback",
                    "★\n" + ItemNames.Get(pull.itemId), large ? 26 : 18,
                    TextAlignmentOptions.Center, UiFactory.MoonWhite);
                UiFactory.Place(fb.rectTransform, 8, -8, size.x - 16, artH);
            }

            // Name + rarity stars (below the art, no overlap with it).
            float nameY = large ? 336f : size.y - 62f;
            float starsY = large ? 372f : size.y - 36f;
            var nameLabel = UiFactory.Label(card.transform, "Name", ItemNames.Get(pull.itemId),
                large ? 26 : 18, TextAlignmentOptions.Center, UiFactory.MoonWhite, true);
            UiFactory.Place(nameLabel.rectTransform, 6, -nameY, size.x - 12, large ? 36 : 26);

            var stars = UiFactory.Label(card.transform, "Stars", ItemNames.Stars(pull.rarity),
                large ? 26 : 18, TextAlignmentOptions.Center,
                PullResultScreen.StarsColor(pull.rarity), true);
            UiFactory.Place(stars.rectTransform, 6, -starsY, size.x - 12, large ? 34 : 26);

            // UP badge (featured 5★) — clearly marked "UP".
            if (pull.isFeatured)
            {
                var upBg = UiFactory.RoundedPanel(card.transform, "UpBadgeBg",
                    new Vector2(1, 1), new Vector2(1, 1), new Vector2(large ? 64 : 48, large ? 34 : 26),
                    UiFactory.PaleGold);
                var bRt = upBg.GetComponent<RectTransform>();
                bRt.pivot = new Vector2(1, 1);
                bRt.anchoredPosition = new Vector2(-8, -12);
                var up = UiFactory.Label(upBg.transform, "UpBadge", "UP", large ? 22 : 16,
                    TextAlignmentOptions.Center, UiFactory.DeepUltramarine, true);
                UiFactory.Stretch(up.rectTransform);
            }
            else if (pull.rarity == 5)
            {
                // Non-UP 5★: standard-pool label (not an UP badge).
                var std = UiFactory.Label(card.transform, "StdBadge", "标准池", large ? 18 : 14,
                    TextAlignmentOptions.Center, UiFactory.SilverWhite);
                UiFactory.Place(std.rectTransform, size.x - (large ? 96 : 76), -12,
                    large ? 88 : 68, large ? 26 : 20);
            }

            // NEW badge vs 重复 label.
            bool isNew = PullResultScreen.IsNew(_newItemIds, pull.itemId);
            var tag = UiFactory.Label(card.transform, "NewTag", isNew ? "NEW" : "重复",
                large ? 20 : 15, TextAlignmentOptions.Center,
                isNew ? UiFactory.SakuraPink : UiFactory.SilverWhite, isNew);
            UiFactory.Place(tag.rectTransform, 8, -12, large ? 80 : 56, large ? 28 : 22);

            // Tap to expand the detail panel.
            var tap = card.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;

            // Hidden until revealed (or instantly shown when reduced motion is on).
            var cg = card.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
            _cardGroups.Add(cg);
            _cardRoots.Add(card);
            return card;
        }

        private void BuildDetailPanel(Transform panel)
        {
            // Inline detail strip, hidden until a card is tapped (or 查看详情).
            // y -508..-596; buttons sit below it at -612..-648.
            _detailPanel = UiFactory.RoundedPanel(panel, "DetailPanel",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(880, 88),
                UiFactory.Glass(0.92f));
            var dRt = _detailPanel.GetComponent<RectTransform>();
            dRt.pivot = new Vector2(0.5f, 1);
            dRt.anchoredPosition = new Vector2(600, -508);
            UiFactory.AddSilverBorder(dRt);

            _detailTitle = UiFactory.Label(_detailPanel.transform, "DetailTitle", "", 22,
                TextAlignmentOptions.Left, UiFactory.MoonWhite, true);
            UiFactory.Place(_detailTitle.rectTransform, 16, -6, 848, 30);

            _detailLines = UiFactory.Label(_detailPanel.transform, "DetailLines", "", 18,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            UiFactory.Place(_detailLines.rectTransform, 16, -36, 848, 26);

            _detailNote = UiFactory.Label(_detailPanel.transform, "DetailNote", "", 16,
                TextAlignmentOptions.Left, UiFactory.PaleGold);
            UiFactory.Place(_detailNote.rectTransform, 16, -62, 848, 22);

            _detailPanel.SetActive(false);
        }

        // Buttons are placed from the panel's top-left corner.
        // UiFactory.Button leaves a fresh RectTransform anchored at the
        // parent's CENTRE, and UiFactory.Place only sets anchoredPosition /
        // sizeDelta — so Place(btn, x, -612) put the buttons 612px below
        // the panel centre, off the bottom of the panel entirely (found
        // via the result screenshot, 2026-11-16: card + title rendered,
        // all four buttons missing). Anchor top-left first.
        private static void PlaceButton(Button btn, float x, float y, float w, float h)
        {
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            UiFactory.Place(rt, x, y, w, h);
        }

        private void BuildButtons(Transform panel)
        {
            // Bottom row, y -612..-648, centred as a group on x = 600.
            const float y = -612f;
            if (_pulls.Length == 1)
            {
                var detailBtn = UiFactory.Button(panel, "DetailButton", "查看详情",
                    () => ShowDetail(0), new Vector2(160, 36), UiFactory.GlassWhite(0.12f));
                PlaceButton(detailBtn, 220, y, 160, 36);

                var confirm = UiFactory.Button(panel, "ConfirmButton", "确认",
                    () => Close(), new Vector2(160, 36));
                PlaceButton(confirm, 420, y, 160, 36);

                var again = UiFactory.Button(panel, "PullAgainButton", "再次祈愿",
                    () => PullAgain(), new Vector2(160, 36));
                PlaceButton(again, 620, y, 160, 36);

                _skipButton = UiFactory.Button(panel, "SkipButton", "跳过演出",
                    () => SkipAll(), new Vector2(160, 36), UiFactory.GlassWhite(0.12f)).gameObject;
                PlaceButton(_skipButton.GetComponent<Button>(), 820, y, 160, 36);
            }
            else
            {
                var confirm = UiFactory.Button(panel, "ConfirmButton", "确认",
                    () => Close(), new Vector2(160, 36));
                PlaceButton(confirm, 300, y, 160, 36);

                var again = UiFactory.Button(panel, "PullAgainButton", "再次祈愿",
                    () => PullAgain(), new Vector2(160, 36));
                PlaceButton(again, 500, y, 160, 36);

                _skipButton = UiFactory.Button(panel, "SkipButton", "跳过演出",
                    () => SkipAll(), new Vector2(160, 36), UiFactory.GlassWhite(0.12f)).gameObject;
                PlaceButton(_skipButton.GetComponent<Button>(), 700, y, 160, 36);
            }
            // Skip is visible only during the reveal.
            _skipButton.SetActive(false);
        }

        // ---------------- Detail ----------------

        private void ShowDetail(int index)
        {
            if (_closed || index < 0 || index >= _pulls.Length) return;
            // Tapping the already-selected card toggles the panel closed.
            if (_selectedIndex == index && _detailPanel.activeSelf)
            {
                _detailPanel.SetActive(false);
                _selectedIndex = -1;
                return;
            }
            _selectedIndex = index;
            var p = _pulls[index];
            bool isNew = PullResultScreen.IsNew(_newItemIds, p.itemId);

            _detailTitle.text = ItemNames.Get(p.itemId) + "　" + ItemNames.Stars(p.rarity);
            _detailLines.text = "种类：" + PullResultScreen.KindName(p.kind)
                + "　　稀有度：" + p.rarity + "★"
                + "　　" + (p.isFeatured ? "UP（概率提升）" : "标准池")
                + "　　" + (isNew ? "NEW（首次获得）" : "重复");
            _detailNote.text = (p.rarity == 5 && !p.isFeatured)
                ? "非 UP 5★：下个 5★ 必定 UP"
                : "";
            _detailPanel.SetActive(true);
        }

        // ---------------- Reduced motion ----------------

        private void RefreshMotionLabel()
        {
            if (_motionLabel == null) return;
            _motionLabel.text = PullResultScreen.IsReduceMotionEnabled()
                ? "简化演出：开" : "简化演出：关";
        }

        private void ToggleReduceMotion()
        {
            bool on = !PullResultScreen.IsReduceMotionEnabled();
            PlayerPrefs.SetInt(PullResultScreen.ReduceMotionKey, on ? 1 : 0);
            PlayerPrefs.Save();
            RefreshMotionLabel();
            // Turning it on mid-reveal skips the rest of the reveal immediately.
            if (on && _revealing) SkipAll();
        }

        // ---------------- Reveal (presentation only) ----------------

        private IEnumerator RevealRoutine()
        {
            // Beat 1 — summon preparation: moon + glow + a few stars, ~0.6 s.
            _prepLayer.SetActive(true);
            if (_prepText != null) _prepText.SetActive(true);
            float t = 0f;
            while (t < PullResultScreen.PrepBeatSeconds)
            {
                if (_closed || _skipRequested) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (_closed) yield break;
            if (_skipRequested) { RevealAllInstant(); yield break; }
            _prepLayer.SetActive(false);
            if (_prepText != null) _prepText.SetActive(false);

            // Beat 2 — cards reveal one by one, in SERVER order (never re-sorted).
            // Fades overlap: the next card starts fading 0.32 s after the
            // previous one started, so the full ten-pull takes ~3.8 s.
            if (_pulls.Length == 1)
            {
                yield return StartCoroutine(FadeCardIn(0, PullResultScreen.SingleFadeSeconds));
                if (_closed) yield break;
                if (_skipRequested) { RevealAllInstant(); yield break; }
            }
            else
            {
                for (int i = 0; i < _cardGroups.Count; i++)
                {
                    if (_closed || _skipRequested) break;
                    var pull = _pulls[i];
                    if (pull.rarity == 5 && _cardGlows[i] != null)
                    {
                        _cardGlows[i].SetActive(true);
                        StartCoroutine(FiveStarFlourish(i));
                    }
                    StartCoroutine(FadeCardIn(i, PullResultScreen.CardFadeSeconds));
                    t = 0f;
                    while (t < PullResultScreen.CardIntervalSeconds)
                    {
                        if (_closed || _skipRequested) break;
                        t += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
                // Let the last card's fade finish before declaring the reveal done.
                t = 0f;
                while (t < PullResultScreen.CardFadeSeconds - PullResultScreen.CardIntervalSeconds)
                {
                    if (_closed || _skipRequested) break;
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            if (_closed) yield break;
            if (_skipRequested) { RevealAllInstant(); yield break; }
            FinishReveal();
        }

        private IEnumerator FadeCardIn(int index, float seconds)
        {
            var cg = _cardGroups[index];
            var rt = _cardRoots[index].GetComponent<RectTransform>();
            float t = 0f;
            Vector3 startScale = new Vector3(0.92f, 0.92f, 1f);
            while (t < seconds)
            {
                if (_closed || _skipRequested) break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / seconds);
                cg.alpha = k;
                rt.localScale = Vector3.Lerp(startScale, Vector3.one, k);
                yield return null;
            }
            if (!_closed)
            {
                cg.alpha = 1f;
                rt.localScale = Vector3.one;
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
        }

        /// <summary>5★ flourish: brief sakura bloom around the card — a few
        /// petals drifting down + the gold glow, ~0.9 s, then gone. Runs
        /// concurrently with the reveal; it adds no time to the total.</summary>
        private IEnumerator FiveStarFlourish(int index)
        {
            var card = _cardRoots[index];
            var petals = new List<RectTransform>();
            for (int i = 0; i < 8; i++)
            {
                var petal = UiFactory.Label(card.transform, "Petal" + index + "_" + i, "❀", 20,
                    TextAlignmentOptions.Center, UiFactory.SakuraPink);
                var pRt = petal.rectTransform;
                pRt.anchorMin = new Vector2(0.5f, 0.5f);
                pRt.anchorMax = new Vector2(0.5f, 0.5f);
                pRt.pivot = new Vector2(0.5f, 0.5f);
                float px = (i - 3.5f) * 26f + Random.Range(-8f, 8f);
                pRt.anchoredPosition = new Vector2(px, 70f);
                pRt.sizeDelta = new Vector2(24, 24);
                petals.Add(pRt);
            }
            float t = 0f;
            while (t < PullResultScreen.FiveStarFlourishSeconds)
            {
                if (_closed || _skipRequested) break;
                t += Time.unscaledDeltaTime;
                float k = t / PullResultScreen.FiveStarFlourishSeconds;
                for (int i = 0; i < petals.Count; i++)
                {
                    if (petals[i] == null) continue;
                    var p = petals[i].anchoredPosition;
                    p.y -= 90f * Time.unscaledDeltaTime;
                    p.x += Mathf.Sin((t * 3f) + i) * 12f * Time.unscaledDeltaTime;
                    petals[i].anchoredPosition = p;
                    var c = UiFactory.SakuraPink;
                    c.a = 1f - k;
                    petals[i].GetComponent<TextMeshProUGUI>().color = c;
                }
                yield return null;
            }
            foreach (var p in petals)
                if (p != null) Destroy(p.gameObject);
        }

        /// <summary>Shows every card immediately (skip / reduced motion /
        /// interrupted reveal). Content is never changed here — only visibility.</summary>
        private void RevealAllInstant()
        {
            if (_prepLayer != null) _prepLayer.SetActive(false);
            if (_prepText != null) _prepText.SetActive(false);
            for (int i = 0; i < _cardGroups.Count; i++)
            {
                var cg = _cardGroups[i];
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
                _cardRoots[i].GetComponent<RectTransform>().localScale = Vector3.one;
                if (_pulls[i].rarity == 5 && _cardGlows[i] != null)
                    _cardGlows[i].SetActive(true);
            }
            FinishReveal();
        }

        private void FinishReveal()
        {
            _revealing = false;
            _skipRequested = false;
            if (_skipButton != null) _skipButton.SetActive(false);
        }

        // ---------------- Skip / close ----------------

        /// <summary>Skip: immediately show all results (correct, server-given
        /// outcomes — nothing is re-decided). Callable from the 跳过演出 button
        /// or by tapping the overlay background during the reveal.</summary>
        public void SkipAll()
        {
            if (_closed || !_revealing) return;
            _skipRequested = true;
            if (_revealRoutine != null) StopCoroutine(_revealRoutine);
            _revealRoutine = null;
            RevealAllInstant();
        }

        private void Close()
        {
            if (_closed) return;
            _closed = true;
            if (_revealRoutine != null) StopCoroutine(_revealRoutine);
            _revealRoutine = null;
            var cb = _onClose;
            Destroy(gameObject);
            cb?.Invoke();
        }

        private void PullAgain()
        {
            if (_closed) return;
            _closed = true;
            if (_revealRoutine != null) StopCoroutine(_revealRoutine);
            _revealRoutine = null;
            var closeCb = _onClose;
            var againCb = _onPullAgain;
            Destroy(gameObject);
            closeCb?.Invoke();
            againCb?.Invoke();
        }
    }
}
