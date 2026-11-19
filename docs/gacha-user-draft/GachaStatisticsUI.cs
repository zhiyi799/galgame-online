using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class GachaStatisticsUI : MonoBehaviour
{
    [Header("数据")]
    public GachaManager manager;

    [Header("统计文本")]
    public Text totalPullsText;
    public Text threeStarText;
    public Text fourStarText;
    public Text fiveStarText;
    public Text upFiveStarText;
    public Text upRateText;
    public Text pityText;
    public Text guaranteedText;
    public Text resonanceText;

    private void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (manager == null || manager.Save == null)
            return;

        GachaSaveData save = manager.Save;

        int three = 0;
        int four = 0;
        int five = 0;
        int upFive = 0;

        foreach (GachaPullRecord record in save.history)
        {
            if (record == null)
                continue;

            switch (record.rarity)
            {
                case Rarity.ThreeStar:
                    three++;
                    break;

                case Rarity.FourStar:
                    four++;
                    break;

                case Rarity.FiveStar:
                    five++;

                    if (record.isRateUp)
                        upFive++;

                    break;
            }
        }

        float upRate = five > 0
            ? (float)upFive / five * 100f
            : 0f;

        SetText(totalPullsText, "累计抽卡：" + save.totalPulls);
        SetText(threeStarText, "三星获得：" + three);
        SetText(fourStarText, "四星获得：" + four);
        SetText(fiveStarText, "五星获得：" + five);
        SetText(upFiveStarText, "五星 UP：" + upFive);
        SetText(upRateText, "五星 UP 命中率：" + upRate.ToString("F1") + "%");

        if (manager.banner != null)
        {
            SetText(
                pityText,
                "当前五星保底：" +
                save.pityFiveStar +
                "/" +
                manager.banner.hardPity
            );
        }
        else
        {
            SetText(pityText, "当前五星保底：" + save.pityFiveStar);
        }

        SetText(
            guaranteedText,
            save.guaranteedRateUp
                ? "下次五星：必定 UP"
                : "下次五星：概率 UP"
        );

        SetText(resonanceText, "当前共鸣：" + save.resonance);
    }

    private void SetText(Text target, string value)
    {
        if (target != null)
            target.text = value;
    }
}
