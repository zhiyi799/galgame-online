using System;
using System.Collections.Generic;
using UnityEngine;

public class GachaWeaponManager : MonoBehaviour
{
    [Header("数据")]
    public GachaManager manager;

    [Header("武器数据库")]
    public List<GachaWeaponConfig> weaponDatabase =
        new List<GachaWeaponConfig>();

    public bool GrantWeapon(string weaponId)
    {
        if (manager == null || manager.Save == null)
            return false;

        GachaWeaponConfig config =
            weaponDatabase.Find(x =>
                x != null && x.weaponId == weaponId
            );

        if (config == null)
            return false;

        string instanceId = Guid.NewGuid().ToString("N");

        manager.Save.ownedWeapons.Add(
            new GachaWeaponOwnedSave
            {
                instanceId = instanceId,
                weaponId = weaponId,
                level = 1,
                ascension = 0,
                locked = false
            }
        );

        manager.SaveGame();
        return true;
    }

    public bool EquipWeapon(
        string characterId,
        string weaponInstanceId)
    {
        if (manager == null ||
            !manager.HasCharacter(characterId))
            return false;

        GachaWeaponOwnedSave weapon =
            manager.Save.ownedWeapons.Find(
                x => x.instanceId == weaponInstanceId
            );

        if (weapon == null)
            return false;

        GachaWeaponConfig config =
            GetWeaponConfig(weapon.weaponId);

        if (config == null)
            return false;

        GachaCharacter character =
            GetCharacterFromDatabase(characterId);

        if (character == null)
            return false;

        // 暂时使用角色 combatRole 字段判断兼容性。
        if (!IsWeaponCompatible(character, config))
            return false;

        GachaWeaponEquipmentSave equipment =
            manager.Save.equippedWeapons.Find(
                x => x.characterId == characterId
            );

        if (equipment == null)
        {
            equipment = new GachaWeaponEquipmentSave
            {
                characterId = characterId,
                weaponInstanceId = weaponInstanceId
            };

            manager.Save.equippedWeapons.Add(equipment);
        }
        else
        {
            equipment.weaponInstanceId = weaponInstanceId;
        }

        manager.SaveGame();
        return true;
    }

    public bool UnequipWeapon(string characterId)
    {
        if (manager == null)
            return false;

        GachaWeaponEquipmentSave equipment =
            manager.Save.equippedWeapons.Find(
                x => x.characterId == characterId
            );

        if (equipment == null)
            return false;

        manager.Save.equippedWeapons.Remove(equipment);
        manager.SaveGame();

        return true;
    }

    public GachaWeaponOwnedSave GetEquippedWeapon(string characterId)
    {
        if (manager == null)
            return null;

        GachaWeaponEquipmentSave equipment =
            manager.Save.equippedWeapons.Find(
                x => x.characterId == characterId
            );

        if (equipment == null)
            return null;

        return manager.Save.ownedWeapons.Find(
            x => x.instanceId == equipment.weaponInstanceId
        );
    }

    public GachaWeaponConfig GetWeaponConfig(string weaponId)
    {
        return weaponDatabase.Find(
            x => x != null && x.weaponId == weaponId
        );
    }

    public CharacterCombatStats AddWeaponStats(
        string characterId,
        CharacterCombatStats characterStats)
    {
        if (characterStats == null)
            return new CharacterCombatStats();

        CharacterCombatStats result = characterStats.Clone();

        GachaWeaponOwnedSave weapon =
            GetEquippedWeapon(characterId);

        if (weapon == null)
            return result;

        GachaWeaponConfig config =
            GetWeaponConfig(weapon.weaponId);

        if (config == null)
            return result;

        int level = Mathf.Max(1, weapon.level);

        result.attack +=
            config.baseAttack +
            (level - 1) * config.attackPerLevel;

        result.maxHP +=
            config.baseHP +
            (level - 1) * config.hpPerLevel;

        result.defense +=
            config.baseDefense +
            (level - 1) * config.defensePerLevel;

        result.critRate += config.baseCritRate;

        return result;
    }

    public bool EnhanceWeapon(
        string instanceId,
        int experience)
    {
        if (experience <= 0 || manager == null)
            return false;

        GachaWeaponOwnedSave weapon =
            manager.Save.ownedWeapons.Find(
                x => x.instanceId == instanceId
            );

        if (weapon == null || weapon.locked)
            return false;

        GachaWeaponConfig config =
            GetWeaponConfig(weapon.weaponId);

        if (config == null ||
            weapon.level >= config.maxLevel)
            return false;

        int levelLimit = GetWeaponLevelLimit(
            weapon.ascension,
            config.maxLevel
        );

        if (weapon.level >= levelLimit)
            return false;

        // 简化原型：每 100 点经验提升一级。
        int levels = experience / 100;

        if (levels <= 0)
            return false;

        weapon.level = Mathf.Min(
            levelLimit,
            weapon.level + levels
        );

        manager.SaveGame();
        return true;
    }

    public bool AscendWeapon(string instanceId)
    {
        if (manager == null)
            return false;

        GachaWeaponOwnedSave weapon =
            manager.Save.ownedWeapons.Find(
                x => x.instanceId == instanceId
            );

        if (weapon == null)
            return false;

        GachaWeaponConfig config =
            GetWeaponConfig(weapon.weaponId);

        if (config == null)
            return false;

        if (weapon.ascension >= 6)
            return false;

        int currentLimit = GetWeaponLevelLimit(
            weapon.ascension,
            config.maxLevel
        );

        if (weapon.level < currentLimit)
            return false;

        string materialId = "weapon_ascension_material";
        int materialCost = 5 + weapon.ascension * 5;

        GachaMaterialSave material =
            manager.Save.materials.Find(
                x => x.materialId == materialId
            );

        if (material == null || material.amount < materialCost)
            return false;

        material.amount -= materialCost;
        weapon.ascension++;

        manager.SaveGame();
        return true;
    }

    private int GetWeaponLevelLimit(int ascension, int maxLevel)
    {
        int limit = 20 + ascension * 10;

        return Mathf.Min(limit, maxLevel);
    }

    private GachaCharacter GetCharacterFromDatabase(string id)
    {
        // 由外部角色数据库提供。
        return characterDatabase != null
            ? characterDatabase.GetCharacter(id)
            : null;
    }

    public GachaCharacterDatabase characterDatabase;

    private bool IsWeaponCompatible(
        GachaCharacter character,
        GachaWeaponConfig weapon)
    {
        if (character == null || weapon == null)
            return false;

        // 后续可以扩展成角色专属武器限制。
        return true;
    }
}
