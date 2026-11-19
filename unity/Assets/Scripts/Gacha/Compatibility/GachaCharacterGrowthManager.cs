using UnityEngine;

public class GachaCharacterGrowthManager : MonoBehaviour
{
    [Header("数据")]
    public GachaManager manager;
    public GachaGrowthConfig config;

    public GachaFinalStatSnapshotManager snapshotManager;

    public GachaCharacterGrowthSave GetGrowth(string characterId)
    {
        if (manager == null || manager.Save == null)
            return null;

        GachaCharacterGrowthSave growth =
            manager.Save.characterGrowth.Find(
                x => x.characterId == characterId
            );

        if (growth == null)
        {
            growth = new GachaCharacterGrowthSave
            {
                characterId = characterId,
                level = 1,
                experience = 0,
                ascension = 0
            };

            manager.Save.characterGrowth.Add(growth);
            manager.SaveGame();
        }

        return growth;
    }

    public bool AddExperience(string characterId, int amount)
    {
        if (amount <= 0 || config == null)
            return false;

        if (manager == null ||
            !manager.HasCharacter(characterId))
            return false;

        GachaCharacterGrowthSave growth =
            GetGrowth(characterId);

        if (growth == null)
            return false;

        int levelLimit = GetLevelLimit(growth.ascension);

        if (growth.level >= levelLimit)
            return false;

        growth.experience += amount;

        while (growth.level < levelLimit)
        {
            int required =
                config.GetRequiredExperience(growth.level);

            if (growth.experience < required)
                break;

            growth.experience -= required;
            growth.level++;
        }

        manager.SaveGame();

        if (snapshotManager != null)
        {
            snapshotManager.Invalidate(characterId);
        }

        return true;
    }

    public bool Ascend(string characterId)
    {
        if (config == null || manager == null)
            return false;

        if (!manager.HasCharacter(characterId))
            return false;

        GachaCharacterGrowthSave growth =
            GetGrowth(characterId);

        if (growth == null ||
            growth.ascension >= config.ascensions.Count)
            return false;

        GachaAscensionRequirement requirement =
            config.ascensions[growth.ascension];

        if (growth.level < requirement.requiredLevel)
            return false;

        if (!SpendMaterial(
            requirement.materialId,
            requirement.materialAmount))
            return false;

        growth.ascension++;

        manager.SaveGame();

        if (snapshotManager != null)
        {
            snapshotManager.Invalidate(characterId);
        }

        return true;
    }

    private int GetLevelLimit(int ascension)
    {
        return Mathf.Min(
            config.maxLevel,
            20 + ascension * 10
        );
    }

    public bool AddMaterial(string materialId, int amount)
    {
        if (manager == null ||
            manager.Save == null ||
            string.IsNullOrEmpty(materialId) ||
            amount <= 0)
            return false;

        GachaMaterialSave material =
            manager.Save.materials.Find(
                x => x.materialId == materialId
            );

        if (material == null)
        {
            material = new GachaMaterialSave
            {
                materialId = materialId,
                amount = 0
            };

            manager.Save.materials.Add(material);
        }

        material.amount += amount;
        manager.SaveGame();

        return true;
    }

    public bool SpendMaterial(string materialId, int amount)
    {
        if (manager == null ||
            manager.Save == null ||
            string.IsNullOrEmpty(materialId) ||
            amount <= 0)
            return false;

        GachaMaterialSave material =
            manager.Save.materials.Find(
                x => x.materialId == materialId
            );

        if (material == null || material.amount < amount)
            return false;

        material.amount -= amount;
        manager.SaveGame();

        return true;
    }

    public int GetMaterialAmount(string materialId)
    {
        if (manager == null || manager.Save == null)
            return 0;

        GachaMaterialSave material =
            manager.Save.materials.Find(
                x => x.materialId == materialId
            );

        return material == null ? 0 : material.amount;
    }
}
