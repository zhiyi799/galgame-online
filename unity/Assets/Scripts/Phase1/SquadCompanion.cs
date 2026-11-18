// SquadCompanion — 星律小队队友 AI（Phase 1 / P1-A）。
// 非操作队员：自动寻敌（复用 EnemyAI 的“找最近目标后靠近攻击”逻辑，
// 方向反过来：找最近的暗星之影并攻击），无敌人时跟随玩家。
// v1 限制：敌人只攻击玩家（TideSong EnemyAI 的目标就是 PlayerCharacter），
// 队友不承伤；队友 HP 只在被切换为操作角色时才变化（见 SquadManager）。
// 队友用 CharacterBuilder 的几何体占位（与玩家同款式、按角色配色）。
using UnityEngine;
using TideSong;

namespace AstralBloom
{
    public class SquadCompanion : MonoBehaviour
    {
        public int MemberIndex { get; private set; }

        private SquadMemberDef _def;
        private CharacterRig _rig;
        private float _attackCooldown;
        private float _targetRefresh;
        private EnemyAI _target;
        private float _animTime;
        private float _speedNow;
        private bool _moving;

        public void Setup(int memberIndex, Vector3 pos)
        {
            MemberIndex = memberIndex;
            _def = SquadData.Roster[memberIndex];
            transform.position = pos;
            _rig = CharacterBuilder.Build(transform, _def.clothColor);
            gameObject.name = "SquadMate_" + _def.name;
        }

        /// <summary>切换时改身份：换配色与攻击力（几何体占位重上色）。</summary>
        public void SetMember(int memberIndex)
        {
            MemberIndex = memberIndex;
            _def = SquadData.Roster[memberIndex];
            gameObject.name = "SquadMate_" + _def.name;
            if (_rig != null && _rig.body != null)
            {
                var r = _rig.body.GetComponent<Renderer>();
                if (r != null) r.material.color = _def.clothColor;
            }
        }

        public float Attack { get { return _def.attack; } }
        public string MemberName { get { return _def.name; } }

        private void Update()
        {
            var gm = TideSong.GameManager.Instance;
            if (gm == null || gm.Paused) return;
            var player = gm.Player;
            if (player == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _attackCooldown -= dt;
            _targetRefresh -= dt;
            if (_targetRefresh <= 0f)
            {
                _targetRefresh = 0.3f;
                _target = FindNearestEnemy();
            }
            if (_target != null && _target.Dead) _target = null;

            Vector3 pos = transform.position;
            _moving = false;

            if (_target != null)
            {
                Vector3 to = _target.transform.position - pos;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist <= 2.7f)
                {
                    // 面向敌人并攻击
                    if (to.sqrMagnitude > 0.001f)
                        transform.rotation = Quaternion.Slerp(transform.rotation,
                            Quaternion.LookRotation(to), 1f - Mathf.Exp(-10f * dt));
                    if (_attackCooldown <= 0f)
                    {
                        _attackCooldown = 1.1f;
                        AttackEnemy(_target);
                    }
                }
                else
                {
                    MoveToward(_target.transform.position, 5.5f, dt);
                }
            }
            else
            {
                // 跟随玩家：超出 4 m 才靠近
                Vector3 to = player.transform.position - pos;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist > 4f)
                {
                    MoveToward(player.transform.position, 6.5f, dt);
                }
                else if (dist > 0.5f)
                {
                    // 面向玩家朝向
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        player.transform.rotation, 1f - Mathf.Exp(-6f * dt));
                }
            }

            UpdateAnim(dt);
        }

        private void MoveToward(Vector3 dest, float speed, float dt)
        {
            Vector3 to = dest - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            Vector3 dir = to.normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir), 1f - Mathf.Exp(-10f * dt));
            Vector3 next = transform.position + dir * speed * dt;
            next = WorldData.Clamp(next);
            next.y = WorldData.HeightAt(next.x, next.z);
            transform.position = next;
            _speedNow = speed;
            _moving = true;
        }

        private void AttackEnemy(EnemyAI e)
        {
            Vector3 push = (e.transform.position - transform.position).normalized;
            e.TakeDamage(_def.attack, push * 4f);
            FX.SlashArc(transform, new Color(0.86f, 0.95f, 1f), 2.4f, 130f, 0.2f);
            SquadManager.Instance?.OnCompanionHit();
        }

        private EnemyAI FindNearestEnemy()
        {
            var all = Object.FindObjectsOfType<EnemyAI>();
            EnemyAI best = null;
            float bestD = float.MaxValue;
            foreach (var e in all)
            {
                if (e == null || e.Dead) continue;
                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d < bestD) { bestD = d; best = e; }
            }
            return best;
        }

        private void UpdateAnim(float dt)
        {
            if (_rig == null) return;
            _animTime += dt;
            float moveAmt = _moving ? Mathf.Clamp01(_speedNow / 6.2f) : 0f;
            float cycle = _animTime * (7f + _speedNow * 0.55f);
            float bob = Mathf.Sin(_animTime * 2.2f) * 0.015f;
            float walkBob = Mathf.Abs(Mathf.Sin(cycle)) * 0.06f * moveAmt;
            if (_rig.body != null)
                _rig.body.localPosition = new Vector3(0f, 1.06f + bob + walkBob, 0f);
            float legSwing = Mathf.Sin(cycle) * 32f * moveAmt;
            if (_rig.legL != null) _rig.legL.localRotation = Quaternion.Euler(legSwing, 0f, 0f);
            if (_rig.legR != null) _rig.legR.localRotation = Quaternion.Euler(-legSwing, 0f, 0f);
            float armSwing = -Mathf.Sin(cycle) * 26f * moveAmt;
            if (_rig.armL != null) _rig.armL.localRotation = Quaternion.Euler(armSwing, 0f, 8f);
            if (_rig.armR != null) _rig.armR.localRotation = Quaternion.Euler(-armSwing, 0f, -8f);
        }
    }
}
