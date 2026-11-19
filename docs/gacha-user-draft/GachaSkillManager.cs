using System.Collections.Generic;
using UnityEngine;

public class GachaSkillManager : MonoBehaviour
{
    public GachaManager manager;

    public List<GachaCharacterSkill> skills =
        new List<GachaCharacterSkill>();

    public int GetSkillLevel(string skillId)
    {
        if (manager == null || manager.Save == null)
            return 1;

        GachaSkillLevelSave save =
            manager.Save.skillLevels.Find(
                x => x.skillId == skillId
            );

        return save == null ? 1 : save.level;
    }

    public bool UpgradeSkill(string skillId)
    {
        if (manager == null || manager.Save == null)
            return false;

        GachaCharacterSkill skill =
            skills.Find(x => x != null && x.skillId == skillId);

        if (skill == null)
            return false;

        if (!manager.HasCharacter(skill.characterId))
            return false;

        GachaSkillLevelSave levelSave =
            manager.Save.skillLevels.Find(
                x => x.skillId == skillId
            );

        if (levelSave == null)
        {
            levelSave = new GachaSkillLevelSave
            {
                skillId = skillId,
                level = 1
            };

            manager.Save.skillLevels.Add(levelSave);
        }

        if (levelSave.level >= skill.maxLevel)
            return false;

        int cost = skill.GetMaterialCost(levelSave.level);

        GachaMaterialSave material =
            manager.Save.materials.Find(
                x => x.materialId == skill.materialId
            );

        if (material == null || material.amount < cost)
            return false;

        material.amount -= cost;
        levelSave.level++;

        manager.SaveGame();

        return true;
    }

    public float GetSkillMultiplier(string skillId)
    {
        GachaCharacterSkill skill =
            skills.Find(x => x != null && x.skillId == skillId);

        if (skill == null)
            return 1f;

        int level = GetSkillLevel(skillId);

        return skill.GetMultiplier(level);
    }
}
