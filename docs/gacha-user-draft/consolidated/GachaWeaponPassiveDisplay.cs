using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GachaWeaponPassiveDisplay : MonoBehaviour
{
    public TMP_Text passiveNameText;
    public TMP_Text passiveValueText;

    public void Refresh(
        GachaWeaponConfig config,
        int refinement)
    {
        if (config == null || config.passive == null)
        {
            Clear();
            return;
        }

        WeaponPassiveType type =
            config.passive.passiveType;

        if (type == WeaponPassiveType.None)
        {
            Clear();
            return;
        }

        float value =
            config.passive.GetValue(refinement);

        if (passiveNameText != null)
            passiveNameText.text = GetPassiveName(type);

        if (passiveValueText != null)
            passiveValueText.text =
                "+" + (value * 100f).ToString("0.0") + "%";
    }

    private string GetPassiveName(WeaponPassiveType type)
    {
        switch (type)
        {
            case WeaponPassiveType.AttackBonus:
                return "攻击力加成";

            case WeaponPassiveType.HPBonus:
                return "生命值加成";

            case WeaponPassiveType.DefenseBonus:
                return "防御力加成";

            case WeaponPassiveType.CritRateBonus:
                return "暴击率加成";

            case WeaponPassiveType.CritDamageBonus:
                return "暴击伤害加成";

            case WeaponPassiveType.SkillDamageBonus:
                return "技能伤害加成";

            case WeaponPassiveType.UltimateDamageBonus:
                return "终结技伤害加成";

            case WeaponPassiveType.EnergyRechargeBonus:
                return "能量回复效率";

            default:
                return "特殊效果";
        }
    }

    private void Clear()
    {
        if (passiveNameText != null)
            passiveNameText.text = "";

        if (passiveValueText != null)
            passiveValueText.text = "";
    }
}
