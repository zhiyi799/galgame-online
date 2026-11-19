using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewMaterialStage",
    menuName = "ASTRAL BLOOM/Material Stage Config"
)]
public class GachaMaterialStageConfig : ScriptableObject
{
    [Header("关卡信息")]
    public string stageId;
    public string stageName;

    [TextArea]
    public string description;

    [Header("推荐等级")]
    [Min(1)]
    public int recommendedLevel = 1;

    [Header("通关奖励")]
    public List<GachaMaterialReward> clearRewards =
        new List<GachaMaterialReward>();

    [Header("首次通关奖励")]
    public List<GachaMaterialReward> firstClearRewards =
        new List<GachaMaterialReward>();

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(stageId);
    }
}
