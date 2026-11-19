using UnityEngine;

public static class GachaCombatDamageCalculator
{
    /// <summary>
    /// 普通攻击伤害。
    /// </summary>
    public static float CalculateBasicDamage(
        float attack,
        float defense,
        float skillMultiplier,
        GachaFinalCombatStats attacker,
        GachaFinalCombatStats defender,
        bool canCrit = true)
    {
        return CalculateDamage(
            attack,
            defense,
            skillMultiplier,
            1f,
            attacker,
            defender,
            canCrit
        );
    }

    /// <summary>
    /// 技能伤害。
    /// skillMultiplier 为技能自身倍率，例如 1.5 = 150%。
    /// </summary>
    public static float CalculateSkillDamage(
        float attack,
        float defense,
        float skillMultiplier,
        GachaFinalCombatStats attacker,
        GachaFinalCombatStats defender,
        bool canCrit = true)
    {
        float bonus = attacker != null
            ? attacker.skillDamageBonus
            : 0f;

        return CalculateDamage(
            attack,
            defense,
            skillMultiplier,
            1f + bonus,
            attacker,
            defender,
            canCrit
        );
    }

    /// <summary>
    /// 终结技伤害。
    /// </summary>
    public static float CalculateUltimateDamage(
        float attack,
        float defense,
        float ultimateMultiplier,
        GachaFinalCombatStats attacker,
        GachaFinalCombatStats defender,
        bool canCrit = true)
    {
        float bonus = attacker != null
            ? attacker.ultimateDamageBonus
            : 0f;

        return CalculateDamage(
            attack,
            defense,
            ultimateMultiplier,
            1f + bonus,
            attacker,
            defender,
            canCrit
        );
    }

    private static float CalculateDamage(
        float attack,
        float defense,
        float baseMultiplier,
        float bonusMultiplier,
        GachaFinalCombatStats attacker,
        GachaFinalCombatStats defender,
        bool canCrit)
    {
        attack = Mathf.Max(0f, attack);
        defense = Mathf.Max(0f, defense);

        baseMultiplier = Mathf.Max(0f, baseMultiplier);
        bonusMultiplier = Mathf.Max(0f, bonusMultiplier);

        // 防御公式暂用常见的等级无关模型。
        // 正式版本应替换为项目最终确定的防御公式。
        float defenseMultiplier =
            100f / (100f + defense);

        float damage =
            attack *
            baseMultiplier *
            bonusMultiplier *
            defenseMultiplier;

        if (canCrit && attacker != null)
        {
            float critRate = Mathf.Clamp01(
                attacker.critRate
            );

            float critDamage = Mathf.Max(
                1f,
                attacker.critDamage
            );

            if (Random.value < critRate)
                damage *= critDamage;
        }

        // 目标减伤最后应用。
        if (defender != null)
        {
            float reduction = Mathf.Clamp01(
                defender.damageReduction
            );

            damage *= 1f - reduction;
        }

        return Mathf.Max(0f, damage);
    }

    /// <summary>
    /// 治疗量。
    /// </summary>
    public static float CalculateHealing(
        float baseHealing,
        GachaFinalCombatStats healer)
    {
        baseHealing = Mathf.Max(0f, baseHealing);

        float bonus = healer != null
            ? healer.healingBonus
            : 0f;

        return baseHealing * (1f + bonus);
    }

    /// <summary>
    /// 最终治疗量，不能超过目标缺失生命值。
    /// </summary>
    public static int CalculateActualHealing(
        float calculatedHealing,
        int currentHP,
        int maxHP)
    {
        if (maxHP <= 0)
            return 0;

        int missingHP = Mathf.Max(
            0,
            maxHP - currentHP
        );

        int healing = Mathf.RoundToInt(
            Mathf.Max(0f, calculatedHealing)
        );

        return Mathf.Min(healing, missingHP);
    }
}
