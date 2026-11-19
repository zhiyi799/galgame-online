using UnityEngine;

/// <summary>
/// 角色基础属性计算器（等级成长 + 突破成长）。
///
/// 只执行一次角色等级与突破成长。
/// 输入是角色基础属性，输出是成长后的基础属性。
/// 不叠加武器、潜能、天赋——那些走收集器的其他路。
///
/// 突破加成是比例（batch19 已确定：生命每阶 +8%，
/// 攻击 / 防御每阶 +5%，线性累加），不是固定值，
/// 因此用成长倍率 × (1 + 突破加成)。
/// 暴击率 / 暴击伤害 / 充能不随等级与突破成长。
/// </summary>
public class GachaCharacterStatCalculator
{
    public GachaFinalCombatStats Calculate(
        GachaFinalCombatStats baseStats,
        GachaGrowthConfig growthConfig,
        int level,
        int ascension)
    {
        if (baseStats == null ||
            growthConfig == null ||
            !growthConfig.IsValid())
        {
            return null;
        }

        int safeAscension = Mathf.Clamp(
            ascension,
            0,
            growthConfig.maxAscension);

        int safeLevel = Mathf.Clamp(
            level,
            1,
            growthConfig.GetLevelCap(safeAscension));

        GachaFinalCombatStats result = baseStats.Clone();

        float hpGrowth = growthConfig.GetGrowth(
            growthConfig.hpGrowth, safeLevel);

        float attackGrowth = growthConfig.GetGrowth(
            growthConfig.attackGrowth, safeLevel);

        float defenseGrowth = growthConfig.GetGrowth(
            growthConfig.defenseGrowth, safeLevel);

        float speedGrowth = growthConfig.GetGrowth(
            growthConfig.speedGrowth, safeLevel);

        float hpAscension = growthConfig.GetAscensionBonus(
            growthConfig.ascensionHPBonus, safeAscension);

        float attackAscension = growthConfig.GetAscensionBonus(
            growthConfig.ascensionAttackBonus, safeAscension);

        float defenseAscension = growthConfig.GetAscensionBonus(
            growthConfig.ascensionDefenseBonus, safeAscension);

        float speedAscension = growthConfig.GetAscensionBonus(
            growthConfig.ascensionSpeedBonus, safeAscension);

        result.maxHP *= hpGrowth * (1f + hpAscension);
        result.attack *= attackGrowth * (1f + attackAscension);
        result.defense *= defenseGrowth * (1f + defenseAscension);
        result.speed *= speedGrowth * (1f + speedAscension);

        if (!result.IsValid())
            return null;

        return result;
    }
}
