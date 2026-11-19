using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class GachaShopUI : MonoBehaviour
{
    [Header("商店逻辑")]
    public GachaExchangeShop shop;

    [Header("界面引用")]
    public GameObject shopPanel;
    public Transform contentRoot;
    public Button itemButtonPrefab;

    [Header("文本")]
    public Text resonanceText;
    public Text messageText;

    private readonly List<Button> generatedButtons =
        new List<Button>();

    private void Start()
    {
        Refresh();
    }

    public void Open()
    {
        if (shopPanel != null)
            shopPanel.SetActive(true);

        Refresh();
    }

    public void Close()
    {
        if (shopPanel != null)
            shopPanel.SetActive(false);
    }

    public void Refresh()
    {
        if (shop == null || shop.manager == null)
            return;

        if (resonanceText != null)
        {
            resonanceText.text =
                "共鸣：" + shop.manager.Save.resonance;
        }

        ClearItems();

        if (contentRoot == null || itemButtonPrefab == null)
            return;

        foreach (GachaExchangeItem item in shop.items)
        {
            if (item == null)
                continue;

            GachaExchangeItem currentItem = item;

            Button button = Instantiate(
                itemButtonPrefab,
                contentRoot
            );

            generatedButtons.Add(button);

            Text buttonText = button.GetComponentInChildren<Text>();

            int purchased = shop.GetPurchaseCount(currentItem.itemId);

            string limitText = currentItem.limitedPurchase
                ? "  已兑换 " + purchased + "/" + currentItem.purchaseLimit
                : "";

            if (buttonText != null)
            {
                buttonText.text =
                    currentItem.itemName +
                    "\n" +
                    currentItem.description +
                    "\n需要共鸣：" +
                    currentItem.resonanceCost +
                    limitText;
            }

            button.interactable = shop.CanPurchase(currentItem);

            button.onClick.AddListener(() =>
            {
                bool success = shop.Purchase(currentItem);

                if (messageText != null)
                {
                    messageText.text = success
                        ? "兑换成功：" + currentItem.itemName
                        : "兑换失败，请检查兑换条件。";
                }

                Refresh();
            });
        }
    }

    private void ClearItems()
    {
        foreach (Button button in generatedButtons)
        {
            if (button != null)
                Destroy(button.gameObject);
        }

        generatedButtons.Clear();
    }

    private void OnDestroy()
    {
        ClearItems();
    }
}
