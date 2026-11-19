using System;
using UnityEngine;

/// <summary>
/// 战斗中的实时生命值状态。
/// 保存战斗中的当前生命值，不修改角色养成存档。
/// </summary>
[Serializable]
public class GachaCombatantRuntime
{
    public string combatantId;

    public GachaCombatantStats stats = new GachaCombatantStats();

    [SerializeField]
    private float currentHP;

    [SerializeField]
    private bool initialized;

    public float CurrentHP => currentHP;

    public bool IsAlive => currentHP > 0f;

    public bool Initialize()
    {
        if (stats == null || !stats.IsValid())
            return false;

        currentHP = stats.maxHP;
        initialized = true;

        return true;
    }

    public bool SetCurrentHP(float value)
    {
        if (!initialized || stats == null || !stats.IsValid())
            return false;

        if (float.IsNaN(value) || float.IsInfinity(value))
            return false;

        currentHP = Mathf.Clamp(value, 0f, stats.maxHP);
        return true;
    }

    public float ReceiveDamage(float damage)
    {
        if (!initialized || !IsAlive)
            return 0f;

        if (float.IsNaN(damage) ||
            float.IsInfinity(damage) ||
            damage < 0f)
        {
            return 0f;
        }

        float actualDamage = Mathf.Min(currentHP, damage);

        currentHP = Mathf.Max(0f, currentHP - actualDamage);

        return actualDamage;
    }

    public float Heal(float amount)
    {
        if (!initialized || !IsAlive)
            return 0f;

        if (float.IsNaN(amount) ||
            float.IsInfinity(amount) ||
            amount < 0f)
        {
            return 0f;
        }

        float missingHP = Mathf.Max(0f, stats.maxHP - currentHP);
        float actualHealing = Mathf.Min(missingHP, amount);

        currentHP = Mathf.Min(stats.maxHP, currentHP + actualHealing);

        return actualHealing;
    }

    public void Reset()
    {
        currentHP = 0f;
        initialized = false;
    }
}
