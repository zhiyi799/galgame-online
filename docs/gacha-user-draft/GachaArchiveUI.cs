using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GachaArchiveUI : MonoBehaviour
{
    public enum ArchiveFilter
    {
        All,
        ThreeStar,
        FourStar,
        FiveStar,
        Owned,
        Unowned
    }

    [Header("数据")]
    public GachaCharacterDatabase database;
    public GachaManager manager;

    [Header("界面")]
    public GameObject archivePanel;
    public Transform contentRoot;
    public Button characterCardPrefab;

    [Header("详情")]
    public GachaCharacterDetailUI detailUI;

    [Header("筛选按钮")]
    public Button allButton;
    public Button threeStarButton;
    public Button fourStarButton;
    public Button fiveStarButton;
    public Button ownedButton;
    public Button unownedButton;

    private ArchiveFilter currentFilter = ArchiveFilter.All;

    private readonly List<Button> cards =
        new List<Button>();

    private void Awake()
    {
        BindFilter(allButton, ArchiveFilter.All);
        BindFilter(threeStarButton, ArchiveFilter.ThreeStar);
        BindFilter(fourStarButton, ArchiveFilter.FourStar);
        BindFilter(fiveStarButton, ArchiveFilter.FiveStar);
        BindFilter(ownedButton, ArchiveFilter.Owned);
        BindFilter(unownedButton, ArchiveFilter.Unowned);
    }

    private void BindFilter(Button button, ArchiveFilter filter)
    {
        if (button == null)
            return;

        button.onClick.AddListener(() =>
        {
            currentFilter = filter;
            Refresh();
        });
    }

    public void Open()
    {
        if (archivePanel != null)
            archivePanel.SetActive(true);

        Refresh();
    }

    public void Close()
    {
        if (archivePanel != null)
            archivePanel.SetActive(false);
    }

    public void Refresh()
    {
        ClearCards();

        if (database == null ||
            manager == null ||
            contentRoot == null ||
            characterCardPrefab == null)
            return;

        foreach (GachaCharacter character in database.characters)
        {
            if (character == null)
                continue;

            if (!MatchesFilter(character))
                continue;

            CreateCard(character);
        }
    }

    private bool MatchesFilter(GachaCharacter character)
    {
        bool owned = manager.HasCharacter(character.id);

        switch (currentFilter)
        {
            case ArchiveFilter.ThreeStar:
                return character.rarity == Rarity.ThreeStar;

            case ArchiveFilter.FourStar:
                return character.rarity == Rarity.FourStar;

            case ArchiveFilter.FiveStar:
                return character.rarity == Rarity.FiveStar;

            case ArchiveFilter.Owned:
                return owned;

            case ArchiveFilter.Unowned:
                return !owned;

            default:
                return true;
        }
    }

    private void CreateCard(GachaCharacter character)
    {
        Button card = Instantiate(
            characterCardPrefab,
            contentRoot
        );

        cards.Add(card);

        Text label = card.GetComponentInChildren<Text>();

        bool owned = manager.HasCharacter(character.id);

        if (label != null)
        {
            label.text = owned
                ? character.characterName + "\n" + GetStars(character.rarity)
                : "？？？\n" + GetStars(character.rarity);
        }

        Image image = card.GetComponentInChildren<Image>();

        if (image != null)
        {
            image.sprite = character.portrait;
            image.color = owned
                ? Color.white
                : new Color(0.25f, 0.25f, 0.25f, 0.9f);
        }

        GachaCharacter current = character;

        card.onClick.AddListener(() =>
        {
            if (!owned)
                return;

            if (detailUI != null)
            {
                GachaPullResult result = new GachaPullResult
                {
                    character = current,
                    isNew = false,
                    pullIndex = 0
                };

                detailUI.Show(result);
            }
        });
    }

    private string GetStars(Rarity rarity)
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

    private void ClearCards()
    {
        foreach (Button card in cards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }

        cards.Clear();
    }
}
