using UnityEngine;
using TMPro;

public class GachaTalentUI : MonoBehaviour
{
    public GachaTalentManager talentManager;

    public TMP_Text titleText;
    public TMP_Text ascensionText;
    public TMP_Text talentListText;

    private string currentCharacterId;

    public void Open(string characterId)
    {
        currentCharacterId = characterId;
        Refresh();
    }

    public void Refresh()
    {
        if (talentManager == null ||
            string.IsNullOrEmpty(currentCharacterId))
            return;

        int ascension =
            talentManager.GetAscension(currentCharacterId);

        if (titleText != null)
            titleText.text = "角色天赋";

        if (ascensionText != null)
            ascensionText.text = "突破等级 " + ascension;

        if (talentListText == null)
            return;

        var config =
            talentManager.GetConfig(currentCharacterId);

        if (config == null || config.talents == null)
        {
            talentListText.text = "暂无天赋配置";
            return;
        }

        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();

        foreach (var talent in config.talents)
        {
            if (talent == null)
                continue;

            bool unlocked =
                talentManager.IsUnlocked(
                    currentCharacterId,
                    talent
                );

            builder.AppendLine(
                (unlocked ? "● " : "○ ") +
                talent.talentName
            );

            builder.AppendLine(
                unlocked
                    ? talent.description
                    : "需要突破等级 " +
                      talent.requiredAscension
            );

            builder.AppendLine();
        }

        talentListText.text = builder.ToString();
    }
}
