using UnityEngine;

namespace TideSong
{
    /// <summary>共鸣属性（对应鸣潮六系）。</summary>
    public enum ElementType
    {
        Aero,      // 气动
        Glacio,    // 冷凝
        Spectro,   // 衍射
        Havoc,     // 湮灭
        Fusion,    // 热熔
        Electro    // 导电
    }

    /// <summary>武器形态，决定普攻表现。</summary>
    public enum WeaponStyle
    {
        Sword,     // 近战剑
        Guns,      // 远程射击
        Gauntlet,  // 近战拳
        Scythe     // 近战镰
    }

    /// <summary>一位共鸣者的静态定义。运行时的血量/能量另存在 ResonatorState。</summary>
    public class ResonatorDef
    {
        public string id;
        public string name;
        public string title;

        public ElementType element;
        public WeaponStyle weapon;

        public Color primary;     // 主衣色
        public Color accent;      // 装饰 / 发色
        public Color elementColor; // 属性光效色

        public float maxHP = 150f;
        public float walkSpeed = 6.2f;
        public float sprintSpeed = 10.8f;

        public float atkMul = 1f;    // 普攻倍率
        public float skillMul = 1f;  // 共鸣技能倍率
        public float burstMul = 1f;  // 共鸣解放倍率

        /// <summary>三段普攻的基础伤害。</summary>
        public float[] comboDamage = { 24f, 28f, 56f };

        public float attackRange = 2.9f;   // 近战半径；远程角色用于散弹扩散
        public float skillCooldown = 7.5f;
        public float burstCost = 100f;

        public string skillName, skillDesc;
        public string burstName, burstDesc;
        public string passiveName, passiveDesc;
        public string introName, introDesc;  // 变奏技能（切人登场）
        public string outroName, outroDesc;  // 延奏技能（切人离场）

        public string ElementName
        {
            get
            {
                switch (element)
                {
                    case ElementType.Aero: return "气动";
                    case ElementType.Glacio: return "冷凝";
                    case ElementType.Spectro: return "衍射";
                    case ElementType.Havoc: return "湮灭";
                    case ElementType.Fusion: return "热熔";
                    case ElementType.Electro: return "导电";
                }
                return "未知";
            }
        }

        public string WeaponName
        {
            get
            {
                switch (weapon)
                {
                    case WeaponStyle.Sword: return "长刃";
                    case WeaponStyle.Guns: return "佩枪";
                    case WeaponStyle.Gauntlet: return "臂铠";
                    case WeaponStyle.Scythe: return "迅刀";
                }
                return "长刃";
            }
        }
    }

