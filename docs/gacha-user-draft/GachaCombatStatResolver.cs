using UnityEngine;

public class GachaCombatStatResolver : MonoBehaviour
{
    public GachaFinalStatProvider finalStatProvider;

    public GachaPotentialManager potentialManager;

    public GachaTalentManager talentManager;

    public GachaWeaponManager weaponManager;

    public GachaSkillManager skillManager;

    public CharacterCombatStats GetCombatStats(
        string characterId)
    {
        if (finalStatProvider == null)
            return new CharacterCombatStats();

        CharacterCombatStats stats =
            finalStatProvider.GetFinalStats(characterId);

        float attackBonus = 0f;
        float hpBonus = 0f;
        float defenseBonus = 0f;

        if (potentialManager != null)
        {
            attackBonus += potentialManager.GetEffectValue(
                characterId,
                PotentialEffectType.AttackPercent
            );

            hpBonus += potentialManager.GetEffectValue(
                characterId,
                PotentialEffectType.HPPercent
            );

            defenseBonus += potentialManager.GetEffectValue(
                characterId,
                PotentialEffectType.DefensePercent
            );

            stats.critRate += potentialManager.GetEffectValue(
                characterId,
                PotentialEffectType.CritRate
            );

            stats.critDamage += potentialManager.GetEffectValue(
                characterId,
                PotentialEffectType.CritDamage
            );

            stats.energyRecharge +=
                potentialManager.GetEffectValue(
                    characterId,
                    PotentialEffectType.EnergyRecharge
                ) * 100f;
        }

        if (talentManager != null)
        {
            attackBonus += talentManager.GetTalentBonus(
                characterId,
                CharacterTalentType.AttackPercent
            );

            hpBonus += talentManager.GetTalentBonus(
                characterId,
                CharacterTalentType.HPPercent
            );

            defenseBonus += talentManager.GetTalentBonus(
                characterId,
                CharacterTalentType.DefensePercent
            );
        }

        if (weaponManager != null)
        {
            attackBonus += weaponManager.GetWeaponPassiveBonus(
                characterId,
                WeaponPassiveType.AttackPercent
            );

            stats.critRate +=
                weaponManager.GetWeaponPassiveBonus(
                    characterId,
                    WeaponPassiveType.CritRate
                );

            stats.critDamage +=
                weaponManager.GetWeaponPassiveBonus(
                    characterId,
                    WeaponPassiveType.CritDamage
                );

            stats.energyRecharge +=
                weaponManager.GetWeaponPassiveBonus(
                    characterId,
                    WeaponPassiveType.EnergyRecharge
                ) * 100f;
        }

        stats.attack *= 1f + attackBonus;
        stats.maxHP *= 1f + hpBonus;
        stats.defense *= 1f + defenseBonus;

        return stats;
    }

    public float GetSkillBonus(
        string characterId,
        CharacterSkillType skillType)
    {
        float bonus = 0f;

        if (potentialManager != null)
        {
            switch (skillType)
            {
                case CharacterSkillType.Skill:
                    bonus += potentialManager.GetEffectValue(
                        characterId,
                        PotentialEffectType.SkillDamage
                    );
                    break;

                case CharacterSkillType.Ultimate:
                    bonus += potentialManager.GetEffectValue(
                        characterId,
                        PotentialEffectType.UltimateDamage
                    );
                    break;
            }
        }

        if (talentManager != null)
        {
            switch (skillType)
            {
                case CharacterSkillType.BasicAttack:
                    bonus += talentManager.GetTalentBonus(
                        characterId,
                        CharacterTalentType.BasicAttackDamage
                    );
                    break;

                case CharacterSkillType.Skill:
                    bonus += talentManager.GetTalentBonus(
                        characterId,
                        CharacterTalentType.SkillDamage
                    );
                    break;

                case CharacterSkillType.Ultimate:
                    bonus += talentManager.GetTalentBonus(
                        characterId,
                        CharacterTalentType.UltimateDamage
                    );
                    break;
            }
        }

        if (weaponManager != null)
        {
            switch (skillType)
            {
                case CharacterSkillType.BasicAttack:
                    bonus += weaponManager.GetWeaponPassiveBonus(
                        characterId,
                        WeaponPassiveType.BasicAttackDamage
                    );
                    break;

                case CharacterSkillType.Skill:
                    bonus += weaponManager.GetWeaponPassiveBonus(
                        characterId,
                        WeaponPassiveType.SkillDamage
                    );
                    break;

                case CharacterSkillType.Ultimate:
                    bonus += weaponManager.GetWeaponPassiveBonus(
                        characterId,
                        WeaponPassiveType.UltimateDamage
                    );
                    break;
            }
        }

        return bonus;
    }

    public float CalculateDamage(
        string characterId,
        string skillId,
        CharacterSkillType skillType,
        float enemyDefense,
        float enemyDamageReduction = 0f,
        float externalMultiplier = 1f)
    {
        CharacterCombatStats stats =
            GetCombatStats(characterId);

        if (skillManager == null)
            return 0f;

        float skillMultiplier =
            skillManager.GetSkillMultiplier(skillId);

        float bonus =
            GetSkillBonus(characterId, skillType);

        float defenseMultiplier =
            100f / (100f + Mathf.Max(0f, enemyDefense));

        float damage =
            stats.attack *
            skillMultiplier *
            (1f + bonus) *
            Mathf.Max(0f, externalMultiplier) *
            defenseMultiplier;

        float reduction = Mathf.Clamp(
            enemyDamageReduction,
            0f,
            0.95f
        );

        damage *= 1f - reduction;

        bool critical =
            Random.value < stats.critRate / 100f;

        if (critical)
        {
            damage *= 1f + stats.critDamage / 100f;
        }

        return Mathf.Max(0f, damage);
    }
}
