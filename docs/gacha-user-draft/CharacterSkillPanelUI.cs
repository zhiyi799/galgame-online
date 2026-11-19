using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSkillPanelUI : MonoBehaviour
{
    [System.Serializable]
    public class SkillSlot
    {
        public Text nameText;
        public Text levelText;
        public Text descriptionText;
        public Text multiplierText;
        public Button upgradeButton;
    }

    public GachaSkillManager skillManager;

    public List<SkillSlot> slots =
        new List<SkillSlot>();

    public List<GachaCharacterSkill> skills =
        new List<GachaCharacterSkill>();

    private string currentCharacterId;

    public void Refresh(string characterId)
    {
        currentCharacterId = characterId;

        for (int i = 0; i < slots.Count; i++)
        {
            if (i >= skills.Count)
                continue;

            GachaCharacterSkill skill = skills[i];
            SkillSlot slot = slots[i];

            if (skill == null || slot == null)
                continue;

            if (skill.characterId != characterId)
            {
                Set(slot.nameText, "未配置");
                Set(slot.levelText, "");
                Set(slot.descriptionText, "");
                Set(slot.multiplierText, "");
                if (slot.upgradeButton != null)
                    slot.upgradeButton.interactable = false;
                continue;
            }

            int level =
                skillManager.GetSkillLevel(skill.skillId);

            Set(slot.nameText, skill.skillName);

            Set(slot.levelText,
                "Lv." + level + " / " + skill.maxLevel);

            Set(slot.descriptionText, skill.description);

            Set(slot.multiplierText,
                "效果倍率 " +
                skill.GetMultiplier(level).ToString("F2"));

            if (slot.upgradeButton != null)
            {
                slot.upgradeButton.interactable =
                    level < skill.maxLevel;

                slot.upgradeButton.onClick.RemoveAllListeners();

                string id = skill.skillId;

                slot.upgradeButton.onClick.AddListener(
                    () => Upgrade(id)
                );
            }
        }
    }

    private void Upgrade(string skillId)
    {
        if (skillManager == null)
            return;

        bool success =
            skillManager.UpgradeSkill(skillId);

        if (success)
            Refresh(currentCharacterId);
    }

    private void Set(Text target, string value)
    {
        if (target != null)
            target.text = value;
    }
}
