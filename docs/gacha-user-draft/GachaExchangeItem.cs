using UnityEngine;

[CreateAssetMenu(
    fileName = "NewGachaExchangeItem",
    menuName = "ASTRAL BLOOM/Gacha/Exchange Item"
)]
public class GachaExchangeItem : ScriptableObject
{
    [Header("商品信息")]
    public string itemId;
    public string itemName;

    [TextArea(2, 5)]
    public string description;

    public Sprite icon;

    [Header("兑换内容")]
    public GachaCharacter characterReward;

    [Header("兑换价格")]
    [Min(1)]
    public int resonanceCost = 200;

    [Header("限购")]
    public bool limitedPurchase = false;

    [Min(1)]
    public int purchaseLimit = 1;
}
