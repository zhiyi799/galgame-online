using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CharacterPotentialConfig",
    menuName = "ASTRAL BLOOM/Character/Potential Config"
)]
public class GachaCharacterPotentialConfig : ScriptableObject
{
    public string characterId;

    [Header("潜能阶段")]
    public List<CharacterPotentialStage> stages =
        new List<CharacterPotentialStage>();

    public CharacterPotentialStage GetStage(int level)
    {
        if (level <= 0 || stages == null)
            return null;

        foreach (CharacterPotentialStage stage in stages)
        {
            if (stage != null && stage.stage == level)
                return stage;
        }

        return null;
    }

    private void OnValidate()
    {
        if (stages == null)
            stages = new List<CharacterPotentialStage>();

        foreach (CharacterPotentialStage stage in stages)
        {
            if (stage == null)
                continue;

            stage.stage = Mathf.Clamp(stage.stage, 1, 6);
        }
    }
}
