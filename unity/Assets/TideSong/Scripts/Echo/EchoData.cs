using System.Collections.Generic;
using UnityEngine;

namespace TideSong
{
    public enum EchoSet
    {
        TideEcho,      // 潮汐回响
        FrostLaw,      // 凝霜之律
        NightWhisper,  // 幽夜低语
        RockVow,       // 磐岩之誓
        GaleWing       // 疾风之翼
    }

    public enum EchoMainStat
    {
        AtkPct,
        HpPct,
        DefPct,
        CritRate,
        CritDmg,
        ElemBonus,
        EnergyRegen
    }

    public enum EchoSkillType
    {
        Summon,   // 召唤幻兽冲撞
        Buff,     // 攻击增益
        Heal,     // 治疗护盾
        Control   // 范围控制
    }

    /// <summary>声骸模板。</summary>
    public class EchoDef
    {
        public string id;
        public string name;
        public int cost;          // 1 / 3 / 4
        public EchoSet set;
        public EchoSkillType skill;
        public string skillName;
        public string skillDesc;
        public Color color;
        public float summonDamage = 60f;
    }

    public static class EchoLibrary
    {
        public static readonly EchoDef[] All = new EchoDef[]
        {
            // cost 4：主声骸，决定声骸技能
            new EchoDef { id = "tide_king",  name = "潮汐王座 · 鸣海", cost = 4, set = EchoSet.TideEcho,     skill = EchoSkillType.Summon,  skillName = "声骸 · 潮鸣", skillDesc = "召唤潮汐巨兽冲撞前方，造成大范围气动伤害。", color = new Color(0.42f, 0.9f, 1f), summonDamage = 120f },
            new EchoDef { id = "frost_queen", name = "霜冠 · 凛冬女王", cost = 4, set = EchoSet.FrostLaw,    skill = EchoSkillType.Control, skillName = "声骸 · 霜封", skillDesc = "冻结周围敌人 3 秒并造成冷凝伤害。", color = new Color(0.62f, 0.86f, 1f), summonDamage = 80f },
            new EchoDef { id = "night_lord",  name = "夜主 · 无面", cost = 4, set = EchoSet.NightWhisper,   skill = EchoSkillType.Buff,    skillName = "声骸 · 夜袭", skillDesc = "8 秒内攻击力提升 30%，暴击率提升 10%。", color = new Color(0.72f, 0.45f, 1f), summonDamage = 0f },
            new EchoDef { id = "rock_guard",  name = "磐卫 · 石心", cost = 4, set = EchoSet.RockVow,        skill = EchoSkillType.Heal,    skillName = "声骸 · 磐壁", skillDesc = "回复 30% 生命并获得等同 25% 生命的护盾。", color = new Color(0.9f, 0.78f, 0.5f), summonDamage = 0f },
            new EchoDef { id = "gale_hawk",   name = "疾风 · 苍隼", cost = 4, set = EchoSet.GaleWing,       skill = EchoSkillType.Summon,  skillName = "声骸 · 俯冲", skillDesc = "召唤苍隼俯冲，对路径上的敌人造成气动伤害。", color = new Color(0.5f, 0.95f, 0.85f), summonDamage = 95f },

            // cost 3
            new EchoDef { id = "tide_serpent", name = "潮蛇 · 环流", cost = 3, set = EchoSet.TideEcho,     skill = EchoSkillType.Summon,  skillName = "声骸 · 环流", skillDesc = "召唤潮蛇冲击前方。", color = new Color(0.4f, 0.85f, 1f), summonDamage = 70f },
            new EchoDef { id = "frost_wolf",   name = "霜狼 · 雪痕", cost = 3, set = EchoSet.FrostLaw,    skill = EchoSkillType.Summon,  skillName = "声骸 · 冰牙", skillDesc = "召唤霜狼扑击。", color = new Color(0.66f, 0.88f, 1f), summonDamage = 72f },
            new EchoDef { id = "night_raven",  name = "夜鸦 · 低语", cost = 3, set = EchoSet.NightWhisper, skill = EchoSkillType.Buff,    skillName = "声骸 · 暗羽", skillDesc = "短时提升攻击与暴击。", color = new Color(0.68f, 0.42f, 0.95f), summonDamage = 0f },
            new EchoDef { id = "rock_beast",   name = "岩石兽 · 重甲", cost = 3, set = EchoSet.RockVow,    skill = EchoSkillType.Heal,    skillName = "声骸 · 岩甲", skillDesc = "回血并生成护盾。", color = new Color(0.85f, 0.75f, 0.5f), summonDamage = 0f },
            new EchoDef { id = "gale_wisp",    name = "风灵 · 轻羽", cost = 3, set = EchoSet.GaleWing,     skill = EchoSkillType.Control, skillName = "声骸 · 轻羽", skillDesc = "卷起气流击退并眩晕敌人。", color = new Color(0.55f, 0.95f, 0.85f), summonDamage = 50f },

            // cost 1
            new EchoDef { id = "tide_shell", name = "潮贝 · 空鸣", cost = 1, set = EchoSet.TideEcho,     skill = EchoSkillType.Summon,  skillName = "声骸 · 空鸣", skillDesc = "轻量冲撞。", color = new Color(0.45f, 0.8f, 0.95f), summonDamage = 40f },
            new EchoDef { id = "frost_drop", name = "霜滴 · 微光", cost = 1, set = EchoSet.FrostLaw,    skill = EchoSkillType.Control, skillName = "声骸 · 微光", skillDesc = "小范围减速。", color = new Color(0.7f, 0.9f, 1f), summonDamage = 26f },
            new EchoDef { id = "night_moth", name = "夜蛾 · 影粉", cost = 1, set = EchoSet.NightWhisper, skill = EchoSkillType.Buff,    skillName = "声骸 · 影粉", skillDesc = "短暂攻击提升。", color = new Color(0.6f, 0.4f, 0.85f), summonDamage = 0f },
            new EchoDef { id = "rock_pebble", name = "石砾 · 定音", cost = 1, set = EchoSet.RockVow,    skill = EchoSkillType.Heal,    skillName = "声骸 · 定音", skillDesc = "小幅回血。", color = new Color(0.8f, 0.72f, 0.5f), summonDamage = 0f },
            new EchoDef { id = "gale_leaf",  name = "风叶 · 轻语", cost = 1, set = EchoSet.GaleWing,    skill = EchoSkillType.Control, skillName = "声骸 · 轻语", skillDesc = "小小击退。", color = new Color(0.55f, 0.9f, 0.8f), summonDamage = 18f }
        };

