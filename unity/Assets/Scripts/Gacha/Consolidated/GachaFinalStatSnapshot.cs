using System;

/// <summary>
/// 最终战斗属性快照。
///
/// 创建后不允许外部直接修改内部数据。
/// 如需修改属性，必须重新计算快照。
/// </summary>
public sealed class GachaFinalStatSnapshot
{
    private readonly GachaFinalCombatStats stats;

    public GachaFinalStatSnapshot(
        GachaFinalCombatStats source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (!source.IsValid())
            throw new ArgumentException(
                "最终属性数据无效。",
                nameof(source)
            );

        stats = source.Clone();
    }

    /// <summary>
    /// 返回独立副本，避免外部修改内部快照。
    /// </summary>
    public GachaFinalCombatStats GetStats()
    {
        return stats.Clone();
    }

    public float Attack => stats.attack;
    public float MaxHP => stats.maxHP;
    public float Defense => stats.defense;
    public float Speed => stats.speed;

    public float CritRate => stats.critRate;
    public float CritDamage => stats.critDamage;
    public float EnergyRecharge => stats.energyRecharge;

    public float HealingBonus => stats.healingBonus;
    public float DamageReduction => stats.damageReduction;

    public float SkillDamageBonus =>
        stats.skillDamageBonus;

    public float UltimateDamageBonus =>
        stats.ultimateDamageBonus;
}
