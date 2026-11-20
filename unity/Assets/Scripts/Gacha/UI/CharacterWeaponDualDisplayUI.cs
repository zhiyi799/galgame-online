using TMPro;
using UnityEngine;

/// <summary>
/// 武器双区域显示。
/// 当前装备区和下拉选择区完全独立。
/// 只负责显示，不修改武器数据。
/// </summary>
public class CharacterWeaponDualDisplayUI : MonoBehaviour
{
    [Header("武器管理器")]
    public GachaWeaponManager weaponManager;

    [Header("当前装备武器")]
    public TMP_Text equippedWeaponIdText;
    public TMP_Text equippedWeaponLevelText;
    public TMP_Text equippedWeaponAttackText;
    public TMP_Text equippedWeaponAscensionText;

    [Header("下拉选择武器")]
    public TMP_Text selectedWeaponIdText;
    public TMP_Text selectedWeaponLevelText;
    public TMP_Text selectedWeaponAttackText;
    public TMP_Text selectedWeaponAscensionText;

    public void RefreshEquippedWeapon(GachaWeaponOwnedSave weapon)
    {
        RefreshDisplay(
            weapon,
            equippedWeaponIdText,
            equippedWeaponLevelText,
            equippedWeaponAttackText,
            equippedWeaponAscensionText
        );
    }

    public void RefreshSelectedWeapon(GachaWeaponOwnedSave weapon)
    {
        RefreshDisplay(
            weapon,
            selectedWeaponIdText,
            selectedWeaponLevelText,
            selectedWeaponAttackText,
            selectedWeaponAscensionText
        );
    }

    private void RefreshDisplay(
        GachaWeaponOwnedSave weapon,
        TMP_Text idText,
        TMP_Text levelText,
        TMP_Text attackText,
        TMP_Text ascensionText)
    {
        if (weapon == null)
        {
            SetText(idText, "未选择武器");
            SetText(levelText, "--");
            SetText(attackText, "--");
            SetText(ascensionText, "--");
            return;
        }

        if (weaponManager == null)
        {
            SetText(idText, "武器管理器未绑定");
            SetText(levelText, "--");
            SetText(attackText, "--");
            SetText(ascensionText, "--");
            return;
        }

        GachaWeaponConfig config =
            weaponManager.GetConfig(weapon.weaponId);

        if (config == null)
        {
            SetText(idText, weapon.weaponId);
            SetText(levelText, "--");
            SetText(attackText, "配置缺失");
            SetText(ascensionText, "--");
            return;
        }

        float attack = config.GetAttack(
            weapon.level,
            weapon.ascension
        );

        SetText(idText, weapon.weaponId);
        SetText(levelText, $"Lv. {weapon.level}");
        SetText(attackText, attack.ToString("F1"));
        SetText(ascensionText, $"突破 {weapon.ascension}");
    }

    private void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    public void ClearEquippedDisplay()
    {
        RefreshEquippedWeapon(null);
    }

    public void ClearSelectedDisplay()
    {
        RefreshSelectedWeapon(null);
    }
}
