using System.Collections.Generic;
using UnityEngine;

public class GachaMaterialStageManager : MonoBehaviour
{
    [Header("系统依赖")]
    public GachaMaterialInventory inventory;
    public GachaManager gachaManager;

    [Header("关卡配置")]
    public List<GachaMaterialStageConfig> stages =
        new List<GachaMaterialStageConfig>();

    private Dictionary<string, GachaMaterialStageConfig> stageLookup;

    private void Awake()
    {
        BuildLookup();
    }

    private void BuildLookup()
    {
        stageLookup =
            new Dictionary<string, GachaMaterialStageConfig>();

        foreach (GachaMaterialStageConfig stage in stages)
        {
            if (stage == null || !stage.IsValid())
                continue;

            if (!stageLookup.ContainsKey(stage.stageId))
                stageLookup.Add(stage.stageId, stage);
        }
    }

    public GachaMaterialStageConfig GetStage(string stageId)
    {
        if (stageLookup == null)
            BuildLookup();

        if (string.IsNullOrWhiteSpace(stageId))
            return null;

        stageLookup.TryGetValue(stageId, out var stage);

        return stage;
    }

    public bool IsFirstClear(string stageId)
    {
        if (gachaManager == null ||
            gachaManager.Save == null)
            return false;

        if (gachaManager.Save.completedMaterialStages == null)
            return true;

        return !gachaManager.Save.completedMaterialStages
            .Contains(stageId);
    }

    public bool CompleteStage(string stageId)
    {
        GachaMaterialStageConfig config = GetStage(stageId);

        if (config == null)
            return false;

        return CompleteStage(config);
    }

    public bool CompleteStage(GachaMaterialStageConfig config)
    {
        if (config == null ||
            gachaManager == null ||
            gachaManager.Save == null ||
            inventory == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.stageId))
            return false;

        var save = gachaManager.Save;

        if (save.completedMaterialStages == null)
        {
            save.completedMaterialStages = new List<string>();
        }

        bool firstClear =
            !save.completedMaterialStages.Contains(config.stageId);

        var rewards = new List<GachaMaterialReward>();

        if (config.clearRewards != null)
            rewards.AddRange(config.clearRewards);

        if (firstClear && config.firstClearRewards != null)
            rewards.AddRange(config.firstClearRewards);

        // 先整体预检。
        if (rewards.Count > 0 && !inventory.CanAddBatch(rewards))
            return false;

        // 再整体发放，不立即保存。
        if (rewards.Count > 0 &&
            !inventory.TryAddBatch(rewards, false))
        {
            return false;
        }

        // 发奖成功后再记录首次通关。
        if (firstClear)
            save.completedMaterialStages.Add(config.stageId);

        // 统一保存一次。
        gachaManager.SaveGame();

        return true;
    }
}
