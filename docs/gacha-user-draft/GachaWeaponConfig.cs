using UnityEngine;

public enum WeaponType
{
    Sword,
    GreatSword,
    Pistol,
    Rectifier,
    Gauntlet,
    Spear,
    Catalyst
}

[CreateAssetMenu(
    fileName = "NewWeapon",
    menuName = "ASTRAL BLOOM/Equipment/Weapon"
)]
public class GachaWeaponConfig : ScriptableObject
{
    [Header("武器信息")]
    public string weaponId;
    public string weaponName;
    public WeaponType weaponType;

    [TextArea(2, 6)]
    public string description;

    public Sprite artwork;

    [Header("稀有度")]
    [Range(3, 5)]
    public int rarity = 5;

    [Header("基础属性")]
    public float baseAttack = 100;
    public float baseHP = 0;
    public float baseDefense = 0;
    public float baseCritRate = 0;

    [Header("强化成长")]
    public float attackPerLevel = 4;
    public float hpPerLevel = 0;
    public float defensePerLevel = 0;

    [Header("突破")]
    public int maxLevel = 90;
}
