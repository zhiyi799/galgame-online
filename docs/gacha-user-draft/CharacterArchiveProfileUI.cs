using UnityEngine;
using UnityEngine.UI;

public class CharacterArchiveProfileUI : MonoBehaviour
{
    [Header("角色数据库")]
    public GachaCharacterDatabase characterDatabase;

    [Header("角色养成")]
    public GachaCharacterGrowthManager growthManager;
    public GachaPotentialManager potentialManager;

    [Header("UI")]
    public Image characterPortrait;
    public Text characterNameText;
    public Text rarityText;
    public Text elementText;
    public Text roleText;
    public Text levelText;
    public Text potentialText;
    public Text descriptionText;

    public void Refresh(string characterId)
    {
        if (characterDatabase == null)
            return;

        GachaCharacter character =
            characterDatabase.GetCharacter(characterId);

        if (character == null)
            return;

        if (characterPortrait != null)
        {
            characterPortrait.sprite = character.portrait;
            characterPortrait.preserveAspect = true;
        }

        Set(characterNameText, character.characterName);
        Set(rarityText, GetRarityName(character.rarity));
        Set(elementText, character.element);
        Set(roleText, character.combatRole);
        Set(descriptionText, character.description);

        GachaCharacterGrowthSave growth =
            growthManager != null
                ? growthManager.GetGrowth(characterId)
                : null;

        int level = growth == null ? 1 : growth.level;

        Set(levelText, "LV. " + level);

        int potential = potentialManager != null
            ? potentialManager.GetPotentialLevel(characterId)
            : 0;

        Set(potentialText, "潜能 " + potential + " / 6");
    }

    private string GetRarityName(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.FiveStar:
                return "★★★★★";

            case Rarity.FourStar:
                return "★★★★";

            default:
                return "★★★";
        }
    }

    private void Set(Text target, string value)
    {
        if (target != null)
            target.text = value;
    }
}
