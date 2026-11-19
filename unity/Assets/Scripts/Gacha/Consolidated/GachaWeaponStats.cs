using System;

[Serializable]
public struct GachaWeaponStats
{
    public float attack;
    public float hp;
    public float defense;
    public float critRate;

    public static GachaWeaponStats Zero
    {
        get
        {
            return new GachaWeaponStats();
        }
    }

    public static GachaWeaponStats operator +(
        GachaWeaponStats a,
        GachaWeaponStats b)
    {
        return new GachaWeaponStats
        {
            attack = a.attack + b.attack,
            hp = a.hp + b.hp,
            defense = a.defense + b.defense,
            critRate = a.critRate + b.critRate
        };
    }
}
