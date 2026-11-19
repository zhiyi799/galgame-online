using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace AstralBloom.Gacha
{
    public class GachaUI : MonoBehaviour
    {
        public GachaManager manager;

        private Font font;

        private Text currencyText;
        private Text pityText;
        private Text titleText;
        private Text subtitleText;
        private Text statusText;

        private Image bannerImage;

        private GameObject overlay;

        private Button singleButton;
        private Button tenButton;

        private readonly Color background =
            new Color(0.025f, 0.055f, 0.09f);

        private readonly Color panelColor =
            new Color(0.055f, 0.11f, 0.16f);

        private readonly Color cyan =
            new Color(0.25f, 0.85f, 0.95f);

        private readonly Color gold =
            new Color(1f, 0.76f, 0.34f);

        private void Start()
        {
            if (manager == null)
                manager = FindObjectOfType<GachaManager>();

            if (manager == null)
            {
                Debug.LogError("找不到 GachaManager");
                return;
            }

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            EnsureEventSystem();

            BuildInterface();

            Refresh();

            manager.OnDataChanged += Refresh;
        }

        private void OnDestroy()
        {
            if (manager != null)
                manager.OnDataChanged -= Refresh;
        }

        private void BuildInterface()
        {
            GameObject canvasObject = new GameObject(
                "ASTRAL BLOOM Gacha Canvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );

            canvasObject.transform.SetParent(transform, false);

            Canvas canvas =
                canvasObject.GetComponent<Canvas>();

            canvas.renderMode =
                RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler =
                canvasObject.GetComponent<CanvasScaler>();

            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;

            scaler.referenceResolution =
                new Vector2(1920, 1080);

            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = CreatePanel(
                canvasObject.transform,
                "Background",
                background,
                Vector2.zero,
                Vector2.one
            );

            CreateText(
                root,
                "ASTRAL BLOOM / SUMMON ARCHIVE",
                24,
                cyan,
                TextAnchor.MiddleLeft,
                new Vector2(0.045f, 0.91f),
                new Vector2(0.7f, 0.98f)
            );

            currencyText = CreateText(
                root,
                "",
                24,
                Color.white,
                TextAnchor.MiddleRight,
                new Vector2(0.72f, 0.91f),
                new Vector2(0.95f, 0.98f)
            );

            BuildBanner(root);

            BuildControls(root);

            CreateText(
                root,
                "ASTRAL BLOOM · STELLAR RECRUITMENT",
                15,
                new Color(0.4f, 0.58f, 0.68f),
                TextAnchor.MiddleLeft,
                new Vector2(0.045f, 0.025f),
                new Vector2(0.7f, 0.08f)
            );
        }

        private void BuildBanner(Transform root)
        {
            RectTransform bannerPanel = CreatePanel(
                root,
                "BannerPanel",
                panelColor,
                new Vector2(0.045f, 0.18f),
                new Vector2(0.66f, 0.87f)
            );

            titleText = CreateText(
                bannerPanel,
                "星轨回响",
                46,
                Color.white,
                TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.78f),
                new Vector2(0.92f, 0.96f)
            );

            subtitleText = CreateText(
                bannerPanel,
                "",
                21,
                new Color(0.7f, 0.85f, 0.93f),
                TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.63f),
                new Vector2(0.9f, 0.79f)
            );

            bannerImage = CreateImage(
                bannerPanel,
                "FeaturedArtwork",
                new Color(0.12f, 0.25f, 0.34f),
                new Vector2(0.35f, 0.08f),
                new Vector2(0.96f, 0.70f)
            );

            CreateText(
                bannerPanel,
                "LIMITED CHARACTER EVENT",
                16,
                cyan,
                TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.51f),
                new Vector2(0.55f, 0.60f)
            );
        }

        private void BuildControls(Transform root)
        {
            RectTransform controlPanel = CreatePanel(
                root,
                "ControlPanel",
                panelColor,
                new Vector2(0.68f, 0.18f),
                new Vector2(0.955f, 0.87f)
            );

            CreateText(
                controlPanel,
                "召唤终端",
                32,
                Color.white,
                TextAnchor.MiddleLeft,
                new Vector2(0.08f, 0.84f),
                new Vector2(0.9f, 0.96f)
            );

            pityText = CreateText(
                controlPanel,
                "",
                19,
                new Color(0.7f, 0.83f, 0.9f),
                TextAnchor.MiddleLeft,
                new Vector2(0.08f, 0.66f),
                new Vector2(0.92f, 0.81f)
            );

            statusText = CreateText(
                controlPanel,
                "选择召唤次数",
                17,
                cyan,
                TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.55f),
                new Vector2(0.92f, 0.65f)
            );

            singleButton = CreateButton(
                controlPanel,
                "召唤 ×1",
                new Vector2(0.08f, 0.37f),
                new Vector2(0.92f, 0.51f),
                cyan,
                () => Pull(1)
            );

            tenButton = CreateButton(
                controlPanel,
                "召唤 ×10",
                new Vector2(0.08f, 0.19f),
                new Vector2(0.92f, 0.33f),
                gold,
                () => Pull(10)
            );

            CreateButton(
                controlPanel,
                "召唤记录",
                new Vector2(0.08f, 0.045f),
                new Vector2(0.92f, 0.15f),
                new Color(0.16f, 0.28f, 0.39f),
                ShowHistory
            );
        }

        private void Refresh()
        {
            if (manager == null || manager.banner == null)
                return;

            var save = manager.Save;
            var config = manager.banner;

            currencyText.text =
                "✦ " + save.currency.ToString("N0");

            titleText.text = config.bannerTitle;

            subtitleText.text =
                config.subtitle + "\n" +
                config.featuredName;

            pityText.text =
                "距五星保底：" +
                Mathf.Max(
                    0,
                    config.fiveStarHardPity -
                    save.pityFiveStar
                ) +
                "\n累计召唤：" +
                save.totalPulls;

            bannerImage.sprite = config.bannerArtwork;

            bannerImage.preserveAspect = true;

            bannerImage.color =
                config.bannerArtwork == null
                ? new Color(0.12f, 0.25f, 0.34f)
                : Color.white;

            singleButton.interactable =
                manager.CanPull(1);

            tenButton.interactable =
                manager.CanPull(10);
        }

        private void Pull(int count)
        {
            if (!manager.CanPull(count))
            {
                statusText.text = "星辉不足";
                return;
            }

            List<GachaPullResult> results =
                manager.Pull(count);

            if (results.Count == 0)
                return;

            StartCoroutine(ShowResults(results));
        }

        private IEnumerator ShowResults(
            List<GachaPullResult> results)
        {
            statusText.text = "正在连接星轨……";

            yield return new WaitForSeconds(0.6f);

            ShowResultPanel(results);

            bool hasFiveStar =
                results.Exists(
                    x => x.character.rarity ==
                    Rarity.FiveStar
                );

            statusText.text = hasFiveStar
                ? "检测到五星响应"
                : "召唤完成";
        }

        private void ShowResultPanel(
            List<GachaPullResult> results)
        {
            CloseOverlay();

            overlay = new GameObject(
                "SummonResults",
                typeof(Image)
            );

            overlay.transform.SetParent(
                transform.GetChild(0),
                false
            );

            RectTransform rt =
                overlay.GetComponent<RectTransform>();

            rt.anchorMin = new Vector2(0.06f, 0.07f);
            rt.anchorMax = new Vector2(0.94f, 0.9f);

            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            overlay.GetComponent<Image>().color =
                new Color(0.012f, 0.03f, 0.055f, 0.98f);

            CreateText(
                rt,
                "SUMMON RESULT / 召唤结果",
                32,
                cyan,
                TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.87f),
                new Vector2(0.95f, 0.98f)
            );

            int columns = results.Count == 1 ? 1 : 5;

            float gap = 0.015f;

            float width =
                (0.9f - gap * (columns - 1)) /
                columns;

            for (int i = 0; i < results.Count; i++)
            {
                int row = i / columns;
                int column = i % columns;

                float x = 0.05f +
                    column * (width + gap);

                float top = 0.82f - row * 0.39f;
                float bottom = top - 0.34f;

                CreateCharacterCard(
                    rt,
                    results[i],
                    new Vector2(x, bottom),
                    new Vector2(x + width, top)
                );
            }

            CreateButton(
                rt,
                "返回卡池",
                new Vector2(0.38f, 0.025f),
                new Vector2(0.62f, 0.12f),
                new Color(0.12f, 0.36f, 0.48f),
                CloseOverlay
            );
        }

        private void CreateCharacterCard(
            Transform parent,
            GachaPullResult result,
            Vector2 min,
            Vector2 max)
        {
            Color rarityColor =
                result.character.rarity ==
                Rarity.FiveStar ? gold :
                result.character.rarity ==
                Rarity.FourStar ? cyan :
                new Color(0.6f, 0.7f, 0.8f);

            RectTransform card = CreatePanel(
                parent,
                "CharacterCard",
                rarityColor * 0.22f,
                min,
                max
            );

            Image portrait = CreateImage(
                card,
                "Portrait",
                new Color(0.1f, 0.18f, 0.24f),
                new Vector2(0.07f, 0.21f),
                new Vector2(0.93f, 0.87f)
            );

            portrait.sprite = result.character.portrait;
            portrait.preserveAspect = true;

            if (portrait.sprite != null)
                portrait.color = Color.white;

            CreateText(
                card,
                new string(
                    '★',
                    (int)result.character.rarity
                ),
                19,
                rarityColor,
                TextAnchor.MiddleCenter,
                new Vector2(0.04f, 0.86f),
                new Vector2(0.96f, 0.99f)
            );

            CreateText(
                card,
                result.character.displayName,
                18,
                Color.white,
                TextAnchor.MiddleCenter,
                new Vector2(0.03f, 0.02f),
                new Vector2(0.97f, 0.22f)
            );

            if (result.isRateUp)
            {
                CreateText(
                    card,
                    "UP",
                    15,
                    gold,
                    TextAnchor.UpperRight,
                    new Vector2(0.68f, 0.76f),
                    new Vector2(0.97f, 0.96f)
                );
            }
        }

        private void ShowHistory()
        {
            CloseOverlay();

            overlay = new GameObject(
                "SummonHistory",
                typeof(Image)
            );

            overlay.transform.SetParent(
                transform.GetChild(0),
                false
            );

            RectTransform rt =
                overlay.GetComponent<RectTransform>();

            rt.anchorMin = new Vector2(0.12f, 0.1f);
            rt.anchorMax = new Vector2(0.88f, 0.9f);

            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            overlay.GetComponent<Image>().color =
                new Color(0.012f, 0.03f, 0.055f, 0.98f);

            CreateText(
                rt,
                "SUMMON HISTORY / 召唤记录",
                30,
                cyan,
                TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.89f),
                new Vector2(0.95f, 0.98f)
            );

            int count = Mathf.Min(
                12,
                manager.Save.history.Count
            );

            for (int i = 0; i < count; i++)
            {
                GachaPullRecord record =
                    manager.Save.history[i];

                Color color =
                    record.rarity == 5 ? gold :
                    record.rarity == 4 ? cyan :
                    Color.white;

                CreateText(
                    rt,
                    record.time +
                    "    " +
                    new string('★', record.rarity) +
                    "    " +
                    record.characterName +
                    (record.wasRateUp ? "    UP" : ""),
                    17,
                    color,
                    TextAnchor.MiddleLeft,
                    new Vector2(0.07f, 0.82f - i * 0.06f),
                    new Vector2(0.93f, 0.88f - i * 0.06f)
                );
            }

            CreateButton(
                rt,
                "关闭",
                new Vector2(0.4f, 0.025f),
                new Vector2(0.6f, 0.11f),
                cyan,
                CloseOverlay
            );
        }

        private void CloseOverlay()
        {
            if (overlay != null)
                Destroy(overlay);

            overlay = null;
        }

        private RectTransform CreatePanel(
            Transform parent,
            string name,
            Color color,
            Vector2 min,
            Vector2 max)
        {
            GameObject go = new GameObject(
                name,
                typeof(Image)
            );

            go.transform.SetParent(parent, false);

            RectTransform rt =
                go.GetComponent<RectTransform>();

            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            go.GetComponent<Image>().color = color;

            return rt;
        }

        private Image CreateImage(
            Transform parent,
            string name,
            Color color,
            Vector2 min,
            Vector2 max)
        {
            GameObject go = new GameObject(
                name,
                typeof(Image)
            );

            go.transform.SetParent(parent, false);

            RectTransform rt =
                go.GetComponent<RectTransform>();

            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image image = go.GetComponent<Image>();
            image.color = color;

            return image;
        }

        private Text CreateText(
            Transform parent,
            string value,
            int size,
            Color color,
            TextAnchor alignment,
            Vector2 min,
            Vector2 max)
        {
            GameObject go = new GameObject(
                "Text",
                typeof(Text)
            );

            go.transform.SetParent(parent, false);

            RectTransform rt =
                go.GetComponent<RectTransform>();

            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = new Vector2(4, 2);
            rt.offsetMax = new Vector2(-4, -2);

            Text text = go.GetComponent<Text>();

            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;

            text.horizontalOverflow =
                HorizontalWrapMode.Wrap;

            text.verticalOverflow =
                VerticalWrapMode.Overflow;

            return text;
        }

        private Button CreateButton(
            Transform parent,
            string label,
            Vector2 min,
            Vector2 max,
            Color color,
            UnityEngine.Events.UnityAction action)
        {
            GameObject go = new GameObject(
                label,
                typeof(Image),
                typeof(Button)
            );

            go.transform.SetParent(parent, false);

            RectTransform rt =
                go.GetComponent<RectTransform>();

            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image image = go.GetComponent<Image>();
            image.color = color;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;

            button.onClick.AddListener(action);

            Text text = CreateText(
                go.transform,
                label,
                22,
                background,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one
            );

            text.fontStyle = FontStyle.Bold;

            return button;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;

            new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule)
            );
        }
    }
}
