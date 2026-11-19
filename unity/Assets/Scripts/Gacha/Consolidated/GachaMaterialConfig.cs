using UnityEngine;

public enum GachaMaterialCategory
{
    CharacterEXP,
    CharacterAscension,
    SkillUpgrade,
    WeaponEXP,
    WeaponAscension,
    WeaponRefinement,
    Currency,
    Other
}

[CreateAssetMenu(
    fileName = "NewMaterial",
    menuName = "ASTRAL BLOOM/Material Config"
)]
public class GachaMaterialConfig : ScriptableObject
{
    [Header("材料信息")]
    public string materialId;
    public string materialName;

    [TextArea]
    public string description;

    public Sprite icon;

    public GachaMaterialCategory category;

    [Header("稀有度")]
    [Range(1, 5)]
    public int rarity = 1;

    [Header("库存限制")]
    [Min(0)]
    public int maxStack = 999999;

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(materialId)
            && maxStack > 0;
    }
}
