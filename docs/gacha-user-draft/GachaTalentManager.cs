using System.Collections.Generic;
using UnityEngine;

public class GachaTalentManager : MonoBehaviour
{
    public GachaManager manager;

    public List<GachaCharacterTalent> talentDatabase =
        new List<GachaCharacterTalent>();

    public float GetTalentBonus(
        string characterId,
        CharacterTalentType type)
    {
        if (manager == null || manager.Save == null)
            return 0f;

        GachaCharacterGrowthSave growth =
            manager.Save.characterGrowth.Find(
                x => x.characterId == characterId
            );

        if (growth == null)
            return 0f;

        GachaCharacterTalent config =
            talentDatabase.Find(
                x => x != null &&
                     x.characterId == characterId
            );

        if (config == null || config.talents == null)
            return 0f;

        float total = 0f;

        foreach (CharacterTalentNode talent in config.talents)
        {
            if (talent == null ||
                talent.talentType != type)
                continue;

            if (growth.ascension >= talent.requiredAscension)
                total += talent.value;
        }

        return total;
    }

    public List<CharacterTalentNode> GetUnlockedTalents(
        string characterId)
    {
        List<CharacterTalentNode> result =
            new List<CharacterTalentNode>();

        if (manager == null || manager.Save == null)
            return result;

        GachaCharacterGrowthSave growth =
            manager.Save.characterGrowth.Find(
                x => x.characterId == characterId
            );

        GachaCharacterTalent config =
            talentDatabase.Find(
                x => x != null &&
                     x.characterId == characterId
            );

        if (growth == null || config == null ||
            config.talents == null)
            return result;

        foreach (CharacterTalentNode talent in config.talents)
        {
            if (talent != null &&
                growth.ascension >= talent.requiredAscension)
            {
                result.Add(talent);
            }
        }

        return result;
    }
}
