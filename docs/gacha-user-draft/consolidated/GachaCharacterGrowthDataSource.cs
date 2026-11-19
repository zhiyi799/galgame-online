using System;

/// <summary>
/// 角色成长数据源（只读）。
///
/// 从实际拥有角色数据读取成长信息，转换成统一成长数据，
/// 交给属性计算器。
///
/// 只负责读取，不负责：
/// 扣除材料、保存存档、修改等级 / 突破 / 技能等级、
/// 修改重复获取次数、修改潜能等级。
///
/// 注意：等级与突破等级不在 GachaOwnedCharacter 上，
/// 而在 GachaCharacterGrowthSave 中，
/// 因此需要两个读取回调，不能只传其一。
/// 成长存档不存在时返回 null，不用默认等级伪造数据。
/// </summary>
public sealed class GachaCharacterGrowthDataSource
{
    private readonly Func<string, GachaOwnedCharacter> getOwnedCharacter;
    private readonly Func<string, GachaCharacterGrowthSave> getGrowthSave;

    public GachaCharacterGrowthDataSource(
        Func<string, GachaOwnedCharacter> getOwnedCharacter,
        Func<string, GachaCharacterGrowthSave> getGrowthSave)
    {
        this.getOwnedCharacter = getOwnedCharacter
            ?? throw new ArgumentNullException(nameof(getOwnedCharacter));
        this.getGrowthSave = getGrowthSave
            ?? throw new ArgumentNullException(nameof(getGrowthSave));
    }

    public GachaCharacterGrowthData GetGrowthData(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return null;

        GachaOwnedCharacter owned = getOwnedCharacter(characterId);

        if (owned == null)
            return null;

        if (!string.Equals(
                owned.characterId,
                characterId,
                StringComparison.Ordinal))
            return null;

        GachaCharacterGrowthSave growth = getGrowthSave(characterId);

        if (growth == null)
            return null;

        GachaCharacterGrowthData data =
            GachaCharacterGrowthData.FromOwnedCharacter(owned, growth);

        if (data == null || !data.IsValid())
            return null;

        return data;
    }

    /// <summary>
    /// 检查角色是否存在于玩家拥有的角色数据中。
    /// </summary>
    public bool HasOwnedCharacter(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return false;

        GachaOwnedCharacter owned = getOwnedCharacter(characterId);

        return owned != null &&
               string.Equals(
                   owned.characterId,
                   characterId,
                   StringComparison.Ordinal);
    }
}
