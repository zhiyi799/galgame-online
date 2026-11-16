// BannerDetailsScreen — banner details overlay (Gacha scene).
//
// Pure display overlay: shows one banner's probabilities, pools, pity
// rules and live pity progress. All rates / pity limits / pool tables
// come from GalgameOnline.Api.GachaRules (copied from the server:
// cloud/workers/src/gacha.ts and 0004_seed.sql — there is no API that
// returns pools or rates, so the client config is the source of truth
// for display; the server is always authoritative for pull outcomes).
// Item names come from ItemNames; character art uses the single shared
// table ItemNames.ArtForItem (Resources/Characters).
// Things the server does NOT configure are shown as "未配置", never
// invented: banner end time, 4-star UP, duplicate compensation,
// first-pull / NEW marking, ticket pulls, pity transfer on banner end.
//
// Self-contained: builds its own full-screen dimmed overlay under the
// parent Transform passed in and destroys it on close. Does NOT depend
// on GachaScreen and does NOT use FindObjectOfType<Canvas>().
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GalgameOnline.Api;
using GalgameOnline.UI;

namespace GalgameOnline.Screens
{
    public static class BannerDetailsScreen
    {
        /// <summary>
        /// Builds the banner-details overlay as a child of <paramref name="parent"/>
        /// and returns its root GameObject. <paramref name="info"/> may be null
        /// (then only the static rules are shown, without live pity progress).
        /// Closing (× or 返回) destroys the root and invokes
        /// <paramref name="onClose"/> once. Returns null if parent is null.
        /// </summary>
        public static GameObject Show(Transform parent, BannerDto banner, BannerInfo info, Action onClose)
        {
            if (parent == null) return null;

            string bannerName = banner != null && !string.IsNullOrEmpty(banner.name)
                ? banner.name : "未知卡池";
            int bannerId = banner != null ? banner.id : 0;

            // Featured 5-star: prefer the server-sent value, fall back to the
            // client pool table. -1 / 0 means "no featured character configured".
            int featured = banner != null && banner.featuredFiveStar > 0
                ? banner.featuredFiveStar
                : GachaRules.GetFeaturedFiveStar(bannerId);
            int[] standardFive = GachaRules.GetStandardFiveStarPool(bannerId);
            int[] fourStar = GachaRules.GetFourStarPool(bannerId);
            int[] threeStar = GachaRules.GetThreeStarPool(bannerId);
            bool knownBanner = featured > 0
                || standardFive.Length > 0 || fourStar.Length > 0 || threeStar.Length > 0;

            // ---- Root: full-screen dimmed overlay ----
            var root = UiFactory.Panel(parent, "BannerDetailsOverlay",
                Vector2.zero, Vector2.one, Vector2.zero,
                new Color(0.02f, 0.03f, 0.08f, 0.78f));

            bool closed = false;
            Action close = null;
            close = () =>
            {
                if (closed) return;
                closed = true;
                UnityEngine.Object.Destroy(root);
                onClose?.Invoke();
            };

            // ---- Centered panel ----
            var panel = UiFactory.RoundedPanel(root.transform, "Panel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(960, 640), UiFactory.Glass(0.96f));
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchoredPosition = Vector2.zero;
            UiFactory.AddSilverBorder(panelRt);

            // ---- Title bar ----
            var titleBar = UiFactory.Panel(panel.transform, "TitleBar",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 56),
                UiFactory.Glass(0.92f));
            var titleRt = titleBar.GetComponent<RectTransform>();
            titleRt.pivot = new Vector2(0.5f, 1);
            titleRt.anchoredPosition = Vector2.zero;

            var title = UiFactory.Label(titleBar.transform, "Title",
                bannerName + " · 卡池详情", 24,
                TextAlignmentOptions.Left, UiFactory.MoonWhite, true);
            UiFactory.Place(title.rectTransform, 16, -10, 760, 40);

            var closeBtn = UiFactory.Button(titleBar.transform, "CloseButton", "×",
                new UnityEngine.Events.UnityAction(close), new Vector2(48, 40));
            var closeRt = closeBtn.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1, 0.5f);
            closeRt.anchorMax = new Vector2(1, 0.5f);
            closeRt.pivot = new Vector2(1, 0.5f);
            closeRt.anchoredPosition = new Vector2(-12, 0);

            // ---- Bottom bar with 返回 ----
            var bottomBar = UiFactory.Panel(panel.transform, "BottomBar",
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 64),
                UiFactory.Glass(0.92f));
            var bottomRt = bottomBar.GetComponent<RectTransform>();
            bottomRt.pivot = new Vector2(0.5f, 0);
            bottomRt.anchoredPosition = Vector2.zero;

