using System;
using System.Collections.Generic;
using UnityEngine;

public class GachaWeaponManager : MonoBehaviour
{
    [Header("系统依赖")]
    public GachaManager gachaManager;
    public GachaMaterialInventory materialInventory;
    public GachaFinalStatSnapshotManager snapshotManager;

    [Header("武器配置")]
    public List<GachaWeaponConfig> weaponConfigs =
        new List<GachaWeaponConfig>();

    [Header("强化参数")]
    [Min(1)] public int experiencePerMaterial = 100;
    [Min(1)] public int experiencePerLevel = 100;

    [Header("强化材料")]
    public string weaponEnhancementMaterialId =
        "weapon_exp_material";

    private Dictionary<string, GachaWeaponConfig> configLookup;

    private void Awake()
    {
        BuildConfigLookup();
    }

    private void BuildConfigLookup()
    {
        configLookup =
            new Dictionary<string, GachaWeaponConfig>();

        foreach (GachaWeaponConfig config in weaponConfigs)
        {
            if (config == null ||
                string.IsNullOrWhiteSpace(config.weaponId))
                continue;

            if (!configLookup.ContainsKey(config.weaponId))
                configLookup.Add(config.weaponId, config);
        }
    }

    private GachaSaveData GetSaveData()
    {
        if (gachaManager == null)
            return null;

        return gachaManager.Save;
    }

    private void Save()
    {
        if (gachaManager != null)
            gachaManager.SaveGame();
    }

    private void InvalidateCharacter(string characterId)
    {
        if (snapshotManager == null ||
            string.IsNullOrWhiteSpace(characterId))
            return;

        snapshotManager.Invalidate(characterId);
    }

    private void InvalidateCharacters(
        string firstCharacterId,
        string secondCharacterId
    )
    {
        InvalidateCharacter(firstCharacterId);

        if (!string.IsNullOrWhiteSpace(secondCharacterId) &&
            secondCharacterId != firstCharacterId)
        {
            InvalidateCharacter(secondCharacterId);
        }
    }

    private void InvalidateEquippedCharacter(string instanceId)
    {
        InvalidateCharacter(GetEquippedCharacter(instanceId));
    }

    public GachaWeaponConfig GetConfig(string weaponId)
    {
        if (configLookup == null)
            BuildConfigLookup();

        if (string.IsNullOrWhiteSpace(weaponId))
            return null;

        configLookup.TryGetValue(weaponId, out var config);
        return config;
    }

    public GachaWeaponOwnedSave GetOwnedWeapon(string instanceId)
    {
        GachaSaveData data = GetSaveData();

        if (data == null || data.ownedWeapons == null)
            return null;

        return data.ownedWeapons.Find(
            x => x != null && x.instanceId == instanceId
        );
    }

    public List<GachaWeaponOwnedSave> GetAllOwnedWeapons()
    {
        GachaSaveData data = GetSaveData();

        if (data == null || data.ownedWeapons == null)
            return new List<GachaWeaponOwnedSave>();

        return new List<GachaWeaponOwnedSave>(data.ownedWeapons);
    }

    public GachaWeaponOwnedSave GrantWeapon(string weaponId)
    {
        GachaSaveData data = GetSaveData();
        GachaWeaponConfig config = GetConfig(weaponId);

        if (data == null || config == null || !config.IsValid())
            return null;

        if (data.ownedWeapons == null)
            data.ownedWeapons = new List<GachaWeaponOwnedSave>();

        GachaWeaponOwnedSave weapon =
            new GachaWeaponOwnedSave
            {
                instanceId = Guid.NewGuid().ToString("N"),
                weaponId = weaponId,
                level = 1,
                ascension = 0,
                refinement = 1,
                enhancementExperience = 0,
                locked = false
            };

        data.ownedWeapons.Add(weapon);
        Save();

        return weapon;
    }

    public GachaWeaponOwnedSave GetEquippedWeapon(string characterId)
    {
        GachaSaveData data = GetSaveData();

        if (data == null || data.equippedWeapons == null)
            return null;

        GachaWeaponEquipmentSave equipment =
            data.equippedWeapons.Find(
                x => x != null && x.characterId == characterId
            );

        if (equipment == null)
            return null;

        return GetOwnedWeapon(equipment.weaponInstanceId);
    }

