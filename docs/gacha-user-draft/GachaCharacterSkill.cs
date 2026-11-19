using UnityEngine;

public enum CharacterSkillType
{
    BasicAttack,
    Skill,
    Ultimate,
    Passive
}

[CreateAssetMenu(
    fileName = "NewCharacterSkill",
    menuName = "ASTRAL BLOOM/Characters/Skill"
)]
public class GachaCharacterSkill : ScriptableObject
{
    [Header("技能信息")]
    public string skillId;
    public string characterId;
    public string skillName;

    [TextArea(3, 8)]
    public string description;

    public CharacterSkillType skillType;

    [Header("技能等级")]
    public int maxLevel = 10;

    [Header("技能效果")]
    public float baseMultiplier = 1f;
    public float multiplierPerLevel = 0.1f;

    [Header("升级消耗")]
    public string materialId = "skill_material";
    public int baseMaterialCost = 2;

    public float GetMultiplier(int level)
    {
        return baseMultiplier +
               Mathf.Max(0, level - 1) * multiplierPerLevel;
    }

    public int GetMaterialCost(int currentLevel)
    {
        return baseMaterialCost * currentLevel;
    }
}
