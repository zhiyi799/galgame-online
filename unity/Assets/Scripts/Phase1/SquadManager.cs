// SquadManager — 星律小队管理器（Phase 1 / P1-A）。
// 4 人编队：1 人操作（TideSong 的 PlayerCharacter）+ 3 人 AI 队友。
// 负责：数字键 1–4 切换、入场技/退场技、星律共鸣值、共享 SP、
// 连携技（R）、完美闪避反击、F 键路由（无可交互物时触发回响技能）。
// 由 TideSong GameManager.BuildWorld 挂接（1 行 hook），不改 TideSong 流程。
using System.Collections;
using UnityEngine;
using TideSong;

namespace AstralBloom
{
    public class SquadManager : MonoBehaviour
    {
        public static SquadManager Instance { get; private set; }

        public struct MemberState
        {
            public float hp;
            public float energy;
        }

        public int ActiveIndex { get; private set; }
        public float Resonance { get; private set; }
        public float SP { get; private set; }
        public bool ComboReady { get; private set; }
        public float SwitchCooldownLeft { get; private set; }
        public string LastSkillText { get; private set; }

        /// <summary>非操作队员的保存 HP / 能量（供 HUD 读取）。</summary>
        public float GetMemberHp(int i)
        {
            if (i < 0 || i > 3) return 0f;
            return _states[i].hp;
        }

        public float GetMemberEnergy(int i)
        {
            if (i < 0 || i > 3) return 0f;
            return _states[i].energy;
        }

        /// <summary>队员有效 HP 上限（基础 + 生命回响）。</summary>
        private float EffMaxHp(int i)
        {
            var em = EchoManager.Instance;
            return em != null ? em.EffectiveMaxHp(i) : SquadData.Roster[i].maxHP;
        }

        private MemberState[] _states = new MemberState[4];
        private TideSong.GameManager _gm;
        private PlayerCharacter _player;
        private SquadCompanion[] _companions = new SquadCompanion[3];
        private float _speedBuffT;
        private bool _wasDodging;
        private bool _slowMo;

        public SquadMemberDef ActiveDef { get { return SquadData.Roster[ActiveIndex]; } }

        public void Init(TideSong.GameManager gm)
        {
            Instance = this;
            _gm = gm;
            _player = gm.Player;
            ActiveIndex = 0;
            for (int i = 0; i < 4; i++)
            {
                _states[i].hp = SquadData.Roster[i].maxHP;
                _states[i].energy = 0f;
            }
            Resonance = 0f;
            SP = 0f;

            // 3 名队友：主角左右各一、身后一，出生点附近。
            Vector3 spawn = _player.transform.position;
            int[] mates = { 1, 2, 3 };
            Vector3[] offsets = {
                new Vector3(-2.2f, 0f, -1.2f),
                new Vector3(2.2f, 0f, -1.2f),
                new Vector3(0f, 0f, -2.6f),
            };
            for (int k = 0; k < 3; k++)
            {
                var go = new GameObject("SquadMate");
                var c = go.AddComponent<SquadCompanion>();
                Vector3 p = spawn + offsets[k];
                p.y = WorldData.HeightAt(p.x, p.z);
                c.Setup(mates[k], p);
                _companions[k] = c;
            }

            PlayerCharacter.OnMeleeHit += OnMeleeHit;
            PlayerCharacter.OnSkillUsed += OnSkillUsed;
            PlayerCharacter.OnBurstUsed += OnBurstUsed;
            EnemyAI.OnEnemyDied += OnEnemyDied;
            EnemyAI.OnEnemyStaggered += OnEnemyStaggered;

            gameObject.AddComponent<SquadHud>();
            Debug.Log("[Squad] 星律小队就绪：4 人编队（无星者 + 星夜·雪 + 星夜·花 + 月下·凛）");
        }

        private void OnDestroy()
        {
            PlayerCharacter.OnMeleeHit -= OnMeleeHit;
            PlayerCharacter.OnSkillUsed -= OnSkillUsed;
            PlayerCharacter.OnBurstUsed -= OnBurstUsed;
            EnemyAI.OnEnemyDied -= OnEnemyDied;
            EnemyAI.OnEnemyStaggered -= OnEnemyStaggered;
        }

        // ---------------- 资源积累 ----------------

        private void OnMeleeHit(int count)
        {
            AddResonance(2f * count);
            AddSP(1f * count);
        }

        private void OnSkillUsed()
        {
            AddResonance(5f);
        }

        private void OnBurstUsed()
        {
            GameHUD.Instance?.Toast("星鸣 · 觉醒 —— " + ActiveDef.name);
            Debug.Log("[Squad] 星鸣觉醒：" + ActiveDef.name);
        }

        public void OnCompanionHit()
        {
            AddResonance(1f);
            AddSP(0.5f);
        }

        private void OnEnemyDied(EnemyAI e)
        {
            AddResonance(5f);
            AddSP(1f);
        }

        public void AddResonance(float v)
        {
            Resonance = Mathf.Clamp(Resonance + v, 0f, SquadData.ResonanceMax);
        }

