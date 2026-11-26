using System.Collections.Generic;
using UnityEngine;

namespace TideSong
{
    /// <summary>
    /// 声骸系统：掉落、背包、5 槽装备（cost 上限 12）、升级、分解、套装效果、声骸技能。
    /// 属性加成通过 Recalc 汇总成一组只读属性，伤害结算统一从这里取。
    /// </summary>
    public class EchoSystem : MonoBehaviour
    {
        public static EchoSystem Instance { get; private set; }

        public const int MaxCost = 12;
        public const int SlotCount = 5;
        public const float EchoSkillCooldown = 18f;

        public List<EchoInstance> bag = new List<EchoInstance>();
        public EchoInstance[] equipped = new EchoInstance[SlotCount];
        public int dust;

        // 汇总属性（百分比点数，33 表示 +33%）
        public float AtkBonus { get; private set; }
        public float HpBonus { get; private set; }
        public float DefBonus { get; private set; }
        public float CritRate { get; private set; }
        public float CritDmg { get; private set; }
        public float EnergyRegen { get; private set; }
        public float HealBonus { get; private set; }
        public float StaminaBonus { get; private set; }
        public float ClimbCostReduction { get; private set; }
        public float ShieldBonus { get; private set; }

        readonly float[] elemBonus = new float[6];

        public float EchoSkillCd { get; private set; }
        public int OwnedCount { get { int n = bag.Count; for (int i = 0; i < SlotCount; i++) if (equipped[i] != null) n++; return n; } }

        public int UsedCost
        {
            get
            {
                int c = 0;
                for (int i = 0; i < SlotCount; i++) if (equipped[i] != null) c += equipped[i].Cost;
                return c;
            }
        }

        public float ElementBonus(ElementType el)
        {
            return elemBonus[(int)el] / 100f;
        }

        public float ComboDamageBonus
        {
            get
            {
                if (SetCount(EchoSet.TideEcho) < 5 || PlayerCharacter.Instance == null) return 0f;
                return Mathf.Min(0.15f, PlayerCharacter.Instance.comboCount * 0.03f);
            }
        }

        public int SetCount(EchoSet set)
        {
            int n = 0;
            for (int i = 0; i < SlotCount; i++)
                if (equipped[i] != null && equipped[i].Def.set == set) n++;
            return n;
        }

        public EchoInstance MainEcho
        {
            get
            {
                EchoInstance best = null;
                for (int i = 0; i < SlotCount; i++)
                {
                    var e = equipped[i];
                    if (e == null) continue;
                    if (best == null || e.Cost > best.Cost) best = e;
                }
                return best;
            }
        }

        void Awake()
        {
            Instance = this;
        }

        void Update()
        {
            if (EchoSkillCd > 0f) EchoSkillCd -= Time.deltaTime;
        }

        // ---------------- 属性汇总 ----------------

