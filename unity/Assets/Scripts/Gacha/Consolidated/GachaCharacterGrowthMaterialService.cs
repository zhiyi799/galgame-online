using System.Collections.Generic;
using UnityEngine;

public class GachaCharacterGrowthMaterialService : MonoBehaviour
{
    [Header("材料库存")]
    public GachaMaterialInventory inventory;

    [Header("角色成长配置")]
    public List<GachaCharacterGrowthMaterialConfig> configs =
        new List<GachaCharacterGrowthMaterialConfig>();

    private Dictionary<string, GachaCharacterGrowthMaterialConfig>
        lookup;

    private void Awake()
    {
        BuildLookup();
    }

    private void BuildLookup()
    {
        lookup =
            new Dictionary<string, GachaCharacterGrowthMaterialConfig>();

        foreach (var config in configs)
        {
            if (config == null ||
                string.IsNullOrWhiteSpace(config.characterId))
                continue;

            if (!lookup.ContainsKey(config.characterId))
                lookup.Add(config.characterId, config);
        }
    }

    public GachaCharacterGrowthMaterialConfig GetConfig(
        string characterId)
    {
        if (lookup == null)
            BuildLookup();

        if (string.IsNullOrWhiteSpace(characterId))
            return null;

        lookup.TryGetValue(characterId, out var config);

        return config;
    }

    public bool TrySpendExperienceMaterials(
        string characterId,
        int materialCount)
    {
        if (inventory == null || materialCount <= 0)
            return false;

        var config = GetConfig(characterId);

        if (config == null ||
            string.IsNullOrWhiteSpace(config.experienceMaterialId))
            return false;

        return inventory.TrySpend(
            config.experienceMaterialId,
            materialCount
        );
    }

    public bool TrySpendAscensionMaterials(
        string characterId,
        int targetAscension)
    {
        if (inventory == null || targetAscension <= 0)
            return false;

        var config = GetConfig(characterId);

        if (config == null)
            return false;

        List<GachaMaterialCost> costs =
            config.GetAscensionCosts(targetAscension);

        if (costs == null || costs.Count == 0)
            return false;

        return inventory.TrySpendBatch(costs);
    }

    public bool TryConsumeExperience(
        string characterId,
        int materialCount,
        out int gainedExperience)
    {
        gainedExperience = 0;

        if (inventory == null || materialCount <= 0)
            return false;

        var config = GetConfig(characterId);

        if (config == null ||
            string.IsNullOrWhiteSpace(config.experienceMaterialId) ||
            config.experiencePerMaterial <= 0)
            return false;

        long totalExperience =
            (long)materialCount * config.experiencePerMaterial;

        if (totalExperience > int.MaxValue)
            return false;

        if (!inventory.TrySpend(
            config.experienceMaterialId,
            materialCount))
            return false;

        gainedExperience = (int)totalExperience;
        return true;
    }

    public int GetExperienceFromMaterials(
        string characterId,
        int materialCount)
    {
        if (materialCount <= 0)
            return 0;

        var config = GetConfig(characterId);

        if (config == null)
            return 0;

        long experience =
            (long)materialCount *
            config.experiencePerMaterial;

        return experience > int.MaxValue
            ? int.MaxValue
            : (int)experience;
    }
}
