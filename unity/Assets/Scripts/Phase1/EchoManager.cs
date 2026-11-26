// EchoManager — 星律回响管理器（Phase 1 / P1-B）。
// 掉落：击败暗星之影 18% 掉落 1 个回响（先做 3 种，均为「雾海残响」组）。
// 装备：每名队员 2 槽；第 1 槽主动技能（F 释放：召唤残影攻击最近敌人一次，
// 50 伤害，冷却 12 秒）；2 件套（同组 ×2：攻击 +10%）。
// 强化：+1 级消耗星律板 ×2（工坊产物，见 P1-C），上限 5 级。
// 属性：攻击回响 +8%/级档（每级 +4%）、生命回响 +25 HP/级档（每级 +12）、
// 防御回响 -8%/级档（每级 -4%，上限 -30%）。加算叠加。
// 存档：EchoStore（本地 JSON），每次变更立即保存。
using System.Collections.Generic;
using UnityEngine;
using TideSong;

namespace AstralBloom
{
    public class EchoManager : MonoBehaviour
    {
        public static EchoManager Instance { get; private set; }

        public const int EchoKindAttack = 0;
        public const int EchoKindHp = 1;
        public const int EchoKindDefense = 2;
        public const int DropChancePercent = 18;
        public const int MaxLevel = 5;
        public const int UpgradePlateCost = 2;
        public const float EchoSkillCooldown = 12f;
        public const float EchoSkillDamage = 50f;

        private EchoSaveData _data;
        private float _skillCooldownLeft;
        private bool _subscribed;

        /// <summary>为 false 时，RecalculateStats 不再把回响属性写到 PlayerCharacter。
        /// TideSong 场景由 v2.0 EchoSystem 接管实时回响增益（2026-11-25 决定），
        /// 此处只保留掉落 / 装备 / 强化 / 存档等养成数据，运行时不叠加增益。</summary>
        public static bool ApplyRuntimeBuffs = true;

        public int StarPlates { get { return _data != null ? _data.starPlates : 0; } }
        public float SkillCooldownLeft { get { return _skillCooldownLeft; } }
        public EchoSaveData Data { get { return _data; } }

        public static string DefName(int defId)
        {
            switch (defId)
            {
                case EchoKindAttack: return "雾海残响·攻击";
                case EchoKindHp: return "雾海残响·生命";
                case EchoKindDefense: return "雾海残响·防御";
                default: return "未知回响";
            }
        }

        public static string DefStatText(int defId, int level)
        {
            switch (defId)
            {
                case EchoKindAttack: return "攻击 +" + Mathf.RoundToInt(AttackBonus(level) * 100f) + "%";
                case EchoKindHp: return "生命 +" + Mathf.RoundToInt(HpBonus(level));
                case EchoKindDefense: return "减伤 " + Mathf.RoundToInt(DefenseBonus(level) * 100f) + "%";
                default: return "";
            }
        }

        public static float AttackBonus(int level) { return 0.08f + 0.04f * (level - 1); }
        public static float HpBonus(int level) { return 25f + 12f * (level - 1); }
        public static float DefenseBonus(int level) { return 0.08f + 0.04f * (level - 1); }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _data = EchoStore.Current;
            RecalculateStats();
        }

