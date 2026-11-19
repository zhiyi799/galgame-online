using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色稀有度
/// </summary>
public enum Rarity
{
    ThreeStar = 3,
    FourStar = 4,
    FiveStar = 5
}

/// <summary>
/// 卡池类型
/// </summary>
public enum GachaBannerType
{
    Standard,
    Limited,
    Weapon,
    Beginner
}

/// <summary>
/// 角色属性
/// </summary>
public enum CharacterElement
{
    None,
    Fire,
    Water,
    Wind,
    Light,
    Dark,
    Sakura
}

/// <summary>
/// 角色战斗定位
/// </summary>
public enum CharacterCombatRole
{
    None,
    MainDPS,
    SubDPS,
    Support,
    Healer,
    Tank,
    Control
}

/// <summary>
/// 角色基础属性
/// </summary>
[Serializable]
public class CharacterCombatStats
{
    public float maxHP = 100f;
    public float attack = 10f;
    public float defense = 5f;
    public float speed = 100f;
    public float critRate = 0.05f;
    public float critDamage = 0.5f;
    public float energyRecharge = 1f;

    public CharacterCombatStats Clone()
    {
        return new CharacterCombatStats
        {
            maxHP = maxHP,
            attack = attack,
            defense = defense,
            speed = speed,
            critRate = critRate,
            critDamage = critDamage,
            energyRecharge = energyRecharge
        };
    }
}

/// <summary>
/// 角色定义数据
/// </summary>
[Serializable]
public class GachaCharacter
{
    public string id;
    public string characterName;

    public Rarity rarity = Rarity.FourStar;

    public Sprite portrait;

    public bool isRateUp;

    [TextArea]
    public string description;

    // 角色属性与定位
    public CharacterElement element = CharacterElement.None;
    public CharacterCombatRole combatRole = CharacterCombatRole.None;

    public CharacterCombatStats baseStats =
        new CharacterCombatStats();
}

/// <summary>
/// 抽卡历史记录
/// </summary>
[Serializable]
public class GachaPullRecord
{
    public string characterId;
    public string characterName;

    public Rarity rarity;

    public bool isRateUp;

    public int pullIndex;

    public string bannerId;
    public string bannerName;

    public string time;
}

/// <summary>
/// 已拥有角色
/// </summary>
[Serializable]
public class GachaOwnedCharacter
{
    public string characterId;

    // 获取次数
    public int count = 1;

    // 首次获取时的抽数
    public int firstObtainedPull;

    // 阶段式潜能：0~6
    public int potentialLevel = 0;

    // 技能等级：默认 1。
    public int skillLevel = 1;

    // 重复等级：默认 0。
    // 注意：重复等级不是潜能等级，不能代替潜能等级。
    public int duplicateLevel = 0;
}

/// <summary>
/// 卡池独立保底记录
/// </summary>
[Serializable]
public class GachaBannerPitySave
{
    public string bannerId;

    public int pityCount;

    public bool guaranteed;
}

/// <summary>
/// 角色成长存档
/// </summary>
[Serializable]
public class GachaCharacterGrowthSave
{
    public string characterId;

    public int level = 1;

    public int experience = 0;

    public int ascension = 0;
}

/// <summary>
/// 技能等级存档
/// </summary>
[Serializable]
public class GachaSkillLevelSave
{
    public string characterId;
    public string skillId;

    public int level = 1;
}

/// <summary>
/// 材料库存存档
/// </summary>
[Serializable]
public class GachaMaterialSave
{
    public string materialId;

    public int amount;
}

/// <summary>
/// 已拥有武器存档
/// </summary>
[Serializable]
public class GachaWeaponOwnedSave
{
    public string instanceId;
    public string weaponId;

    public int level = 1;
    public int ascension = 0;

    // 精炼等级：1~5
    public int refinement = 1;

    public int enhancementExperience = 0;

    public bool locked;
}

/// <summary>
/// 武器装备关系
/// </summary>
[Serializable]
public class GachaWeaponEquipmentSave
{
    public string characterId;
    public string weaponInstanceId;
}

/// <summary>
/// 服装拥有记录
/// </summary>
[Serializable]
public class GachaCostumeSave
{
    public string characterId;
    public string costumeId;
}

/// <summary>
/// 角色当前外观记录
/// </summary>
[Serializable]
public class GachaAppearanceSave
{
    public string characterId;
    public string costumeId;
}

/// <summary>
/// 完整抽卡与养成存档
/// </summary>
[Serializable]
public class GachaSaveData
{
    // 基础资源
    public int currency = 10000;
    public int resonance;

    // 全局抽卡统计
    public int totalPulls;

    // 兼容旧版全局保底字段（v2 命名）
    public int pityFiveStar;
    public bool guaranteedRateUp;

    // 抽卡记录
    public List<GachaPullRecord> history =
        new List<GachaPullRecord>();

    public List<GachaOwnedCharacter> ownedCharacters =
        new List<GachaOwnedCharacter>();

    // 独立卡池保底
    public List<GachaBannerPitySave> bannerPities =
        new List<GachaBannerPitySave>();

    // 角色成长
    public List<GachaCharacterGrowthSave> characterGrowth =
        new List<GachaCharacterGrowthSave>();

    // 技能等级
    public List<GachaSkillLevelSave> skillLevels =
        new List<GachaSkillLevelSave>();

    // 材料
    public List<GachaMaterialSave> materials =
        new List<GachaMaterialSave>();

    // 武器
    public List<GachaWeaponOwnedSave> ownedWeapons =
        new List<GachaWeaponOwnedSave>();

    public List<GachaWeaponEquipmentSave> equippedWeapons =
        new List<GachaWeaponEquipmentSave>();

    // 服装与外观
    public List<GachaCostumeSave> costumes =
        new List<GachaCostumeSave>();

    public List<GachaAppearanceSave> appearances =
        new List<GachaAppearanceSave>();

    // 材料关卡首次通关记录
    public List<string> completedMaterialStages =
        new List<string>();
}

/// <summary>
/// 单次抽卡结果
/// </summary>
[Serializable]
public class GachaPullResult
{
    public GachaCharacter character;

    public Rarity rarity;

    public bool isRateUp;

    public int pullIndex;

    public bool isNew;

    public string bannerId;
    public string bannerName;
}
