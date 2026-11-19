using System.Collections.Generic;
using UnityEngine;

public class GachaTalentManager : MonoBehaviour
{
    public GachaManager manager;
    public GachaCharacterGrowthManager growthManager;
    public GachaPotentialManager potentialManager;

    public List<GachaCharacterTalent> talents =
        new List<GachaCharacterTalent>();

    public List<GachaCharacterTalent> GetCharacterTalents(
        string characterId)
    {
        return talents.FindAll(
            x => x != null && x.characterId == characterId
        );
    }

    public bool IsUnlocked(GachaCharacterTalent talent)
    {
        if (talent == null || manager == null)
            return false;

        if (!manager.OwnsCharacter(talent.characterId))
            return false;

        GachaCharacterGrowthSave growth =
            growthManager != null
                ? growthManager.GetGrowth(talent.characterId)
                : null;

        int level = growth == null ? 1 : growth.level;

        int potential = potentialManager != null
            ? potentialManager.GetPotentialLevel(talent.characterId)
            : 0;

        return talent.IsUnlocked(level, potential);
    }

    public float GetBonus(
        string characterId,
        CharacterTalentType type)
    {
        float total = 0f;

        foreach (GachaCharacterTalent talent in talents)
        {
            if (talent == null ||
                talent.characterId != characterId ||
                talent.talentType != type)
                continue;

            if (IsUnlocked(talent))
                total += talent.value;
        }

        return total;
    }

    public CharacterCombatStats ApplyTalentStats(
        string characterId,
        CharacterCombatStats original)
    {
        if (original == null)
            return new CharacterCombatStats();

        CharacterCombatStats result = original.Clone();

        result.attack *=
            1f + GetBonus(
                characterId,
                CharacterTalentType.AttackBonus
            ) / 100f;

        result.maxHP *=
            1f + GetBonus(
                characterId,
                CharacterTalentType.HPBonus
            ) / 100f;

        result.defense *=
            1f + GetBonus(
                characterId,
                CharacterTalentType.DefenseBonus
            ) / 100f;

        result.energyRecharge +=
            GetBonus(
                characterId,
                CharacterTalentType.EnergyRechargeBonus
            );

        return result;
    }

    public float GetDamageReduction(string characterId)
    {
        return GetBonus(
            characterId,
            CharacterTalentType.DamageReduction
        );
    }

    public float GetHealingBonus(string characterId)
    {
        return GetBonus(
            characterId,
            CharacterTalentType.HealingBonus
        );
    }
}
