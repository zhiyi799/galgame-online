using UnityEngine;

namespace TideSong
{
    /// <summary>暗星之影：漂浮追踪、蓄力冲撞、受击硬直。</summary>
    public class EnemyAI : MonoBehaviour
    {
        public float maxHp = 70f;
        public float hp = 70f;
        public float speed = 3.4f;
        public float damage = 14f;
        public int reward = 4;
        public bool Dead { get; private set; }
        public float HpRatio { get { return Mathf.Clamp01(hp / Mathf.Max(1f, maxHp)); } }
        public Transform Visual { get { return visual; } }

        // Phase 1 失衡条 / 连携技（AstralBloom 使用）
        public static event System.Action<EnemyAI> OnEnemyDied;
        public static event System.Action<EnemyAI> OnEnemyStaggered;
        public float StaggerMax = 100f;
        public float StaggerValue;
        public float StaggerRatio { get { return Mathf.Clamp01(StaggerValue / StaggerMax); } }
        public bool Downed { get; private set; }
        public bool IsWindingUp { get { return windup > 0f; } }

        Transform visual;
        Renderer[] renderers;
        MaterialPropertyBlock mpb;
        Color baseColor = new Color(0.13f, 0.10f, 0.22f);

        float attackCd;
        float windup;
        float stagger;
        float flash;
        float hoverPhase;
        Vector3 knock;
        Vector3 pullVel;
        float downedT;
        float lifeTime;

        Transform ring;

        public static EnemyAI Spawn(Vector3 pos, float hpScale = 1f, float speedScale = 1f, Transform parent = null)
        {
            var go = new GameObject("Shadow");
            go.transform.SetParent(parent, false);
            pos.y = WorldData.HeightAt(pos.x, pos.z) + 1.7f;
            go.transform.position = pos;

            var col = go.AddComponent<CapsuleCollider>();
            col.height = 2.0f;
            col.radius = 0.7f;
            col.isTrigger = true;

            var ai = go.AddComponent<EnemyAI>();
            ai.maxHp = 70f * hpScale;
            ai.hp = ai.maxHp;
            ai.speed = 3.4f * speedScale;
            ai.damage = 14f * Mathf.Lerp(1f, 1.6f, hpScale - 1f);

            ai.visual = CharacterBuilder.BuildEnemy(go.transform, 1f);
            ai.hoverPhase = Random.value * 10f;
            ai.mpb = new MaterialPropertyBlock();
            ai.renderers = go.GetComponentsInChildren<Renderer>();
            foreach (var r in ai.renderers)
            {
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
                    r.sharedMaterial = new Material(r.sharedMaterial);
            }
            ai.ring = ai.visual.Find("Ring");
            FX.Burst(pos, new Color(0.25f, 0.41f, 0.85f), 18, 4f, 0.3f, 0.6f);
            return ai;
        }

        void Update()
        {
            if (Dead) return;
            if (GameManager.Instance != null && GameManager.Instance.Paused) return;
            float dt = Time.deltaTime;
            lifeTime += dt;
            attackCd -= dt;
            stagger -= dt;
            if (downedT > 0f)
            {
                downedT -= dt;
                if (downedT <= 0f) Downed = false;
            }
            flash = Mathf.Max(0f, flash - dt * 4f);

            var p = PlayerCharacter.Instance;
            if (p == null) return;

            Vector3 to = p.transform.position - transform.position;
            float dist = to.magnitude;
            to.y = 0f;
            if (to.sqrMagnitude > 0.001f) to.Normalize();

            // 击退衰减
            if (knock.sqrMagnitude > 0.01f)
            {
                transform.position += knock * dt;
                knock = Vector3.Lerp(knock, Vector3.zero, 1f - Mathf.Exp(-6f * dt));
            }

            // 星律入场技的聚怪拉力（平滑衰减）
            if (pullVel.sqrMagnitude > 0.01f)
            {
                transform.position += pullVel * dt;
                pullVel = Vector3.Lerp(pullVel, Vector3.zero, 1f - Mathf.Exp(-2.5f * dt));
            }

            if (stagger <= 0f && !Downed)
            {
                if (windup > 0f)
                {
                    windup -= dt;
                    transform.position += to * speed * 0.35f * dt;
                    if (visual != null)
                    {
                        float k = 1f - windup / 0.65f;
                        visual.localScale = Vector3.one * (1f + k * 0.22f);
                    }
                    if (windup <= 0f)
                    {
                        // 冲撞
                        Vector3 hitCenter = transform.position;
                        if (Vector3.Distance(hitCenter, p.transform.position) < 3.4f)
                        {
                            p.TakeDamage(damage, transform.position);
                            FX.Burst(p.transform.position + Vector3.up, new Color(1f, 0.35f, 0.45f), 22, 5f, 0.35f, 0.6f);
                        }
                        FX.Burst(transform.position, new Color(0.25f, 0.41f, 0.85f), 20, 6f, 0.35f, 0.6f);
                        Sfx.Play(SfxId.Hit, transform.position);
                        knock = to * 14f;
                        attackCd = 1.9f;
                        if (visual != null) visual.localScale = Vector3.one;
                    }
                }
                else if (dist > 2.6f)
                {
                    transform.position += to * speed * dt;
                    if (visual != null)
                    {
                        float s = 1f + Mathf.Sin(Time.time * 3f) * 0.04f;
                        visual.localScale = Vector3.one * s;
                    }
                }
                else if (attackCd <= 0f)
                {
                    windup = 0.65f;
                    Sfx.Play(SfxId.Swing, transform.position);
                }
            }

            // 朝向玩家
            if (to.sqrMagnitude > 0.001f)
            {
                var look = Quaternion.LookRotation(to);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-8f * dt));
            }

