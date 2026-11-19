using UnityEngine;

public enum CharacterTalentType
{
    AttackPercent,
    HPPercent,
    DefensePercent,
    SkillDamage,
    UltimateDamage,
    BasicAttackDamage,
    HealingBonus,
    ShieldBonus,
    EnergyRecharge
}

[System.Serializable]
public class CharacterTalentNode
{
    public string talentId;
    public string talentName;

    [TextArea(2, 5)]
    public string description;

    [Range(0, 6)]
    public int requiredAscension;

    public CharacterTalentType talentType;

    public float value;
}

[CreateAssetMenu(
    fileName = "NewCharacterTalent",
    menuName = "ASTRAL BLOOM/Characters/Talent"
)]
public class GachaCharacterTalent : ScriptableObject
{
    public string characterId;

    public CharacterTalentNode[] talents;
}
