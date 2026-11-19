using System;

/// <summary>
/// 五路属性数据收集器。
///
/// 五路来源：
/// 1. 角色成长
/// 2. 武器基础
/// 3. 武器被动
/// 4. 潜能
/// 5. 天赋
///
/// 每路通过数据读取回调接入现有管理器。
/// </summary>
public sealed class GachaFinalStatDataCollector
{
    // 角色基础属性 + 等级成长 + 突破成长
    private readonly Func<GachaFinalCombatStats>
        characterSource;

    // 武器基础属性
    private readonly Func<GachaFinalCombatStats>
        weaponBaseSource;

    // 武器被动贡献
    private readonly Func<GachaFinalStatSourceData>
        weaponPassiveSource;

    // 潜能贡献
    private readonly Func<GachaFinalStatSourceData>
        potentialSource;

    // 天赋贡献
    private readonly Func<GachaFinalStatSourceData>
        talentSource;

    public GachaFinalStatDataCollector(
        Func<GachaFinalCombatStats> characterSource,
        Func<GachaFinalCombatStats> weaponBaseSource,
        Func<GachaFinalStatSourceData> weaponPassiveSource,
        Func<GachaFinalStatSourceData> potentialSource,
        Func<GachaFinalStatSourceData> talentSource)
    {
        this.characterSource = characterSource;
        this.weaponBaseSource = weaponBaseSource;
        this.weaponPassiveSource = weaponPassiveSource;
        this.potentialSource = potentialSource;
        this.talentSource = talentSource;
    }

    /// <summary>
    /// 收集五路数据并生成最终快照。
    /// </summary>
    public GachaFinalStatSnapshot Collect(
        out string error)
    {
        error = null;

        if (characterSource == null ||
            weaponBaseSource == null ||
            weaponPassiveSource == null ||
            potentialSource == null ||
            talentSource == null)
        {
            error = "存在未绑定的属性数据源。";
            return null;
        }

        try
        {
            // 第一条：角色基础数据
            GachaFinalCombatStats character =
                characterSource();

            if (character == null)
            {
                error = "角色属性读取失败。";
                return null;
            }

            // 第二条：武器基础数据
            GachaFinalCombatStats weapon =
                weaponBaseSource();

            if (weapon == null)
            {
                error = "武器基础属性读取失败。";
                return null;
            }

            // 第三条：武器被动贡献
            GachaFinalStatSourceData weaponPassive =
                weaponPassiveSource();

            if (weaponPassive == null || !weaponPassive.IsValid())
            {
                error = "武器被动属性读取失败。";
                return null;
            }

            // 第四条：潜能贡献
            GachaFinalStatSourceData potential =
                potentialSource();

            if (potential == null || !potential.IsValid())
            {
                error = "潜能属性读取失败。";
                return null;
            }

            // 第五条：天赋贡献
            GachaFinalStatSourceData talent =
                talentSource();

            if (talent == null || !talent.IsValid())
            {
                error = "天赋属性读取失败。";
                return null;
            }

            // 汇总基础属性。
            GachaFinalCombatStats baseStats =
                character.Clone();

            AddBaseStats(baseStats, weapon);

            // 汇总固定属性。
            GachaFinalCombatStats flatBonus =
                new GachaFinalCombatStats();

            AddFlat(flatBonus, weaponPassive.flatBonus);
            AddFlat(flatBonus, potential.flatBonus);
            AddFlat(flatBonus, talent.flatBonus);

            // 汇总百分比属性。
            GachaFinalCombatStats percentBonus =
                new GachaFinalCombatStats();

            AddPercent(percentBonus, weaponPassive.percentBonus);
            AddPercent(percentBonus, potential.percentBonus);
            AddPercent(percentBonus, talent.percentBonus);

            // 汇总战斗专属倍率。
            GachaFinalCombatStats combatBonus =
                new GachaFinalCombatStats();

            AddCombat(combatBonus, weaponPassive.combatBonus);
            AddCombat(combatBonus, potential.combatBonus);
            AddCombat(combatBonus, talent.combatBonus);

            // 统一进入最终属性提供器。
            GachaFinalCombatStats finalStats =
                GachaFinalStatProvider.BuildSnapshot(
                    baseStats,
                    flatBonus,
                    percentBonus,
                    combatBonus
                );

            if (finalStats == null)
            {
                error = "最终属性计算或校验失败。";
                return null;
            }

            return new GachaFinalStatSnapshot(finalStats);
        }
        catch (Exception exception)
        {
            error = "属性收集异常：" + exception.Message;
            return null;
        }
    }

    private static void AddBaseStats(
        GachaFinalCombatStats target,
        GachaFinalCombatStats source)
    {
        target.attack += source.attack;
        target.maxHP += source.maxHP;
        target.defense += source.defense;
        target.speed += source.speed;

        target.critRate += source.critRate;
        target.critDamage += source.critDamage;
        target.energyRecharge += source.energyRecharge;

        target.healingBonus += source.healingBonus;
        target.damageReduction += source.damageReduction;
    }

    private static void AddFlat(
        GachaFinalCombatStats target,
        GachaFinalCombatStats source)
    {
        GachaFinalStatProvider.AddFlat(target, source);
    }

    private static void AddPercent(
        GachaFinalCombatStats target,
        GachaFinalCombatStats source)
    {
        GachaFinalStatProvider.AddPercent(target, source);
    }

    private static void AddCombat(
        GachaFinalCombatStats target,
        GachaFinalCombatStats source)
    {
        GachaFinalStatProvider.AddCombatBonus(target, source);
    }
}
