using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器类型。枚举数值对应 v2 原始定义（0~6），新类型从 7 起追加，
/// 不要删除或重排旧成员，否则已有 ScriptableObject 资产会错映射。
/// </summary>
public enum WeaponType
{
    Sword,       // 0
    GreatSword,  // 1
    Pistol,      // 2
    Rectifier,   // 3
    Gauntlet,    // 4
    Spear,       // 5
    Catalyst,    // 6
    Broadblade,  // 7
    Pistols,     // 8
    Gauntlets,   // 9
    Other        // 10
}

[CreateAssetMenu(
    fileName = "NewWeaponConfig",
    menuName = "ASTRAL BLOOM/Weapon Config"
)]
public class GachaWeaponConfig : ScriptableObject
{
    [Header("基础信息")]
    public string weaponId;
    public string weaponName;
    public WeaponType weaponType;
    [TextArea] public string description;
    public Sprite icon;
    public int rarity = 5;

    [Header("基础属性")]
    public float baseAttack;
    public float baseHP;
    public float baseDefense;

    [Tooltip("基础暴击率，使用小数比例：0.05 = 5%")]
    [Range(0f, 1f)]
    public float baseCritRate;

    [Header("等级成长")]
    [Tooltip("等级成长倍率曲线，横轴为当前等级/当前等级上限")]
    public AnimationCurve growthCurve =
        AnimationCurve.Linear(0f, 1f, 1f, 2f);

    public int maxLevel = 90;

    [Header("突破等级配置")]
    [Tooltip("每个突破阶段的等级上限，索引0代表未突破")]
    public List<int> levelCaps = new List<int>
    {
        20, 40, 50, 60, 70, 80, 90
    };

    [Header("突破材料")]
    public string ascensionMaterialId;

    [Tooltip("索引0代表升至突破1阶所需数量")]
    public List<int> ascensionMaterialCosts =
        new List<int> { 1, 2, 3, 4, 5, 6 };

    [Header("精炼材料")]
    public string refinementMaterialId;

    [Tooltip("索引0代表升至精炼2阶所需数量")]
    public List<int> refinementMaterialCosts =
        new List<int> { 1, 2, 3, 4 };

    [Header("武器被动")]
    public GachaWeaponPassive passive =
        new GachaWeaponPassive();

    public int MaxAscension
    {
        get
        {
            return levelCaps == null
                ? 0
                : Mathf.Max(0, levelCaps.Count - 1);
        }
    }

    public int GetLevelCap(int ascension)
    {
        if (levelCaps == null || levelCaps.Count == 0)
            return Mathf.Max(1, maxLevel);

        ascension = Mathf.Clamp(
            ascension,
            0,
            levelCaps.Count - 1
        );

        return Mathf.Min(
            Mathf.Max(1, levelCaps[ascension]),
            Mathf.Max(1, maxLevel)
        );
    }

    public int GetAscensionMaterialCost(int targetAscension)
    {
        int index = targetAscension - 1;

        if (ascensionMaterialCosts == null ||
            index < 0 ||
            index >= ascensionMaterialCosts.Count)
            return -1;

        return Mathf.Max(0, ascensionMaterialCosts[index]);
    }

    public int GetRefinementMaterialCost(int targetRefinement)
    {
        int index = targetRefinement - 2;

        if (refinementMaterialCosts == null ||
            index < 0 ||
            index >= refinementMaterialCosts.Count)
            return -1;

        return Mathf.Max(0, refinementMaterialCosts[index]);
    }

    public float GetGrowthMultiplier(int level, int ascension)
    {
        if (growthCurve == null)
            return 1f;

        // 没有突破等级表时，保留原有的基础计算行为。
        if (levelCaps == null || levelCaps.Count == 0)
        {
            int cap = Mathf.Max(1, maxLevel);
            int fallbackLevel = Mathf.Clamp(level, 1, cap);

            float normalized = cap <= 1
                ? 1f
                : (float)fallbackLevel / cap;

            return Mathf.Max(
                0f,
                growthCurve.Evaluate(normalized)
            );
        }

        // 限制突破阶段，防止数组越界。
        int maxAscension = levelCaps.Count - 1;

        int currentAscension = Mathf.Clamp(
            ascension,
            0,
            maxAscension
        );

        int firstCap = GetLevelCap(0);
        int finalCap = GetLevelCap(currentAscension);

        // 当前等级不能超过当前突破阶段的上限。
        int currentLevel = Mathf.Clamp(
            level,
            1,
            finalCap
        );

        // 第一阶段完全保留原有曲线。
        if (currentLevel <= firstCap)
        {
            float normalized = firstCap <= 1
                ? 1f
                : (float)currentLevel / firstCap;

            return Mathf.Max(
                0f,
                growthCurve.Evaluate(normalized)
            );
        }

        // 第一阶段达到上限时的累计倍率。
        float accumulatedMultiplier = Mathf.Max(
            0f,
            growthCurve.Evaluate(1f)
        );

        int previousCap = firstCap;

        // 从第二阶段开始，逐段累计成长。
        for (int stage = 1; stage <= currentAscension; stage++)
        {
            int stageCap = GetLevelCap(stage);

            // 防止异常配置造成零长度或倒退区间。
            if (stageCap <= previousCap)
                continue;

            float startNormalized = Mathf.Clamp01(
                (float)previousCap / stageCap
            );

            float startCurveValue = growthCurve.Evaluate(
                startNormalized
            );

            // 当前等级处于本阶段。
            if (currentLevel <= stageCap)
            {
                float currentNormalized = Mathf.Clamp01(
                    (float)currentLevel / stageCap
                );

                float currentCurveValue = growthCurve.Evaluate(
                    currentNormalized
                );

                float growth = Mathf.Max(
                    0f,
                    currentCurveValue - startCurveValue
                );

                return accumulatedMultiplier + growth;
            }

            // 当前等级已经超过本阶段上限，
            // 将整个阶段的成长累加，再进入下一阶段。
            float stageGrowth = Mathf.Max(
                0f,
                growthCurve.Evaluate(1f) - startCurveValue
            );

            accumulatedMultiplier += stageGrowth;
            previousCap = stageCap;
        }

        return accumulatedMultiplier;
    }

    /// <summary>
    /// 按等级与突破计算武器攻击。UI 显示某把武器实例时请用双参数版，
    /// 传入该武器的实际突破等级；单参数版按满突破（等级上限 90）换算。
    /// </summary>
    public float GetAttack(int level, int ascension)
    {
        return baseAttack * GetGrowthMultiplier(level, ascension);
    }

    public float GetAttack(int level)
    {
        return GetAttack(level, MaxAscension);
    }

    public float GetHP(int level, int ascension)
    {
        return baseHP * GetGrowthMultiplier(level, ascension);
    }

    public float GetHP(int level)
    {
        return GetHP(level, MaxAscension);
    }

    public float GetDefense(int level, int ascension)
    {
        return baseDefense * GetGrowthMultiplier(level, ascension);
    }

    public float GetDefense(int level)
    {
        return GetDefense(level, MaxAscension);
    }

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(weaponId)
            && levelCaps != null
            && levelCaps.Count > 0
            && levelCaps[0] > 0
            && refinementMaterialCosts != null
            && refinementMaterialCosts.Count >= 4
            && ascensionMaterialCosts != null
            && ascensionMaterialCosts.Count >= MaxAscension;
    }
}
