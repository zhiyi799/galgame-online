using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GachaPotentialUI : MonoBehaviour
{
    public GachaPotentialManager potentialManager;

    [Header("显示")]
    public TMP_Text levelText;
    public TMP_Text stageNameText;
    public TMP_Text descriptionText;
    public TMP_Text progressText;

    [Header("六阶节点")]
    public Image[] stageIcons;

    private string currentCharacterId;

    public void Open(string characterId)
    {
        currentCharacterId = characterId;
        Refresh();
    }

    public void Refresh()
    {
        if (potentialManager == null ||
            string.IsNullOrEmpty(currentCharacterId))
            return;

        int level =
            potentialManager.GetPotentialLevel(currentCharacterId);

        if (levelText != null)
            levelText.text = "潜能 " + level + " / 6";

        if (progressText != null)
            progressText.text = level + " / 6 阶段已解锁";

        CharacterPotentialStage current =
            potentialManager.GetStage(
                currentCharacterId,
                level
            );

        if (stageNameText != null)
            stageNameText.text =
                current == null ? "尚未激活" : current.stageName;

        if (descriptionText != null)
        {
            if (current == null)
            {
                descriptionText.text =
                    "重复获得该角色以解锁潜能阶段。";
            }
            else
            {
                descriptionText.text = current.description;
            }
        }

        if (stageIcons != null)
        {
            for (int i = 0; i < stageIcons.Length; i++)
            {
                if (stageIcons[i] == null)
                    continue;

                bool unlocked = i < level;

                stageIcons[i].color = unlocked
                    ? Color.white
                    : new Color(0.4f, 0.4f, 0.4f, 0.5f);
            }
        }
    }
}
