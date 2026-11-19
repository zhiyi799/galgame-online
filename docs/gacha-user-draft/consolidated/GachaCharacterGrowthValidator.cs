using System;

/// <summary>
/// 角色成长数据校验器。
///
/// 统一验证角色成长数据，避免等级、突破和技能等级越界。
/// </summary>
public static class GachaCharacterGrowthValidator
{
    public static bool Validate(
        GachaCharacterGrowthData data,
        GachaGrowthConfig config,
        int maxSkillLevel,
        out string error)
    {
        error = null;

        if (data == null)
        {
            error = "角色成长数据为空。";
            return false;
        }

        if (config == null || !config.IsValid())
        {
            error = "角色成长配置无效。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(data.characterId))
        {
            error = "角色 ID 为空。";
            return false;
        }

        if (data.ascension < 0 ||
            data.ascension > config.maxAscension)
        {
            error = "角色突破等级超出配置范围。";
            return false;
        }

        int maxLevel = config.GetLevelCap(data.ascension);

        if (data.level < 1 || data.level > maxLevel)
        {
            error = "角色等级超出当前突破等级上限。";
            return false;
        }

        if (maxSkillLevel < 1 ||
            data.skillLevel < 1 ||
            data.skillLevel > maxSkillLevel)
        {
            error = "角色技能等级超出配置范围。";
            return false;
        }

        if (data.duplicateLevel < 0)
        {
            error = "角色重复等级不能小于零。";
            return false;
        }

        return true;
    }
}
