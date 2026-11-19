using UnityEngine;
using UnityEngine.UI;

public class GachaCharacterGrowthUI : MonoBehaviour
{
    public GachaCharacterGrowthManager growthManager;

    [Header("角色")]
    public string selectedCharacterId;

    [Header("显示")]
    public Text levelText;
    public Text experienceText;
    public Text ascensionText;
    public Text materialText;

    [Header("按钮")]
    public Button levelUpButton;
    public Button ascendButton;

    public int experiencePerClick = 100;

    private void Start()
    {
        if (levelUpButton != null)
            levelUpButton.onClick.AddListener(LevelUp);

        if (ascendButton != null)
            ascendButton.onClick.AddListener(Ascend);

        Refresh();
    }

    public void SelectCharacter(string characterId)
    {
        selectedCharacterId = characterId;
        Refresh();
    }

    public void LevelUp()
    {
        if (growthManager == null)
            return;

        growthManager.AddExperience(
            selectedCharacterId,
            experiencePerClick
        );

        Refresh();
    }

    public void Ascend()
    {
        if (growthManager == null)
            return;

        growthManager.Ascend(selectedCharacterId);
        Refresh();
    }

    public void Refresh()
    {
        if (growthManager == null ||
            string.IsNullOrEmpty(selectedCharacterId))
            return;

        GachaCharacterGrowthSave growth =
            growthManager.GetGrowth(selectedCharacterId);

        if (growth == null)
            return;

        if (levelText != null)
            levelText.text = "等级 Lv." + growth.level;

        if (experienceText != null)
        {
            int required =
                growthManager.config.GetRequiredExperience(
                    growth.level
                );

            experienceText.text =
                "经验：" +
                growth.experience +
                "/" +
                required;
        }

        if (ascensionText != null)
            ascensionText.text = "突破阶段：" + growth.ascension;

        if (materialText != null)
            materialText.text = "材料库存：" +
                growthManager.GetMaterialAmount(
                    "ascension_material"
                );
    }

    private void OnDestroy()
    {
        if (levelUpButton != null)
            levelUpButton.onClick.RemoveListener(LevelUp);

        if (ascendButton != null)
            ascendButton.onClick.RemoveListener(Ascend);
    }
}
