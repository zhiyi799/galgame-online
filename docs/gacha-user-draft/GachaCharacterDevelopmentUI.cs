using UnityEngine;
using UnityEngine.UI;

public class GachaCharacterDevelopmentUI : MonoBehaviour
{
    [Header("系统")]
    public GachaManager manager;
    public GachaCharacterGrowthManager growthManager;
    public GachaSkillManager skillManager;
    public GachaPotentialManager potentialManager;
    public GachaTalentManager talentManager;
    public GachaWeaponManager weaponManager;
    public GachaCombatStatResolver statResolver;

    [Header("角色信息")]
    public Text characterNameText;
    public Text levelText;
    public Text ascensionText;

    [Header("属性")]
    public Text hpText;
    public Text attackText;
    public Text defenseText;
    public Text speedText;
    public Text critText;

    [Header("潜能")]
    public Text potentialText;

    [Header("武器")]
    public Text weaponText;

    [Header("技能")]
    public Text basicAttackText;
    public Text skillText;
    public Text ultimateText;
    public Text passiveText;

    private string currentCharacterId;

    public void OpenCharacter(string characterId)
    {
        currentCharacterId = characterId;
        Refresh();
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void Refresh()
    {
        if (string.IsNullOrEmpty(currentCharacterId) ||
            manager == null)
            return;

        GachaCharacter character =
            manager.characterDatabase != null
                ? manager.characterDatabase.GetCharacter(
                    currentCharacterId)
                : null;

        if (character == null)
            return;

        if (characterNameText != null)
            characterNameText.text = character.characterName;

        GachaCharacterGrowthSave growth =
            manager.Save.characterGrowth.Find(
                x => x.characterId == currentCharacterId
            );

        if (growth != null)
        {
            if (levelText != null)
                levelText.text = "等级 " + growth.level;

            if (ascensionText != null)
                ascensionText.text =
                    "突破阶段 " + growth.ascension;
        }

        if (statResolver != null)
        {
            CharacterCombatStats stats =
                statResolver.GetCombatStats(currentCharacterId);

            if (hpText != null)
                hpText.text = "生命 " +
                    Mathf.RoundToInt(stats.maxHP);

            if (attackText != null)
                attackText.text = "攻击 " +
                    Mathf.RoundToInt(stats.attack);

            if (defenseText != null)
                defenseText.text = "防御 " +
                    Mathf.RoundToInt(stats.defense);

            if (speedText != null)
                speedText.text = "速度 " +
                    Mathf.RoundToInt(stats.speed);

            if (critText != null)
                critText.text =
                    "暴击率 " + stats.critRate.ToString("F1") +
                    "% / 暴击伤害 " +
                    stats.critDamage.ToString("F1") + "%";
        }

        if (potentialManager != null)
        {
            GachaCharacterPotentialSave potential =
                potentialManager.GetPotentialSave(
                    currentCharacterId
                );

            if (potentialText != null && potential != null)
            {
                potentialText.text =
                    "潜能点：" + potential.potentialPoints +
                    " / 已解锁：" +
                    potential.unlockedNodes.Count;
            }
        }

        if (weaponManager != null)
        {
            GachaWeaponOwnedSave weapon =
                weaponManager.GetEquippedWeapon(
                    currentCharacterId
                );

            if (weaponText != null)
            {
                if (weapon == null)
                {
                    weaponText.text = "尚未装备武器";
                }
                else
                {
                    GachaWeaponConfig config =
                        weaponManager.GetWeaponConfig(
                            weapon.weaponId
                        );

                    weaponText.text =
                        (config != null
                            ? config.weaponName
                            : weapon.weaponId) +
                        "  Lv." + weapon.level +
                        "  突破 " + weapon.ascension;
                }
            }
        }

        RefreshSkillText();
    }

    private void RefreshSkillText()
    {
        if (skillManager == null)
            return;

        RefreshSkill(
            "basic_attack",
            basicAttackText
        );

        RefreshSkill(
            "character_skill",
            skillText
        );

        RefreshSkill(
            "ultimate",
            ultimateText
        );

        RefreshSkill(
            "passive",
            passiveText
        );
    }

    private void RefreshSkill(
        string skillId,
        Text target)
    {
        if (target == null)
            return;

        int level =
            skillManager.GetSkillLevel(skillId);

        target.text = skillId + " Lv." + level;
    }
}
