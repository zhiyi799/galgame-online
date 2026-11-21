// 武器成长配置自动校验器（第 15 项）— Editor 只读工具，不修改任何资产。
// 校验口径（2026-11-21 定案）：
//   Error: IsValid 不通过 / growthCurve 为 null / levelCaps 为 null、空、非正数、重复或倒序 /
//          maxLevel < 1 或低于 levelCaps 最后一项原始值 / 曲线采样出现 NaN、Infinity、负数或下降 /
//          逐整数等级实际倍率出现 NaN、Infinity、负数或倒退 / 合法等级跨突破阶段倍率不一致。
//   全部通过为 Pass。运行时容错（空表回退、空曲线返回 1）在正式检查中不算合法配置。
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class GachaWeaponConfigValidator
{
    public struct Issue
    {
        public string check;      // 检查项名称
        public int stage;         // 突破阶段，-1 表示不适用
        public int level;         // 等级，-1 表示不适用
        public float actualValue; // 实际值（倍率类检查），NaN 表示不适用
        public string message;    // 原因与期望条件
    }

    const float CurveScanStep = 0.001f;
    const float Epsilon = 0.000001f;
    const float ContinuityEpsilon = 0.0001f;

    /// <summary>
    /// 对单个配置执行全部正式检查，返回 Error 列表（空列表 = Pass）。只读，不修改资产。
    /// </summary>
    public static List<Issue> Validate(GachaWeaponConfig config)
    {
        var issues = new List<Issue>();
        if (config == null)
        {
            issues.Add(Make("配置引用", -1, -1, float.NaN, "配置为空引用"));
            return issues;
        }

        // 1) 现有最低限度检查
        if (!config.IsValid())
            issues.Add(Make("IsValid", -1, -1, float.NaN,
                "现有 IsValid() 不通过（weaponId 为空、等级表为空、首项非正、或材料成本数量不足）"));

        // 2) growthCurve 不得为 null（返回 1f 只是运行时容错，正式配置必须提供成长曲线）
        bool curveOk = config.growthCurve != null;
        if (!curveOk)
            issues.Add(Make("growthCurve", -1, -1, float.NaN,
                "growthCurve 为 null：武器将完全失去等级成长，正式配置必须提供成长曲线"));

        // 3) levelCaps 原始值合法性（直接读原表，不经过 GetLevelCap 的截断）
        var caps = config.levelCaps;
        bool capsOk = true;
        if (caps == null || caps.Count == 0)
        {
            capsOk = false;
            issues.Add(Make("levelCaps", -1, -1, float.NaN,
                "levelCaps 为 null 或空表：空表只是运行时容错回退，正式配置必须提供等级表"));
        }
        else
        {
            for (int i = 0; i < caps.Count; i++)
            {
                if (caps[i] <= 0)
                {
                    capsOk = false;
                    issues.Add(Make("levelCaps", i, -1, caps[i],
                        "levelCaps[" + i + "] = " + caps[i] + "，等级上限必须为正数"));
                }
                if (i > 0 && caps[i] <= caps[i - 1])
                {
                    capsOk = false;
                    issues.Add(Make("levelCaps", i, -1, caps[i],
                        "levelCaps[" + i + "] = " + caps[i] + " 不大于 levelCaps[" + (i - 1) + "] = " + caps[i - 1] +
                        "，等级表必须严格递增（重复或倒序不合规）"));
                }
            }
        }

        // 4) maxLevel 合法性：不小于 1，且不低于 levelCaps 最后一项原始值
        if (config.maxLevel < 1)
            issues.Add(Make("maxLevel", -1, -1, config.maxLevel,
                "maxLevel = " + config.maxLevel + "，必须不小于 1"));
        if (caps != null && caps.Count > 0 && config.maxLevel < caps[caps.Count - 1])
            issues.Add(Make("maxLevel", -1, -1, config.maxLevel,
                "maxLevel = " + config.maxLevel + " 低于最高阶段上限原始值 " + caps[caps.Count - 1] +
                "，配置会被截断，必须不低于最高阶段上限"));

        // 5) 曲线本身扫描：归一化区间 [0,1]，步长 0.001，检查关键帧之间的插值
        if (curveOk)
        {
            float prev = config.growthCurve.Evaluate(0f);
            if (!IsFiniteNonNegative(prev, out string why0))
                issues.Add(Make("曲线倍率", -1, -1, prev, "growthCurve 在 t=0 处" + why0));
            for (float t = CurveScanStep; t <= 1.0001f; t += CurveScanStep)
            {
                float tc = Mathf.Clamp01(t);
                float v = config.growthCurve.Evaluate(tc);
                if (!IsFiniteNonNegative(v, out string why))
                {
                    issues.Add(Make("曲线倍率", -1, -1, v,
                        "growthCurve 在 t=" + tc.ToString("F3") + " 处" + why));
                }
                else if (v < prev - Epsilon)
                {
                    issues.Add(Make("曲线单调性", -1, -1, v,
                        "growthCurve 在 t=" + tc.ToString("F3") + " 处下降：" + prev + " -> " + v +
                        "，成长曲线必须单调不减"));
                }
                prev = v;
            }
        }

        // 6) 逐整数等级实际倍率扫描 + 7) 跨阶段连续性（只比较双方都合法的相同等级）
        if (capsOk)
        {
            int maxAsc = config.MaxAscension;
            var stageMultipliers = new List<float[]>();
            for (int asc = 0; asc <= maxAsc; asc++)
            {
                int cap = config.GetLevelCap(asc);
                var row = new float[cap + 1];
                float prevM = float.NegativeInfinity;
                for (int lv = 1; lv <= cap; lv++)
                {
                    float m = config.GetGrowthMultiplier(lv, asc);
                    row[lv] = m;
                    if (!IsFiniteNonNegative(m, out string why))
                    {
                        issues.Add(Make("实际成长倍率", asc, lv, m,
                            "突破 " + asc + "、等级 " + lv + " 的实际倍率" + why));
                    }
                    else if (m < prevM - Epsilon)
                    {
                        issues.Add(Make("实际成长倍率", asc, lv, m,
                            "突破 " + asc + "、等级 " + lv + " 倍率倒退：" + prevM + " -> " + m +
                            "，等级间倍率不得下降"));
                    }
                    prevM = m;
                }
                stageMultipliers.Add(row);
            }

            for (int asc = 0; asc < maxAsc; asc++)
            {
                int bothLegalCap = Mathf.Min(config.GetLevelCap(asc), config.GetLevelCap(asc + 1));
                for (int lv = 1; lv <= bothLegalCap; lv++)
                {
                    float a = stageMultipliers[asc][lv];
                    float b = stageMultipliers[asc + 1][lv];
                    if (Mathf.Abs(a - b) > ContinuityEpsilon)
                        issues.Add(Make("阶段连续性", asc, lv, b,
                            "等级 " + lv + " 在突破 " + asc + " 与突破 " + (asc + 1) + " 下倍率不一致：" +
                            a + " vs " + b + "，合法等级跨突破阶段倍率必须一致"));
                }
            }
        }

        return issues;
    }

    static Issue Make(string check, int stage, int level, float actual, string message)
    {
        return new Issue { check = check, stage = stage, level = level, actualValue = actual, message = message };
    }

    static bool IsFiniteNonNegative(float v, out string why)
    {
        if (float.IsNaN(v)) { why = "出现 NaN"; return false; }
        if (float.IsInfinity(v)) { why = "出现 Infinity"; return false; }
        if (v < 0f) { why = "出现负值 " + v; return false; }
        why = "";
        return true;
    }

    static string Describe(GachaWeaponConfig config)
    {
        return "武器 " + (string.IsNullOrEmpty(config.weaponId) ? "<未填 weaponId>" : config.weaponId) +
               "（" + (string.IsNullOrEmpty(config.weaponName) ? "未命名" : config.weaponName) + "）";
    }

    static void Report(GachaWeaponConfig config, List<Issue> issues)
    {
        if (issues.Count == 0)
        {
            Debug.Log("[武器配置校验] PASS " + Describe(config));
            return;
        }
        foreach (var issue in issues)
            Debug.LogError("[武器配置校验] Error " + Describe(config) + "｜" + issue.check + "｜" + issue.message);
    }

    // ---------- 入口一：单个检查（选中资产后执行） ----------

    [MenuItem("ASTRAL BLOOM/校验选中的武器配置", false, 100)]
    static void ValidateSelectedMenu()
    {
        ValidateSelected();
    }

    [MenuItem("Assets/ASTRAL BLOOM/校验武器配置", false, 100)]
    static void ValidateSelectedAssetMenu()
    {
        ValidateSelected();
    }

    static void ValidateSelected()
    {
        int count = 0;
        foreach (var obj in Selection.objects)
        {
            var config = obj as GachaWeaponConfig;
            if (config == null) continue;
            count++;
            Report(config, Validate(config));
        }
        if (count == 0)
            Debug.LogWarning("[武器配置校验] 未选中任何 GachaWeaponConfig 资产。");
    }

    // ---------- 入口二：批量检查（扫描全部武器配置资产） ----------

    [MenuItem("ASTRAL BLOOM/校验全部武器配置", false, 101)]
    static void ValidateAllMenu()
    {
        var guids = AssetDatabase.FindAssets("t:GachaWeaponConfig");
        int passed = 0, failed = 0;
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var config = AssetDatabase.LoadAssetAtPath<GachaWeaponConfig>(path);
            if (config == null) continue;
            var issues = Validate(config);
            Report(config, issues);
            if (issues.Count == 0) passed++; else failed++;
        }
        Debug.Log("[武器配置校验] 批量完成：总数 " + (passed + failed) + "，通过 " + passed + "，失败 " + failed + "。");
    }
}
