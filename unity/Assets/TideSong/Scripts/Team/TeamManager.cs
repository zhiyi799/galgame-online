using System.Collections.Generic;
using UnityEngine;

namespace TideSong
{
    /// <summary>一位角色在队伍里的运行时状态。</summary>
    public class ResonatorState
    {
        public ResonatorDef def;
        public CharacterRig rig;
        public GameObject root;
        public float hp;
        public float maxHp;
        public float energy;
        public float skillCd;
        public bool unlocked = true;
    }

    /// <summary>
    /// 队伍与共鸣系统：多角色切换、协奏能量、变奏 / 延奏、共鸣解放。
    /// 所有角色共用同一个 PlayerCharacter（一套物理与输入），切换时只换外观和数值。
    /// </summary>
    public class TeamManager : MonoBehaviour
    {
        public static TeamManager Instance { get; private set; }

        public const float IntroCost = 20f;   // 变奏所需协奏能量
        public float concerto;
        public int activeIndex;

        readonly List<ResonatorState> roster = new List<ResonatorState>();
        PlayerCharacter player;
        float switchCd;
        bool windStance;

        public IReadOnlyList<ResonatorState> Roster { get { return roster; } }
        public ResonatorState Active { get { return (activeIndex >= 0 && activeIndex < roster.Count) ? roster[activeIndex] : null; } }
        public bool WindStance { get { return windStance; } }
        public float SwitchCooldown { get { return switchCd; } }
        public float Concerto { get { return concerto; } }

        // ---------------- 初始化 ----------------

        public void Initialize(PlayerCharacter p)
        {
            Instance = this;
            player = p;

            for (int i = 0; i < ResonatorLibrary.All.Length; i++)
            {
                var def = ResonatorLibrary.All[i];
                var root = new GameObject("Rig_" + def.id);
                root.transform.SetParent(p.transform, false);

                var rig = ResonatorVisuals.Build(root.transform, def);
                root.SetActive(false);

                var st = new ResonatorState
                {
                    def = def,
                    rig = rig,
                    root = root,
                    maxHp = def.maxHP,
                    hp = def.maxHP,
                    energy = 0f,
                    skillCd = 0f,
                    unlocked = (def.id != "yuan")
                };
                roster.Add(st);
            }

            activeIndex = 0;
            ApplyActive(0, true);
        }

