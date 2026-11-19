using UnityEngine;

public class GachaCombatFormula : MonoBehaviour
{
    public GachaFinalStatProvider statProvider;
    public GachaSkillManager skillManager;
    public GachaPotentialManager potentialManager;
    public GachaTalentManager talentManager;
    public GachaWeaponPassiveManager weaponPassiveManager;

    public float CalculateDamage(
        string characterId,
        string skillId,
        bool isUltimate,
        float defenseMultiplier = 1f,
        float resistanceMultiplier = 1f)
    {
        if (statProvider == null || skillManager == null)
            return 0f;

        CharacterCombatStats stats =
            statProvider.GetFinalStats(characterId);

        float skillMultiplier =
            skillManager.GetSkillMultiplier(skillId);

        float bonus = 0f;

        if (potentialManager != null)
        {
            bonus += potentialManager.GetSkillBonus(
                characterId,
                isUltimate
            );
        }

        if (talentManager != null)
        {
            bonus += talentManager.GetBonus(
                characterId,
                isUltimate
                    ? CharacterTalentType.UltimateDamageBonus
                    : CharacterTalentType.SkillDamageBonus
            );
        }

        if (weaponPassiveManager != null)
        {
            bonus += isUltimate
                ? weaponPassiveManager.GetUltimateDamageBonus(characterId)
                : weaponPassiveManager.GetSkillDamageBonus(characterId);
        }

        float finalMultiplier =
            skillMultiplier * (1f + bonus / 100f);

        float damage =
            stats.attack *
            finalMultiplier *
            Mathf.Max(0f, defenseMultiplier) *
            Mathf.Max(0f, resistanceMultiplier);

        return Mathf.Max(0f, damage);
    }
}
