// GachaScreen — banner list, pity counters, 1-pull / 10-pull, results list.
// Banner data comes from GET /api/game/banners + GET /api/game/banner/{id}.
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GalgameOnline.Api;
using GalgameOnline.Core;
using GalgameOnline.UI;

namespace GalgameOnline.Screens
{
    public class GachaScreen : MonoBehaviour
    {
        private TextMeshProUGUI _walletLabel;
        private TextMeshProUGUI _bannerTitle;
        private TextMeshProUGUI _bannerDetail;
        private RectTransform _resultsContent;
        private BannerDto[] _banners;
        private BannerDto _selected;
        private bool _busy;

        private void Start()
        {
            var canvas = FindObjectOfType<Canvas>();
            var (root, _) = UiFactory.ScreenBase(canvas.transform, "抽卡",
                () => GameManager.Instance.GoTo(GameManager.SceneMainMenu));

            _walletLabel = UiFactory.Label(root.transform, "Wallet", "钱包加载中…", 20,
                TextAlignmentOptions.Left, new Color(1f, 0.85f, 0.4f, 1f));
            _walletLabel.rectTransform.anchorMin = new Vector2(0, 1);
            _walletLabel.rectTransform.anchorMax = new Vector2(0, 1);
            UiFactory.Place(_walletLabel.rectTransform, 20, -76, 800, 30);

            // Banner buttons row.
            var bannerRow = UiFactory.Panel(root.transform, "BannerRow",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 56), new Color(0, 0, 0, 0));
            var brRt = bannerRow.GetComponent<RectTransform>();
            brRt.pivot = new Vector2(0.5f, 1);
            brRt.anchoredPosition = new Vector2(0, -108);

            _bannerTitle = UiFactory.Label(root.transform, "BannerTitle", "", 24,
                TextAlignmentOptions.Left, Color.white, true);
            _bannerTitle.rectTransform.anchorMin = new Vector2(0, 1);
            _bannerTitle.rectTransform.anchorMax = new Vector2(0, 1);
            UiFactory.Place(_bannerTitle.rectTransform, 20, -178, 900, 36);

            _bannerDetail = UiFactory.Label(root.transform, "BannerDetail", "", 18,
                TextAlignmentOptions.Left, new Color(0.8f, 0.85f, 1f, 1f));
            _bannerDetail.rectTransform.anchorMin = new Vector2(0, 1);
            _bannerDetail.rectTransform.anchorMax = new Vector2(0, 1);
            UiFactory.Place(_bannerDetail.rectTransform, 20, -216, 900, 60);

            var pull1 = UiFactory.Button(root.transform, "Pull1", "单抽", () => DoPull(1), new Vector2(200, 52));
            var p1Rt = pull1.GetComponent<RectTransform>();
            p1Rt.anchorMin = new Vector2(0, 1); p1Rt.anchorMax = new Vector2(0, 1);
            p1Rt.anchoredPosition = new Vector2(120, -292);

            var pull10 = UiFactory.Button(root.transform, "Pull10", "十连", () => DoPull(10), new Vector2(200, 52));
            var p10Rt = pull10.GetComponent<RectTransform>();
            p10Rt.anchorMin = new Vector2(0, 1); p10Rt.anchorMax = new Vector2(0, 1);
            p10Rt.anchoredPosition = new Vector2(340, -292);

            var (scroll, content) = UiFactory.ScrollView(root.transform, "Results",
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0));
            var sRt = scroll.GetComponent<RectTransform>();
            sRt.offsetMin = new Vector2(16, 16);
            sRt.offsetMax = new Vector2(-16, -360);
            _resultsContent = content;

            var empty = UiFactory.Label(content, "Empty", "还没有抽卡结果。", 18,
                TextAlignmentOptions.Center, new Color(0.7f, 0.75f, 0.85f, 1f));
            empty.rectTransform.sizeDelta = new Vector2(0, 30);

