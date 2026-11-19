using UnityEngine;

public enum WeaponPassiveType
{
    AttackBonus,
    CritRateBonus,
    CritDamageBonus,
    SkillDamageBonus,
    UltimateDamageBonus,
    EnergyRechargeBonus,
    DefenseBonus
}

[CreateAssetMenu(
    fileName = "NewWeaponPassive",
    menuName = "ASTRAL BLOOM/Equipment/Weapon Passive"
)]
public class GachaWeaponPassive : ScriptableObject
{
    public string passiveId;
    public string passiveName;

    [TextArea(2, 5)]
    public string description;

    public WeaponPassiveType passiveType;

    [Header("效果")]
    public float baseValue = 5f;
    public float valuePerRefinement = 1f;

    public float GetValue(int refinement)
    {
        return baseValue +
               Mathf.Max(0, refinement - 1) *
               valuePerRefinement;
    }
}