            // 贴地漂浮
            float ground = WorldData.HeightAt(transform.position.x, transform.position.z);
            float targetY = Mathf.Max(ground, WorldData.WaterLevel) + 1.7f + Mathf.Sin(Time.time * 1.6f + hoverPhase) * 0.25f;
            var pos = transform.position;
            pos.y = Mathf.Lerp(pos.y, targetY, 1f - Mathf.Exp(-6f * dt));
            transform.position = pos;

            if (ring != null) ring.Rotate(Vector3.up, 90f * dt, Space.Self);

            // 受击闪白
            if (renderers != null && mpb != null)
            {
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor("_Color", Color.Lerp(baseColor, Color.white, flash));
                    mpb.SetColor("_EmissionColor", Color.Lerp(Color.black, Color.white, flash * 0.8f));
                    r.SetPropertyBlock(mpb);
                }
            }

            // 掉进水里就慢慢往回飘
            if (transform.position.y < WorldData.WaterLevel)
                transform.position += Vector3.up * dt * 0.5f;

            if (lifeTime > 240f && Vector3.Distance(transform.position, p.transform.position) > 90f)
                Destroy(gameObject);
        }

        public void Stagger(float t) { stagger = Mathf.Max(stagger, t); }

        /// <summary>星律入场技：把敌人拉向目标点（平滑拉力，speed 22 约拉 9 m）。</summary>
        public void PullToward(Vector3 target, float speed)
        {
            if (Dead) return;
            Vector3 to = target - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            pullVel = to.normalized * speed;
        }

        /// <summary>smoke 测试用：强制失衡打满。</summary>
        public void DebugFillStagger()
        {
            if (Dead) return;
            StaggerValue = StaggerMax;
            Downed = true;
            downedT = 3f;
            stagger = 3f;
            OnEnemyStaggered?.Invoke(this);
        }

        public void TakeDamage(float dmg, Vector3 knockback)
        {
            if (Dead) return;
            if (Downed) dmg *= 1.5f;
            hp -= dmg;
            flash = 1f;
            knock += knockback;
            // 失衡积累：伤害 ×0.5，打满后倒地 3 秒（期间受伤 ×1.5）
            if (!Downed)
            {
                StaggerValue += dmg * 0.5f;
                if (StaggerValue >= StaggerMax)
                {
                    StaggerValue = StaggerMax;
                    Downed = true;
                    downedT = 3f;
                    stagger = 3f;
                    OnEnemyStaggered?.Invoke(this);
                }
            }
            Sfx.Play(SfxId.Hit, transform.position);
            FX.Burst(transform.position, new Color(1f, 0.55f, 0.5f), 12, 4f, 0.28f, 0.45f);
            GameHUD.Instance?.PopDamage(transform.position + Vector3.up * 1.6f, Mathf.RoundToInt(dmg));
            if (hp <= 0f) Die();
        }

        void Die()
        {
            Dead = true;
            OnEnemyDied?.Invoke(this);
            var p = PlayerCharacter.Instance;
            if (p != null)
            {
                p.CrystalCount += reward;
                p.AddExp(18);
                p.AddEnergy(18f);
                GameHUD.Instance?.PopText(transform.position + Vector3.up * 1.4f, "结晶 +" + reward, new Color(0.55f, 0.95f, 1f));
            }
            Sfx.Play(SfxId.Heavy, transform.position);
            FX.Burst(transform.position, new Color(0.25f, 0.41f, 0.85f), 46, 8f, 0.4f, 1.1f);
            FX.Shockwave(transform.position, new Color(0.35f, 0.55f, 1f), 5f, 0.5f);
            Destroy(gameObject, 0.1f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 2.6f);
        }
    }
}
