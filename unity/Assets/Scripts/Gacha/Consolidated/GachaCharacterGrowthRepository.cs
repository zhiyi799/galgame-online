using System;

/// <summary>
/// 角色成长数据仓库。
///
/// 统一读取角色成长数据。
/// 通过回调连接现有存档，不擅自假定 GachaSaveData 的内部字段。
/// 只负责读取和保存统一成长数据，
/// 不负责扣材料或修改成长规则。
/// </summary>
public class GachaCharacterGrowthRepository
{
    private readonly Func<string, GachaCharacterGrowthData> getGrowth;
    private readonly Action<string, GachaCharacterGrowthData> saveGrowth;

    public GachaCharacterGrowthRepository(
        Func<string, GachaCharacterGrowthData> getGrowth,
        Action<string, GachaCharacterGrowthData> saveGrowth)
    {
        this.getGrowth = getGrowth;
        this.saveGrowth = saveGrowth;
    }

    public bool TryGet(
        string characterId,
        out GachaCharacterGrowthData data)
    {
        data = null;

        if (string.IsNullOrWhiteSpace(characterId) ||
            getGrowth == null)
            return false;

        try
        {
            data = getGrowth(characterId);

            if (data == null ||
                !string.Equals(
                    data.characterId,
                    characterId,
                    StringComparison.Ordinal))
            {
                data = null;
                return false;
            }

            data.Normalize();
            return true;
        }
        catch
        {
            data = null;
            return false;
        }
    }

    public bool TrySave(GachaCharacterGrowthData data)
    {
        if (data == null ||
            !data.IsValid() ||
            saveGrowth == null)
            return false;

        try
        {
            saveGrowth(data.characterId, data);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