        private void OnEnable()
        {
            if (!_subscribed)
            {
                _subscribed = true;
                EnemyAI.OnEnemyDied += RollDrop;
            }
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                _subscribed = false;
                EnemyAI.OnEnemyDied -= RollDrop;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_skillCooldownLeft > 0f) _skillCooldownLeft -= Time.deltaTime;
        }

        // ---------------- 掉落 ----------------

        private void RollDrop(EnemyAI e)
        {
            if (Random.Range(0, 100) >= DropChancePercent) return;
            int defId = Random.Range(0, 3);
            var inst = new EchoInstance { uid = _data.nextUid++, defId = defId, level = 1 };
            _data.inventory.Add(inst);
            EchoStore.Save();
            GameHUD.Instance?.Toast("获得回响：" + DefName(defId));
            Debug.Log("[Echo] 掉落回响：" + DefName(defId) + "（uid " + inst.uid + "）");
        }

        // ---------------- 装备 / 卸下 / 强化 ----------------

        public EchoInstance FindEcho(int uid)
        {
            foreach (var e in _data.inventory)
                if (e.uid == uid) return e;
            return null;
        }

        public int EquippedUid(int member, int slot)
        {
            if (member < 0 || member > 3 || slot < 0 || slot > 1) return -1;
            return _data.equipped[member * 2 + slot];
        }

        public EchoInstance EquippedEcho(int member, int slot)
        {
            int uid = EquippedUid(member, slot);
            return uid < 0 ? null : FindEcho(uid);
        }

        public bool Equip(int uid, int member, int slot)
        {
            var echo = FindEcho(uid);
            if (echo == null || member < 0 || member > 3 || slot < 0 || slot > 1) return false;
            // 该回响已被其他槽装备时，先卸下。
            for (int i = 0; i < 8; i++)
                if (_data.equipped[i] == uid) _data.equipped[i] = -1;
            _data.equipped[member * 2 + slot] = uid;
            EchoStore.Save();
            RecalculateStats();
            Debug.Log("[Echo] " + SquadData.Roster[member].name + " 槽位 " + (slot + 1) +
                      " 装备 " + DefName(echo.defId) + " Lv." + echo.level);
            return true;
        }

        public bool Unequip(int member, int slot)
        {
            if (member < 0 || member > 3 || slot < 0 || slot > 1) return false;
            if (_data.equipped[member * 2 + slot] < 0) return false;
            _data.equipped[member * 2 + slot] = -1;
            EchoStore.Save();
            RecalculateStats();
            return true;
        }

        public bool Upgrade(int uid)
        {
            var echo = FindEcho(uid);
            if (echo == null || echo.level >= MaxLevel) return false;
            if (_data.starPlates < UpgradePlateCost) return false;
            _data.starPlates -= UpgradePlateCost;
            echo.level++;
            EchoStore.Save();
            RecalculateStats();
            Debug.Log("[Echo] " + DefName(echo.defId) + " 强化至 Lv." + echo.level +
                      "（消耗星律板 ×" + UpgradePlateCost + "）");
            return true;
        }

        public void AddPlates(int n)
        {
            if (n <= 0) return;
            _data.starPlates += n;
            EchoStore.Save();
        }

        public bool SpendPlates(int n)
        {
            if (n <= 0) return true;
            if (_data.starPlates < n) return false;
            _data.starPlates -= n;
            EchoStore.Save();
            return true;
        }

        // ---------------- 属性计算 ----------------

        public float EffectiveMaxHp(int member)
        {
            float hp = SquadData.Roster[member].maxHP;
            for (int slot = 0; slot < 2; slot++)
            {
                var e = EquippedEcho(member, slot);
                if (e != null && e.defId == EchoKindHp) hp += HpBonus(e.level);
            }
            return hp;
        }

        public bool HasTwoSet(int member)
        {
            // v0：3 种回响同属「雾海残响」组，2 槽都装满即 2 件套。
            return EquippedEcho(member, 0) != null && EquippedEcho(member, 1) != null;
        }

        public float DamageMultiplierFor(int member)
        {
            float bonus = 0f;
            for (int slot = 0; slot < 2; slot++)
            {
                var e = EquippedEcho(member, slot);
                if (e != null && e.defId == EchoKindAttack) bonus += AttackBonus(e.level);
            }
            if (HasTwoSet(member)) bonus += 0.10f;
            return 1f + bonus;
        }

        public float DamageReductionFor(int member)
        {
            float r = 0f;
            for (int slot = 0; slot < 2; slot++)
            {
                var e = EquippedEcho(member, slot);
                if (e != null && e.defId == EchoKindDefense) r += DefenseBonus(e.level);
            }
            return Mathf.Min(0.30f, r);
        }

        /// <summary>把当前操作队员的回响属性应用到 PlayerCharacter；非操作队员的
        /// HP 上限在 SquadManager 切换时读取 EffectiveMaxHp。
        /// TideSong 场景中 ApplyRuntimeBuffs 为 false 时直接返回（v2.0 EchoSystem 接管）。</summary>
        public void RecalculateStats()
        {
            if (!ApplyRuntimeBuffs) return;
            var sq = SquadManager.Instance;
            var player = TideSong.GameManager.Instance?.Player;
            if (sq != null && player != null)
            {
                int active = sq.ActiveIndex;
                player.DamageMultiplier = DamageMultiplierFor(active);
                player.DamageReduction = DamageReductionFor(active);
                float effMax = EffectiveMaxHp(active);
                player.maxHP = effMax;
                player.hp = Mathf.Min(player.hp, effMax);
            }
        }

        // ---------------- 主动技能（F） ----------------

        public string SlotOneEchoName()
        {
            var sq = SquadManager.Instance;
            if (sq == null) return "未装备回响";
            var e = EquippedEcho(sq.ActiveIndex, 0);
            return e == null ? "未装备回响" : DefName(e.defId) + " Lv." + e.level;
        }

        public string EchoSkillCooldownText()
        {
            if (SlotOneEchoName() == "未装备回响") return "";
            return _skillCooldownLeft > 0f ? "（冷却 " + Mathf.CeilToInt(_skillCooldownLeft) + "s）" : "（就绪）";
        }

        public bool TryUseEchoSkill()
        {
            var sq = SquadManager.Instance;
            var player = TideSong.GameManager.Instance?.Player;
            if (sq == null || player == null) return false;
            var echo = EquippedEcho(sq.ActiveIndex, 0);
            if (echo == null)
            {
                GameHUD.Instance?.Toast("第 1 回响槽未装备");
                return false;
            }
            if (_skillCooldownLeft > 0f) return false;

            EnemyAI best = null;
            float bestD = 10f;
            foreach (var e in Object.FindObjectsOfType<EnemyAI>())
            {
                if (e == null || e.Dead) continue;
                float d = Vector3.Distance(player.transform.position, e.transform.position);
                if (d < bestD) { bestD = d; best = e; }
            }
            if (best == null)
            {
                GameHUD.Instance?.Toast("附近没有敌人");
                return false;
            }
            _skillCooldownLeft = EchoSkillCooldown;
            Vector3 push = (best.transform.position - player.transform.position).normalized;
            best.TakeDamage(EchoSkillDamage, push * 6f);
            // 残影：召唤一个短暂的半透明几何体幻影冲向敌人
            FX.Burst(best.transform.position + Vector3.up * 1.2f,
                new Color(0.65f, 0.85f, 1f), 50, 8f, 0.5f, 1.1f);
            FX.Shockwave(best.transform.position, new Color(0.65f, 0.85f, 1f), 5f, 0.4f);
            GameHUD.Instance?.Toast("回响技能 · 残影一击（" + EchoSkillDamage + " 伤害）");
            Debug.Log("[Echo] 回响技能释放：" + DefName(echo.defId) + "，命中 " + best.name);
            return true;
        }

        // ---------------- 测试钩子（smoke 用） ----------------

        public int TestGrantEcho(int defId)
        {
            var inst = new EchoInstance { uid = _data.nextUid++, defId = defId, level = 1 };
            _data.inventory.Add(inst);
            EchoStore.Save();
            return inst.uid;
        }

        public int InventoryCount { get { return _data != null ? _data.inventory.Count : 0; } }
    }
}
