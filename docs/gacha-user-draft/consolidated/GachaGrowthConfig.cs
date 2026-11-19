using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GachaAscensionRequirement
{
    public int requiredLevel = 20;

    public string materialId = "ascension_material";

    public int materialAmount = 5;
}

[CreateAssetMenu(
    fileName = "GachaGrowthConfig",
    menuName = "ASTRAL BLOOM/Characters/Growth Config")]
public class GachaGrowthConfig : ScriptableObject
{
    [Header("等级限制")]
    public int maxLevel = 90;

    [Header("每级升级经验")]
    public AnimationCurve experienceCurve =
        AnimationCurve.Linear(1, 100, 90, 10000);

    [Header("突破配置")]
    public List<GachaAscensionRequirement> ascensions =
        new List<GachaAscensionRequirement>();

    [Header("角色成长曲线（倍率：1 级 = x1）")]
    [Tooltip("batch19 已确定数值：1 级 x1，90 级 x5。")]
    public AnimationCurve hpGrowth =
        AnimationCurve.Linear(1, 1, 90, 5);

    [Tooltip("batch19 已确定数值：1 级 x1，90 级 x5。")]
    public AnimationCurve attackGrowth =
        AnimationCurve.Linear(1, 1, 90, 5);

    [Tooltip("batch19 已确定数值：1 级 x1，90 级 x5。")]
    public AnimationCurve defenseGrowth =
        AnimationCurve.Linear(1, 1, 90, 5);

    [Tooltip("batch19 已确定数值：1 级 x1，90 级 x1.2。")]
    public AnimationCurve speedGrowth =
        AnimationCurve.Linear(1, 1, 90, 1.2f);

    [Header("突破属性加成（按突破阶查表，比例）")]
    [Tooltip("batch19 已确定数值：每突破 1 阶 +8%，线性累加。")]
    public float[] ascensionHPBonus =
    {
        0f, 0.08f, 0.16f, 0.24f, 0.32f, 0.40f, 0.48f
    };

    [Tooltip("batch19 已确定数值：每突破 1 阶 +5%，线性累加。")]
    public float[] ascensionAttackBonus =
    {
        0f, 0.05f, 0.10f, 0.15f, 0.20f, 0.25f, 0.30f
    };

    [Tooltip("batch19 已确定数值：每突破 1 阶 +5%，线性累加。")]
    public float[] ascensionDefenseBonus =
    {
        0f, 0.05f, 0.10f, 0.15f, 0.20f, 0.25f, 0.30f
    };

    [Tooltip("速度不随突破成长，保留数组以备后续调整。")]
    public float[] ascensionSpeedBonus =
    {
        0f, 0f, 0f, 0f, 0f, 0f, 0f
    };

    [Header("突破等级上限")]
    public int maxAscension = 6;

    public int GetRequiredExperience(int level)
    {
        return Mathf.Max(
            1,
            Mathf.RoundToInt(experienceCurve.Evaluate(level))
        );
    }

    /// <summary>
    /// 角色等级上限，统一公式：20 + 突破等级 × 10。
    /// 与 v2 成长管理器一致，不再使用固定等级表。
    /// </summary>
    public int GetLevelCap(int ascension)
    {
        int safeAscension = Mathf.Clamp(
            ascension,
            0,
            maxAscension
        );

        return 20 + safeAscension * 10;
    }

    public float GetGrowth(
        AnimationCurve curve,
        int level)
    {
        if (curve == null)
            return 1f;

        return Mathf.Max(0f, curve.Evaluate(
            Mathf.Max(1, level)));
    }

    public float GetAscensionBonus(
        float[] values,
        int ascension)
    {
        if (values == null || values.Length == 0)
            return 0f;

        int index = Mathf.Clamp(
            ascension,
            0,
            values.Length - 1);

        return values[index];
    }

    public bool IsValid()
    {
        return hpGrowth != null &&
               attackGrowth != null &&
               defenseGrowth != null &&
               speedGrowth != null;
    }
}