    /// <summary>角色库。全部原创角色，走鸣潮那套「属性 + 定位 + 共鸣回路」的结构。</summary>
    public static class ResonatorLibrary
    {
        public static readonly ResonatorDef[] All = new ResonatorDef[]
        {
            new ResonatorDef
            {
                id = "shio", name = "汐", title = "风起之时",
                element = ElementType.Aero, weapon = WeaponStyle.Sword,
                primary = new Color(0.16f, 0.36f, 0.62f),
                accent = new Color(0.85f, 0.94f, 1f),
                elementColor = new Color(0.45f, 0.95f, 0.90f),
                maxHP = 150f, walkSpeed = 6.3f, sprintSpeed = 11.2f,
                atkMul = 1.0f, skillMul = 1.0f, burstMul = 1.0f,
                comboDamage = new[] { 24f, 27f, 58f },
                attackRange = 2.9f, skillCooldown = 7.0f,
                skillName = "旋风斩 · 断潮",
                skillDesc = "向前突进并卷起气刃，360° 两段伤害，命中后积累「风势」。",
                burstName = "共鸣解放 · 风暴回旋",
                burstDesc = "展开持续 9 秒的风暴领域，牵引并持续撕裂范围内的敌人，自身移速与攻速提升。",
                passiveName = "共鸣回路 · 疾风",
                passiveDesc = "第三段普攻命中后，下一次共鸣技能伤害提升 60%。",
                introName = "变奏 · 掠风",
                introDesc = "登场时向前斩出一道风刃，造成 90% 攻击力的气动伤害。",
                outroName = "延奏 · 余风",
                outroDesc = "离场后留下风场 5 秒，持续牵引并伤害敌人。"
            },
            new ResonatorDef
            {
                id = "shuang", name = "霜见", title = "雪落无声",
                element = ElementType.Glacio, weapon = WeaponStyle.Guns,
                primary = new Color(0.62f, 0.74f, 0.92f),
                accent = new Color(0.78f, 0.92f, 1f),
                elementColor = new Color(0.62f, 0.86f, 1f),
                maxHP = 122f, walkSpeed = 6.0f, sprintSpeed = 10.4f,
                atkMul = 0.82f, skillMul = 1.24f, burstMul = 1.1f,
                comboDamage = new[] { 16f, 17f, 34f },
                attackRange = 1.6f, skillCooldown = 8.5f,
                skillName = "共鸣技能 · 霜棱散射",
                skillDesc = "扇形射出 5 道冰棱，命中造成冷凝伤害并叠加「霜蚀」。",
                burstName = "共鸣解放 · 极寒领域",
                burstDesc = "展开 10 秒冰封领域，范围内敌人减速 45% 并持续受到冷凝伤害。",
                passiveName = "共鸣回路 · 凝华",
                passiveDesc = "普攻为射击，第三段射出穿透冰锥；命中减速目标时暴击率 +15%。",
                introName = "变奏 · 霜降",
                introDesc = "登场时向四周倾泻冰晶，造成范围冷凝伤害并减速。",
                outroName = "延奏 · 寒潮",
                outroDesc = "离场后留下寒潮 5 秒，范围内敌人减速 40%。"
            },
            new ResonatorDef
            {
                id = "lan", name = "岚", title = "晨光守卫",
                element = ElementType.Spectro, weapon = WeaponStyle.Gauntlet,
                primary = new Color(0.86f, 0.80f, 0.58f),
                accent = new Color(1f, 0.96f, 0.82f),
                elementColor = new Color(1f, 0.92f, 0.62f),
                maxHP = 178f, walkSpeed = 5.9f, sprintSpeed = 10.0f,
                atkMul = 0.86f, skillMul = 0.9f, burstMul = 0.95f,
                comboDamage = new[] { 22f, 24f, 46f },
                attackRange = 2.6f, skillCooldown = 9.0f,
                skillName = "共鸣技能 · 圣辉壁垒",
                skillDesc = "为全队附加护盾（自身 22% 生命），并回复自身 12% 生命。",
                burstName = "共鸣解放 · 光耀壁垒",
                burstDesc = "展开 8 秒圣光领域，全队减伤 40%，范围内敌人被持续眩晕与灼伤。",
                passiveName = "共鸣回路 · 守护",
                passiveDesc = "受到伤害时，全队获得 8 点协奏能量；生命低于 40% 时防御提升。",
                introName = "变奏 · 晨祷",
                introDesc = "登场时爆发圣光，为全队回血并击退周围敌人。",
                outroName = "延奏 · 庇护",
                outroDesc = "离场后留下圣光领域 5 秒，每秒为玩家回复生命。"
            },
            new ResonatorDef
            {
                id = "yuan", name = "渊", title = "深潮之影",
                element = ElementType.Havoc, weapon = WeaponStyle.Scythe,
                primary = new Color(0.30f, 0.22f, 0.38f),
                accent = new Color(0.66f, 0.52f, 0.95f),
                elementColor = new Color(0.72f, 0.45f, 1f),
                maxHP = 132f, walkSpeed = 6.4f, sprintSpeed = 11.6f,
                atkMul = 1.18f, skillMul = 1.3f, burstMul = 1.45f,
                comboDamage = new[] { 30f, 34f, 72f },
                attackRange = 3.2f, skillCooldown = 8.0f,
                skillName = "共鸣技能 · 暗潮撕裂",
                skillDesc = "向前方横扫三道暗刃，造成高额湮灭伤害并吸取生命。",
                burstName = "共鸣解放 · 湮灭降临",
                burstDesc = "消耗 18% 生命，爆发大范围湮灭冲击，对生命低于 30% 的敌人额外造成 80% 伤害。",
                passiveName = "共鸣回路 · 蚀骨",
                passiveDesc = "对生命低于 50% 的敌人造成的伤害提升 25%，击杀回复 10 点能量。",
                introName = "变奏 · 影袭",
                introDesc = "登场时瞬移斩击，对路径上的敌人造成 130% 湮灭伤害。",
                outroName = "延奏 · 暗蚀",
                outroDesc = "离场后留下暗蚀领域 5 秒，敌人受到的伤害提升 15%。"
            }
        };

        /// <summary>第四位角色需要收集 6 件声骸后解锁。</summary>
        public const int YuanUnlockEchoes = 6;

        public static ResonatorDef Get(string id)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].id == id) return All[i];
            return All[0];
        }

        public static ResonatorDef Shio { get { return All[0]; } }
        public static ResonatorDef Shuang { get { return All[1]; } }
        public static ResonatorDef Lan { get { return All[2]; } }
        public static ResonatorDef Yuan { get { return All[3]; } }
    }
}
