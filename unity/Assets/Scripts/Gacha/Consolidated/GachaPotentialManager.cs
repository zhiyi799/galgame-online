using System;
using System.Collections.Generic;
using UnityEngine;

public class GachaPotentialManager : MonoBehaviour
{
    [Header("依赖")]
    public GachaManager gachaManager;
    public GachaFinalStatSnapshotManager snapshotManager;

    [Header("角色潜能配置")]
    public List<GachaCharacterPotentialConfig> configs =
        new List<GachaCharacterPotentialConfig>();

    public event Action<string> OnPotentialChanged;

    public int MaxPotential => 6;

    public int GetPotentialLevel(string characterId)
    {
        if (gachaManager == null ||
            gachaManager.Save == null ||
            gachaManager.Save.ownedCharacters == null)
            return 0;

        GachaOwnedCharacter owned =
            gachaManager.Save.ownedCharacters.Find(
                x => x != null && x.characterId == characterId
            );

        return owned == null
            ? 0
            : Mathf.Clamp(owned.potentialLevel, 0, MaxPotential);
    }

    public GachaCharacterPotentialConfig GetConfig(string characterId)
    {
        if (configs == null)
            return null;

        return configs.Find(
            x => x != null && x.characterId == characterId
        );
    }

    public CharacterPotentialStage GetCurrentStage(string characterId)
    {
        int level = GetPotentialLevel(characterId);

        if (level <= 0)
            return null;

        GachaCharacterPotentialConfig config =
            GetConfig(characterId);

        return config == null ? null : config.GetStage(level);
    }

    public CharacterPotentialStage GetStage(
        string characterId,
        int stage
    )
    {
        GachaCharacterPotentialConfig config =
            GetConfig(characterId);

        return config == null ? null : config.GetStage(stage);
    }

    public bool IsStageUnlocked(string characterId, int stage)
    {
        return GetPotentialLevel(characterId) >= stage;
    }

    public bool CanUpgrade(string characterId)
    {
        return GetPotentialLevel(characterId) < MaxPotential;
    }

    /// <summary>
    /// 由重复抽卡系统调用。
    /// 不在这里发放重复角色奖励，避免奖励重复发放。
    /// </summary>
    public bool AddDuplicatePotential(string characterId)
    {
        if (gachaManager == null ||
            gachaManager.Save == null ||
            string.IsNullOrEmpty(characterId))
            return false;

        if (!gachaManager.HasCharacter(characterId))
            return false;

        GachaOwnedCharacter owned =
            gachaManager.Save.ownedCharacters.Find(
                x => x != null && x.characterId == characterId
            );

        if (owned == null)
            return false;

        if (owned.potentialLevel >= MaxPotential)
            return false;

        owned.potentialLevel =
            Mathf.Clamp(owned.potentialLevel + 1, 0, MaxPotential);

        if (snapshotManager != null)
        {
            snapshotManager.Invalidate(characterId);
        }

        OnPotentialChanged?.Invoke(characterId);

        return true;
    }

    /// <summary>
    /// 获取潜能属性加成。
    /// 当前只应用已解锁的当前阶段。
    /// </summary>
    public void ApplyPotentialStats(
        string characterId,
        CharacterCombatStats stats
    )
    {
        if (stats == null)
            return;

        CharacterPotentialStage stage =
            GetCurrentStage(characterId);

        if (stage == null)
            return;

        stats.attack *= 1f + stage.attackBonus;
        stats.maxHP *= 1f + stage.hpBonus;
        stats.defense *= 1f + stage.defenseBonus;
        stats.speed *= 1f + stage.speedBonus;

        stats.critRate += stage.critRateBonus;
        stats.energyRecharge *=
            1f + stage.energyRechargeBonus;
    }

    public float GetSkillBonus(string characterId, bool isUltimate)
    {
        CharacterPotentialStage stage =
            GetCurrentStage(characterId);

        if (stage == null)
            return 0f;

        return isUltimate
            ? stage.ultimateDamageBonus
            : stage.skillDamageBonus;
    }
}
