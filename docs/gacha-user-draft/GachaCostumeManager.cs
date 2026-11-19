using System.Collections.Generic;
using UnityEngine;

public class GachaCostumeManager : MonoBehaviour
{
    public GachaManager manager;

    public List<GachaCharacterCostume> allCostumes =
        new List<GachaCharacterCostume>();

    private void Start()
    {
        InitializeDefaultCostumes();
    }

    private void InitializeDefaultCostumes()
    {
        if (manager == null)
            return;

        foreach (GachaCharacterCostume costume in allCostumes)
        {
            if (costume == null)
                continue;

            if (costume.unlockedByDefault)
                UnlockCostume(costume.costumeId);
        }
    }

    public bool IsUnlocked(string costumeId)
    {
        return manager.Save.costumes.Exists(
            x => x.costumeId == costumeId && x.unlocked
        );
    }

    public bool UnlockCostume(string costumeId)
    {
        if (manager == null ||
            manager.Save == null ||
            string.IsNullOrEmpty(costumeId))
            return false;

        GachaCharacterCostumeSave existing =
            manager.Save.costumes.Find(
                x => x.costumeId == costumeId
            );

        if (existing != null)
        {
            existing.unlocked = true;
        }
        else
        {
            manager.Save.costumes.Add(
                new GachaCharacterCostumeSave
                {
                    costumeId = costumeId,
                    unlocked = true
                }
            );
        }

        manager.SaveGame();
        return true;
    }

    public bool EquipCostume(string characterId, string costumeId)
    {
        if (manager == null ||
            !manager.HasCharacter(characterId) ||
            !IsUnlocked(costumeId))
            return false;

        GachaCharacterCostume costume =
            allCostumes.Find(x => x.costumeId == costumeId);

        if (costume == null ||
            costume.characterId != characterId)
            return false;

        GachaCharacterAppearanceSave appearance =
            manager.Save.appearances.Find(
                x => x.characterId == characterId
            );

        if (appearance == null)
        {
            appearance = new GachaCharacterAppearanceSave
            {
                characterId = characterId,
                equippedCostumeId = costumeId
            };

            manager.Save.appearances.Add(appearance);
        }
        else
        {
            appearance.equippedCostumeId = costumeId;
        }

        manager.SaveGame();
        return true;
    }

    public GachaCharacterCostume GetEquippedCostume(
        string characterId)
    {
        if (manager == null)
            return null;

        GachaCharacterAppearanceSave appearance =
            manager.Save.appearances.Find(
                x => x.characterId == characterId
            );

        if (appearance == null)
            return null;

        return allCostumes.Find(
            x => x.costumeId == appearance.equippedCostumeId
        );
    }

    public Sprite GetCurrentArtwork(GachaCharacter character)
    {
        if (character == null)
            return null;

        GachaCharacterCostume costume =
            GetEquippedCostume(character.id);

        if (costume != null && costume.artwork != null)
            return costume.artwork;

        return character.portrait;
    }
}
