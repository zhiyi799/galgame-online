using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GachaAscensionMaterialRequirement
{
    [Min(1)]
    public int targetAscension = 1;

    public List<GachaMaterialCost> costs =
        new List<GachaMaterialCost>();
}

[CreateAssetMenu(
    fileName = "CharacterGrowthMaterials",
    menuName = "ASTRAL BLOOM/Character Growth Materials"
)]
public class GachaCharacterGrowthMaterialConfig : ScriptableObject
{
    [Header("角色")]
    public string characterId;

    [Header("经验材料")]
    public string experienceMaterialId;

    [Min(1)]
    public int experiencePerMaterial = 100;

    [Header("突破材料")]
    public List<GachaAscensionMaterialRequirement> ascensionCosts =
        new List<GachaAscensionMaterialRequirement>();

    public List<GachaMaterialCost> GetAscensionCosts(
        int targetAscension)
    {
        GachaAscensionMaterialRequirement requirement =
            ascensionCosts.Find(
                x => x != null &&
                     x.targetAscension == targetAscension
            );

        return requirement == null
            ? null
            : requirement.costs;
    }
}
