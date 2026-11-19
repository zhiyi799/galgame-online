using UnityEngine;

public enum CharacterProfileTab
{
    Archive,
    Stats,
    Skills,
    Weapon,
    Potential
}

public class CharacterProfileController : MonoBehaviour
{
    [Header("角色数据库")]
    public GachaCharacterDatabase characterDatabase;

    [Header("页面")]
    public GameObject archivePanel;
    public GameObject statsPanel;
    public GameObject skillsPanel;
    public GameObject weaponPanel;
    public GameObject potentialPanel;

    [Header("页面管理器")]
    public CharacterStatsPanelUI statsUI;
    public CharacterSkillPanelUI skillsUI;
    public CharacterWeaponPanelUI weaponUI;
    public GachaPotentialUI potentialUI;

    public string CurrentCharacterId { get; private set; }

    public void OpenCharacter(string characterId)
    {
        if (characterDatabase == null)
            return;

        GachaCharacter character =
            characterDatabase.GetCharacter(characterId);

        if (character == null)
            return;

        CurrentCharacterId = characterId;

        OpenTab(CharacterProfileTab.Archive);
    }

    public void OpenTab(CharacterProfileTab tab)
    {
        SetActive(archivePanel,
            tab == CharacterProfileTab.Archive);

        SetActive(statsPanel,
            tab == CharacterProfileTab.Stats);

        SetActive(skillsPanel,
            tab == CharacterProfileTab.Skills);

        SetActive(weaponPanel,
            tab == CharacterProfileTab.Weapon);

        SetActive(potentialPanel,
            tab == CharacterProfileTab.Potential);

        RefreshTab(tab);
    }

    private void RefreshTab(CharacterProfileTab tab)
    {
        if (string.IsNullOrEmpty(CurrentCharacterId))
            return;

        switch (tab)
        {
            case CharacterProfileTab.Stats:
                if (statsUI != null)
                    statsUI.Refresh(CurrentCharacterId);
                break;

            case CharacterProfileTab.Skills:
                if (skillsUI != null)
                    skillsUI.Refresh(CurrentCharacterId);
                break;

            case CharacterProfileTab.Weapon:
                if (weaponUI != null)
                    weaponUI.Refresh(CurrentCharacterId);
                break;

            case CharacterProfileTab.Potential:
                if (potentialUI != null)
                    potentialUI.Open(CurrentCharacterId);
                break;
        }
    }

    private void SetActive(GameObject target, bool value)
    {
        if (target != null)
            target.SetActive(value);
    }
}
