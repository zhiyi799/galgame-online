using UnityEngine;

/// <summary>
/// 五路属性收集器工厂。
///
/// 五路来源：
/// 1. 角色基础：角色数据库条目 + 成长数据源 + 成长计算器
/// 2. 武器基础：实际装备武器的基础属性
/// 3. 武器被动：实际装备武器的被动效果
/// 4. 潜能：当前阶总加成
/// 5. 天赋：当前突破已解锁天赋
///
/// 任一必需管理器缺失时，不创建收集器。
/// 任一必需属性来源失败时，不提交不完整快照。
/// </summary>
public class GachaFinalStatCollectorFactory : MonoBehaviour
{
    [Header("五路数据源")]
    public GachaCharacterDatabase characterDatabase;
    public GachaManager gachaManager;
    public GachaWeaponManager weaponManager;
    public GachaPotentialManager potentialManager;
    public GachaTalentManager talentManager;

    [Header("快照管理器")]
    public GachaFinalStatSnapshotManager snapshotManager;

    // 普通无状态计算器，不走 Inspector。
    private readonly GachaCharacterStatCalculator statCalculator =
        new GachaCharacterStatCalculator();

    /// <summary>
    /// 为指定角色创建一个绑定五路真实回调的收集器。
    /// </summary>
    public GachaFinalStatDataCollector CreateCollector(
        string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return null;

        if (characterDatabase == null ||
            gachaManager == null ||
            weaponManager == null ||
            potentialManager == null ||
            talentManager == null)
        {
            return null;
        }

        // 两个回调分别读取拥有角色记录和成长存档。
        // 数据源只读，不修改存档。
        GachaCharacterGrowthDataSource growthDataSource =
            new GachaCharacterGrowthDataSource(
                FindOwnedCharacter,
                FindGrowthSave
            );

        return new GachaFinalStatDataCollector(
            () => ReadCharacterBase(characterId, growthDataSource),
            () => ReadWeaponBase(characterId),
            () => ReadWeaponPassive(characterId),
            () => ReadPotential(characterId),
            () => ReadTalent(characterId)
        );
    }

    /// <summary>
    /// 生成指定角色的最终属性快照并提交。
    /// 收集失败时不提交不完整快照。
    /// </summary>
    public bool RefreshSnapshot(string characterId)
    {
        if (snapshotManager == null)
            return false;

        GachaFinalStatDataCollector collector =
            CreateCollector(characterId);

        if (collector == null)
            return false;

        return snapshotManager.Refresh(
            characterId,
            () =>
            {
                GachaFinalStatSnapshot snapshot =
                    collector.Collect(out string error);

                if (snapshot == null)
                {
                    Debug.LogError(
                        "[GachaFinalStatCollectorFactory] 属性收集失败：" +
                        characterId + "，" + error
                    );
                }

                return snapshot;
            }
        );
    }

    /// <summary>
    /// 第1路：角色基础属性。
    ///
    /// 角色数据库提供基础属性与成长配置。
    /// 成长数据源提供玩家实际等级与突破等级。
    /// 计算器只计算一次角色自身成长。
    ///
    /// skillLevel 和 duplicateLevel 不参与基础属性计算。
    /// 任一必要数据缺失时返回 null。
    /// </summary>
    private GachaFinalCombatStats ReadCharacterBase(
        string characterId,
        GachaCharacterGrowthDataSource growthDataSource)
    {
        if (string.IsNullOrWhiteSpace(characterId) ||
            growthDataSource == null ||
            characterDatabase == null)
        {
            return null;
        }

        GachaCharacterDatabaseEntry definition =
            characterDatabase.GetCharacter(characterId);

        if (definition == null || !definition.IsValid())
            return null;

        GachaCharacterGrowthData growth =
            growthDataSource.GetGrowthData(characterId);

        if (growth == null || !growth.IsValid())
            return null;

        GachaFinalCombatStats result =
            statCalculator.Calculate(
                definition.baseStats,
                definition.growthConfig,
                growth.level,
                growth.ascension
            );

        if (result == null || !result.IsValid())
            return null;

        return result;
    }

    /// <summary>
    /// 读取玩家实际拥有的角色记录。
    /// </summary>
    private GachaOwnedCharacter FindOwnedCharacter(
        string characterId)
    {
        if (gachaManager == null ||
            gachaManager.Save == null ||
            gachaManager.Save.ownedCharacters == null)
        {
            return null;
        }

        return gachaManager.Save.ownedCharacters.Find(
            x => x != null && x.characterId == characterId
        );
    }

    /// <summary>
    /// 读取角色独立成长存档。
    /// </summary>
    private GachaCharacterGrowthSave FindGrowthSave(
        string characterId)
    {
        if (gachaManager == null ||
            gachaManager.Save == null ||
            gachaManager.Save.characterGrowth == null)
        {
            return null;
        }

        return gachaManager.Save.characterGrowth.Find(
            x => x != null && x.characterId == characterId
        );
    }

    // =========================================================
    // 第2路：武器基础属性
    // 以下保留原有实现。
    // =========================================================

