using UnityEngine;

public class GachaWeaponPassiveManager : MonoBehaviour
{
    public GachaWeaponManager weaponManager;

    public float GetWeaponBonus(
        string characterId,
        WeaponPassiveType type)
    {
        if (weaponManager == null)
            return 0f;

        GachaWeaponOwnedSave owned =
            weaponManager.GetEquippedWeapon(characterId);

        if (owned == null)
            return 0f;

        GachaWeaponConfig config =
            weaponManager.GetWeaponConfig(owned.weaponId);

        if (config == null || config.passive == null)
            return 0f;

        if (!IsMatchingType(config.passive.passiveType, type))
            return 0f;

        return config.passive.GetValue(
            Mathf.Clamp(owned.refinement, 1, 5)
        );
    }

    private bool IsMatchingType(
        WeaponPassiveType a,
        WeaponPassiveType b)
    {
        return a == b;
    }

    public CharacterCombatStats ApplyWeaponPassive(
        string characterId,
        CharacterCombatStats original)
    {
        if (original == null)
            return new CharacterCombatStats();

        CharacterCombatStats result = original.Clone();

        result.attack *=
            1f + GetWeaponBonus(
                characterId,
                WeaponPassiveType.AttackBonus
            ) / 100f;

        result.critRate +=
            GetWeaponBonus(
                characterId,
                WeaponPassiveType.CritRateBonus
            );

        result.critDamage +=
            GetWeaponBonus(
                characterId,
                WeaponPassiveType.CritDamageBonus
            );

        result.defense *=
            1f + GetWeaponBonus(
                characterId,
                WeaponPassiveType.DefenseBonus
            ) / 100f;

        result.energyRecharge +=
            GetWeaponBonus(
                characterId,
                WeaponPassiveType.EnergyRechargeBonus
            );

        return result;
    }

    public float GetSkillDamageBonus(string characterId)
    {
        return GetWeaponBonus(
            characterId,
            WeaponPassiveType.SkillDamageBonus
        );
    }

    public float GetUltimateDamageBonus(string characterId)
    {
        return GetWeaponBonus(
            characterId,
            WeaponPassiveType.UltimateDamageBonus
        );
    }
}
