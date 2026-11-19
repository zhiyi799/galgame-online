using UnityEngine;

public enum CharacterTalentType
{
    AttackBonus,
    HPBonus,
    DefenseBonus,
    SkillDamageBonus,
    UltimateDamageBonus,
    HealingBonus,
    DamageReduction,
    EnergyRechargeBonus
}

[CreateAssetMenu(
    fileName = "NewCharacterTalent",
    menuName = "ASTRAL BLOOM/Characters/Talent"
)]
public class GachaCharacterTalent : ScriptableObject
{
    [Header("天赋信息")]
    public string talentId;
    public string characterId;
    public string talentName;

    [TextArea(3, 6)]
    public string description;

    public CharacterTalentType talentType;

    [Header("解锁条件")]
    [Range(1, 90)]
    public int requiredLevel = 20;

    [Range(0, 6)]
    public int requiredPotential = 0;

    [Header("效果")]
    public float value = 5f;

    public bool IsUnlocked(
        int characterLevel,
        int potentialLevel)
    {
        return characterLevel >= requiredLevel &&
               potentialLevel >= requiredPotential;
    }
}
