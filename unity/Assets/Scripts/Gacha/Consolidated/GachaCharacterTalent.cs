using System;
using UnityEngine;

[Serializable]
public class GachaCharacterTalent
{
    [Header("基础信息")]
    public string talentId;
    public string characterId;
    public string talentName;

    [TextArea]
    public string description;

    [Header("解锁条件")]
    [Range(0, 10)]
    public int requiredAscension = 1;

    [Header("天赋类型")]
    public CharacterTalentType talentType;

    [Header("效果数值")]
    [Tooltip("使用小数比例，例如 0.08 表示 8%")]
    public float baseValue;

    [Tooltip("每个额外等级增加的效果")]
    public float valuePerLevel;

    [Header("是否启用")]
    public bool enabled = true;

    public float GetValue(int characterLevel)
    {
        if (!enabled)
            return 0f;

        return baseValue +
            Mathf.Max(0, characterLevel - 1) * valuePerLevel;
    }
}