    public string GetEquippedCharacter(string instanceId)
    {
        GachaSaveData data = GetSaveData();

        if (data == null || data.equippedWeapons == null)
            return null;

        GachaWeaponEquipmentSave equipment =
            data.equippedWeapons.Find(
                x => x != null &&
                     x.weaponInstanceId == instanceId
            );

        return equipment == null ? null : equipment.characterId;
    }

    public bool IsWeaponEquipped(string instanceId)
    {
        return !string.IsNullOrEmpty(
            GetEquippedCharacter(instanceId)
        );
    }

    public bool EquipWeapon(string characterId, string instanceId)
    {
        GachaSaveData data = GetSaveData();

        if (data == null ||
            string.IsNullOrWhiteSpace(characterId))
            return false;

        if (gachaManager == null ||
            !gachaManager.HasCharacter(characterId))
            return false;

        GachaWeaponOwnedSave weapon = GetOwnedWeapon(instanceId);

        if (weapon == null || weapon.locked)
            return false;

        if (GetConfig(weapon.weaponId) == null)
            return false;

        if (data.equippedWeapons == null)
            data.equippedWeapons =
                new List<GachaWeaponEquipmentSave>();

        // 同一武器实例不能同时装备给多个角色。
        string previousCharacter =
            GetEquippedCharacter(instanceId);

        if (!string.IsNullOrEmpty(previousCharacter) &&
            previousCharacter != characterId)
        {
            data.equippedWeapons.RemoveAll(
                x => x != null &&
                     x.weaponInstanceId == instanceId
            );
        }

        // 替换目标角色原来的武器。
        data.equippedWeapons.RemoveAll(
            x => x != null && x.characterId == characterId
        );

        data.equippedWeapons.Add(
            new GachaWeaponEquipmentSave
            {
                characterId = characterId,
                weaponInstanceId = instanceId
            }
        );

        Save();

        InvalidateCharacters(previousCharacter, characterId);

        return true;
    }

    public bool UnequipWeapon(string characterId)
    {
        GachaSaveData data = GetSaveData();

        if (data == null || data.equippedWeapons == null)
            return false;

        int removed = data.equippedWeapons.RemoveAll(
            x => x != null && x.characterId == characterId
        );

        if (removed > 0)
        {
            Save();
            InvalidateCharacter(characterId);
        }

        return removed > 0;
    }

    public bool SetWeaponLocked(string instanceId, bool locked)
    {
        GachaWeaponOwnedSave weapon = GetOwnedWeapon(instanceId);

        if (weapon == null)
            return false;

        weapon.locked = locked;
        Save();
        return true;
    }

    public bool EnhanceWeapon(string instanceId, int materialCount)
    {
        // 强化参数防御性检查
        if (experiencePerMaterial <= 0 ||
            experiencePerLevel <= 0)
        {
            Debug.LogError(
                "[GachaWeaponManager] 强化参数无效：" +
                $"experiencePerMaterial={experiencePerMaterial}, " +
                $"experiencePerLevel={experiencePerLevel}"
            );

            return false;
        }

        if (materialCount <= 0 ||
            materialInventory == null)
            return false;

        GachaWeaponOwnedSave weapon = GetOwnedWeapon(instanceId);

        if (weapon == null || weapon.locked)
            return false;

        GachaWeaponConfig config = GetConfig(weapon.weaponId);

        if (config == null)
            return false;

        int levelCap = config.GetLevelCap(weapon.ascension);

        if (weapon.level >= levelCap)
            return false;

        // 只计算到当前等级上限所需的经验，避免浪费材料。
        long requiredExperience =
            (long)(levelCap - weapon.level) *
            experiencePerLevel -
            weapon.enhancementExperience;

        if (requiredExperience <= 0)
            return false;

        int usableCount = (int)Math.Min(
            materialCount,
            (requiredExperience + experiencePerMaterial - 1)
            / experiencePerMaterial
        );

        if (usableCount <= 0)
            return false;

        if (string.IsNullOrWhiteSpace(
            weaponEnhancementMaterialId))
            return false;

        if (!materialInventory.TrySpend(
            weaponEnhancementMaterialId,
            usableCount))
            return false;

        long gained =
            (long)usableCount * experiencePerMaterial;

        long total =
            weapon.enhancementExperience + gained;

        long levelExperience =
            (long)experiencePerLevel;

        int levelsGained = (int)(total / levelExperience);

        weapon.level += levelsGained;
        weapon.enhancementExperience =
            (int)(total % levelExperience);

        if (weapon.level >= levelCap)
        {
            weapon.level = levelCap;
            weapon.enhancementExperience = 0;
        }

        Save();
        InvalidateEquippedCharacter(instanceId);

        return true;
    }