            LoadBanners(bannerRow.transform);
            RefreshWallet();
        }

        private void LoadBanners(Transform row)
        {
            StartCoroutine(GameManager.Instance.Api.GetBanners(r =>
            {
                if (!r.Success)
                {
                    if (r.Failure == ApiFailureKind.Unauthorized) GameManager.Instance.HandleUnauthorized();
                    else Toast.Show(r.ErrorMessage);
                    return;
                }
                _banners = r.Data.banners ?? new BannerDto[0];
                float x = 20;
                foreach (var b in _banners)
                {
                    var banner = b;
                    var btn = UiFactory.Button(row, "Banner" + b.id, b.name, () => SelectBanner(banner),
                        new Vector2(220, 44));
                    var rt = btn.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0, 0.5f);
                    rt.anchorMax = new Vector2(0, 0.5f);
                    rt.pivot = new Vector2(0, 0.5f);
                    rt.anchoredPosition = new Vector2(x, 0);
                    x += 232;
                }
                if (_banners.Length > 0) SelectBanner(_banners[0]);
                else _bannerTitle.text = "暂无卡池。";
            }));
        }

        private void SelectBanner(BannerDto b)
        {
            _selected = b;
            _bannerTitle.text = b.name + "　（单抽 " + b.costPerPull + " 货币）";
            _bannerDetail.text = "加载保底信息…";
            StartCoroutine(GameManager.Instance.Api.GetBannerDetail(b.id, r =>
            {
                if (!r.Success) { _bannerDetail.text = "保底信息加载失败。"; return; }
                var info = r.Data;
                string featured = ItemNames.Get(b.featuredFiveStar);
                _bannerDetail.text = $"UP 五星：{featured}　·　已抽 {info.totalCount}　·　" +
                    $"距五星保底 {info.fiveRemain} 抽　·　距四星保底 {info.fourRemain} 抽" +
                    (b.dailyLimit > 0 ? $"　·　今日剩余 {info.dailyLeft} 抽" : "");
            }));
        }

        private void RefreshWallet()
        {
            GameManager.Instance.RefreshWallet(() =>
            {
                var w = GameManager.Instance.Wallet;
                _walletLabel.text = w == null ? "钱包加载失败"
                    : $"高级货币 {w.premium} · 免费货币 {w.free} · 抽卡券 {w.tickets}";
            });
        }

        private void DoPull(int count)
        {
            if (_busy || _selected == null) return;
            _busy = true;
            Toast.Show(count == 1 ? "抽卡中…" : "十连抽卡中…");
            StartCoroutine(GameManager.Instance.Api.Pull(_selected.id, count, r =>
            {
                _busy = false;
                if (!r.Success)
                {
                    if (r.Failure == ApiFailureKind.Unauthorized) GameManager.Instance.HandleUnauthorized();
                    else Toast.Show(r.ErrorMessage);
                    return;
                }
                if (!r.Data.ok)
                {
                    Toast.Show(r.Data.error ?? "抽卡失败。");
                    return;
                }
                UiFactory.ClearChildren(_resultsContent);
                foreach (var p in r.Data.pulls)
                {
                    string line = ItemNames.Stars(p.rarity) + "　" + ItemNames.Get(p.itemId) +
                        (p.isFeatured ? "　（UP）" : "");
                    var l = UiFactory.Label(_resultsContent, "Result", line, 20,
                        TextAlignmentOptions.Left, p.rarity >= 5
                            ? new Color(1f, 0.8f, 0.3f, 1f)
                            : p.rarity == 4 ? new Color(0.7f, 0.6f, 1f, 1f) : Color.white);
                    l.rectTransform.sizeDelta = new Vector2(0, 30);
                }
                if (r.Data.banner != null)
                {
                    var info = r.Data.banner;
                    _bannerDetail.text = $"UP 五星：{ItemNames.Get(_selected.featuredFiveStar)}　·　已抽 {info.totalCount}　·　" +
                        $"距五星保底 {info.fiveRemain} 抽　·　距四星保底 {info.fourRemain} 抽";
                }
                RefreshWallet();
            }));
        }
    }
}
