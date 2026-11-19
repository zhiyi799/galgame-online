using System.Collections.Generic;
using UnityEngine;

public class GachaPotentialManager : MonoBehaviour
{
    public GachaManager manager;

    public List<GachaCharacterPotential> potentialDatabase =
        new List<GachaCharacterPotential>();

    public GachaCharacterPotentialSave GetPotentialSave(
        string characterId)
    {
        if (manager == null || manager.Save == null)
            return null;

        var save = manager.Save.characterPotentials.Find(
            x => x.characterId == characterId
        );

        if (save == null)
        {
            save = new GachaCharacterPotentialSave
            {
                characterId = characterId,
                potentialPoints = 0
            };

            manager.Save.characterPotentials.Add(save);
        }

        if (save.unlockedNodes == null)
            save.unlockedNodes = new List<string>();

        return save;
    }

    public bool AddPotentialPoints(
        string characterId,
        int amount)
    {
        if (amount <= 0 || manager == null ||
            !manager.HasCharacter(characterId))
            return false;

        GachaCharacterPotentialSave save =
            GetPotentialSave(characterId);

        if (save == null)
            return false;

        save.potentialPoints += amount;

        manager.SaveGame();
        return true;
    }

    public bool UnlockNode(
        string characterId,
        string nodeId)
    {
        GachaCharacterPotential config =
            potentialDatabase.Find(
                x => x != null &&
                     x.characterId == characterId
            );

        if (config == null || config.nodes == null)
            return false;

        PotentialNode node =
            System.Array.Find(
                config.nodes,
                x => x != null && x.nodeId == nodeId
            );

        if (node == null)
            return false;

        GachaCharacterPotentialSave save =
            GetPotentialSave(characterId);

        if (save == null ||
            save.unlockedNodes.Contains(nodeId))
            return false;

        if (save.potentialPoints < node.requiredPotential)
            return false;

        save.potentialPoints -= node.requiredPotential;
        save.unlockedNodes.Add(nodeId);

        manager.SaveGame();
        return true;
    }

    public bool IsNodeUnlocked(
        string characterId,
        string nodeId)
    {
        GachaCharacterPotentialSave save =
            GetPotentialSave(characterId);

        return save != null &&
               save.unlockedNodes.Contains(nodeId);
    }

    public float GetEffectValue(
        string characterId,
        PotentialEffectType type)
    {
        GachaCharacterPotential config =
            potentialDatabase.Find(
                x => x != null &&
                     x.characterId == characterId
            );

        GachaCharacterPotentialSave save =
            GetPotentialSave(characterId);

        if (config == null || save == null ||
            config.nodes == null)
            return 0f;

        float total = 0f;

        foreach (PotentialNode node in config.nodes)
        {
            if (node == null ||
                node.effectType != type)
                continue;

            if (save.unlockedNodes.Contains(node.nodeId))
                total += node.effectValue;
        }

        return total;
    }
}
