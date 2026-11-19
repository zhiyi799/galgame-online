using System.Collections.Generic;
using UnityEngine;

public class GachaTalentManager : MonoBehaviour
{
    [Header("依赖")]
    public GachaManager gachaManager;
    public GachaCharacterGrowthManager growthManager;

    [Header("角色天赋配置")]
    public List<GachaCharacterTalentConfig> configs =
        new List<GachaCharacterTalentConfig>();

    public GachaCharacterTalentConfig GetConfig(
        string characterId
    )
    {
        if (configs == null)
            return null;

        return configs.Find(
            x => x != null && x.characterId == characterId
        );
    }

    public int GetAscension(string characterId)
    {
        if (gachaManager == null ||
            !gachaManager.HasCharacter(characterId))
            return 0;

        if (growthManager == null)
            return 0;

        var growth = growthManager.GetGrowth(characterId);

        return growth == null
            ? 0
            : Mathf.Max(0, growth.ascension);
    }

    public List<GachaCharacterTalent> GetCharacterTalents(
        string characterId
    )
    {
        GachaCharacterTalentConfig config =
            GetConfig(characterId);

        if (config == null)
            return new List<GachaCharacterTalent>();

        return config.GetUnlockedTalents(
            GetAscension(characterId)
        );
    }

    public bool IsUnlocked(
        string characterId,
        GachaCharacterTalent talent
    )
    {
        if (talent == null ||
            gachaManager == null ||
            !gachaManager.HasCharacter(characterId))
            return false;

        if (talent.characterId != characterId)
            return false;

        return GetAscension(characterId) >=
            talent.requiredAscension;
    }

    public float GetBonus(
        string characterId,
        CharacterTalentType type
    )
    {
        GachaCharacterTalentConfig config =
            GetConfig(characterId);

        if (config == null || config.talents == null)
            return 0f;

        int ascension = GetAscension(characterId);

        float total = 0f;

        foreach (GachaCharacterTalent talent in config.talents)
        {
            if (talent == null ||
                !talent.enabled ||
                talent.talentType != type)
                continue;

            if (ascension < talent.requiredAscension)
                continue;

            int level = 1;

            if (growthManager != null)
            {
                var growth = growthManager.GetGrowth(characterId);

                if (growth != null)
                    level = Mathf.Max(1, growth.level);
            }

            total += talent.GetValue(level);
        }

        return total;
    }

    public void ApplyTalentStats(
        string characterId,
        CharacterCombatStats stats
    )
    {
        if (stats == null)
            return;

        stats.attack *=
            1f + GetBonus(characterId, CharacterTalentType.AttackBonus);

        stats.maxHP *=
            1f + GetBonus(characterId, CharacterTalentType.HPBonus);

        stats.defense *=
            1f + GetBonus(characterId, CharacterTalentType.DefenseBonus);

        stats.speed *=
            1f + GetBonus(characterId, CharacterTalentType.SpeedBonus);

        stats.critRate +=
            GetBonus(characterId, CharacterTalentType.CritRateBonus);

        stats.critDamage +=
            GetBonus(characterId, CharacterTalentType.CritDamageBonus);

        stats.energyRecharge *=
            1f + GetBonus(characterId, CharacterTalentType.EnergyRechargeBonus);
    }

    public float GetSkillDamageBonus(string characterId)
    {
        return GetBonus(
            characterId,
            CharacterTalentType.SkillDamageBonus
        );
    }

    public float GetUltimateDamageBonus(string characterId)
    {
        return GetBonus(
            characterId,
            CharacterTalentType.UltimateDamageBonus
        );
    }

    public float GetHealingBonus(string characterId)
    {
        return GetBonus(
            characterId,
            CharacterTalentType.HealingBonus
        );
    }

    public float GetDamageReduction(string characterId)
    {
        return Mathf.Clamp01(
            GetBonus(characterId, CharacterTalentType.DamageReduction)
        );
    }
}
