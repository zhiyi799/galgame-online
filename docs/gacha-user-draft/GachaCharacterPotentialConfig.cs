using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CharacterPotentialStage
{
    [Range(1, 6)]
    public int stage = 1;

    public string title;

    [TextArea(2, 5)]
    public string description;

    [Header("属性加成百分比")]
    public float hpBonus;
    public float attackBonus;
    public float defenseBonus;

    [Header("技能倍率加成")]
    public float skillMultiplierBonus;
    public float ultimateMultiplierBonus;
}

[CreateAssetMenu(
    fileName = "CharacterPotential",
    menuName = "ASTRAL BLOOM/Characters/Potential Config"
)]
public class GachaCharacterPotentialConfig : ScriptableObject
{
    public string characterId;
    public string characterName;

    [Range(1, 6)]
    public int maxPotentialLevel = 6;

    public List<CharacterPotentialStage> stages =
        new List<CharacterPotentialStage>();

    public CharacterPotentialStage GetStage(int level)
    {
        return stages.Find(x => x.stage == level);
    }
}
