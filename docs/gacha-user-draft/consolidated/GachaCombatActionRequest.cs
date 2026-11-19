using System;

[Serializable]
public class GachaCombatActionRequest
{
    public string attackerCharacterId;
    public string targetId;

    public GachaCombatActionType actionType;

    // 必须填写原始倍率，不得填写 UI 预览后的倍率。
    public float rawMultiplier = 1f;

    public float targetDefense;
    public float targetDamageReduction;

    public bool canCritical = true;

    public bool forceCritical;
}
