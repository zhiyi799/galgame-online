using UnityEngine;

/// <summary>
/// 存档数据修复与兼容工具。
/// 不负责读写文件，只负责修复反序列化后的数据。
/// </summary>
public static class GachaSaveDataUtility
{
    public static GachaSaveData Normalize(GachaSaveData data)
    {
        if (data == null)
        {
            data = new GachaSaveData();
        }

        // 基础资源
        data.currency = Mathf.Max(0, data.currency);
        data.resonance = Mathf.Max(0, data.resonance);
        data.totalPulls = Mathf.Max(0, data.totalPulls);

        data.pityFiveStar = Mathf.Max(0, data.pityFiveStar);

        // 抽卡记录
        if (data.history == null)
            data.history = new System.Collections.Generic.List<GachaPullRecord>();

        if (data.ownedCharacters == null)
            data.ownedCharacters = new System.Collections.Generic.List<GachaOwnedCharacter>();

        if (data.bannerPities == null)
            data.bannerPities = new System.Collections.Generic.List<GachaBannerPitySave>();

        // 角色成长
        if (data.characterGrowth == null)
            data.characterGrowth = new System.Collections.Generic.List<GachaCharacterGrowthSave>();

        if (data.skillLevels == null)
            data.skillLevels = new System.Collections.Generic.List<GachaSkillLevelSave>();

        // 材料
        if (data.materials == null)
            data.materials = new System.Collections.Generic.List<GachaMaterialSave>();

        // 武器
        if (data.ownedWeapons == null)
            data.ownedWeapons = new System.Collections.Generic.List<GachaWeaponOwnedSave>();

        if (data.equippedWeapons == null)
            data.equippedWeapons = new System.Collections.Generic.List<GachaWeaponEquipmentSave>();

        // 外观
        if (data.costumes == null)
            data.costumes = new System.Collections.Generic.List<GachaCostumeSave>();

        if (data.appearances == null)
            data.appearances = new System.Collections.Generic.List<GachaAppearanceSave>();

        // 材料关卡首次通关记录
        if (data.completedMaterialStages == null)
            data.completedMaterialStages = new System.Collections.Generic.List<string>();

        // 修复角色数据
        foreach (GachaOwnedCharacter character in data.ownedCharacters)
        {
            if (character == null)
                continue;

            character.count = Mathf.Max(1, character.count);
            character.potentialLevel =
                Mathf.Clamp(character.potentialLevel, 0, 6);
            character.skillLevel = Mathf.Max(1, character.skillLevel);
            character.duplicateLevel =
                Mathf.Max(0, character.duplicateLevel);
        }

        // 修复成长数据
        foreach (GachaCharacterGrowthSave growth in data.characterGrowth)
        {
            if (growth == null)
                continue;

            growth.level = Mathf.Max(1, growth.level);
            growth.experience = Mathf.Max(0, growth.experience);
            growth.ascension = Mathf.Max(0, growth.ascension);
        }

        // 修复技能等级
        foreach (GachaSkillLevelSave skill in data.skillLevels)
        {
            if (skill == null)
                continue;

            skill.level = Mathf.Max(1, skill.level);
        }

        // 修复材料数量
        foreach (GachaMaterialSave material in data.materials)
        {
            if (material == null)
                continue;

            material.amount = Mathf.Max(0, material.amount);
        }

        // 修复武器数据
        foreach (GachaWeaponOwnedSave weapon in data.ownedWeapons)
        {
            if (weapon == null)
                continue;

            weapon.level = Mathf.Max(1, weapon.level);
            weapon.ascension = Mathf.Max(0, weapon.ascension);

            // 旧存档没有 refinement 字段时，默认按 1 处理
            weapon.refinement =
                Mathf.Clamp(weapon.refinement <= 0 ? 1 : weapon.refinement, 1, 5);
        }

        // 修复卡池保底
        foreach (GachaBannerPitySave banner in data.bannerPities)
        {
            if (banner == null)
                continue;

            banner.pityCount = Mathf.Max(0, banner.pityCount);
        }

        return data;
    }
}
