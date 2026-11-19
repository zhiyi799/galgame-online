using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CharacterTalentConfig",
    menuName = "ASTRAL BLOOM/Character/Talent Config"
)]
public class GachaCharacterTalentConfig : ScriptableObject
{
    public string characterId;

    public List<GachaCharacterTalent> talents =
        new List<GachaCharacterTalent>();

    public List<GachaCharacterTalent> GetUnlockedTalents(
        int ascension
    )
    {
        List<GachaCharacterTalent> result =
            new List<GachaCharacterTalent>();

        if (talents == null)
            return result;

        foreach (GachaCharacterTalent talent in talents)
        {
            if (talent == null || !talent.enabled)
                continue;

            if (ascension >= talent.requiredAscension)
                result.Add(talent);
        }

        return result;
    }
}
