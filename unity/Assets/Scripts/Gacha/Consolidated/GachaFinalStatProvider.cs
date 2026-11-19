using System;
using UnityEngine;

/// <summary>
/// 最终属性提供器。
///
/// 职责：
/// 1. 接收各系统收集的属性数据。
/// 2. 明确区分四种属性来源。
/// 3. 调用 GachaFinalStatCalculator。
/// 4. 输出最终战斗属性快照。
///
/// 不负责修改角色、武器或存档数据。
/// </summary>
public static class GachaFinalStatProvider
{
    /// <summary>
    /// 根据四个属性桶生成最终属性快照。
    /// 先校验属性桶，再进行计算。
    /// </summary>
    public static GachaFinalCombatStats BuildSnapshot(
        GachaFinalCombatStats baseStats,
        GachaFinalCombatStats flatBonus,
        GachaFinalCombatStats percentBonus,
        GachaFinalCombatStats combatBonus)
    {
        // 空桶统一初始化，避免空引用。
        if (baseStats == null)
            return null;

        if (flatBonus == null)
            flatBonus = CreateEmptyBucket();

        if (percentBonus == null)
            percentBonus = CreateEmptyBucket();

        if (combatBonus == null)
            combatBonus = CreateEmptyBucket();

        // 先检查属性桶，再进行计算。
        string error;

        if (!GachaFinalStatValidator.ValidateBuckets(
            baseStats,
            flatBonus,
            percentBonus,
            combatBonus,
            out error))
        {
            UnityEngine.Debug.LogError(
                "[GachaFinalStatProvider] 属性桶校验失败：" + error
            );

            return null;
        }

        GachaFinalCombatStats result =
            GachaFinalStatCalculator.Calculate(
                baseStats.Clone(),
                flatBonus.Clone(),
                percentBonus.Clone(),
                combatBonus.Clone()
            );

        if (result == null || !result.IsValid())
        {
            UnityEngine.Debug.LogError(
                "[GachaFinalStatProvider] 最终属性计算失败。"
            );

            return null;
        }

        return result;
    }

    /// <summary>
    /// 生成不可直接修改的最终属性快照。
    /// </summary>
    public static GachaFinalStatSnapshot BuildImmutableSnapshot(
        GachaFinalCombatStats baseStats,
        GachaFinalCombatStats flatBonus,
        GachaFinalCombatStats percentBonus,
        GachaFinalCombatStats combatBonus)
    {
        GachaFinalCombatStats result = BuildSnapshot(
            baseStats,
            flatBonus,
            percentBonus,
            combatBonus
        );

        if (result == null)
            return null;

        return new GachaFinalStatSnapshot(result);
    }

    /// <summary>
    /// 创建空属性桶。
    /// </summary>
    public static GachaFinalCombatStats CreateEmptyBucket()
    {
        return new GachaFinalCombatStats();
    }

    /// <summary>
    /// 安全累加固定属性。
    /// </summary>
    public static void AddFlat(
        GachaFinalCombatStats bucket,
        GachaFinalCombatStats value)
    {
        if (bucket == null || value == null)
            return;

        bucket.attack += value.attack;
        bucket.maxHP += value.maxHP;
        bucket.defense += value.defense;
        bucket.speed += value.speed;

        bucket.critRate += value.critRate;
        bucket.critDamage += value.critDamage;
        bucket.energyRecharge += value.energyRecharge;

        bucket.healingBonus += value.healingBonus;
        bucket.damageReduction += value.damageReduction;
    }

    /// <summary>
    /// 安全累加百分比属性。
    /// 所有比例使用小数，例如 0.05 = 5%。
    /// </summary>
    public static void AddPercent(
        GachaFinalCombatStats bucket,
        GachaFinalCombatStats value)
    {
        if (bucket == null || value == null)
            return;

        bucket.attack += value.attack;
        bucket.maxHP += value.maxHP;
        bucket.defense += value.defense;
        bucket.speed += value.speed;

        bucket.critRate += value.critRate;
        bucket.critDamage += value.critDamage;
        bucket.energyRecharge += value.energyRecharge;

        bucket.healingBonus += value.healingBonus;
        bucket.damageReduction += value.damageReduction;
    }

    /// <summary>
    /// 安全累加战斗专属倍率。
    /// 不得把这些数值加入基础攻击力等属性。
    /// </summary>
    public static void AddCombatBonus(
        GachaFinalCombatStats bucket,
        GachaFinalCombatStats value)
    {
        if (bucket == null || value == null)
            return;

        bucket.skillDamageBonus += value.skillDamageBonus;
        bucket.ultimateDamageBonus += value.ultimateDamageBonus;
    }
}