        void ApplyActive(int index, bool restoreHP)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].root != null) roster[i].root.SetActive(i == index);
            }

            var st = roster[index];
            player.Def = st.def;
            player.SetRig(st.rig);

            // 无队伍存档时按角色定义回满
            if (restoreHP)
            {
                if (st.hp <= 0f) st.hp = st.def.maxHP;
            }

            float hpBonus = EchoSystem.Instance != null ? EchoSystem.Instance.HpBonus / 100f : 0f;
            st.maxHp = st.def.maxHP * (1f + hpBonus);

            player.maxHP = st.maxHp;
            player.hp = Mathf.Clamp(st.hp <= 0f ? st.maxHp : st.hp, 1f, st.maxHp);
            player.maxStamina = 100f + (EchoSystem.Instance != null ? EchoSystem.Instance.StaminaBonus : 0f);
            player.walkSpeed = st.def.walkSpeed;
            player.sprintSpeed = st.def.sprintSpeed;
        }

        void SaveActiveHP()
        {
            if (Active != null) Active.hp = player.hp;
        }

        // ---------------- 切换 ----------------

        public void CycleNext()
        {
            if (roster.Count == 0) return;
            for (int step = 1; step <= roster.Count; step++)
            {
                int idx = (activeIndex + step) % roster.Count;
                if (roster[idx].unlocked) { SwitchTo(idx); return; }
            }
        }

        public void SwitchTo(int index)        {
            if (index < 0 || index >= roster.Count) return;
            if (index == activeIndex) return;
            if (switchCd > 0f) return;
            if (!roster[index].unlocked)
            {
                GameHUD.Instance?.Toast("渊尚未共鸣 · 收集 " + ResonatorLibrary.YuanUnlockEchoes + " 件声骸解锁");
                return;
            }

            SaveActiveHP();

            bool doIntro = concerto >= IntroCost;
            if (doIntro) concerto -= IntroCost;

            var prev = Active;
            ResonanceCombat.Outro(this, prev);

            activeIndex = index;
            ApplyActive(index, false);

            if (doIntro) ResonanceCombat.Intro(this, roster[index]);
            else GameHUD.Instance?.Toast("切换 · " + roster[index].def.name);

            // 切换后短暂无敌，避免切人被秒
            player.invulnerable = true;
            player.Invoke("EndInvulnPublic", 0.35f);

            switchCd = 0.45f;
            Sfx.Play(SfxId.UI, player.transform.position);
        }

        // ---------------- 战斗事件 ----------------

        public void OnDamageDealt(float dmg)
        {
            var st = Active;
            if (st == null) return;
            st.energy = Mathf.Min(100f, st.energy + dmg * 0.30f);
            concerto = Mathf.Min(100f, concerto + dmg * 0.10f);
        }

        public void OnDamageTaken(float dmg)
        {
            var st = Active;
            if (st == null) return;
            st.energy = Mathf.Min(100f, st.energy + dmg * 0.45f);
            concerto = Mathf.Min(100f, concerto + dmg * 0.06f);

            // 岚的共鸣回路：受伤给全队回协奏
            if (st.def.id == "lan") concerto = Mathf.Min(100f, concerto + 8f);
        }

        public void OnEnemyKilled()
        {
            var st = Active;
            if (st != null) st.energy = Mathf.Min(100f, st.energy + 10f);
        }

        public void GrantWindStance() { windStance = true; }
        public void ConsumeWindStance() { windStance = false; }

        // ---------------- 技能 ----------------

        public void UseSkill()
        {
            var st = Active;
            if (st == null) return;
            if (st.skillCd > 0f) return;
            if (player.stamina < 20f) return;

            player.stamina -= 20f;
            st.skillCd = st.def.skillCooldown;
            ResonanceCombat.CastSkill(this, st);
        }

        public void UseBurst()
        {
            var st = Active;
            if (st == null) return;
            if (st.energy < st.def.burstCost)
            {
                GameHUD.Instance?.Toast("共鸣能量不足 · " + Mathf.RoundToInt(st.energy) + " / " + Mathf.RoundToInt(st.def.burstCost));
                return;
            }
            st.energy = 0f;
            ResonanceCombat.CastBurst(this, st);
        }

        public float ActiveSkillCooldown { get { var s = Active; return s != null ? s.skillCd : 0f; } }
        public float ActiveEnergy { get { var s = Active; return s != null ? s.energy : 0f; } }

        // ---------------- 每帧 ----------------

        void Update()
        {
            if (player == null) return;

            float dt = Time.deltaTime;
            if (switchCd > 0f) switchCd -= dt;

            if (GameManager.Instance != null && GameManager.Instance.Paused) return;

            // 协奏能量缓慢自然回复
            concerto = Mathf.Min(100f, concerto + 1.4f * dt);

            for (int i = 0; i < roster.Count; i++)
                if (roster[i].skillCd > 0f) roster[i].skillCd -= dt;

            // 渊的解锁条件
            var yuan = roster.Count > 3 ? roster[3] : null;
            if (yuan != null && !yuan.unlocked)
            {
                var echo = EchoSystem.Instance;
                if (echo != null && echo.OwnedCount >= ResonatorLibrary.YuanUnlockEchoes)
                {
                    yuan.unlocked = true;
                    GameHUD.Instance?.Toast("渊 · 深潮之影 已共鸣 · 按 4 切换");
                    FX.Burst(player.transform.position + Vector3.up, new Color(0.72f, 0.45f, 1f), 60, 8f, 0.5f, 1.1f);
                }
            }

            if (player.hp > 0f && Active != null) Active.hp = player.hp;

            // 数字键切人
            if (!InputHub.Mobile)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchTo(0);
                if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchTo(1);
                if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchTo(2);
                if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchTo(3);
            }
        }

        // ---------------- 存读档 ----------------

        public void Save()
        {
            if (Active != null) Active.hp = player != null ? player.hp : Active.hp;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < roster.Count; i++)
            {
                sb.Append(roster[i].hp.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                  .Append(roster[i].energy.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                  .Append(roster[i].unlocked ? 1 : 0).Append(';');
            }
            PlayerPrefs.SetString("TideSong.Team.v1", activeIndex + "#" +
                concerto.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "#" + sb.ToString());
            PlayerPrefs.Save();
        }

        public void Load()
        {
            string raw = PlayerPrefs.GetString("TideSong.Team.v1", "");
            if (string.IsNullOrEmpty(raw)) return;

            var parts = raw.Split('#');
            int idx;
            float cnc;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (parts.Length < 3 || !int.TryParse(parts[0], out idx) ||
                !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, inv, out cnc)) return;

            concerto = Mathf.Clamp(cnc, 0f, 100f);
            var entries = parts[2].Split(';');
            for (int i = 0; i < roster.Count && i < entries.Length; i++)
            {
                if (string.IsNullOrEmpty(entries[i])) continue;
                var f = entries[i].Split('|');
                if (f.Length < 3) continue;
                float hp, en;
                int un;
                if (float.TryParse(f[0], System.Globalization.NumberStyles.Float, inv, out hp)) roster[i].hp = Mathf.Clamp(hp, 1f, 99999f);
                if (float.TryParse(f[1], System.Globalization.NumberStyles.Float, inv, out en)) roster[i].energy = Mathf.Clamp(en, 0f, 100f);
                if (int.TryParse(f[2], out un)) roster[i].unlocked = un == 1;
            }

            if (idx >= 0 && idx < roster.Count && roster[idx].unlocked)
            {
                activeIndex = idx;
                ApplyActive(idx, false);
            }
        }
    }
}
