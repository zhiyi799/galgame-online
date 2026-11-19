using System.Collections.Generic;
using UnityEngine;

public class GachaSkillMaterialService : MonoBehaviour
{
    public GachaMaterialInventory inventory;
    public GachaSkillMaterialConfig config;

    public bool TrySpendUpgradeMaterials(
        string characterId,
        string skillId,
        int targetLevel)
    {
        if (inventory == null || config == null)
            return false;

        if (string.IsNullOrWhiteSpace(characterId) ||
            string.IsNullOrWhiteSpace(skillId) ||
            targetLevel <= 1)
            return false;

        List<GachaMaterialCost> costs =
            config.GetCosts(
                characterId,
                skillId,
                targetLevel
            );

        if (costs == null || costs.Count == 0)
            return false;

        return inventory.TrySpendBatch(costs);
    }
}
