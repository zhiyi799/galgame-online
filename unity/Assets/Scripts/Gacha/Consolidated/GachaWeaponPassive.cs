using System;
using UnityEngine;

public enum WeaponPassiveType
{
    None,
    AttackBonus,
    HPBonus,
    DefenseBonus,
    CritRateBonus,
    CritDamageBonus,
    SkillDamageBonus,
    UltimateDamageBonus,
    EnergyRechargeBonus
}

[Serializable]
public class GachaWeaponPassive
{
    public WeaponPassiveType passiveType = WeaponPassiveType.None;

    [Tooltip("精炼1阶的基础加成，0.05代表5%")]
    [Range(0f, 2f)]
    public float baseValue = 0.05f;

    [Tooltip("每提升1级精炼增加的加成")]
    [Range(0f, 1f)]
    public float valuePerRefinement = 0.01f;

    public float GetValue(int refinement)
    {
        refinement = Mathf.Clamp(refinement, 1, 5);

        return Mathf.Max(
            0f,
            baseValue + (refinement - 1) * valuePerRefinement
        );
    }
}
