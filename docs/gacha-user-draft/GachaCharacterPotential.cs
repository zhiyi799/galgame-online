using UnityEngine;

public enum PotentialEffectType
{
    AttackPercent,
    HPPercent,
    DefensePercent,
    CritRate,
    CritDamage,
    SkillDamage,
    UltimateDamage,
    EnergyRecharge
}

[System.Serializable]
public class PotentialNode
{
    public string nodeId;
    public string nodeName;

    [TextArea(2, 5)]
    public string description;

    public PotentialEffectType effectType;

    public float effectValue = 0.05f;

    public int requiredPotential = 1;
}

[CreateAssetMenu(
    fileName = "NewCharacterPotential",
    menuName = "ASTRAL BLOOM/Characters/Potential"
)]
public class GachaCharacterPotential : ScriptableObject
{
    public string characterId;

    public PotentialNode[] nodes = new PotentialNode[6];
}
