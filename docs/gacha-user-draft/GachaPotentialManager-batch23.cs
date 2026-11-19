using System.Collections.Generic;
using UnityEngine;

public class GachaPotentialManager : MonoBehaviour
{
    public GachaManager manager;

    public List<GachaCharacterPotentialConfig> configurations =
        new List<GachaCharacterPotentialConfig>();

    public int GetPotentialLevel(string characterId)
    {
        if (manager == null || manager.Save == null)
            return 0;

        GachaOwnedCharacter owned =
            manager.Save.ownedCharacters.Find(
                x => x.characterId == characterId
            );

        return owned == null ? 0 : owned.potentialLevel;
    }

    public GachaCharacterPotentialConfig GetConfig(
        string characterId)
    {
        return configurations.Find(
            x => x != null && x.characterId == characterId
        );
    }

    public CharacterPotentialStage GetCurrentStage(
        string characterId)
    {
        int level = GetPotentialLevel(characterId);

        if (level <= 0)
            return null;

        GachaCharacterPotentialConfig config =
            GetConfig(characterId);

        return config == null
            ? null
            : config.GetStage(level);
    }

    public bool IsStageUnlocked(
        string characterId,
        int stage)
    {
        return GetPotentialLevel(characterId) >= stage;
    }

    public CharacterCombatStats ApplyPotentialStats(
        string characterId,
        CharacterCombatStats original)
    {
        if (original == null)
            return new CharacterCombatStats();

        CharacterCombatStats result = original.Clone();

        GachaCharacterPotentialConfig config =
            GetConfig(characterId);

        if (config == null)
            return result;

        int level = Mathf.Clamp(
            GetPotentialLevel(characterId),
            0,
            config.maxPotentialLevel
        );

        for (int i = 1; i <= level; i++)
        {
            CharacterPotentialStage stage =
                config.GetStage(i);

            if (stage == null)
                continue;

            result.maxHP *= 1f + stage.hpBonus / 100f;
            result.attack *= 1f + stage.attackBonus / 100f;
            result.defense *= 1f + stage.defenseBonus / 100f;
        }

        return result;
    }

    public float GetSkillBonus(
        string characterId,
        bool isUltimate)
    {
        GachaCharacterPotentialConfig config =
            GetConfig(characterId);

        if (config == null)
            return 0f;

        int level = GetPotentialLevel(characterId);
        float bonus = 0f;

        for (int i = 1; i <= level; i++)
        {
            CharacterPotentialStage stage =
                config.GetStage(i);

            if (stage == null)
                continue;

            bonus += isUltimate
                ? stage.ultimateMultiplierBonus
                : stage.skillMultiplierBonus;
        }

        return bonus;
    }
}
