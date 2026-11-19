using System;
using System.Collections.Generic;
using UnityEngine;

public enum Rarity
{
    ThreeStar = 3,
    FourStar = 4,
    FiveStar = 5
}

[Serializable]
public class GachaCharacter
{
    public string id;
    public string characterName;
    public Rarity rarity;
    public Sprite portrait;
    public bool isRateUp;
    [TextArea(3, 8)]
    public string description;
}

[Serializable]
public class GachaPullRecord
{
    public string characterId;
    public string characterName;
    public Rarity rarity;
    public bool isRateUp;
    public int pullIndex;
    public string time;
}

[Serializable]
public class GachaOwnedCharacter
{
    public string characterId;
    public int count;
    public int firstObtainedPull;
}

[Serializable]
public class GachaSaveData
{
    public int currency = 10000;
    public int pityFiveStar = 0;
    public bool guaranteedRateUp = false;

    public int totalPulls = 0;
    public int resonance = 0;

    public List<GachaPullRecord> history =
        new List<GachaPullRecord>();

    public List<GachaOwnedCharacter> ownedCharacters =
        new List<GachaOwnedCharacter>();
}

[Serializable]
public class GachaPullResult
{
    public GachaCharacter character;
    public bool isNew;
    public int pullIndex;
}