    public bool AscendWeapon(string instanceId)
    {
        if (materialInventory == null)
            return false;

        GachaWeaponOwnedSave weapon = GetOwnedWeapon(instanceId);

        if (weapon == null || weapon.locked)
            return false;

        GachaWeaponConfig config = GetConfig(weapon.weaponId);

        if (config == null || !config.IsValid())
            return false;

        int targetAscension = weapon.ascension + 1;

        if (targetAscension > config.MaxAscension)
            return false;

        int currentCap =
            config.GetLevelCap(weapon.ascension);

        if (weapon.level < currentCap)
            return false;

        int cost =
            config.GetAscensionMaterialCost(targetAscension);

        if (cost < 0)
            return false;

        if (cost > 0)
        {
            if (string.IsNullOrWhiteSpace(
                config.ascensionMaterialId))
                return false;

            if (!materialInventory.TrySpend(
                config.ascensionMaterialId,
                cost))
                return false;
        }

        weapon.ascension = targetAscension;

        Save();
        InvalidateEquippedCharacter(instanceId);

        return true;
    }

    public bool RefineWeapon(string instanceId)
    {
        if (materialInventory == null)
            return false;

        GachaWeaponOwnedSave weapon = GetOwnedWeapon(instanceId);

        if (weapon == null || weapon.locked)
            return false;

        GachaWeaponConfig config = GetConfig(weapon.weaponId);

        if (config == null || !config.IsValid())
            return false;

        if (weapon.refinement >= 5)
            return false;

        int targetRefinement = weapon.refinement + 1;

        int cost =
            config.GetRefinementMaterialCost(targetRefinement);

        if (cost < 0)
            return false;

        if (cost > 0)
        {
            if (string.IsNullOrWhiteSpace(
                config.refinementMaterialId))
                return false;

            if (!materialInventory.TrySpend(
                config.refinementMaterialId,
                cost))
                return false;
        }

        weapon.refinement = targetRefinement;

        Save();
        InvalidateEquippedCharacter(instanceId);

        return true;
    }

    public GachaWeaponStats GetWeaponStats(string instanceId)
    {
        GachaWeaponOwnedSave weapon = GetOwnedWeapon(instanceId);

        if (weapon == null)
            return GachaWeaponStats.Zero;

        GachaWeaponConfig config = GetConfig(weapon.weaponId);

        if (config == null)
            return GachaWeaponStats.Zero;

        float multiplier = config.GetGrowthMultiplier(
            weapon.level,
            weapon.ascension
        );

        return new GachaWeaponStats
        {
            attack = config.baseAttack * multiplier,
            hp = config.baseHP * multiplier,
            defense = config.baseDefense * multiplier,
            critRate = config.baseCritRate
        };
    }

    public GachaWeaponStats GetEquippedWeaponStats(
        string characterId)
    {
        GachaWeaponOwnedSave weapon =
            GetEquippedWeapon(characterId);

        return weapon == null
            ? GachaWeaponStats.Zero
            : GetWeaponStats(weapon.instanceId);
    }

    public float GetPassiveValue(
        string instanceId,
        WeaponPassiveType passiveType)
    {
        GachaWeaponOwnedSave weapon = GetOwnedWeapon(instanceId);

        if (weapon == null)
            return 0f;

        GachaWeaponConfig config = GetConfig(weapon.weaponId);

        if (config == null || config.passive == null)
            return 0f;

        if (config.passive.passiveType != passiveType)
            return 0f;

        return config.passive.GetValue(weapon.refinement);
    }

    public float GetEquippedPassiveValue(
        string characterId,
        WeaponPassiveType passiveType)
    {
        GachaWeaponOwnedSave weapon =
            GetEquippedWeapon(characterId);

        if (weapon == null)
            return 0f;

        return GetPassiveValue(
            weapon.instanceId,
            passiveType
        );
    }
}
