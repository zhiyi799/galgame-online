using UnityEngine;

/// <summary>
/// 统一受击入口。
///
/// 只负责扣除目标当前生命值，不再次计算防御与减伤。
/// 防御与减伤已由 GachaCombatActionResolver 计算过一次。
/// </summary>
public class GachaCombatantDamageReceiver : MonoBehaviour
{
    [SerializeField]
    private GachaCombatantRuntime runtime;

    public GachaCombatantRuntime Runtime => runtime;

    public bool Initialize()
    {
        return runtime != null && runtime.Initialize();
    }

    public GachaDamageTakenResult ReceiveDamage(
        float damage,
        string attackerId)
    {
        if (runtime == null)
        {
            return GachaDamageTakenResult.Failed(
                "目标战斗状态未绑定。");
        }

        if (runtime.stats == null || !runtime.stats.IsValid())
        {
            return GachaDamageTakenResult.Failed(
                "目标战斗属性无效。");
        }

        if (float.IsNaN(damage) ||
            float.IsInfinity(damage) ||
            damage < 0f)
        {
            return GachaDamageTakenResult.Failed(
                "伤害数值无效。");
        }

        if (!runtime.IsAlive)
        {
            return GachaDamageTakenResult.Failed(
                "目标已经无法继续战斗。");
        }

        float hpBefore = runtime.CurrentHP;
        float actualDamage = runtime.ReceiveDamage(damage);
        float hpAfter = runtime.CurrentHP;

        return new GachaDamageTakenResult
        {
            success = true,

            targetId = runtime.combatantId,
            attackerId = attackerId,

            requestedDamage = damage,
            actualDamage = actualDamage,

            hpBefore = hpBefore,
            hpAfter = hpAfter,

            targetDefeated = hpAfter <= 0f
        };
    }

    public float ReceiveHealing(float amount)
    {
        if (runtime == null)
            return 0f;

        return runtime.Heal(amount);
    }
}