        public void Recalc()
        {
            AtkBonus = HpBonus = DefBonus = CritRate = CritDmg = 0f;
            EnergyRegen = HealBonus = StaminaBonus = ClimbCostReduction = ShieldBonus = 0f;
            for (int i = 0; i < elemBonus.Length; i++) elemBonus[i] = 0f;

            for (int i = 0; i < SlotCount; i++)
            {
                var e = equipped[i];
                if (e == null) continue;

                float v = e.MainValue;
                switch ((EchoMainStat)e.mainType)
                {
                    case EchoMainStat.AtkPct: AtkBonus += v; break;
                    case EchoMainStat.HpPct: HpBonus += v; break;
                    case EchoMainStat.DefPct: DefBonus += v; break;
                    case EchoMainStat.CritRate: CritRate += v; break;
                    case EchoMainStat.CritDmg: CritDmg += v; break;
                    case EchoMainStat.EnergyRegen: EnergyRegen += v; break;
                    case EchoMainStat.ElemBonus: elemBonus[(int)ElementOfSet(e.Def.set)] += v; break;
                }

                for (int k = 0; k < e.subTypes.Count && k < e.subValues.Count; k++)
                {
                    float sv = e.subValues[k];
                    switch ((EchoMainStat)e.subTypes[k])
                    {
                        case EchoMainStat.AtkPct: AtkBonus += sv; break;
                        case EchoMainStat.HpPct: HpBonus += sv; break;
                        case EchoMainStat.DefPct: DefBonus += sv; break;
                        case EchoMainStat.CritRate: CritRate += sv; break;
                        case EchoMainStat.CritDmg: CritDmg += sv; break;
                        case EchoMainStat.EnergyRegen: EnergyRegen += sv; break;
                        case EchoMainStat.ElemBonus: elemBonus[(int)ElementOfSet(e.Def.set)] += sv; break;
                    }
                }
            }

            // 套装
            if (SetCount(EchoSet.TideEcho) >= 2) AtkBonus += 12f;
            if (SetCount(EchoSet.FrostLaw) >= 2) elemBonus[(int)ElementType.Glacio] += 10f;
            if (SetCount(EchoSet.NightWhisper) >= 2) CritRate += 8f;
            if (SetCount(EchoSet.NightWhisper) >= 5) CritDmg += 25f;
            if (SetCount(EchoSet.RockVow) >= 2) HpBonus += 15f;
            if (SetCount(EchoSet.RockVow) >= 5) ShieldBonus += 25f;
            if (SetCount(EchoSet.GaleWing) >= 2) { StaminaBonus += 20f; ClimbCostReduction += 25f; }

            if (PlayerCharacter.Instance != null) PlayerCharacter.Instance.RefreshStatsFromEcho();
        }

        static ElementType ElementOfSet(EchoSet set)
        {
            switch (set)
            {
                case EchoSet.TideEcho: return ElementType.Aero;
                case EchoSet.FrostLaw: return ElementType.Glacio;
                case EchoSet.NightWhisper: return ElementType.Havoc;
                case EchoSet.RockVow: return ElementType.Spectro;
                case EchoSet.GaleWing: return ElementType.Aero;
            }
            return ElementType.Aero;
        }

        // ---------------- 掉落与生成 ----------------

        public void OnEnemyKilled(Vector3 pos, float strength)
        {
            dust += Random.Range(1, 4);

            // 35% 掉声骸；敌人越强，星级越高
            if (Random.value > 0.35f) return;

            var echo = Roll(strength);
            bag.Add(echo);
            EchoPickup.Spawn(pos + Vector3.up * 1.5f, echo);
        }

        public EchoInstance Roll(float strength)
        {
            // cost 权重：1 星 60% / 3 星 30% / 4 星 10%
            float r = Random.value;
            int targetCost = r < 0.10f ? 4 : (r < 0.40f ? 3 : 1);

            var pool = new List<EchoDef>();
            for (int i = 0; i < EchoLibrary.All.Length; i++)
                if (EchoLibrary.All[i].cost == targetCost) pool.Add(EchoLibrary.All[i]);
            var def = pool[Random.Range(0, pool.Count)];

            int star = 3;
            if (strength > 1.5f) star = Random.value < 0.35f ? 5 : 4;
            else if (strength > 1.15f) star = Random.value < 0.25f ? 5 : 4;
            else star = Random.value < 0.15f ? 4 : 3;

            var e = new EchoInstance
            {
                defId = def.id,
                star = star,
                level = 0,
                mainType = (int)RollMain(def.cost),
                mainBase = MainBase((EchoMainStat)0, def.cost)
            };
            e.mainType = (int)RollMain(def.cost);
            e.mainBase = MainBase((EchoMainStat)e.mainType, def.cost);

            int subCount = star <= 2 ? 2 : (star == 3 ? 3 : (star == 4 ? 4 : 5));
            var used = new HashSet<int>();
            for (int i = 0; i < subCount; i++)
            {
                int t;
                int guard = 0;
                do { t = Random.Range(0, 7); guard++; } while (used.Contains(t) && guard < 30);
                used.Add(t);
                e.subTypes.Add(t);
                e.subValues.Add(RollSubValue((EchoMainStat)t, star));
            }
            return e;
        }

