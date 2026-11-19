using System.Collections.Generic;
using UnityEngine;

public class GachaMaterialInventory : MonoBehaviour
{
    [Header("存档依赖")]
    public GachaManager gachaManager;

    [Header("材料配置")]
    public List<GachaMaterialConfig> materialConfigs =
        new List<GachaMaterialConfig>();

    private Dictionary<string, GachaMaterialConfig> configLookup;

    private void Awake()
    {
        BuildLookup();
        NormalizeInventory();
    }

    private void BuildLookup()
    {
        configLookup =
            new Dictionary<string, GachaMaterialConfig>();

        foreach (GachaMaterialConfig config in materialConfigs)
        {
            if (config == null || !config.IsValid())
                continue;

            if (!configLookup.ContainsKey(config.materialId))
                configLookup.Add(config.materialId, config);
        }
    }

    private GachaSaveData GetSaveData()
    {
        return gachaManager == null
            ? null
            : gachaManager.Save;
    }

    private void NormalizeInventory()
    {
        GachaSaveData data = GetSaveData();

        if (data == null)
            return;

        if (data.materials == null)
            data.materials = new List<GachaMaterialSave>();

        data.materials.RemoveAll(
            x => x == null ||
                 string.IsNullOrWhiteSpace(x.materialId) ||
                 x.amount < 0
        );

        Save();
    }

    private void Save()
    {
        if (gachaManager != null)
            gachaManager.SaveGame();
    }

    public GachaMaterialConfig GetConfig(string materialId)
    {
        if (configLookup == null)
            BuildLookup();

        if (string.IsNullOrWhiteSpace(materialId))
            return null;

        configLookup.TryGetValue(materialId, out var config);

        return config;
    }

    private GachaMaterialSave GetEntry(string materialId)
    {
        GachaSaveData data = GetSaveData();

        if (data == null || data.materials == null)
            return null;

        return data.materials.Find(
            x => x != null && x.materialId == materialId
        );
    }

    public int GetAmount(string materialId)
    {
        GachaMaterialSave entry = GetEntry(materialId);

        return entry == null ? 0 : Mathf.Max(0, entry.amount);
    }

    public bool HasEnough(string materialId, int amount)
    {
        if (amount < 0 || string.IsNullOrWhiteSpace(materialId))
            return false;

        return GetAmount(materialId) >= amount;
    }

    public bool Add(string materialId, int amount)
    {
        if (amount <= 0)
            return false;

        GachaSaveData data = GetSaveData();

        if (data == null)
            return false;

        GachaMaterialConfig config = GetConfig(materialId);

        if (config == null)
        {
            Debug.LogWarning("未找到材料配置：" + materialId);
            return false;
        }

        if (data.materials == null)
            data.materials = new List<GachaMaterialSave>();

        GachaMaterialSave entry = GetEntry(materialId);

        if (entry == null)
        {
            entry = new GachaMaterialSave
            {
                materialId = materialId,
                amount = 0
            };

            data.materials.Add(entry);
        }

        long newAmount = (long)entry.amount + amount;

        entry.amount = (int)System.Math.Min(
            newAmount,
            config.maxStack
        );

        Save();
        return true;
    }

    public bool TrySpend(string materialId, int amount)
    {
        if (amount <= 0 ||
            string.IsNullOrWhiteSpace(materialId))
            return false;

        GachaMaterialSave entry = GetEntry(materialId);

        if (entry == null || entry.amount < amount)
            return false;

        entry.amount -= amount;

        Save();
        return true;
    }

    public bool CanAddBatch(
        System.Collections.Generic.List<GachaMaterialReward> rewards)
    {
        GachaSaveData data = GetSaveData();

        if (data == null)
            return false;

        if (rewards == null)
            return true;

        var totals =
            new System.Collections.Generic.Dictionary<string, long>();

        foreach (GachaMaterialReward reward in rewards)
        {
            if (reward == null || !reward.IsValid())
                return false;

            GachaMaterialConfig config =
                GetConfig(reward.materialId);

            if (config == null)
                return false;

            if (!totals.ContainsKey(reward.materialId))
                totals[reward.materialId] = 0;

            totals[reward.materialId] += reward.amount;
        }

        foreach (var pair in totals)
        {
            long current = GetAmount(pair.Key);

            if (current + pair.Value > GetConfig(pair.Key).maxStack)
                return false;
        }

        return true;
    }

