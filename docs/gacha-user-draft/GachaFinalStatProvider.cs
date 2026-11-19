using UnityEngine;

public class GachaFinalStatProvider : MonoBehaviour
{
    public GachaCharacterDatabase characterDatabase;
    public GachaCharacterStatCalculator statCalculator;
    public GachaWeaponManager weaponManager;

    public CharacterCombatStats GetFinalStats(string characterId)
    {
        if (characterDatabase == null ||
            statCalculator == null)
            return new CharacterCombatStats();

        GachaCharacter character =
            characterDatabase.GetCharacter(characterId);

        if (character == null)
            return new CharacterCombatStats();

        CharacterCombatStats stats =
            statCalculator.Calculate(character);

        if (weaponManager != null)
        {
            stats = weaponManager.AddWeaponStats(
                characterId,
                stats
            );
        }

        return stats;
    }
}
