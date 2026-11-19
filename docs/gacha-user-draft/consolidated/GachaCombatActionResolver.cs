using System;
using UnityEngine;

/// <summary>
/// 统一战斗行动入口。
///
/// 读取攻击者的最终属性快照，调用正式伤害计算器，
/// 返回伤害结果。不自行重新叠加角色、武器、潜能和天赋属性。
///
/// 与现有计算器的对接说明：
/// GachaCombatDamageCalculator 是静态类，不能作为字段持有，
/// 直接调用其静态方法。
/// 正式伤害入口 CalculateBasicDamage / CalculateSkillDamage /
/// CalculateUltimateDamage 内部已应用技能加成、防御与减伤，
/// 因此这里不再重复计算这些倍率，只做结果汇报用的倍率记录。
/// 暴击由这里掷骰（以便汇报是否暴击），调用计算器时传 canCrit:
/// false，避免计算器内部再掷一次造成双倍暴击。
/// </summary>
public class GachaCombatActionResolver : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private GachaFinalStatSnapshotManager snapshotManager;

    public GachaCombatActionResult Resolve(
        GachaCombatActionRequest request)
    {
        if (request == null)
            return GachaCombatActionResult.Failed("行动请求为空。");

        if (snapshotManager == null)
            return GachaCombatActionResult.Failed("属性快照管理器未绑定。");

        if (string.IsNullOrWhiteSpace(request.attackerCharacterId))
            return GachaCombatActionResult.Failed("攻击角色 ID 为空。");

        if (request.rawMultiplier < 0f ||
            float.IsNaN(request.rawMultiplier) ||
            float.IsInfinity(request.rawMultiplier))
        {
            return GachaCombatActionResult.Failed("技能倍率无效。");
        }

        if (request.targetDefense < 0f ||
            float.IsNaN(request.targetDefense) ||
            float.IsInfinity(request.targetDefense))
        {
            return GachaCombatActionResult.Failed("目标防御数值无效。");
        }

        if (float.IsNaN(request.targetDamageReduction) ||
            float.IsInfinity(request.targetDamageReduction))
        {
            return GachaCombatActionResult.Failed("目标减伤数值无效。");
        }

        // 只能读取有效快照。
        // 如果快照不存在或处于 dirty 状态，应由上层刷新后重新调用。
        GachaFinalStatSnapshot snapshot =
            snapshotManager.GetSnapshot(request.attackerCharacterId);

        if (snapshot == null)
        {
            return GachaCombatActionResult.Failed(
                "角色属性快照不存在或已失效，请先刷新属性。");
        }

        // 快照没有 finalStats 字段，用 GetStats() 取独立副本。
        GachaFinalCombatStats stats = snapshot.GetStats();

        if (stats == null || !stats.IsValid())
        {
            return GachaCombatActionResult.Failed(
                "角色最终属性无效。");
        }

        // 仅用于结果汇报。实际伤害由计算器内部应用一次技能加成，
        // 这里不把它再传给计算器，避免重复计算。
        float bonusMultiplier = 1f;

        switch (request.actionType)
        {
            case GachaCombatActionType.NormalAttack:
                bonusMultiplier = 1f;
                break;

            case GachaCombatActionType.Skill:
                bonusMultiplier =
                    1f + Mathf.Max(0f, stats.skillDamageBonus);
                break;

            case GachaCombatActionType.Ultimate:
                bonusMultiplier =
                    1f + Mathf.Max(0f, stats.ultimateDamageBonus);
                break;

            default:
                return GachaCombatActionResult.Failed(
                    "未知的战斗行动类型。");
        }

        float criticalRate = Mathf.Clamp01(stats.critRate);
        float criticalDamage = Mathf.Max(1f, stats.critDamage);

        bool isCritical = false;

        if (request.canCritical)
        {
            isCritical = request.forceCritical ||
                         UnityEngine.Random.value < criticalRate;
        }

        float criticalMultiplier =
            isCritical ? criticalDamage : 1f;

        float defenseMultiplier =
            100f / (100f + request.targetDefense);

        float reduction = Mathf.Clamp01(
            request.targetDamageReduction);

        float reductionMultiplier = 1f - reduction;

        // 减伤由计算器内部应用一次。
        // 这里只传减伤数值，不再另行乘一次。
        var defenderStats = new GachaFinalCombatStats
        {
            damageReduction = reduction
        };

        // 直接传入原始倍率。
        // 这里不调用 UI 预览倍率方法。
        // canCrit 传 false：暴击已由上面掷骰并在下面应用一次，
        // 计算器内部不再掷骰，避免双倍暴击。
        float damage;

        switch (request.actionType)
        {
            case GachaCombatActionType.NormalAttack:
                damage = GachaCombatDamageCalculator.CalculateBasicDamage(
                    stats.attack,
                    request.targetDefense,
                    request.rawMultiplier,
                    stats,
                    defenderStats,
                    false
                );
                break;

            case GachaCombatActionType.Skill:
                damage = GachaCombatDamageCalculator.CalculateSkillDamage(
                    stats.attack,
                    request.targetDefense,
                    request.rawMultiplier,
                    stats,
                    defenderStats,
                    false
                );
                break;

            case GachaCombatActionType.Ultimate:
                damage = GachaCombatDamageCalculator.CalculateUltimateDamage(
                    stats.attack,
                    request.targetDefense,
                    request.rawMultiplier,
                    stats,
                    defenderStats,
                    false
                );
                break;

            default:
                return GachaCombatActionResult.Failed(
                    "未知的战斗行动类型。");
        }

        // 暴击只在这里应用一次。
        damage *= criticalMultiplier;

        if (float.IsNaN(damage) ||
            float.IsInfinity(damage) ||
            damage < 0f)
        {
            return GachaCombatActionResult.Failed(
                "伤害计算结果无效。");
        }

        return new GachaCombatActionResult
        {
            success = true,

            attackerCharacterId = request.attackerCharacterId,
            targetId = request.targetId,

            actionType = request.actionType,

            attack = stats.attack,
            rawMultiplier = request.rawMultiplier,
            damageBonusMultiplier = bonusMultiplier,
            defenseMultiplier = defenseMultiplier,
            criticalMultiplier = criticalMultiplier,
            damageReductionMultiplier = reductionMultiplier,

            finalDamage = damage,
            isCritical = isCritical
        };
    }
}
