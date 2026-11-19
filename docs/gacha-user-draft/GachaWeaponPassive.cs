using UnityEngine;

public enum WeaponPassiveType
{
    AttackPercent,
    CritRate,
    CritDamage,
    SkillDamage,
    UltimateDamage,
    BasicAttackDamage,
    EnergyRecharge,
    DamageReduction,
    HealingBonus
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

    public float baseValue = 0.05f;

    public float valuePerAscension = 0.01f;

    public float GetValue(int ascension)
    {
        return baseValue +
               Mathf.Max(0, ascension) * valuePerAscension;
    }
}
