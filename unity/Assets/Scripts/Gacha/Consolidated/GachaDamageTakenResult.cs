using System;

[Serializable]
public class GachaDamageTakenResult
{
    public bool success;
    public string error;

    public string targetId;
    public string attackerId;

    public float requestedDamage;
    public float actualDamage;

    public float hpBefore;
    public float hpAfter;

    public bool targetDefeated;

    public static GachaDamageTakenResult Failed(string message)
    {
        return new GachaDamageTakenResult
        {
            success = false,
            error = message
        };
    }
}
