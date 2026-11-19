using System;

/// <summary>
/// 单个系统提供的属性贡献。
/// </summary>
[Serializable]
public class GachaFinalStatSourceData
{
    public GachaFinalStatSourceType sourceType;

    public GachaFinalCombatStats flatBonus =
        new GachaFinalCombatStats();

    public GachaFinalCombatStats percentBonus =
        new GachaFinalCombatStats();

    public GachaFinalCombatStats combatBonus =
        new GachaFinalCombatStats();

    public bool IsValid()
    {
        return flatBonus != null
            && percentBonus != null
            && combatBonus != null;
    }
}
