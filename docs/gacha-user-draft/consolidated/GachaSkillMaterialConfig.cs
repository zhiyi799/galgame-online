using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GachaSkillLevelMaterialRequirement
{
    [Min(1)]
    public int targetLevel = 2;

    public List<GachaMaterialCost> costs =
        new List<GachaMaterialCost>();
}

[Serializable]
public class GachaSkillMaterialEntry
{
    public string characterId;
    public string skillId;

    public List<GachaSkillLevelMaterialRequirement> levelCosts =
        new List<GachaSkillLevelMaterialRequirement>();

    public List<GachaMaterialCost> GetCosts(int targetLevel)
    {
        var requirement = levelCosts.Find(
            x => x != null && x.targetLevel == targetLevel
        );

        return requirement == null ? null : requirement.costs;
    }
}

[CreateAssetMenu(
    fileName = "SkillMaterialConfig",
    menuName = "ASTRAL BLOOM/Skill Material Config"
)]
public class GachaSkillMaterialConfig : ScriptableObject
{
    public List<GachaSkillMaterialEntry> skills =
        new List<GachaSkillMaterialEntry>();

    public List<GachaMaterialCost> GetCosts(
        string characterId,
        string skillId,
        int targetLevel)
    {
        var entry = skills.Find(
            x => x != null &&
                 x.characterId == characterId &&
                 x.skillId == skillId
        );

        return entry == null
            ? null
            : entry.GetCosts(targetLevel);
    }
}
