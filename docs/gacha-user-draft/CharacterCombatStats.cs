using System;
using UnityEngine;

[Serializable]
public class CharacterCombatStats
{
    [Header("基础属性")]
    public float maxHP = 1000;
    public float attack = 100;
    public float defense = 100;

    [Header("战斗属性")]
    public float speed = 100;
    public float critRate = 5;
    public float critDamage = 50;
    public float energyRecharge = 100;

    public CharacterCombatStats Clone()
    {
        return new CharacterCombatStats
        {
            maxHP = maxHP,
            attack = attack,
            defense = defense,
            speed = speed,
            critRate = critRate,
            critDamage = critDamage,
            energyRecharge = energyRecharge
        };
    }
}
