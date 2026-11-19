using System;

[Serializable]
public class GachaCombatActionResult
{
    public bool success;
    public string error;

    public string attackerCharacterId;
    public string targetId;

    public GachaCombatActionType actionType;

    public float attack;
    public float rawMultiplier;
    public float damageBonusMultiplier;
    public float defenseMultiplier;
    public float criticalMultiplier;
    public float damageReductionMultiplier;

    public float finalDamage;
    public bool isCritical;

    public static GachaCombatActionResult Failed(string error)
    {
        return new GachaCombatActionResult
        {
            success = false,
            error = error
        };
    }
}