    public bool TryAddBatch(
        List<GachaMaterialReward> rewards,
        bool saveImmediately = true)
    {
        if (rewards == null || rewards.Count == 0)
            return false;

        if (gachaManager == null || gachaManager.Save == null)
            return false;

        var data = gachaManager.Save;

        if (data.materials == null)
            data.materials = new List<GachaMaterialSave>();

        if (!CanAddBatch(rewards))
            return false;

        // 先汇总相同材料，避免重复 ID 分散处理。
        var totals = new Dictionary<string, long>();

        foreach (var reward in rewards)
        {
            if (reward == null || !reward.IsValid())
                return false;

            if (!totals.ContainsKey(reward.materialId))
                totals[reward.materialId] = 0;

            totals[reward.materialId] += reward.amount;

            if (totals[reward.materialId] > int.MaxValue)
                return false;
        }

        foreach (var pair in totals)
        {
            var entry = data.materials.Find(
                x => x != null && x.materialId == pair.Key
            );

            if (entry == null)
            {
                entry = new GachaMaterialSave
                {
                    materialId = pair.Key,
                    amount = 0
                };

                data.materials.Add(entry);
            }

            entry.amount += (int)pair.Value;
        }

        if (saveImmediately)
            gachaManager.SaveGame();

        return true;
    }

    public bool CanSpendBatch(
        System.Collections.Generic.List<GachaMaterialCost> costs)
    {
        if (costs == null || costs.Count == 0)
            return false;

        GachaSaveData data = GetSaveData();

        if (data == null)
            return false;

        var totals =
            new System.Collections.Generic.Dictionary<string, long>();

        foreach (GachaMaterialCost cost in costs)
        {
            if (cost == null || !cost.IsValid())
                return false;

            if (!totals.ContainsKey(cost.materialId))
                totals[cost.materialId] = 0;

            totals[cost.materialId] += cost.amount;

            if (totals[cost.materialId] > int.MaxValue)
                return false;
        }

        // 只检查，不修改库存。
        foreach (var pair in totals)
        {
            if (!HasEnough(pair.Key, (int)pair.Value))
                return false;
        }

        return true;
    }

    public bool TrySpendBatch(
        System.Collections.Generic.List<GachaMaterialCost> costs,
        bool saveImmediately = true)
    {
        if (!CanSpendBatch(costs))
            return false;

        GachaSaveData data = GetSaveData();

        if (data == null)
            return false;

        var totals =
            new System.Collections.Generic.Dictionary<string, long>();

        foreach (GachaMaterialCost cost in costs)
        {
            if (!totals.ContainsKey(cost.materialId))
                totals[cost.materialId] = 0;

            totals[cost.materialId] += cost.amount;
        }

        // 统一扣除。
        foreach (var pair in totals)
        {
            GachaMaterialSave entry = GetEntry(pair.Key);

            if (entry == null)
                return false;

            entry.amount -= (int)pair.Value;
        }

        if (saveImmediately)
            Save();

        return true;
    }

    public bool SetAmount(string materialId, int amount)
    {
        if (amount < 0)
            return false;

        GachaSaveData data = GetSaveData();

        if (data == null)
            return false;

        GachaMaterialConfig config = GetConfig(materialId);

        if (config == null)
            return false;

        if (data.materials == null)
            data.materials = new List<GachaMaterialSave>();

        GachaMaterialSave entry = GetEntry(materialId);

        if (entry == null)
        {
            entry = new GachaMaterialSave
            {
                materialId = materialId,
                amount = 0
            };

            data.materials.Add(entry);
        }

        entry.amount = Mathf.Clamp(amount, 0, config.maxStack);

        Save();
        return true;
    }

    public List<GachaMaterialSave> GetAllMaterials()
    {
        GachaSaveData data = GetSaveData();

        if (data == null || data.materials == null)
            return new List<GachaMaterialSave>();

        return new List<GachaMaterialSave>(data.materials);
    }

    public GachaMaterialConfig GetConfigForEntry(
        GachaMaterialSave entry)
    {
        if (entry == null)
            return null;

        return GetConfig(entry.materialId);
    }
}
