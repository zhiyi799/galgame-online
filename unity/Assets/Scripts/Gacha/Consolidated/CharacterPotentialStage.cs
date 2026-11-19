using System;
using UnityEngine;

[Serializable]
public class CharacterPotentialStage
{
    [Range(1, 6)]
    public int stage = 1;

    public string stageName;

    [TextArea]
    public string description;

    [Header("属性加成（百分比小数）")]
    public float attackBonus;
    public float hpBonus;
    public float defenseBonus;
    public float speedBonus;

    [Header("伤害加成")]
    public float skillDamageBonus;
    public float ultimateDamageBonus;

    [Header("其他加成")]
    public float critRateBonus;
    public float energyRechargeBonus;
}
