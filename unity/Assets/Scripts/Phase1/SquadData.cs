// SquadData — 星律小队（Phase 1 / P1-A）的数据定义。
// 角色名沿用现有占位名（ItemNames.cs），不发明新名字；主角名来自圣经。
// 属性与入场技对应关系见设计文档 4.1 节。
using UnityEngine;

namespace AstralBloom
{
    public enum StarAttribute
    {
        Star,   // 星：引力聚怪
        Blaze,  // 炽：爆发伤害
        Storm,  // 岚：加速切割
        Tide    // 潮：共鸣回能
    }

    public static class StarAttributeText
    {
        public static string Name(StarAttribute a)
        {
            switch (a)
            {
                case StarAttribute.Star: return "星";
                case StarAttribute.Blaze: return "炽";
                case StarAttribute.Storm: return "岚";
                case StarAttribute.Tide: return "潮";
                default: return "星";
            }
        }

        public static string EntrySkillName(StarAttribute a)
        {
            switch (a)
            {
                case StarAttribute.Star: return "星律 · 引力聚怪";
                case StarAttribute.Blaze: return "炽律 · 爆发";
                case StarAttribute.Storm: return "岚律 · 疾风";
                case StarAttribute.Tide: return "潮律 · 共鸣回能";
                default: return "星律 · 引力聚怪";
            }
        }
    }

    public struct SquadMemberDef
    {
        public string name;          // 显示名（占位名）
        public string artName;       // Resources/Characters 下的立绘文件名（主角为空）
        public StarAttribute attribute;
        public Color clothColor;     // 3D 几何体占位配色
        public float maxHP;
        public float attack;         // 队友 AI 普攻伤害
    }

    public static class SquadData
    {
        // 4 人编队：主角 + char_102/103/105（设计文档 P1-A）。
        // 属性分配：主角星（圣经：星律印记），102 炽、103 岚、105 潮。
        public static readonly SquadMemberDef[] Roster = new[]
        {
            new SquadMemberDef { name = "无星者", artName = "",          attribute = StarAttribute.Star,  clothColor = new Color(0.14f, 0.30f, 0.67f), maxHP = 140f, attack = 26f },
            new SquadMemberDef { name = "星夜·雪", artName = "char_102", attribute = StarAttribute.Blaze, clothColor = new Color(0.78f, 0.26f, 0.16f), maxHP = 120f, attack = 30f },
            new SquadMemberDef { name = "星夜·花", artName = "char_103", attribute = StarAttribute.Storm, clothColor = new Color(0.16f, 0.58f, 0.42f), maxHP = 110f, attack = 28f },
            new SquadMemberDef { name = "月下·凛", artName = "char_105", attribute = StarAttribute.Tide,  clothColor = new Color(0.42f, 0.62f, 0.95f), maxHP = 125f, attack = 24f },
        };

        public const float ResonanceMax = 100f;
        public const float SpMax = 10f;
        public const float SkillSpCost = 3f;
        public const float SwitchCooldown = 0.5f;
    }
}
