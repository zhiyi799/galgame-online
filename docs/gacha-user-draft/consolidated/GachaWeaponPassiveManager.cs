using UnityEngine;

public class GachaWeaponPassiveManager : MonoBehaviour
{
    [Header("依赖")]
    public GachaWeaponManager weaponManager;

    public float GetPassiveBonus(
        string characterId,
        WeaponPassiveType type
    )
    {
        if (weaponManager == null)
            return 0f;

        return weaponManager.GetPassiveValue(
            characterId,
            type
        );
    }

    public void ApplyWeaponPassiveStats(
        string characterId,
        CharacterCombatStats stats
    )
    {
        if (stats == null)
            return;

        stats.attack *= 1f + GetPassiveBonus(
            characterId,
            WeaponPassiveType.AttackBonus
        );

        stats.maxHP *= 1f + GetPassiveBonus(
            characterId,
            WeaponPassiveType.HPBonus
        );

        stats.defense *= 1f + GetPassiveBonus(
            characterId,
            WeaponPassiveType.DefenseBonus
        );

        stats.critRate += GetPassiveBonus(
            characterId,
            WeaponPassiveType.CritRateBonus
        );

        stats.critDamage += GetPassiveBonus(
            characterId,
            WeaponPassiveType.CritDamageBonus
        );

        stats.energyRecharge *= 1f + GetPassiveBonus(
            characterId,
            WeaponPassiveType.EnergyRechargeBonus
        );
    }

    public float GetSkillDamageBonus(string characterId)
    {
        return GetPassiveBonus(
            characterId,
            WeaponPassiveType.SkillDamageBonus
        );
    }

    public float GetUltimateDamageBonus(string characterId)
    {
        return GetPassiveBonus(
            characterId,
            WeaponPassiveType.UltimateDamageBonus
        );
    }
}
