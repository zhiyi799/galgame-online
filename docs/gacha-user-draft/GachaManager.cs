using System;
using System.Collections.Generic;
using UnityEngine;

public class GachaManager : MonoBehaviour
{
    [Header("卡池配置")]
    public GachaBannerConfig banner;

    [Header("重复角色补偿")]
    public int duplicateThreeStarReward = 10;
    public int duplicateFourStarReward = 30;
    public int duplicateFiveStarReward = 100;

    [Header("潜能系统")]
    public GachaPotentialManager potentialManager;

    private const string SaveKey = "ASTRAL_BLOOM_GACHA_SAVE";

    public GachaSaveData Save { get; private set; }

    private void Awake()
    {
        Load();
    }

    public void Load()
    {
        string json = PlayerPrefs.GetString(SaveKey, "");

        if (string.IsNullOrEmpty(json))
        {
            Save = new GachaSaveData();
        }
        else
        {
            try
            {
                Save = JsonUtility.FromJson<GachaSaveData>(json);

                if (Save == null)
                    Save = new GachaSaveData();
            }
            catch
            {
                Debug.LogError("抽卡存档读取失败，创建新存档。");
                Save = new GachaSaveData();
            }
        }

        if (Save.history == null)
            Save.history = new List<GachaPullRecord>();

        if (Save.ownedCharacters == null)
            Save.ownedCharacters = new List<GachaOwnedCharacter>();
    }

