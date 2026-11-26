using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TideSong
{
    /// <summary>
    /// 所有伤害的统一入口：暴击、属性加成、声骸加成、能量积累都在这里结算，
    /// 避免每个技能各写一套数值。
    /// </summary>
    public static class CombatUtil
    {
        public const float BaseCritRate = 0.05f;
        public const float BaseCritDmg = 1.50f;

        public static float CritRate
        {
            get
            {
                var echo = EchoSystem.Instance;
                return Mathf.Clamp01(BaseCritRate + (echo != null ? echo.CritRate / 100f : 0f));
            }
        }

        public static float CritDmg
        {
            get
            {
                var echo = EchoSystem.Instance;
                return BaseCritDmg + (echo != null ? echo.CritDmg / 100f : 0f);
            }
        }

        public static float AttackMultiplier
        {
            get
            {
                var echo = EchoSystem.Instance;
                float m = 1f + (echo != null ? echo.AtkBonus / 100f : 0f);
                var p = PlayerCharacter.Instance;
                if (p != null && p.Def != null) m *= p.Def.atkMul;
                if (p != null && p.burstActive) m *= 1.25f;
                if (p != null) m *= 1f + p.echoAtkBuff;
                return m;
            }
        }

        /// <summary>对敌人造成一次伤害，返回最终伤害值。</summary>
        public static float DealDamage(EnemyAI e, float baseDmg, Vector3 from, ElementType element, bool canCrit = true, bool showNumber = true)
        {
            if (e == null || e.Dead) return 0f;

            float dmg = baseDmg * AttackMultiplier;

            var echo = EchoSystem.Instance;
            if (echo != null)
            {
                dmg *= 1f + echo.ElementBonus(element);
                // 凝霜之律 5 件：对减速目标额外 +20%
                if (echo.SetCount(EchoSet.FrostLaw) >= 5 && e.slow < 0.99f) dmg *= 1.2f;
                // 潮汐回响 5 件：连击增伤
                dmg *= 1f + echo.ComboDamageBonus;
            }

            bool crit = canCrit && Random.value < CritRate;
            if (crit) dmg *= CritDmg;

            if (e.vuln > 0.01f) dmg *= 1f + e.vuln;

            Vector3 dir = e.transform.position - from;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
            e.TakeDamage(dmg, dir.normalized * 3.5f);

            if (showNumber && GameHUD.Instance != null)
            {
                if (crit) GameHUD.Instance.PopText(e.transform.position + Vector3.up * 2.1f, Mathf.RoundToInt(dmg) + " 暴击", new Color(1f, 0.78f, 0.3f));
            }

            if (TeamManager.Instance != null) TeamManager.Instance.OnDamageDealt(dmg);
            return dmg;
        }

        /// <summary>范围伤害。</summary>
        public static int DamageArea(Vector3 center, float radius, float baseDmg, ElementType element, bool canCrit = true)
        {
            var hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
            var seen = new HashSet<EnemyAI>();
            int count = 0;
            foreach (var h in hits)
            {
                var e = h.GetComponentInParent<EnemyAI>();
                if (e == null || e.Dead || seen.Contains(e)) continue;
                seen.Add(e);
                DealDamage(e, baseDmg, center, element, canCrit);
                count++;
            }
            return count;
        }

        public static Color ElementColor(ElementType el)
        {
            switch (el)
            {
                case ElementType.Aero: return new Color(0.45f, 0.95f, 0.90f);
                case ElementType.Glacio: return new Color(0.62f, 0.86f, 1f);
                case ElementType.Spectro: return new Color(1f, 0.92f, 0.62f);
                case ElementType.Havoc: return new Color(0.72f, 0.45f, 1f);
                case ElementType.Fusion: return new Color(1f, 0.55f, 0.35f);
                case ElementType.Electro: return new Color(0.72f, 0.55f, 1f);
            }
            return Color.cyan;
        }
    }

    /// <summary>共鸣弹丸：直线飞行、命中判定、可穿透。</summary>
    public class ResonanceProjectile : MonoBehaviour
    {
        public Vector3 direction = Vector3.forward;
        public float speed = 36f;
        public float damage = 20f;
        public float life = 1.8f;
        public float radius = 0.55f;
        public ElementType element = ElementType.Aero;
        public bool pierce;
        public bool spin;
        public bool healOwner;

        readonly HashSet<EnemyAI> hitOnce = new HashSet<EnemyAI>();
        float t;
        Vector3 origin;

        public static ResonanceProjectile Spawn(Vector3 pos, Vector3 dir, float dmg, ElementType el, float speed = 36f, float radius = 0.55f, bool pierce = false)
        {
            var go = new GameObject("ResonanceBolt");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 0.001f ? dir : Vector3.forward);

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.25f;

            var mat = MaterialLibrary.Make(CombatUtil.ElementColor(el), 0.1f, 0.8f);
            MaterialLibrary.ApplyGlow(mat, CombatUtil.ElementColor(el) * 3f);
            var body = MaterialLibrary.Prim(PrimitiveType.Sphere, go.transform, mat, Vector3.zero, new Vector3(0.32f, 0.32f, 0.62f));
            body.name = "Bolt";
            body.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var tail = MaterialLibrary.Prim(PrimitiveType.Capsule, go.transform, mat, new Vector3(0f, 0f, -0.5f), new Vector3(0.10f, 0.34f, 0.10f), Quaternion.Euler(90f, 0f, 0f));
            tail.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var p = go.AddComponent<ResonanceProjectile>();
            p.direction = dir.normalized;
            p.damage = dmg;
            p.element = el;
            p.speed = speed;
            p.radius = radius;
            p.pierce = pierce;
            p.origin = pos;
            FX.Burst(pos, CombatUtil.ElementColor(el), 8, 3f, 0.18f, 0.25f);
            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            if (t > life) { Destroy(gameObject); return; }

            transform.position += direction * speed * dt;
            if (spin) transform.Rotate(Vector3.forward, 720f * dt, Space.Self);

            var hits = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Collide);
            foreach (var h in hits)
            {
                var e = h.GetComponentInParent<EnemyAI>();
                if (e == null || e.Dead || hitOnce.Contains(e)) continue;
                hitOnce.Add(e);
                float dealt = CombatUtil.DealDamage(e, damage, transform.position, element);
                if (healOwner && PlayerCharacter.Instance != null) PlayerCharacter.Instance.Heal(dealt * 0.12f);
                FX.Burst(transform.position, CombatUtil.ElementColor(element), 12, 3f, 0.2f, 0.35f);
                if (!pierce) { Destroy(gameObject); return; }
            }
        }
    }

    /// <summary>共鸣领域：牵引 / 减速 / 治疗 / 易伤 / 持续伤害。</summary>
    public class ResonanceField : MonoBehaviour
    {
        public float radius = 6f;
        public float duration = 5f;
        public float tickInterval = 0.5f;
        public float damagePerTick;
        public ElementType element = ElementType.Aero;
        public bool pull;
        public bool slow;
        public bool heal;
        public bool vuln;
        public bool stun;
        public bool followPlayer;
        public float healPerTick = 4f;
        public float slowFactor = 0.55f;
        public float vulnAmount = 0.15f;

        float t;
        float tickT;
        Transform disc;

        public static ResonanceField Spawn(Vector3 pos, Color color, float radius, float duration)
        {
            var go = new GameObject("ResonanceField");
            go.transform.position = pos;

            var mat = MaterialLibrary.MakeTransparent(new Color(color.r, color.g, color.b, 0.22f), 0f, 0.6f);
            var d = MaterialLibrary.Prim(PrimitiveType.Cylinder, go.transform, mat, Vector3.zero, new Vector3(radius * 2f, 0.01f, radius * 2f));
            d.name = "Disc";
            d.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var rimMat = MaterialLibrary.MakeTransparent(new Color(color.r, color.g, color.b, 0.55f), 0.1f, 0.7f);
            var rim = MaterialLibrary.Prim(PrimitiveType.Cylinder, go.transform, rimMat, new Vector3(0f, 0.02f, 0f), new Vector3(radius * 2.02f, 0.004f, radius * 2.02f));
            rim.name = "Rim";
            rim.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var f = go.AddComponent<ResonanceField>();
            f.radius = radius;
            f.duration = duration;
            f.disc = go.transform;
            f.element = ElementType.Aero;
            return f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            if (t >= duration) { Destroy(gameObject); return; }

            if (followPlayer && PlayerCharacter.Instance != null)
            {
                var p = PlayerCharacter.Instance.transform.position;
                p.y = WorldData.HeightAt(p.x, p.z) + 0.06f;
                transform.position = Vector3.Lerp(transform.position, p, 1f - Mathf.Exp(-8f * dt));
            }

            if (disc != null) disc.Rotate(Vector3.up, 22f * dt, Space.World);

            tickT -= dt;
            if (tickT > 0f) return;
            tickT = tickInterval;

            var hits = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Collide);
            var seen = new HashSet<EnemyAI>();
            foreach (var h in hits)
            {
                var e = h.GetComponentInParent<EnemyAI>();
                if (e == null || e.Dead || seen.Contains(e)) continue;
                seen.Add(e);

                if (damagePerTick > 0f) CombatUtil.DealDamage(e, damagePerTick, transform.position, element);
                if (slow) e.ApplySlow(slowFactor, 1.2f);
                if (vuln) e.vuln = Mathf.Max(e.vuln, vulnAmount);
                if (stun) e.Stagger(0.55f);
                if (pull)
                {
                    Vector3 to = transform.position - e.transform.position;
                    to.y = 0f;
                    if (to.sqrMagnitude > 0.01f)
                        e.transform.position += to.normalized * 2.4f * tickInterval;
                }
            }

            if (heal && PlayerCharacter.Instance != null) PlayerCharacter.Instance.Heal(healPerTick);
        }
    }

    /// <summary>技能表现与数值的具体实现，按角色分派。</summary>
    public static class ResonanceCombat
    {
        // ---------------- 变奏（登场） ----------------

        public static void Intro(TeamManager tm, ResonatorState st)
        {
            var p = PlayerCharacter.Instance;
            if (p == null) return;
            var def = st.def;
            Vector3 at = p.transform.position;
            Color c = CombatUtil.ElementColor(def.element);

            FX.Shockwave(at, c, 5.5f, 0.45f);
            FX.Burst(at + Vector3.up, c, 40, 7f, 0.35f, 0.7f);
            Sfx.Play(SfxId.Burst, at);

            switch (def.id)
            {
                case "shio":
                    for (int i = 0; i < 3; i++)
                    {
                        var dir = Quaternion.Euler(0f, -18f + i * 18f, 0f) * p.transform.forward;
                        var pr = ResonanceProjectile.Spawn(at + Vector3.up * 1.1f + p.transform.forward * 1.2f, dir, 26f, def.element, 30f, 1.0f, true);
                        pr.transform.localScale = Vector3.one * 1.35f;
                    }
                    CombatUtil.DamageArea(at + p.transform.forward * 2f, 4.5f, 22f, def.element);
                    break;

                case "shuang":
                    CombatUtil.DamageArea(at, 6f, 34f, def.element);
                    foreach (var e in Object.FindObjectsOfType<EnemyAI>())
                        if (!e.Dead && Vector3.Distance(e.transform.position, at) < 6f) e.ApplySlow(0.5f, 2.5f);
                    break;

                case "lan":
                    p.AddShield(p.maxHP * 0.12f);
                    p.Heal(p.maxHP * 0.10f);
                    CombatUtil.DamageArea(at, 5.5f, 20f, def.element);
                    break;

                case "yuan":
                {
                    Vector3 dash = at + p.transform.forward * 4f;
                    dash.y = WorldData.HeightAt(dash.x, dash.z) + 0.3f;
                    FX.Burst(at, c, 24, 5f, 0.3f, 0.5f);
                    p.Teleport(dash);
                    CombatUtil.DamageArea(dash, 4.5f, 40f, def.element);
                    break;
                }
            }

            GameHUD.Instance?.Toast("变奏 · " + def.introName.Replace("变奏 · ", ""));
        }

        // ---------------- 延奏（离场） ----------------

        public static void Outro(TeamManager tm, ResonatorState st)
        {
            var p = PlayerCharacter.Instance;
            if (p == null) return;
            var def = st.def;
            Vector3 at = p.transform.position;
            Color c = CombatUtil.ElementColor(def.element);

            var f = ResonanceField.Spawn(at, c, 5.5f, 5f);
            f.element = def.element;

            switch (def.id)
            {
                case "shio":
                    f.pull = true;
                    f.damagePerTick = 8f;
                    break;
                case "shuang":
                    f.slow = true;
                    f.slowFactor = 0.6f;
                    f.damagePerTick = 5f;
                    break;
                case "lan":
                    f.heal = true;
                    f.healPerTick = 5f;
                    break;
                case "yuan":
                    f.vuln = true;
                    f.vulnAmount = 0.15f;
                    f.damagePerTick = 4f;
                    break;
            }
            FX.Burst(at, c, 26, 5f, 0.3f, 0.6f);
        }

        // ---------------- 共鸣技能 E ----------------

        public static void CastSkill(TeamManager tm, ResonatorState st)
        {
            var p = PlayerCharacter.Instance;
            if (p == null) return;
            var def = st.def;
            Vector3 at = p.transform.position;
            Color c = CombatUtil.ElementColor(def.element);

            switch (def.id)
            {
                case "shio":
                    tm.StartCoroutine(ShioSkill(p, def));
                    break;

                case "shuang":
                {
                    Vector3 fwd = p.transform.forward;
                    for (int i = -2; i <= 2; i++)
                    {
                        var dir = Quaternion.Euler(0f, i * 11f, 0f) * fwd;
                        ResonanceProjectile.Spawn(at + Vector3.up * 1.1f + fwd * 0.9f, dir, 20f, def.element, 40f, 0.6f, false);
                    }
                    FX.Burst(at + Vector3.up + fwd * 1.2f, c, 26, 6f, 0.3f, 0.5f);
                    Sfx.Play(SfxId.Skill, at);
                    break;
                }

                case "lan":
                    p.AddShield(p.maxHP * 0.22f);
                    p.Heal(p.maxHP * 0.12f);
                    FX.Shockwave(at, c, 6f, 0.6f);
                    FX.Burst(at + Vector3.up, c, 46, 6f, 0.4f, 0.9f);
                    GameHUD.Instance?.Toast("圣辉壁垒 · 护盾 " + Mathf.RoundToInt(p.shield));
                    Sfx.Play(SfxId.Skill, at);
                    break;

                case "yuan":
                {
                    Vector3 fwd = p.transform.forward;
                    for (int i = -1; i <= 1; i++)
                    {
                        var dir = Quaternion.Euler(0f, i * 22f, 0f) * fwd;
                        var pr = ResonanceProjectile.Spawn(at + Vector3.up * 1.1f + fwd * 0.8f, dir, 30f, def.element, 30f, 1.0f, true);
                        pr.healOwner = true;
                        pr.spin = true;
                    }
                    FX.SlashArc(p.transform, c, 3.6f, 160f, 0.25f);
                    Sfx.Play(SfxId.Skill, at);
                    break;
                }
            }
        }

        static IEnumerator ShioSkill(PlayerCharacter p, ResonatorDef def)
        {
            Color c = CombatUtil.ElementColor(def.element);
            Vector3 dir = p.transform.forward;
            float boost = (TeamManager.Instance != null && TeamManager.Instance.WindStance) ? 1.6f : 1f;

            FX.Shockwave(p.transform.position, c, 4.5f, 0.3f);
            Sfx.Play(SfxId.Swing, p.transform.position);

            // 小突进
            p.DashForward(4.2f);

            for (int i = 0; i < 2; i++)
            {
                yield return new WaitForSeconds(0.14f);
                Vector3 center = p.transform.position;
                CombatUtil.DamageArea(center, 4.6f, 34f * boost, def.element);
                FX.Shockwave(center, c, 5.2f, 0.28f);
                FX.Burst(center + Vector3.up, c, 30, 7f, 0.3f, 0.5f);
                Sfx.Play(SfxId.Heavy, center);
            }

            if (TeamManager.Instance != null) TeamManager.Instance.ConsumeWindStance();
        }

        // ---------------- 共鸣解放 Q ----------------

        public static void CastBurst(TeamManager tm, ResonatorState st)
        {
            var p = PlayerCharacter.Instance;
            if (p == null) return;
            var def = st.def;
            Vector3 at = p.transform.position;
            Color c = CombatUtil.ElementColor(def.element);

            FX.Shockwave(at, c, 9f, 0.8f);
            FX.Burst(at + Vector3.up, c, 90, 10f, 0.55f, 1.2f);
            Sfx.Play(SfxId.Burst, at);
            CameraRig.Instance?.Shake(0.6f);

            switch (def.id)
            {
                case "shio":
                    tm.StartCoroutine(ShioBurst(p, def, tm));
                    break;

                case "shuang":
                {
                    var f = ResonanceField.Spawn(at, c, 8.5f, 10f);
                    f.element = def.element;
                    f.slow = true;
                    f.slowFactor = 0.45f;
                    f.damagePerTick = 26f;
                    f.followPlayer = true;
                    tm.StartCoroutine(FieldPulse(f, c));
                    GameHUD.Instance?.Toast("共鸣解放 · " + def.burstName.Replace("共鸣解放 · ", ""));
                    break;
                }

                case "lan":
                {
                    var f = ResonanceField.Spawn(at, c, 8f, 8f);
                    f.element = def.element;
                    f.stun = true;
                    f.damagePerTick = 14f;
                    f.followPlayer = true;
                    p.shieldDamageReduction = 0.4f;
                    p.damageReductionTime = 8f;
                    p.AddShield(p.maxHP * 0.30f);
                    tm.StartCoroutine(FieldPulse(f, c));
                    GameHUD.Instance?.Toast("共鸣解放 · " + def.burstName.Replace("共鸣解放 · ", ""));
                    break;
                }

                case "yuan":
                {
                    float cost = p.maxHP * 0.18f;
                    p.hp = Mathf.Max(1f, p.hp - cost);
                    CombatUtil.DamageArea(at, 9f, 120f, def.element);
                    foreach (var e in Object.FindObjectsOfType<EnemyAI>())
                    {
                        if (e.Dead) continue;
                        if (e.HpRatio < 0.3f) CombatUtil.DealDamage(e, 90f, at, def.element);
                    }
                    tm.StartCoroutine(HavocBlast(at, def));
                    GameHUD.Instance?.Toast("共鸣解放 · " + def.burstName.Replace("共鸣解放 · ", ""));
                    break;
                }
            }
        }

        static IEnumerator FieldPulse(ResonanceField f, Color c)
        {
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSeconds(1.4f);
                if (f == null) yield break;
                FX.Burst(f.transform.position + Vector3.up * 0.5f, c, 16, 4f, 0.25f, 0.4f);
            }
        }

        static IEnumerator ShioBurst(PlayerCharacter p, ResonatorDef def, TeamManager tm)
        {
            Color c = CombatUtil.ElementColor(def.element);
            var f = ResonanceField.Spawn(p.transform.position, c, 7.5f, 9f);
            f.element = def.element;
            f.pull = true;
            f.damagePerTick = 16f;
            f.followPlayer = true;

            p.burstActive = true;
            p.burstTime = 9f;
            GameHUD.Instance?.Toast("共鸣解放 · 风暴回旋");

            for (int i = 0; i < 9; i++)
            {
                yield return new WaitForSeconds(1f);
                if (p == null) yield break;
                if (i % 2 == 0) FX.Burst(p.transform.position + Vector3.up, c, 20, 5f, 0.3f, 0.5f);
            }
        }

        static IEnumerator HavocBlast(Vector3 at, ResonatorDef def)
        {
            Color c = CombatUtil.ElementColor(def.element);
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSeconds(0.12f);
                CombatUtil.DamageArea(at, 9f, 36f, def.element);
                FX.Burst(at + Vector3.up * (1f + i * 0.6f), c, 40, 9f, 0.4f, 0.6f);
            }
        }
    }
}