        static EchoMainStat RollMain(int cost)
        {
            if (cost >= 4)
            {
                int r = Random.Range(0, 4);
                return r == 0 ? EchoMainStat.CritRate : r == 1 ? EchoMainStat.CritDmg : r == 2 ? EchoMainStat.ElemBonus : EchoMainStat.AtkPct;
            }
            if (cost == 3)
            {
                int r = Random.Range(0, 5);
                return r == 0 ? EchoMainStat.AtkPct : r == 1 ? EchoMainStat.HpPct : r == 2 ? EchoMainStat.DefPct : r == 3 ? EchoMainStat.EnergyRegen : EchoMainStat.ElemBonus;
            }
            int q = Random.Range(0, 3);
            return q == 0 ? EchoMainStat.AtkPct : q == 1 ? EchoMainStat.HpPct : EchoMainStat.DefPct;
        }

        static float MainBase(EchoMainStat s, int cost)
        {
            float scale = cost >= 4 ? 1f : (cost == 3 ? 0.55f : 0.26f);
            float v;
            switch (s)
            {
                case EchoMainStat.AtkPct: v = 33f; break;
                case EchoMainStat.HpPct: v = 33f; break;
                case EchoMainStat.DefPct: v = 40f; break;
                case EchoMainStat.CritRate: v = 22f; break;
                case EchoMainStat.CritDmg: v = 44f; break;
                case EchoMainStat.ElemBonus: v = 30f; break;
                case EchoMainStat.EnergyRegen: v = 32f; break;
                default: v = 20f; break;
            }
            return v * scale;
        }

        static float RollSubValue(EchoMainStat s, int star)
        {
            float k = 0.8f + star * 0.06f;
            switch (s)
            {
                case EchoMainStat.AtkPct: return Random.Range(4.2f, 8.6f) * k;
                case EchoMainStat.HpPct: return Random.Range(4.6f, 9.2f) * k;
                case EchoMainStat.DefPct: return Random.Range(5.2f, 10.4f) * k;
                case EchoMainStat.CritRate: return Random.Range(2.1f, 5.4f) * k;
                case EchoMainStat.CritDmg: return Random.Range(5.4f, 11.0f) * k;
                case EchoMainStat.ElemBonus: return Random.Range(3.2f, 7.6f) * k;
                case EchoMainStat.EnergyRegen: return Random.Range(3.4f, 8.2f) * k;
            }
            return 5f;
        }

        // ---------------- 装备 ----------------

        public bool CanEquip(EchoInstance e, int slot)
        {
            if (e == null || slot < 0 || slot >= SlotCount) return false;
            int cost = UsedCost - (equipped[slot] != null ? equipped[slot].Cost : 0) + e.Cost;
            return cost <= MaxCost;
        }

        public bool Equip(EchoInstance e, int slot)
        {
            if (!CanEquip(e, slot))
            {
                GameHUD.Instance?.Toast("声骸 cost 超出上限（" + MaxCost + "）");
                return false;
            }
            bag.Remove(e);
            if (equipped[slot] != null) bag.Add(equipped[slot]);
            equipped[slot] = e;
            Recalc();
            Sfx.Play2D(SfxId.Pickup);
            return true;
        }

        public void Unequip(int slot)
        {
            if (slot < 0 || slot >= SlotCount || equipped[slot] == null) return;
            bag.Add(equipped[slot]);
            equipped[slot] = null;
            Recalc();
        }

        public int UpgradeCost(EchoInstance e)
        {
            return 15 + e.level * 10;
        }

