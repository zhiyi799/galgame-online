using UnityEngine;
using UnityEngine.UI;

public class CharacterStatsPanelUI : MonoBehaviour
{
    public GachaFinalStatProvider statProvider;
    public GachaCharacterGrowthManager growthManager;

    [Header("文本")]
    public Text levelText;
    public Text hpText;
    public Text attackText;
    public Text defenseText;
    public Text speedText;
    public Text critRateText;
    public Text critDamageText;
    public Text energyText;

    public void Refresh(string characterId)
    {
        if (statProvider == null)
            return;

        CharacterCombatStats stats =
            statProvider.GetFinalStats(characterId);

        GachaCharacterGrowthSave growth =
            growthManager != null
                ? growthManager.GetGrowth(characterId)
                : null;

        int level = growth == null ? 1 : growth.level;
        int ascension = growth == null ? 0 : growth.ascension;

        Set(levelText,
            "等级  LV." + level + "    突破 " + ascension);

        Set(hpText, "生命值  " +
            Mathf.RoundToInt(stats.maxHP));

        Set(attackText, "攻击力  " +
            Mathf.RoundToInt(stats.attack));

        Set(defenseText, "防御力  " +
            Mathf.RoundToInt(stats.defense));

        Set(speedText, "速度  " +
            Mathf.RoundToInt(stats.speed));

        Set(critRateText, "暴击率  " +
            stats.critRate.ToString("F1") + "%");

        Set(critDamageText, "暴击伤害  " +
            stats.critDamage.ToString("F1") + "%");

        Set(energyText, "能量回复  " +
            stats.energyRecharge.ToString("F1") + "%");
    }

    private void Set(Text target, string value)
    {
        if (target != null)
            target.text = value;
    }
}