        public void AddSP(float v)
        {
            SP = Mathf.Clamp(SP + v, 0f, SquadData.SpMax);
        }

        /// <summary>战技 E 的 SP 门槛（GameManager 调用）：SP ≥ 3 才放行并扣费。</summary>
        public bool TryUseSkill()
        {
            if (_player == null) return true;
            if (_player.SkillCooldown > 0f || _player.stamina < 20f) return false;
            if (SP < SquadData.SkillSpCost)
            {
                GameHUD.Instance?.Toast("SP 不足（" + Mathf.FloorToInt(SP) + " / 3）");
                return false;
            }
            SP -= SquadData.SkillSpCost;
            return true;
        }

        // ---------------- 切换 ----------------

        private void Update()
        {
            if (_gm == null || _gm.Paused || _player == null) return;
            SwitchCooldownLeft = Mathf.Max(0f, SwitchCooldownLeft - Time.deltaTime);

            if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchTo(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchTo(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchTo(2);
            else if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchTo(3);

            if (Input.GetKeyDown(KeyCode.R)) TryCombo();

            // F 键：附近无可交互物时触发回响技能（P1-B）；有可交互物时
            // 交给 TideSong 的交互流程。同时排掉无目标时的陈旧 F 按压，
            // 防止之后走到宝箱前自动打开。
            if (Input.GetKeyDown(KeyCode.F) && _player.Nearest == null)
            {
                InputHub.ConsumeInteract();
                EchoManager.Instance?.TryUseEchoSkill();
            }
            else if (_player.Nearest == null)
            {
                InputHub.ConsumeInteract();
            }

            // 完美闪避：闪避启动瞬间，5 m 内有敌人正在蓄力冲撞。
            bool dodging = _player.Dodging;
            if (dodging && !_wasDodging) CheckPerfectDodge();
            _wasDodging = dodging;

            // 岚律加速计时
            if (_speedBuffT > 0f)
            {
                _speedBuffT -= Time.deltaTime;
                if (_speedBuffT <= 0f)
                {
                    _player.walkSpeed /= 1.35f;
                    _player.sprintSpeed /= 1.35f;
                }
            }
        }

        public void SwitchTo(int index)
        {
            if (index < 0 || index > 3) return;
            if (index == ActiveIndex) return;
            if (SwitchCooldownLeft > 0f) return;
            SwitchCooldownLeft = SquadData.SwitchCooldown;
            int outgoing = ActiveIndex;

            // 保存下场队员状态
            _states[outgoing].hp = _player.hp;
            _states[outgoing].energy = _player.BurstEnergy;

            // 退场技：共鸣值满时才触发（消耗 100 共鸣）
            bool resonanceFull = Resonance >= SquadData.ResonanceMax;
            if (resonanceFull)
            {
                Resonance = 0f;
                TriggerExitSkill(outgoing);
            }

            // 上场队员：换状态 + 换配色
            ActiveIndex = index;
            var def = SquadData.Roster[index];
            float effMax = EffMaxHp(index);
            _player.maxHP = effMax;
            _player.hp = Mathf.Clamp(_states[index].hp, 1f, effMax);
            _player.SetEnergy(_states[index].energy);
            _player.ApplyMemberVisual(def.clothColor);

            // 原来扮演该队员的队友改扮下场队员
            foreach (var c in _companions)
            {
                if (c != null && c.MemberIndex == index)
                {
                    c.SetMember(outgoing);
                    break;
                }
            }

            TriggerEntrySkill(index, resonanceFull);
        }

        private void TriggerEntrySkill(int index, bool withExit)
        {
            var def = SquadData.Roster[index];
            string text = StarAttributeText.EntrySkillName(def.attribute);
            switch (def.attribute)
            {
                case StarAttribute.Star:
                    // 引力聚怪：10 m 内敌人拉向玩家
                    foreach (var e in Object.FindObjectsOfType<EnemyAI>())
                    {
                        if (e == null || e.Dead) continue;
                        if (Vector3.Distance(e.transform.position, _player.transform.position) <= 10f)
                            e.PullToward(_player.transform.position, 22f);
                    }
                    FX.Shockwave(_player.transform.position, new Color(0.55f, 0.75f, 1f), 10f, 0.5f);
                    break;
                case StarAttribute.Blaze:
                    // 爆发：6 m 内 60 伤害
                    foreach (var e in Object.FindObjectsOfType<EnemyAI>())
                    {
                        if (e == null || e.Dead) continue;
                        if (Vector3.Distance(e.transform.position, _player.transform.position) <= 6f)
                            e.TakeDamage(60f, (_player.transform.position - e.transform.position).normalized * -6f);
                    }
                    FX.Burst(_player.transform.position + Vector3.up, new Color(1f, 0.55f, 0.25f), 60, 9f, 0.5f, 0.9f);
                    FX.Shockwave(_player.transform.position, new Color(1f, 0.6f, 0.3f), 8f, 0.55f);
                    break;
                case StarAttribute.Storm:
                    // 加速：移速 ×1.35，持续 6 秒
                    if (_speedBuffT <= 0f)
                    {
                        _player.walkSpeed *= 1.35f;
                        _player.sprintSpeed *= 1.35f;
                    }
                    _speedBuffT = 6f;
                    FX.Burst(_player.transform.position + Vector3.up, new Color(0.5f, 1f, 0.75f), 40, 7f, 0.4f, 0.8f);
                    break;
                case StarAttribute.Tide:
                    // 回能：SP +4，全队能量 +20，下场者回血 10
                    AddSP(4f);
                    for (int i = 0; i < 4; i++) _states[i].energy = Mathf.Min(100f, _states[i].energy + 20f);
                    _player.AddEnergy(20f);
                    _player.Heal(10f);
                    FX.Burst(_player.transform.position + Vector3.up, new Color(0.5f, 0.9f, 1f), 40, 7f, 0.4f, 0.8f);
                    break;
            }
            LastSkillText = def.name + " · " + text + (withExit ? "（退场技已触发）" : "");
            GameHUD.Instance?.Toast(def.name + " 登场 · " + text);
            Debug.Log("[Squad] 切换到 " + def.name + "（" + StarAttributeText.Name(def.attribute) + "）入场技：" + text);
        }

        private void TriggerExitSkill(int outgoing)
        {
            var def = SquadData.Roster[outgoing];
            // v1 简化：退场技 = 全队治疗 15 + SP +2（按属性区分文案）；
            // 属性差异化退场效果 Phase 2 再做（见 Phase1 实现小结）。
            for (int i = 0; i < 4; i++)
                _states[i].hp = Mathf.Min(EffMaxHp(i), _states[i].hp + 15f);
            _player.Heal(15f);
            AddSP(2f);
            GameHUD.Instance?.Toast(def.name + " 退场技 · 星律庇护（全队 +15 HP）");
            Debug.Log("[Squad] 退场技：" + def.name + "（共鸣值已消耗）");
        }

        // ---------------- 连携技 ----------------

        public void OnEnemyStaggered(EnemyAI e)
        {
            ComboReady = true;
            GameHUD.Instance?.Toast("失衡打满 · 连携技就绪 [R]");
            Debug.Log("[Squad] 失衡打满，连携技就绪");
        }

        public void TryCombo()
        {
            if (!ComboReady) return;
            ComboReady = false;
            int hit = 0;
            foreach (var e in Object.FindObjectsOfType<EnemyAI>())
            {
                if (e == null || e.Dead) continue;
                if (Vector3.Distance(e.transform.position, _player.transform.position) <= 8f)
                {
                    Vector3 push = (e.transform.position - _player.transform.position).normalized;
                    e.TakeDamage(120f, push * 10f);
                    hit++;
                }
            }
            FX.Shockwave(_player.transform.position, new Color(0.75f, 1f, 1f), 12f, 0.8f);
            FX.Burst(_player.transform.position + Vector3.up * 1.5f, new Color(0.7f, 1f, 1f), 80, 10f, 0.6f, 1.2f);
            _gm?.HitStop(0.12f);
            GameHUD.Instance?.Toast("连携技 · 星律交响（命中 " + hit + "）");
            Debug.Log("[Squad] 连携技释放，命中 " + hit + " 个敌人");
        }

        // ---------------- 完美闪避 ----------------

        private void CheckPerfectDodge()
        {
            if (_slowMo) return;
            foreach (var e in Object.FindObjectsOfType<EnemyAI>())
            {
                if (e == null || e.Dead || !e.IsWindingUp) continue;
                if (Vector3.Distance(e.transform.position, _player.transform.position) > 5f) continue;
                // 完美闪避反击：40 伤害 + 短暂慢动作
                e.TakeDamage(40f, (e.transform.position - _player.transform.position).normalized * 8f);
                FX.Burst(e.transform.position + Vector3.up, new Color(0.75f, 1f, 1f), 30, 7f, 0.4f, 0.8f);
                GameHUD.Instance?.Toast("完美闪避 · 反击");
                StartCoroutine(SlowMoRoutine());
                Debug.Log("[Squad] 完美闪避反击");
                return;
            }
        }

        private IEnumerator SlowMoRoutine()
        {
            _slowMo = true;
            Time.timeScale = 0.3f;
            yield return new WaitForSecondsRealtime(0.35f);
            if (_gm == null || !_gm.Paused) Time.timeScale = 1f;
            _slowMo = false;
        }

        // ---------------- 测试钩子（smoke 用） ----------------

        /// <summary>smoke：强制把最近敌人的失衡打满（验证连携技亮起）。</summary>
        public bool TestFillStagger()
        {
            EnemyAI best = null;
            float bestD = float.MaxValue;
            foreach (var e in Object.FindObjectsOfType<EnemyAI>())
            {
                if (e == null || e.Dead) continue;
                float d = Vector3.Distance(e.transform.position, _player.transform.position);
                if (d < bestD) { bestD = d; best = e; }
            }
            if (best == null) return false;
            best.DebugFillStagger();
            return true;
        }

        public int CompanionCount
        {
            get
            {
                int n = 0;
                foreach (var c in _companions) if (c != null) n++;
                return n;
            }
        }
    }
}