    public void SaveGame()
    {
        if (Save == null)
            return;

        string json = JsonUtility.ToJson(Save, true);

        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    public List<GachaPullResult> Pull(int count)
    {
        List<GachaPullResult> results =
            new List<GachaPullResult>();

        if (banner == null)
        {
            Debug.LogError("没有设置 GachaBannerConfig。");
            return results;
        }

        if (count != 1 && count != 10)
        {
            Debug.LogError("抽卡数量只能为 1 或 10。");
            return results;
        }

        int cost = count == 1
            ? banner.singlePullCost
            : banner.tenPullCost;

        if (Save.currency < cost)
        {
            Debug.LogWarning("星轨资源不足。");
            return results;
        }

        if (!ValidateBanner())
            return results;

        Save.currency -= cost;

        for (int i = 0; i < count; i++)
        {
            int pullIndex = Save.totalPulls + 1;

            GachaPullResult result = RollOne(pullIndex);

            if (result == null)
                continue;

            results.Add(result);

            Save.totalPulls++;
            Save.resonance += banner.resonancePerPull;

            AddHistory(result);
            AddOwnedCharacter(result);
        }

        SaveGame();

        return results;
    }

    private bool ValidateBanner()
    {
        if (banner.threeStarPool == null ||
            banner.threeStarPool.Count == 0)
        {
            Debug.LogError("三星角色池为空。");
            return false;
        }

        if (banner.fourStarPool == null ||
            banner.fourStarPool.Count == 0)
        {
            Debug.LogError("四星角色池为空。");
            return false;
        }

        if (banner.fiveStarPool == null ||
            banner.fiveStarPool.Count == 0)
        {
            Debug.LogError("五星角色池为空。");
            return false;
        }

        return true;
    }

    private GachaPullResult RollOne(int pullIndex)
    {
        int nextPity = Save.pityFiveStar + 1;

        float fiveRate = GetFiveStarRate(nextPity);

        float roll = UnityEngine.Random.Range(0f, 100f);

        GachaCharacter selected;

        if (nextPity >= banner.hardPity)
        {
            selected = PickFiveStar();
        }
        else if (roll < fiveRate)
        {
            selected = PickFiveStar();
        }
        else
        {
            float fourRate = banner.fourStarRate;

            if (roll < fiveRate + fourRate)
            {
                selected = PickRandom(banner.fourStarPool);
            }
            else
            {
                selected = PickRandom(banner.threeStarPool);
            }
        }

        if (selected == null)
            return null;

        if (selected.rarity == Rarity.FiveStar)
            Save.pityFiveStar = 0;
        else
            Save.pityFiveStar++;

        return new GachaPullResult
        {
            character = selected,
            pullIndex = pullIndex,
            isNew = !OwnsCharacter(selected.id)
        };
    }

    private float GetFiveStarRate(int pity)
    {
        float baseRate = banner.fiveStarRate;

        if (pity < banner.softPityStart)
            return baseRate;

        int extraPulls = pity - banner.softPityStart + 1;

        return Mathf.Min(
            100f,
            baseRate + extraPulls * banner.softPityIncrease
        );
    }

    private GachaCharacter PickFiveStar()
    {
        if (Save.guaranteedRateUp)
        {
            List<GachaCharacter> upCharacters =
                banner.fiveStarPool.FindAll(x => x.isRateUp);

            if (upCharacters.Count > 0)
            {
                Save.guaranteedRateUp = false;
                return PickRandom(upCharacters);
            }

            Debug.LogError("必定 UP 状态下，五星池没有 UP 角色。");
        }

        bool winRateUp =
            UnityEngine.Random.value < banner.rateUpChance;

        if (winRateUp)
        {
            List<GachaCharacter> upCharacters =
                banner.fiveStarPool.FindAll(x => x.isRateUp);

            if (upCharacters.Count > 0)
                return PickRandom(upCharacters);
        }

        Save.guaranteedRateUp = true;

        List<GachaCharacter> standardCharacters =
            banner.fiveStarPool.FindAll(x => !x.isRateUp);

        if (standardCharacters.Count > 0)
            return PickRandom(standardCharacters);

        // 如果卡池没有常驻五星，避免空列表。
        return PickRandom(banner.fiveStarPool);
    }

    private T PickRandom<T>(List<T> pool)
    {
        if (pool == null || pool.Count == 0)
            return default(T);

        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    private bool OwnsCharacter(string characterId)
    {
        return Save.ownedCharacters.Exists(
            x => x.characterId == characterId
        );
    }

    private void AddOwnedCharacter(GachaPullResult result)
    {
        string id = result.character.id;

        GachaOwnedCharacter owned =
            Save.ownedCharacters.Find(x => x.characterId == id);

        if (owned == null)
        {
            owned = new GachaOwnedCharacter
            {
                characterId = id,
                count = 1,
                firstObtainedPull = result.pullIndex
            };

            Save.ownedCharacters.Add(owned);
            result.isNew = true;
        }
        else
        {
            owned.count++;

            result.isNew = false;

            int reward = GetDuplicateReward(result.character.rarity);
            Save.resonance += reward;

            // 潜能提升失败（已满）不影响重复角色与共鸣奖励，
            // 返回值不参与重复处理结果。
            if (potentialManager != null)
            {
                potentialManager.AddDuplicatePotential(id);
            }
        }
    }

    private int GetDuplicateReward(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.FiveStar:
                return duplicateFiveStarReward;

            case Rarity.FourStar:
                return duplicateFourStarReward;

            default:
                return duplicateThreeStarReward;
        }
    }

    private void AddHistory(GachaPullResult result)
    {
        GachaPullRecord record = new GachaPullRecord
        {
            characterId = result.character.id,
            characterName = result.character.characterName,
            rarity = result.character.rarity,
            isRateUp = result.character.isRateUp,
            pullIndex = result.pullIndex,
            time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        Save.history.Insert(0, record);

        if (Save.history.Count > 500)
            Save.history.RemoveAt(Save.history.Count - 1);
    }

    public bool HasCharacter(string characterId)
    {
        return OwnsCharacter(characterId);
    }

    public int GetCharacterCount(string characterId)
    {
        GachaOwnedCharacter owned =
            Save.ownedCharacters.Find(x => x.characterId == characterId);

        return owned == null ? 0 : owned.count;
    }

    public void AddCurrency(int amount)
    {
        if (amount <= 0)
            return;

        Save.currency += amount;
        SaveGame();
    }
}
