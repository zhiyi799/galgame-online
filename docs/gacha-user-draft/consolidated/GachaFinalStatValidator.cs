using System;

/// <summary>
/// 最终属性桶校验器。
///
/// 核心规则：
/// skillDamageBonus 和 ultimateDamageBonus
/// 只能存在于 combatBonus。
///
/// 本类只负责检查，不修改任何属性数据。
/// </summary>
public static class GachaFinalStatValidator
{
    /// <summary>
    /// 检查四个属性桶是否有效。
    /// </summary>
    public static bool ValidateBuckets(
        GachaFinalCombatStats baseStats,
        GachaFinalCombatStats flatBonus,
        GachaFinalCombatStats percentBonus,
        GachaFinalCombatStats combatBonus,
        out string error)
    {
        error = null;

        if (baseStats == null)
        {
            error = "baseStats 不能为空。";
            return false;
        }

        if (flatBonus == null)
        {
            error = "flatBonus 不能为空。";
            return false;
        }

        if (percentBonus == null)
        {
            error = "percentBonus 不能为空。";
            return false;
        }

        if (combatBonus == null)
        {
            error = "combatBonus 不能为空。";
            return false;
        }

        if (!ValidateFinite(baseStats))
        {
            error = "baseStats 存在 NaN 或无穷大。";
            return false;
        }

        if (!ValidateFinite(flatBonus))
        {
            error = "flatBonus 存在 NaN 或无穷大。";
            return false;
        }

        if (!ValidateFinite(percentBonus))
        {
            error = "percentBonus 存在 NaN 或无穷大。";
            return false;
        }

        if (!ValidateFinite(combatBonus))
        {
            error = "combatBonus 存在 NaN 或无穷大。";
            return false;
        }

        // 战斗倍率不得进入基础属性桶。
        if (HasCombatBonus(baseStats))
        {
            error =
                "发现重复计算风险：baseStats 包含战斗倍率。";
            return false;
        }

        // 战斗倍率不得进入固定属性桶。
        if (HasCombatBonus(flatBonus))
        {
            error =
                "发现重复计算风险：flatBonus 包含战斗倍率。";
            return false;
        }

        // 战斗倍率不得进入百分比属性桶。
        if (HasCombatBonus(percentBonus))
        {
            error =
                "发现重复计算风险：percentBonus 包含战斗倍率。";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 检查所有数值是否为有限值。
    /// </summary>
    private static bool ValidateFinite(
        GachaFinalCombatStats stats)
    {
        return
            IsFinite(stats.attack) &&
            IsFinite(stats.maxHP) &&
            IsFinite(stats.defense) &&
            IsFinite(stats.speed) &&

            IsFinite(stats.critRate) &&
            IsFinite(stats.critDamage) &&
            IsFinite(stats.energyRecharge) &&

            IsFinite(stats.healingBonus) &&
            IsFinite(stats.damageReduction) &&

            IsFinite(stats.skillDamageBonus) &&
            IsFinite(stats.ultimateDamageBonus);
    }

    /// <summary>
    /// 检查是否错误地把战斗倍率放入普通属性桶。
    /// 严格检查：任何非零值都拦截，不放过极小错误数值。
    /// </summary>
    private static bool HasCombatBonus(
        GachaFinalCombatStats stats)
    {
        return
            stats.skillDamageBonus != 0f ||
            stats.ultimateDamageBonus != 0f;
    }

    private static bool IsFinite(float value)
    {
        return
            !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }
}