    private GachaFinalCombatStats ReadWeaponBase(
        string characterId)
    {
        GachaWeaponStats weaponStats =
            weaponManager.GetEquippedWeaponStats(characterId);

        return new GachaFinalCombatStats
        {
            attack = weaponStats.attack,
            maxHP = weaponStats.hp,
            defense = weaponStats.defense,
            critRate = weaponStats.critRate
        };
    }

    // =========================================================
    // 第3路：武器被动
    // 以下保留原有实现。
    // =========================================================

    private GachaFinalStatSourceData ReadWeaponPassive(
        string characterId)
    {
        var contribution = new GachaFinalStatSourceData
        {
            sourceType = GachaFinalStatSourceType.WeaponPassive
        };

        GachaWeaponOwnedSave weapon =
            weaponManager.GetEquippedWeapon(characterId);

        if (weapon == null)
            return contribution;

        GachaWeaponConfig config =
            weaponManager.GetConfig(weapon.weaponId);

        if (config == null || config.passive == null)
            return contribution;

        float value =
            config.passive.GetValue(weapon.refinement);

        if (value == 0f)
            return contribution;

        switch (config.passive.passiveType)
        {
            case WeaponPassiveType.AttackBonus:
                contribution.percentBonus.attack += value;
                break;

            case WeaponPassiveType.HPBonus:
                contribution.percentBonus.maxHP += value;
                break;

            case WeaponPassiveType.DefenseBonus:
                contribution.percentBonus.defense += value;
                break;

            case WeaponPassiveType.EnergyRechargeBonus:
                contribution.percentBonus.energyRecharge += value;
                break;

            case WeaponPassiveType.CritRateBonus:
                contribution.flatBonus.critRate += value;
                break;

            case WeaponPassiveType.CritDamageBonus:
                contribution.flatBonus.critDamage += value;
                break;

            case WeaponPassiveType.SkillDamageBonus:
                contribution.combatBonus.skillDamageBonus += value;
                break;

            case WeaponPassiveType.UltimateDamageBonus:
                contribution.combatBonus.ultimateDamageBonus += value;
                break;

            default:
                break;
        }

        return contribution;
    }

    // =========================================================
    // 第4路：潜能
    // 以下保留原有实现。
    // =========================================================

    private GachaFinalStatSourceData ReadPotential(
        string characterId)
    {
        var contribution = new GachaFinalStatSourceData
        {
            sourceType = GachaFinalStatSourceType.Potential
        };

        CharacterPotentialStage stage =
            potentialManager.GetCurrentStage(characterId);

        if (stage == null)
            return contribution;

        contribution.percentBonus.attack += stage.attackBonus;
        contribution.percentBonus.maxHP += stage.hpBonus;
        contribution.percentBonus.defense += stage.defenseBonus;
        contribution.percentBonus.speed += stage.speedBonus;
        contribution.percentBonus.energyRecharge +=
            stage.energyRechargeBonus;

        contribution.flatBonus.critRate += stage.critRateBonus;

        contribution.combatBonus.skillDamageBonus +=
            stage.skillDamageBonus;

        contribution.combatBonus.ultimateDamageBonus +=
            stage.ultimateDamageBonus;

        return contribution;
    }

    // =========================================================
    // 第5路：天赋
    // 以下保留原有实现。
    // =========================================================

    private GachaFinalStatSourceData ReadTalent(
        string characterId)
    {
        var contribution = new GachaFinalStatSourceData
        {
            sourceType = GachaFinalStatSourceType.Talent
        };

        contribution.percentBonus.attack +=
            talentManager.GetBonus(
                characterId,
                CharacterTalentType.AttackBonus);

        contribution.percentBonus.maxHP +=
            talentManager.GetBonus(
                characterId,
                CharacterTalentType.HPBonus);

        contribution.percentBonus.defense +=
            talentManager.GetBonus(
                characterId,
                CharacterTalentType.DefenseBonus);

        contribution.percentBonus.speed +=
            talentManager.GetBonus(
                characterId,
                CharacterTalentType.SpeedBonus);

        contribution.percentBonus.energyRecharge +=
            talentManager.GetBonus(
                characterId,
                CharacterTalentType.EnergyRechargeBonus);

        contribution.flatBonus.critRate +=
            talentManager.GetBonus(
                characterId,
                CharacterTalentType.CritRateBonus);

        contribution.flatBonus.critDamage +=
            talentManager.GetBonus(
                characterId,
                CharacterTalentType.CritDamageBonus);

        contribution.flatBonus.healingBonus +=
            talentManager.GetBonus(
                characterId,
                CharacterTalentType.HealingBonus);

        contribution.flatBonus.damageReduction +=
            talentManager.GetDamageReduction(characterId);

        contribution.combatBonus.skillDamageBonus +=
            talentManager.GetSkillDamageBonus(characterId);

        contribution.combatBonus.ultimateDamageBonus +=
            talentManager.GetUltimateDamageBonus(characterId);

        return contribution;
    }
}
