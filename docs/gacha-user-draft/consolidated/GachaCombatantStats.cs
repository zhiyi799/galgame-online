using System;
using UnityEngine;

[Serializable]
public class GachaCombatantStats
{
    [Min(1f)]
    public float maxHP = 1000f;

    [Min(0f)]
    public float defense = 100f;

    [Range(0f, 1f)]
    public float damageReduction = 0f;

    [Min(0f)]
    public float speed = 100f;

    public bool IsValid()
    {
        return IsFinite(maxHP) &&
               IsFinite(defense) &&
               IsFinite(damageReduction) &&
               IsFinite(speed) &&
               maxHP > 0f &&
               defense >= 0f &&
               damageReduction >= 0f &&
               damageReduction <= 1f &&
               speed >= 0f;
    }

    private bool IsFinite(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }
}