            var backBtn = UiFactory.Button(bottomBar.transform, "BackButton", "返回",
                new UnityEngine.Events.UnityAction(close), new Vector2(140, 44));
            var backRt = backBtn.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0.5f, 0.5f);
            backRt.anchorMax = new Vector2(0.5f, 0.5f);
            backRt.pivot = new Vector2(0.5f, 0.5f);
            backRt.anchoredPosition = Vector2.zero;

            // ---- Scrollable content ----
            var (scroll, content) = UiFactory.ScrollView(panel.transform, "Content",
                new Vector2(0, 0), new Vector2(1, 1), Vector2.zero);
            var scrollRt = scroll.GetComponent<RectTransform>();
            scrollRt.offsetMin = new Vector2(12, 76);
            scrollRt.offsetMax = new Vector2(-12, -68);

            if (!knownBanner)
            {
                AddBody(content,
                    "该卡池尚未开启或暂无服务端配置，以下仅展示通用规则，未配置的内容标注为“未配置”。",
                    20, UiFactory.SakuraPink);
            }

            // 1. Banner name + type
            AddHeading(content, "卡池");
            AddBody(content, "名称：" + bannerName + "\n类型："
                + (bannerId == 1 || bannerId == 2 ? "限定角色祈愿" : "未配置"));

            // 2. Open / end time (the backend has no end date — never invent one)
            AddHeading(content, "开放时间");
            AddBody(content, "开放时间：长期开放\n结束时间：未配置");

            // 3. Probabilities (base rates, computed from GachaRules — not hardcoded)
            AddHeading(content, "概率");
            double fivePct = GachaRules.FiveStarBaseRate;
            double fourPct = GachaRules.FourStarBaseRate;
            double threePct = 1.0 - fivePct - fourPct;
            AddBody(content,
                "5★：" + Pct(fivePct) + "　　4★：" + Pct(fourPct) + "　　3★：" + Pct(threePct)
                + "\n以上为基础概率。5★ 概率在软保底区间内会随抽数提升（见下方保底规则）。"
                + "\n抽卡费用：单抽 " + GachaRules.CostPerPull
                + "（先扣高级货币，再扣免费货币）／十连 " + GachaRules.TenPullCost
                + "；抽卡券不参与扣费。");

            // 4. UP character + standard 5-star pool (explicitly NON-UP)
            AddHeading(content, "概率提升（UP）");
            if (featured > 0)
            {
                AddArt(content, LoadArt(ItemNames.ArtForItem(featured)));
                AddBody(content,
                    "UP 角色：★★★★★ " + ItemNames.Get(featured)
                    + "\n抽到 5★ 时 " + Pct(GachaRules.FeaturedShare) + " 为 UP"
                    + "（综合概率 " + Pct(fivePct * GachaRules.FeaturedShare) + "）。"
                    + "若本次 5★ 未出 UP，下一个 5★ 必定为 UP。",
                    20, UiFactory.PaleGold);
            }
            else
            {
                AddBody(content, "UP 角色：未配置");
            }
            AddBody(content,
                "标准 5★ 池（非 UP，无概率提升）："
                + (standardFive.Length > 0 ? PoolText(standardFive) : "未配置")
                + "\n注意：标准池角色在歪出时三者均分，没有任何概率提升；不要把它们当作 UP。");
            AddBody(content, "4★ UP：未配置（服务端无 4★ UP 机制）。");

            // 5. 4-star and 3-star pools (per banner, from GachaRules)
            AddHeading(content, "卡池构成");
            AddBody(content, "★★★★：" + (fourStar.Length > 0 ? PoolText(fourStar) : "未配置"));
            AddBody(content,
                "★★★：" + (threeStar.Length > 0 ? PoolText(threeStar) : "未配置")
                + "\n5★ / 4★ 均为角色，3★ 均为道具；本游戏没有武器池。");

            // 6. Pity rules (from GachaRules — not hardcoded)
            AddHeading(content, "保底规则");
            AddBody(content,
                "5★ 保底：自上次 5★ 起第 " + GachaRules.SoftPityStart
                + " 抽进入软保底，5★ 概率随抽数线性提升，第 89 抽时为 100%；"
                + "第 " + GachaRules.FiveStarHardPity + " 抽必出 5★（硬保底）。"
                + "\n4★ 保底：第 " + GachaRules.FourStarHardPity
                + " 抽必出 4★ 或以上；任意连续 10 抽至少获得一个 4★ 或以上。"
                + "\nUP 保底：自上次 UP 5★ 起 " + GachaRules.FeaturedGuaranteePulls
                + " 抽内必出 UP（120 抽内必出 UP）。"
                + "注意：这不是“每 120 抽必出 5★”——5★ 本身由 90 抽硬保底保证，"
                + "120 抽锁死的是两次 UP 之间的最长间隔。");

            // 7. Pity inheritance
            AddHeading(content, "保底继承");
            AddBody(content,
                "保底按（账号，卡池）分别计算，不同卡池之间不共享。"
                + "保底按卡池 ID 永久保存；卡池结束后的保底转移：未配置（无此概念）。");

            // 8. Duplicate handling
            AddHeading(content, "重复获得");
            AddBody(content, "未配置 — 重复获得仅增加背包数量，无额外补偿");

            // 9. Current pity progress (live data only; omitted when info is null)
            AddHeading(content, "当前保底进度");
            if (info != null)
            {
                string dailyText;
                if (banner != null && banner.dailyLimit == 0)
                {
                    // dailyLimit 0 = unlimited; the server returns dailyLeft 0
                    // for such banners, which must NOT be shown as "剩余 0 次".
                    dailyText = "每日上限：不限";
                }
                else if (banner != null)
                {
                    dailyText = "每日上限：" + banner.dailyLimit + " 次／日"
                        + "（今日剩余 " + info.dailyLeft + " 次）";
                }
                else
                {
                    dailyText = "每日上限：未配置";
                }

                AddBody(content,
                    "累计抽取：" + info.totalCount + " 抽"
                    + "\n距 5★ 硬保底：还需 " + info.fiveRemain + " 抽"
                    + "\n距 4★ 硬保底：还需 " + info.fourRemain + " 抽"
                    + "\n" + dailyText);

                // Derived "距 5★ 软保底": honest only when fiveRemain is in its
                // valid range, because sinceFive = hard pity - fiveRemain.
                if (info.fiveRemain > 0 && info.fiveRemain <= GachaRules.FiveStarHardPity)
                {
                    int sinceFive = GachaRules.FiveStarHardPity - info.fiveRemain;
                    string softText;
                    if (sinceFive < GachaRules.SoftPityStart)
                    {
                        softText = "距 5★ 软保底：还需 "
                            + (GachaRules.SoftPityStart - sinceFive) + " 抽"
                            + "（自上次 5★ 已抽 " + sinceFive + " 抽）";
                    }
                    else
                    {
                        softText = "已进入 5★ 软保底区间（自上次 5★ 已抽 " + sinceFive
                            + " 抽，5★ 概率随抽数提升）";
                    }
                    AddBody(content, softText);
                }
            }
            else
            {
                AddBody(content, "暂无实时数据（未获取到卡池详情），仅展示上方静态规则。",
                    20, UiFactory.SilverWhite);
            }

            // Other unconfigured items (server has no such config — never invent)
            AddHeading(content, "其他未配置事项");
            AddBody(content,
                "首抽 / NEW 标记：未配置\n抽卡券抵扣：未配置（抽卡不消耗抽卡券）\n保底转移：未配置");

            // 10. Footer note
            AddBody(content, "概率与规则来自服务端配置；卡池构成无服务端接口，由客户端配置提供。",
                18, UiFactory.SilverWhite);

            return root;
        }

        // ---- UI helpers (match the existing screens' UiFactory patterns) ----

        private static void AddHeading(RectTransform content, string text)
        {
            var h = UiFactory.Label(content, "Heading", "—— " + text + " ——", 22,
                TextAlignmentOptions.Left, UiFactory.MoonlightBlue, true);
            SetHeight(h, "—— " + text + " ——", 22, 34);
        }

        private static void AddBody(RectTransform content, string text, int fontSize, Color color)
        {
            var l = UiFactory.Label(content, "Body", text, fontSize,
                TextAlignmentOptions.Left, color);
            l.enableWordWrapping = true;
            SetHeight(l, text, fontSize, fontSize + 8);
        }

        private static void AddBody(RectTransform content, string text)
        {
            AddBody(content, text, 20, UiFactory.MoonWhite);
        }

        /// <summary>
        /// Sizes a scroll-content label so wrapped text is not clipped.
        /// The scroll content is ~920px wide; at font 20 a line holds ~40
        /// CJK characters. LayoutElement preferredHeight is what the
        /// VerticalLayoutGroup (childControlHeight = false) actually uses.
        /// </summary>
        private static void SetHeight(TextMeshProUGUI label, string text, int fontSize, int lineHeight)
        {
            int charsPerLine = Mathf.Max(20, 920 / Mathf.Max(10, fontSize));
            int lines = 0;
            foreach (string part in text.Split('\n'))
            {
                int len = part.Length;
                lines += Mathf.Max(1, (len + charsPerLine - 1) / charsPerLine);
            }
            float h = lines * lineHeight + 6;
            label.rectTransform.sizeDelta = new Vector2(0, h);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = h;
        }

        private static void AddArt(RectTransform content, Sprite sprite)
        {
            if (sprite == null) return;
            var img = UiFactory.ArtImage(content, "FeaturedArt", sprite, true);
            if (img == null) return;
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 230f;
            img.rectTransform.sizeDelta = new Vector2(0, 230f);
        }

        private static string PoolText(int[] ids)
        {
            var parts = new List<string>();
            foreach (int id in ids) parts.Add(ItemNames.Get(id));
            return string.Join("、", parts.ToArray());
        }

        private static string Pct(double rate)
        {
            return (rate * 100.0).ToString("0.#") + "%";
        }

        // Item -> Resources/Characters art name comes from the single
        // shared table ItemNames.ArtForItem; 3-star items (301-304) have
        // no art.

        private static Sprite LoadArt(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName)) return null;
            var tex = Resources.Load<Texture2D>("Characters/" + resourceName);
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f));
        }
    }
}
