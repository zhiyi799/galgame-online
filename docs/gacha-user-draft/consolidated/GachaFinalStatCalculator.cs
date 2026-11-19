using UnityEngine;

public static class GachaFinalStatCalculator
{
    public static GachaFinalCombatStats Calculate(
        GachaFinalCombatStats baseStats,
        GachaFinalCombatStats flatBonus,
        GachaFinalCombatStats percentBonus,
        GachaFinalCombatStats combatBonus = null)
    {
        if (baseStats == null)
            return null;

        flatBonus = flatBonus ?? new GachaFinalCombatStats();
        percentBonus = percentBonus ?? new GachaFinalCombatStats();
        combatBonus = combatBonus ?? new GachaFinalCombatStats();

        GachaFinalCombatStats result =
            new GachaFinalCombatStats();

        // 基础属性：基础值 + 固定加成，再乘百分比加成。
        result.attack = Apply(
            baseStats.attack,
            flatBonus.attack,
            percentBonus.attack
        );

        result.maxHP = Apply(
            baseStats.maxHP,
            flatBonus.maxHP,
            percentBonus.maxHP
        );

        result.defense = Apply(
            baseStats.defense,
            flatBonus.defense,
            percentBonus.defense
        );

        result.speed = Apply(
            baseStats.speed,
            flatBonus.speed,
            percentBonus.speed
        );

        result.critRate = Apply(
            baseStats.critRate,
            flatBonus.critRate,
            percentBonus.critRate
        );

        result.critDamage = Apply(
            baseStats.critDamage,
            flatBonus.critDamage,
            percentBonus.critDamage
        );

        result.energyRecharge = Apply(
            baseStats.energyRecharge,
            flatBonus.energyRecharge,
            percentBonus.energyRecharge
        );

        result.healingBonus = Apply(
            baseStats.healingBonus,
            flatBonus.healingBonus,
            percentBonus.healingBonus
        );

        result.damageReduction = Apply(
            baseStats.damageReduction,
            flatBonus.damageReduction,
            percentBonus.damageReduction
        );

        // 战斗专属加成不混入攻击力等基础属性。
        result.skillDamageBonus =
            baseStats.skillDamageBonus +
            flatBonus.skillDamageBonus +
            percentBonus.skillDamageBonus +
            combatBonus.skillDamageBonus;

        result.ultimateDamageBonus =
            baseStats.ultimateDamageBonus +
            flatBonus.ultimateDamageBonus +
            percentBonus.ultimateDamageBonus +
            combatBonus.ultimateDamageBonus;

        return result;
    }

    private static float Apply(
        float baseValue,
        float flat,
        float percent)
    {
        return (baseValue + flat) * (1f + percent);
    }

    public static float CalculateSkillMultiplier(
        float baseMultiplier,
        GachaFinalCombatStats stats)
    {
        if (stats == null)
            return baseMultiplier;

        return baseMultiplier *
               (1f + stats.skillDamageBonus);
    }

    public static float CalculateUltimateMultiplier(
        float baseMultiplier,
        GachaFinalCombatStats stats)
    {
        if (stats == null)
            return baseMultiplier;

        return baseMultiplier *
               (1f + stats.ultimateDamageBonus);
    }
}