        public bool Upgrade(EchoInstance e)
        {
            if (e == null) return false;
            if (e.level >= e.MaxLevel)
            {
                GameHUD.Instance?.Toast("已满级");
                return false;
            }
            int cost = UpgradeCost(e);
            if (dust < cost)
            {
                GameHUD.Instance?.Toast("声骸粉尘不足（需要 " + cost + "）");
                return false;
            }
            dust -= cost;
            e.level++;
            Recalc();
            Sfx.Play2D(SfxId.Pickup);
            return true;
        }

        public void Salvage(EchoInstance e)
        {
            if (e == null) return;
            int gain = 8 + e.star * 6 + e.level * 3;
            dust += gain;
            bag.Remove(e);
            for (int i = 0; i < SlotCount; i++) if (equipped[i] == e) equipped[i] = null;
            Recalc();
            GameHUD.Instance?.Toast("分解 +" + gain + " 粉尘");
        }

        // ---------------- 声骸技能 ----------------

        public void CastEchoSkill()
        {
            if (EchoSkillCd > 0f)
            {
                GameHUD.Instance?.Toast("声骸技能冷却 " + EchoSkillCd.ToString("0.0") + "s");
                return;
            }
            var main = MainEcho;
            if (main == null)
            {
                GameHUD.Instance?.Toast("没有装备声骸 · 按 B 打开声骸面板");
                return;
            }

            EchoSkillCd = EchoSkillCooldown;
            var def = main.Def;
            var p = PlayerCharacter.Instance;
            Vector3 at = p.transform.position;
            Vector3 fwd = p.transform.forward;
            Color c = def.color;

            FX.Burst(at + Vector3.up, c, 40, 7f, 0.4f, 0.8f);
            Sfx.Play(SfxId.Skill, at);
            GameHUD.Instance?.Toast("声骸 · " + def.skillName.Replace("声骸 · ", ""));

            switch (def.skill)
            {
                case EchoSkillType.Summon:
                {
                    var pr = ResonanceProjectile.Spawn(at + Vector3.up * 1.2f + fwd * 1.2f, fwd, def.summonDamage, ElementType.Aero, 26f, 2.2f, true);
                    pr.transform.localScale = Vector3.one * 2.2f;
                    pr.spin = true;
                    FX.Shockwave(at + fwd * 2f, c, 5f, 0.4f);
                    break;
                }

                case EchoSkillType.Buff:
                    p.echoAtkBuff = 0.30f;
                    p.echoBuffTime = 8f;
                    p.AddEnergy(20f);
                    FX.Shockwave(at, c, 5f, 0.5f);
                    break;

                case EchoSkillType.Heal:
                    p.Heal(p.maxHP * 0.30f);
                    p.AddShield(p.maxHP * 0.25f);
                    FX.Shockwave(at, c, 5.5f, 0.6f);
                    break;

                case EchoSkillType.Control:
                {
                    CombatUtil.DamageArea(at + fwd * 2f, 6f, def.summonDamage, ElementType.Glacio);
                    foreach (var e in Object.FindObjectsOfType<EnemyAI>())
                        if (!e.Dead && Vector3.Distance(e.transform.position, at) < 6.5f) { e.Stagger(1.6f); e.ApplySlow(0.4f, 3f); }
                    FX.Shockwave(at, c, 6.5f, 0.6f);
                    break;
                }
            }
        }

        /// <summary>全新存档时给一件初始声骸，保证第一次进游戏就能试 R 技能。</summary>
        public void GrantStarterIfEmpty()
        {
            if (OwnedCount > 0) return;

            const string starterId = "tide_shell";
            var e = new EchoInstance
            {
                defId = starterId,
                star = 3,
                level = 0,
                mainType = (int)EchoMainStat.AtkPct,
                mainBase = MainBase(EchoMainStat.AtkPct, 1)
            };
            e.subTypes.Add((int)EchoMainStat.CritRate); e.subValues.Add(3.2f);
            e.subTypes.Add((int)EchoMainStat.AtkPct); e.subValues.Add(5.4f);
            e.subTypes.Add((int)EchoMainStat.CritDmg); e.subValues.Add(6.8f);

            bag.Add(e);
            dust += 40;
            Equip(e, 0);
            GameHUD.Instance?.Toast("获得初始声骸 · 按 R 释放声骸技能，B 打开声骸面板");
        }

