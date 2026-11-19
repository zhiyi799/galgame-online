using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GachaBannerSelector : MonoBehaviour
{
    [Header("抽卡管理器")]
    public GachaManager manager;

    [Header("可用卡池")]
    public List<GachaBannerConfig> banners =
        new List<GachaBannerConfig>();

    [Header("卡池按钮区域")]
    public Transform buttonRoot;
    public Button buttonPrefab;

    [Header("卡池展示")]
    public Image bannerArtwork;
    public Text bannerTitle;
    public Text pityText;
    public Text guaranteeText;

    private readonly List<Button> generatedButtons =
        new List<Button>();

    private void Start()
    {
        BuildButtons();

        if (banners.Count > 0)
            SelectBanner(0);
    }

    public void BuildButtons()
    {
        ClearButtons();

        if (buttonRoot == null || buttonPrefab == null)
            return;

        for (int i = 0; i < banners.Count; i++)
        {
            GachaBannerConfig config = banners[i];

            if (config == null || !config.isEnabled)
                continue;

            int index = i;

            Button button = Instantiate(
                buttonPrefab,
                buttonRoot
            );

            generatedButtons.Add(button);

            Text label = button.GetComponentInChildren<Text>();

            if (label != null)
                label.text = config.bannerDisplayName;

            button.onClick.AddListener(() =>
            {
                SelectBanner(index);
            });
        }
    }

    public void SelectBanner(int index)
    {
        if (manager == null ||
            index < 0 ||
            index >= banners.Count)
            return;

        GachaBannerConfig selected = banners[index];

        if (selected == null || !selected.isEnabled)
            return;

        manager.banner = selected;

        if (bannerTitle != null)
            bannerTitle.text = selected.bannerDisplayName;

        if (bannerArtwork != null)
            bannerArtwork.sprite = selected.bannerArtwork;

        RefreshPity();
    }

    public void RefreshPity()
    {
        if (manager == null || manager.banner == null)
            return;

        string id = manager.banner.bannerId;

        int pity = manager.GetBannerPityCount(id);
        bool guaranteed = manager.IsBannerGuaranteed(id);

        if (pityText != null)
        {
            pityText.text =
                "五星保底：" +
                pity +
                "/" +
                manager.banner.hardPity;
        }

        if (guaranteeText != null)
        {
            guaranteeText.text = guaranteed
                ? "下次五星必定 UP"
                : "下次五星概率 UP";
        }
    }

    private void ClearButtons()
    {
        foreach (Button button in generatedButtons)
        {
            if (button != null)
                Destroy(button.gameObject);
        }

        generatedButtons.Clear();
    }

    private void OnDestroy()
    {
        ClearButtons();
    }
}
