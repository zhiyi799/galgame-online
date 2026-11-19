using UnityEngine;
using UnityEngine.UI;

public class GachaPotentialUI : MonoBehaviour
{
    [Header("系统")]
    public GachaPotentialManager potentialManager;

    [Header("界面")]
    public Text characterNameText;
    public Text potentialLevelText;
    public Text potentialTitleText;
    public Text potentialDescriptionText;

    [Header("角色")]
    public string currentCharacterId;

    public void Open(string characterId)
    {
        currentCharacterId = characterId;
        Refresh();
    }

    public void Refresh()
    {
        if (potentialManager == null)
            return;

        GachaCharacterPotentialConfig config =
            potentialManager.GetConfig(currentCharacterId);

        if (config == null)
        {
            Debug.LogWarning(
                "没有找到角色潜能配置：" + currentCharacterId
            );
            return;
        }

        int level =
            potentialManager.GetPotentialLevel(currentCharacterId);

        if (characterNameText != null)
            characterNameText.text = config.characterName;

        if (potentialLevelText != null)
            potentialLevelText.text =
                "潜能 " + level + " / " + config.maxPotentialLevel;

        CharacterPotentialStage stage =
            potentialManager.GetCurrentStage(currentCharacterId);

        if (stage == null)
        {
            if (potentialTitleText != null)
                potentialTitleText.text = "尚未解锁潜能";

            if (potentialDescriptionText != null)
                potentialDescriptionText.text =
                    "再次获得该角色以解锁第一阶潜能。";

            return;
        }

        if (potentialTitleText != null)
            potentialTitleText.text = stage.title;

        if (potentialDescriptionText != null)
            potentialDescriptionText.text = stage.description;
    }
}
