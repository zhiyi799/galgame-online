using System;
using System.Collections.Generic;
using UnityEngine;

public class GachaExchangeShop : MonoBehaviour
{
    [Header("抽卡管理器")]
    public GachaManager manager;

    [Header("商品列表")]
    public List<GachaExchangeItem> items =
        new List<GachaExchangeItem>();

    [Serializable]
    public class PurchaseRecord
    {
        public string itemId;
        public int purchaseCount;
    }

    [Serializable]
    private class PurchaseSaveData
    {
        public List<PurchaseRecord> records =
            new List<PurchaseRecord>();
    }

    private const string PurchaseSaveKey =
        "ASTRAL_BLOOM_EXCHANGE_PURCHASES";

    private PurchaseSaveData purchaseData;

    private void Awake()
    {
        LoadPurchaseData();
    }

    private void LoadPurchaseData()
    {
        string json = PlayerPrefs.GetString(
            PurchaseSaveKey,
            ""
        );

        if (string.IsNullOrEmpty(json))
        {
            purchaseData = new PurchaseSaveData();
            return;
        }

        try
        {
            purchaseData =
                JsonUtility.FromJson<PurchaseSaveData>(json);
        }
        catch
        {
            purchaseData = new PurchaseSaveData();
        }

        if (purchaseData == null)
            purchaseData = new PurchaseSaveData();

        if (purchaseData.records == null)
            purchaseData.records = new List<PurchaseRecord>();
    }

    private void SavePurchaseData()
    {
        string json = JsonUtility.ToJson(purchaseData);

        PlayerPrefs.SetString(PurchaseSaveKey, json);
        PlayerPrefs.Save();
    }

    public int GetPurchaseCount(string itemId)
    {
        PurchaseRecord record =
            purchaseData.records.Find(x => x.itemId == itemId);

        return record == null ? 0 : record.purchaseCount;
    }

    public bool CanPurchase(GachaExchangeItem item)
    {
        if (manager == null || item == null)
            return false;

        if (item.resonanceCost <= 0)
            return false;

        if (manager.Save.resonance < item.resonanceCost)
            return false;

        if (item.characterReward == null)
            return false;

        if (item.limitedPurchase &&
            GetPurchaseCount(item.itemId) >= item.purchaseLimit)
            return false;

        return true;
    }

    public bool Purchase(GachaExchangeItem item)
    {
        if (!CanPurchase(item))
        {
            Debug.LogWarning("当前条件不满足，无法兑换。");
            return false;
        }

        if (!manager.TrySpendResonance(item.resonanceCost))
            return false;

        manager.GrantCharacter(item.characterReward);

        PurchaseRecord record =
            purchaseData.records.Find(x => x.itemId == item.itemId);

        if (record == null)
        {
            record = new PurchaseRecord
            {
                itemId = item.itemId,
                purchaseCount = 0
            };

            purchaseData.records.Add(record);
        }

        record.purchaseCount++;

        SavePurchaseData();

        Debug.Log("兑换成功：" + item.itemName);

        return true;
    }
}
