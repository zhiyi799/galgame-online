using System;

[Serializable]
public class GachaFinalCombatStats
{
    public float attack;
    public float maxHP;
    public float defense;
    public float speed;

    public float critRate;
    public float critDamage;
    public float energyRecharge;

    public float healingBonus;
    public float damageReduction;

    public float skillDamageBonus;
    public float ultimateDamageBonus;

    public GachaFinalCombatStats Clone()
    {
        return new GachaFinalCombatStats
        {
            attack = attack,
            maxHP = maxHP,
            defense = defense,
            speed = speed,

            critRate = critRate,
            critDamage = critDamage,
            energyRecharge = energyRecharge,

            healingBonus = healingBonus,
            damageReduction = damageReduction,

            skillDamageBonus = skillDamageBonus,
            ultimateDamageBonus = ultimateDamageBonus
        };
    }

    public bool IsValid()
    {
        return IsValidValue(attack)
            && IsValidValue(maxHP)
            && IsValidValue(defense)
            && IsValidValue(speed)
            && IsValidValue(critRate)
            && IsValidValue(critDamage)
            && IsValidValue(energyRecharge)
            && IsValidValue(healingBonus)
            && IsValidValue(damageReduction)
            && IsValidValue(skillDamageBonus)
            && IsValidValue(ultimateDamageBonus);
    }

    private static bool IsValidValue(float value)
    {
        return !float.IsNaN(value)
            && !float.IsInfinity(value)
            && value >= 0f;
    }
}
