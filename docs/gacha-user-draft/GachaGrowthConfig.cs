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
    fileName = "CharacterGrowthConfig",
    menuName = "ASTRAL BLOOM/Characters/Growth Config"
)]
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

    public int GetRequiredExperience(int level)
    {
        return Mathf.Max(
            1,
            Mathf.RoundToInt(experienceCurve.Evaluate(level))
        );
    }
}