        public static EchoDef Get(string id)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].id == id) return All[i];
            return All[0];
        }

        public static Color SetColor(EchoSet set)
        {
            switch (set)
            {
                case EchoSet.TideEcho: return new Color(0.42f, 0.86f, 1f);
                case EchoSet.FrostLaw: return new Color(0.66f, 0.86f, 1f);
                case EchoSet.NightWhisper: return new Color(0.72f, 0.48f, 1f);
                case EchoSet.RockVow: return new Color(0.92f, 0.78f, 0.5f);
                case EchoSet.GaleWing: return new Color(0.5f, 0.95f, 0.85f);
            }
            return Color.white;
        }

        public static string SetName(EchoSet set)
        {
            switch (set)
            {
                case EchoSet.TideEcho: return "潮汐回响";
                case EchoSet.FrostLaw: return "凝霜之律";
                case EchoSet.NightWhisper: return "幽夜低语";
                case EchoSet.RockVow: return "磐岩之誓";
                case EchoSet.GaleWing: return "疾风之翼";
            }
            return "未知";
        }

        public static string SetBonusText(EchoSet set, int pieces)
        {
            switch (set)
            {
                case EchoSet.TideEcho:
                    if (pieces >= 5) return "2 件：攻击 +12%    5 件：连击每段伤害 +3%（最多 +15%）";
                    return "2 件：攻击 +12%";
                case EchoSet.FrostLaw:
                    if (pieces >= 5) return "2 件：冷凝伤害 +10%    5 件：对减速敌人伤害 +20%";
                    return "2 件：冷凝伤害 +10%";
                case EchoSet.NightWhisper:
                    if (pieces >= 5) return "2 件：暴击率 +8%    5 件：暴击伤害 +25%";
                    return "2 件：暴击率 +8%";
                case EchoSet.RockVow:
                    if (pieces >= 5) return "2 件：生命 +15%    5 件：护盾强效 +25%";
                    return "2 件：生命 +15%";
                case EchoSet.GaleWing:
                    if (pieces >= 5) return "2 件：体力 +20 / 攀爬滑翔消耗 -25%    5 件：冲刺后 4 秒攻击 +15%";
                    return "2 件：体力 +20 / 攀爬滑翔消耗 -25%";
            }
            return "";
        }

        public static string MainName(EchoMainStat s)
        {
            switch (s)
            {
                case EchoMainStat.AtkPct: return "攻击";
                case EchoMainStat.HpPct: return "生命";
                case EchoMainStat.DefPct: return "防御";
                case EchoMainStat.CritRate: return "暴击率";
                case EchoMainStat.CritDmg: return "暴击伤害";
                case EchoMainStat.ElemBonus: return "属性伤害加成";
                case EchoMainStat.EnergyRegen: return "共鸣效率";
            }
            return "";
        }

        public static bool IsPercent(EchoMainStat s)
        {
            return s != EchoMainStat.EnergyRegen;
        }
    }

    /// <summary>一件具体的声骸。</summary>
    [System.Serializable]
    public class EchoInstance
    {
        public string defId;
        public int star = 1;        // 1 - 5
        public int level = 0;       // 0 - 25
        public int mainType;        // EchoMainStat
        public float mainBase;      // 主词条基础值
        public List<int> subTypes = new List<int>();
        public List<float> subValues = new List<float>();

        public EchoDef Def { get { return EchoLibrary.Get(defId); } }
        public int Cost { get { return Def.cost; } }
        public int MaxLevel { get { return star >= 5 ? 25 : 20; } }

        public float MainValue
        {
            get
            {
                float per = star >= 5 ? 1f : 0.78f;
                return (mainBase + level * PerLevel()) * per;
            }
        }

        float PerLevel()
        {
            switch ((EchoMainStat)mainType)
            {
                case EchoMainStat.AtkPct: return 0.45f;
                case EchoMainStat.HpPct: return 0.55f;
                case EchoMainStat.DefPct: return 0.55f;
                case EchoMainStat.CritRate: return 0.28f;
                case EchoMainStat.CritDmg: return 0.60f;
                case EchoMainStat.ElemBonus: return 0.36f;
                case EchoMainStat.EnergyRegen: return 0.32f;
            }
            return 0f;
        }
    }
}