        // ---------------- 存读档 ----------------

        [System.Serializable]
        public class SaveData
        {
            public List<EchoInstance> bag = new List<EchoInstance>();
            public EchoInstance[] equipped = new EchoInstance[SlotCount];
            public int dust;
        }

        public void Save()
        {
            var d = new SaveData { bag = bag, equipped = equipped, dust = dust };
            PlayerPrefs.SetString("TideSong.Echo.v1", JsonUtility.ToJson(d));
            PlayerPrefs.Save();
        }

        public void Load()
        {
            string raw = PlayerPrefs.GetString("TideSong.Echo.v1", "");
            if (string.IsNullOrEmpty(raw)) return;
            SaveData d = null;
            try { d = JsonUtility.FromJson<SaveData>(raw); } catch { }
            if (d == null) return;
            if (d.bag != null) bag = d.bag;
            if (d.equipped != null && d.equipped.Length == SlotCount) equipped = d.equipped;
            dust = d.dust;
            Recalc();
        }
    }

    /// <summary>地上的声骸光球：靠近自动吸取。</summary>
    public class EchoPickup : MonoBehaviour
    {
        public EchoInstance echo;
        Transform core;
        float t;
        float life = 90f;
        bool absorbing;
        Vector3 vel;

        public static EchoPickup Spawn(Vector3 pos, EchoInstance echo)
        {
            var go = new GameObject("EchoPickup");
            go.transform.position = pos;

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1.0f;

            var color = EchoLibrary.SetColor(echo.Def.set);
            var mat = MaterialLibrary.Make(color, 0.2f, 0.9f);
            MaterialLibrary.ApplyGlow(mat, color * 2.6f);
            var core = MaterialLibrary.Prim(PrimitiveType.Sphere, go.transform, mat, Vector3.zero, new Vector3(0.34f, 0.34f, 0.34f));
            core.name = "Core";
            core.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var ringMat = MaterialLibrary.MakeTransparent(new Color(color.r, color.g, color.b, 0.6f), 0f, 0.6f);
            var ring = MaterialLibrary.Prim(PrimitiveType.Cylinder, go.transform, ringMat, Vector3.zero, new Vector3(1.0f, 0.01f, 1.0f));
            ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var p = go.AddComponent<EchoPickup>();
            p.echo = echo;
            p.core = core.transform;

            FX.Burst(pos, color, 16, 3f, 0.2f, 0.6f);
            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            transform.Rotate(Vector3.up, 90f * dt, Space.World);
            if (core != null) core.localPosition = new Vector3(0f, Mathf.Sin(t * 2.2f) * 0.15f, 0f);

            var player = PlayerCharacter.Instance;
            if (player == null) return;

            float d = Vector3.Distance(transform.position, player.transform.position + Vector3.up);
            if (d < 4.5f) absorbing = true;

            if (absorbing)
            {
                Vector3 target = player.transform.position + Vector3.up * 1.2f;
                transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-9f * dt));
                if (Vector3.Distance(transform.position, target) < 0.6f)
                {
                    Collect();
                    return;
                }
            }

            if (t > life) Destroy(gameObject);
        }

        void Collect()
        {
            var sys = EchoSystem.Instance;
            if (sys != null && !sys.bag.Contains(echo)) sys.bag.Add(echo);
            Sfx.Play2D(SfxId.Pickup);
            FX.Burst(transform.position, EchoLibrary.SetColor(echo.Def.set), 20, 4f, 0.25f, 0.5f);
            GameHUD.Instance?.Toast("获得声骸 · " + echo.Def.name + "（" + echo.star + "★ cost" + echo.Cost + "）");
            Destroy(gameObject);
        }
    }
}
