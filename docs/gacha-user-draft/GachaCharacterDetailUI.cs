using UnityEngine;
using UnityEngine.UI;

public class GachaCharacterDetailUI : MonoBehaviour
{
    [Header("面板")]
    public GameObject panel;

    [Header("角色信息")]
    public Image portrait;
    public Text characterName;
    public Text rarityText;
    public Text descriptionText;
    public Text rateUpText;
    public Text newCharacterText;

    [Header("关闭")]
    public Button closeButton;

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    public void Show(GachaPullResult result)
    {
        if (result == null || result.character == null)
            return;

        GachaCharacter character = result.character;

        if (panel != null)
            panel.SetActive(true);

        if (characterName != null)
            characterName.text = character.characterName;

        if (rarityText != null)
            rarityText.text = GetRarityName(character.rarity);

        if (portrait != null && character.portrait != null)
            portrait.sprite = character.portrait;

        if (rateUpText != null)
            rateUpText.text = character.isRateUp ? "限定 UP" : "";

        if (newCharacterText != null)
            newCharacterText.text = result.isNew ? "NEW CHARACTER" : "已获得角色";
    }

    private string GetRarityName(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.FiveStar:
                return "★★★★★  五星";
            case Rarity.FourStar:
                return "★★★★  四星";
            default:
                return "★★★  三星";
        }
    }

    public void Close()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }
}
