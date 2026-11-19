using UnityEngine;

public class GachaCharacterStatCalculator : MonoBehaviour
{
    public GachaCharacterGrowthManager growthManager;
    public GachaGrowthConfig config;

    public CharacterCombatStats Calculate(GachaCharacter character)
    {
        if (character == null || character.baseStats == null)
            return new CharacterCombatStats();

        CharacterCombatStats result =
            character.baseStats.Clone();

        if (growthManager == null || config == null)
            return result;

        GachaCharacterGrowthSave growth =
            growthManager.GetGrowth(character.id);

        if (growth == null)
            return result;

        int level = Mathf.Clamp(
            growth.level,
            1,
            config.maxLevel
        );

        float hpMultiplier =
            config.hpGrowth.Evaluate(level);

        float attackMultiplier =
            config.attackGrowth.Evaluate(level);

        float defenseMultiplier =
            config.defenseGrowth.Evaluate(level);

        float speedMultiplier =
            config.speedGrowth.Evaluate(level);

        result.maxHP *= hpMultiplier;
        result.attack *= attackMultiplier;
        result.defense *= defenseMultiplier;
        result.speed *= speedMultiplier;

        float ascensionBonus = growth.ascension;

        result.maxHP *=
            1 + ascensionBonus * config.ascensionHPBonus;

        result.attack *=
            1 + ascensionBonus * config.ascensionAttackBonus;

        result.defense *=
            1 + ascensionBonus * config.ascensionDefenseBonus;

        result.maxHP = Mathf.Max(1, result.maxHP);
        result.attack = Mathf.Max(1, result.attack);
        result.defense = Mathf.Max(0, result.defense);
        result.speed = Mathf.Max(1, result.speed);

        return result;
    }
}
