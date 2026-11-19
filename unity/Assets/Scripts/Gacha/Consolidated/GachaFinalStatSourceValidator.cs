using System;

/// <summary>
/// 属性来源校验器。
///
/// 检查各系统是否越权提供属性。
/// </summary>
public static class GachaFinalStatSourceValidator
{
    public static bool Validate(
        GachaFinalStatSourceType sourceType,
        GachaFinalCombatStats baseStats,
        GachaFinalStatSourceData contribution,
        out string error)
    {
        error = null;

        if (baseStats == null)
        {
            error = "基础属性数据为空。";
            return false;
        }

        if (!IsFinite(baseStats))
        {
            error = "基础属性存在 NaN 或无穷大。";
            return false;
        }

        if (HasCombatBonus(baseStats))
        {
            error = "基础属性中包含战斗倍率。";
            return false;
        }

        if (contribution == null || !contribution.IsValid())
        {
            error = "属性贡献数据不完整。";
            return false;
        }

        if (contribution.sourceType != sourceType)
        {
            error = "属性数据来源类型不匹配。";
            return false;
        }

        if (!IsFinite(contribution.flatBonus) ||
            !IsFinite(contribution.percentBonus) ||
            !IsFinite(contribution.combatBonus))
        {
            error = "属性贡献包含 NaN 或无穷大。";
            return false;
        }

        if (sourceType == GachaFinalStatSourceType.WeaponBase)
        {
            if (HasCombatBonus(contribution.flatBonus) ||
                HasCombatBonus(contribution.percentBonus))
            {
                error = "武器普通属性桶中出现战斗倍率。";
                return false;
            }
        }

        if (sourceType == GachaFinalStatSourceType.WeaponPassive)
        {
            // 武器被动的战斗倍率只能在 combatBonus 中，
            // 不得出现在 flatBonus / percentBonus 中。
            if (HasCombatBonus(contribution.flatBonus) ||
                HasCombatBonus(contribution.percentBonus))
            {
                error = "武器被动普通属性桶中出现战斗倍率。";
                return false;
            }
        }

        return true;
    }

    private static bool HasCombatBonus(
        GachaFinalCombatStats stats)
    {
        return stats.skillDamageBonus != 0f ||
               stats.ultimateDamageBonus != 0f;
    }

    private static bool IsFinite(
        GachaFinalCombatStats stats)
    {
        return stats != null &&
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

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }
}
