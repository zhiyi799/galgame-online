using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterWeaponPanelUI : MonoBehaviour
{
    [Header("武器系统")]
    public GachaWeaponManager weaponManager;

    [Header("双区域显示")]
    public CharacterWeaponDualDisplayUI dualDisplayUI;

    [Header("界面")]
    public TMP_Text weaponNameText;
    public TMP_Text weaponLevelText;
    public TMP_Text weaponAttackText;
    public TMP_Text weaponDescriptionText;
    public TMP_Text weaponAscensionText;
    public Dropdown weaponDropdown;

    public Button equipButton;
    public Button unequipButton;
    public Button enhanceButton;
    public Button ascendButton;
    public Button refineButton;

    [Header("测试强化材料数量")]
    [Min(1)]
    public int testEnhanceMaterialCount = 5;

    private string currentCharacterId;

    private readonly List<GachaWeaponOwnedSave> availableWeapons =
        new List<GachaWeaponOwnedSave>();

    private void Awake()
    {
        if (equipButton != null)
            equipButton.onClick.AddListener(EquipSelected);

        if (unequipButton != null)
            unequipButton.onClick.AddListener(Unequip);

        if (enhanceButton != null)
            enhanceButton.onClick.AddListener(Enhance);

        if (ascendButton != null)
            ascendButton.onClick.AddListener(Ascend);

        if (refineButton != null)
            refineButton.onClick.AddListener(Refine);

        if (weaponDropdown != null)
            weaponDropdown.onValueChanged.AddListener(OnWeaponSelected);
    }

    public void Refresh(string characterId)
    {
        currentCharacterId = characterId;

        if (weaponManager == null ||
            weaponManager.gachaManager == null)
            return;

        RefreshWeaponList();
        RefreshEquippedWeapon();
    }

    private void RefreshWeaponList()
    {
        availableWeapons.Clear();

        List<string> options = new List<string>();

        foreach (GachaWeaponOwnedSave weapon
                 in weaponManager.gachaManager.Save.ownedWeapons)
        {
            if (weapon == null)
                continue;

            GachaWeaponConfig config =
                weaponManager.GetConfig(weapon.weaponId);

            if (config == null)
                continue;

            availableWeapons.Add(weapon);

            options.Add(
                config.weaponName + "  Lv." + weapon.level
            );
        }

        if (weaponDropdown == null)
            return;

        weaponDropdown.ClearOptions();
        weaponDropdown.AddOptions(options);

        if (options.Count > 0)
            weaponDropdown.value = 0;

        weaponDropdown.RefreshShownValue();
    }

    private GachaWeaponOwnedSave GetSelectedWeapon()
    {
        if (weaponDropdown == null ||
            availableWeapons.Count == 0)
            return null;

        int index = weaponDropdown.value;

        if (index < 0 || index >= availableWeapons.Count)
            return null;

        return availableWeapons[index];
    }

    private void OnWeaponSelected(int index)
    {
        RefreshSelectedWeapon();
    }

    private void RefreshSelectedWeapon()
    {
        if (dualDisplayUI != null)
            dualDisplayUI.RefreshSelectedWeapon(GetSelectedWeapon());

        GachaWeaponOwnedSave weapon = GetSelectedWeapon();

        if (weapon == null)
            return;

        GachaWeaponConfig config =
            weaponManager.GetConfig(weapon.weaponId);

        if (config == null)
            return;

        Set(weaponNameText, config.weaponName);
        Set(weaponLevelText, "等级：Lv." + weapon.level);

        Set(
            weaponAttackText,
            "攻击力：" +
            config.GetAttack(weapon.level, weapon.ascension)
        );

        Set(weaponDescriptionText, config.description);

        Set(
            weaponAscensionText,
            "突破阶段：" + weapon.ascension
        );
    }

    private void RefreshEquippedWeapon()
    {
        GachaWeaponOwnedSave equipped =
            weaponManager.GetEquippedWeapon(currentCharacterId);

        if (dualDisplayUI != null)
            dualDisplayUI.RefreshEquippedWeapon(equipped);

        if (equipped == null)
        {
            Set(weaponNameText, "当前未装备武器");
            Set(weaponLevelText, "");
            Set(weaponAttackText, "");
            Set(weaponDescriptionText, "");
            Set(weaponAscensionText, "");
        }
        else
        {
            GachaWeaponConfig config =
                weaponManager.GetConfig(equipped.weaponId);

            if (config != null)
            {
                Set(weaponNameText, config.weaponName);

                Set(
                    weaponLevelText,
                    "等级：Lv." + equipped.level
                );

                Set(
                    weaponAttackText,
                    "攻击力：" +
                    config.GetAttack(
                        equipped.level,
                        equipped.ascension
                    )
                );

                Set(weaponDescriptionText, config.description);

                Set(
                    weaponAscensionText,
                    "突破阶段：" + equipped.ascension
                );
            }
        }

        RefreshSelectedWeapon();
    }

    private void EquipSelected()
    {
        GachaWeaponOwnedSave weapon = GetSelectedWeapon();

        if (weapon == null)
            return;

        if (weaponManager.EquipWeapon(
            currentCharacterId,
            weapon.instanceId))
        {
            Refresh(currentCharacterId);
        }
    }

    private void Unequip()
    {
        if (weaponManager.UnequipWeapon(currentCharacterId))
            Refresh(currentCharacterId);
    }

    private void Enhance()
    {
        GachaWeaponOwnedSave weapon = GetSelectedWeapon();

        if (weapon == null)
            return;

        if (testEnhanceMaterialCount <= 0)
            return;

        if (weaponManager.EnhanceWeapon(
            weapon.instanceId,
            testEnhanceMaterialCount))
        {
            Refresh(currentCharacterId);
        }
    }

    private void Ascend()
    {
        GachaWeaponOwnedSave weapon = GetSelectedWeapon();

        if (weapon == null)
            return;

        if (weaponManager.AscendWeapon(weapon.instanceId))
            Refresh(currentCharacterId);
    }

    private void Refine()
    {
        GachaWeaponOwnedSave weapon = GetSelectedWeapon();

        if (weapon == null)
            return;

        if (weaponManager.RefineWeapon(weapon.instanceId))
            Refresh(currentCharacterId);
    }

    private void Set(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value;
    }

    private void OnDestroy()
    {
        if (equipButton != null)
            equipButton.onClick.RemoveListener(EquipSelected);

        if (unequipButton != null)
            unequipButton.onClick.RemoveListener(Unequip);

        if (enhanceButton != null)
            enhanceButton.onClick.RemoveListener(Enhance);

        if (ascendButton != null)
            ascendButton.onClick.RemoveListener(Ascend);

        if (refineButton != null)
            refineButton.onClick.RemoveListener(Refine);

        if (weaponDropdown != null)
            weaponDropdown.onValueChanged.RemoveListener(OnWeaponSelected);
    }
}
