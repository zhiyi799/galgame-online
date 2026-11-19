using System;

/// <summary>
/// 统一角色成长数据。
///
/// 用于统一表达角色成长数据，不直接替换旧存档类。
/// skillLevel 默认 1，duplicateLevel 默认 0。
/// duplicateLevel 不是潜能等级，不能代替潜能等级。
///
/// 字段来源说明（按现有存档定义调整）：
/// level / ascension 存在 GachaCharacterGrowthSave 中，
/// skillLevel / duplicateLevel 存在 GachaOwnedCharacter 中。
/// 因此映射方法同时接收两个对象，不能只传其一。
/// </summary>
[Serializable]
public class GachaCharacterGrowthData
{
    public string characterId;

    public int level = 1;

    public int ascension = 0;

    public int skillLevel = 1;

    public int duplicateLevel = 0;

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(characterId) &&
               level >= 1 &&
               ascension >= 0 &&
               skillLevel >= 1 &&
               duplicateLevel >= 0;
    }

    public void Normalize()
    {
        level = Math.Max(1, level);
        ascension = Math.Max(0, ascension);
        skillLevel = Math.Max(1, skillLevel);
        duplicateLevel = Math.Max(0, duplicateLevel);
    }

    public static GachaCharacterGrowthData FromOwnedCharacter(
        GachaOwnedCharacter owned,
        GachaCharacterGrowthSave growth)
    {
        if (owned == null || growth == null)
            return null;

        GachaCharacterGrowthData result =
            new GachaCharacterGrowthData
            {
                characterId = owned.characterId,
                level = growth.level,
                ascension = growth.ascension,
                skillLevel = owned.skillLevel,
                duplicateLevel = owned.duplicateLevel
            };

        result.Normalize();

        return result;
    }

    public void ApplyToOwnedCharacter(
        GachaOwnedCharacter owned,
        GachaCharacterGrowthSave growth)
    {
        if (owned == null)
            return;

        owned.skillLevel = skillLevel;
        owned.duplicateLevel = duplicateLevel;

        if (growth == null)
            return;

        growth.level = level;
        growth.ascension = ascension;
    }
}
