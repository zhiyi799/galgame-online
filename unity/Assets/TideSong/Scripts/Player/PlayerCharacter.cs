using System.Collections.Generic;
using UnityEngine;

namespace TideSong
{
    /// <summary>触屏 / 键盘输入的汇合点，UI 层写入，玩家读取。</summary>
    public static class InputHub
    {
        public static Vector2 Move;
        public static bool Mobile;
        public static bool JumpHeld;
        public static bool SprintHeld;
        static bool _jump, _attack, _dodge, _skill, _burst, _interact;

        public static void PressJump() { _jump = true; }
        public static void PressAttack() { _attack = true; }
        public static void PressDodge() { _dodge = true; }
        public static void PressSkill() { _skill = true; }
        public static void PressBurst() { _burst = true; }
        public static void PressInteract() { _interact = true; }

        public static bool ConsumeJump() { var v = _jump; _jump = false; return v; }
        public static bool ConsumeAttack() { var v = _attack; _attack = false; return v; }
        public static bool ConsumeDodge() { var v = _dodge; _dodge = false; return v; }
        public static bool ConsumeSkill() { var v = _skill; _skill = false; return v; }
        public static bool ConsumeBurst() { var v = _burst; _burst = false; return v; }
        public static bool ConsumeInteract() { var v = _interact; _interact = false; return v; }

        public static void ClearAll()
        {
            Move = Vector2.zero;
            _jump = _attack = _dodge = _skill = _burst = _interact = false;
            JumpHeld = SprintHeld = false;
        }
    }

    /// <summary>玩家：移动 / 跳跃 / 滑翔 / 三段连击 / 闪避 / 星律技能 / 星鸣爆发。</summary>
    public class PlayerCharacter : MonoBehaviour
    {
        public static PlayerCharacter Instance { get; private set; }

        // Phase 1 星律小队钩子（AstralBloom 使用）
        public static event System.Action<int> OnMeleeHit;
        public static event System.Action OnSkillUsed;
        public static event System.Action OnBurstUsed;
        public float DamageMultiplier = 1f;   // 回响攻击加成（P1-B）
        public float DamageReduction;         // 回响减伤（P1-B，0–0.30）

        [Header("数值")]
        public float maxHP = 140f;
        public float hp = 140f;
        public float maxStamina = 100f;
        public float stamina = 100f;
        public float walkSpeed = 6.2f;
        public float sprintSpeed = 10.8f;
        public float glideFallSpeed = 2.4f;

        [Header("状态")]
        public bool invulnerable;
        public bool burstActive;
        public float burstTime;
        public int comboCount;
        public float comboTimer;

        public CharacterRig Rig { get; private set; }
        public CharacterController CC { get; private set; }
        public int Level = 1;
        public int Exp;
        public int CrystalCount;
        public int ChestOpened;
        public int ShrinesLit;

        // 内部
        Vector3 velocity;
        float yaw;
        float animTime;
        float attackT;
        float attackDur;
        int attackIndex;
        bool attackDidHit;
        float dodgeT;
        float dodgeCooldown;
        float skillCooldown;
        float burstEnergy;
        public float BurstEnergy { get { return burstEnergy; } }
        public float SkillCooldown { get { return skillCooldown; } }
        public float DodgeCooldown { get { return dodgeCooldown; } }
        public bool Attacking { get { return attackT > 0f; } }
        public bool Dodging { get { return dodgeT > 0f; } }
        public bool Gliding { get; private set; }
        public bool InWater { get; private set; }
        public float SpeedNow { get; private set; }
        public string HitState { get; private set; }

        float hurtFlashT;
        float lastGroundY;

        // ALS 手感（见 Assets/Scripts/Core/ALSLocomotion.cs）
        Vector3 _hVel;          // 水平速度（加速度/摩擦模型，不再瞬切）
        Vector3 _prevPos;       // 上一帧位置（算真实位移速度）
        float _mantleTimer;     // 攀爬探测冷却
        bool _mantling;         // 攀爬进行中
        float _mantleT, _mantleDur, _mantleP;
        Vector3 _mantleFrom, _mantleTo;
        float _rollT;           // 落地翻滚剩余时间
        Vector3 _rollDir;       // 翻滚方向

        void Awake()
        {
            Instance = this;
            CC = gameObject.GetComponent<CharacterController>();
            if (CC == null) CC = gameObject.AddComponent<CharacterController>();
            CC.height = 1.7f;
            CC.radius = 0.33f;
            CC.center = new Vector3(0f, 0.9f, 0f);
            CC.slopeLimit = 55f;
            CC.stepOffset = 0.45f;
            CC.skinWidth = 0.03f;
            _prevPos = transform.position;
        }

        public void SetupVisual()
        {
            Rig = CharacterBuilder.Build(transform, new Color(0.14f, 0.30f, 0.67f));
        }

        public void Teleport(Vector3 worldPos)
        {
            CC.enabled = false;
            transform.position = worldPos;
            CC.enabled = true;
            velocity = Vector3.zero;
            _hVel = Vector3.zero;
            _prevPos = worldPos;
            FX.Burst(worldPos + Vector3.up * 1f, new Color(0.4f, 0.9f, 1f), 30, 6f, 0.4f, 0.7f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (GameManager.Instance != null && GameManager.Instance.Paused) { UpdateAnimOnly(dt); return; }

            ReadInput();
            UpdateTimers(dt);
            UpdateMove(dt);
            UpdateAnim(dt);
            UpdateInteractPrompt();
        }

        void ReadInput()
        {
            Vector2 m = InputHub.Move;
            if (!InputHub.Mobile)
            {
                float x = 0f, y = 0f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
                m = new Vector2(x, y);
                if (m.sqrMagnitude > 1f) m.Normalize();

                InputHub.SprintHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                InputHub.JumpHeld = Input.GetKey(KeyCode.Space);

                if (Input.GetKeyDown(KeyCode.Space)) InputHub.PressJump();
                if (Input.GetMouseButtonDown(0)) InputHub.PressAttack();
                if (Input.GetMouseButtonDown(1)) InputHub.PressDodge();
                if (Input.GetKeyDown(KeyCode.E)) InputHub.PressSkill();
                if (Input.GetKeyDown(KeyCode.Q)) InputHub.PressBurst();
                if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Return)) InputHub.PressInteract();
            }
            moveInput = m;
        }

        Vector2 moveInput;

        void UpdateTimers(float dt)
        {
            if (stamina < maxStamina) stamina = Mathf.Min(maxStamina, stamina + 14f * dt);
            if (dodgeCooldown > 0f) dodgeCooldown -= dt;
            if (skillCooldown > 0f) skillCooldown -= dt;
            if (comboTimer > 0f) { comboTimer -= dt; if (comboTimer <= 0f) comboCount = 0; }
            if (hurtFlashT > 0f) hurtFlashT -= dt * 3f;
            if (burstActive)
            {
                burstTime -= dt;
                if (burstTime <= 0f) { burstActive = false; }
            }

            // 攻击推进
            if (attackT > 0f)
            {
                attackT -= dt;
                float progress = 1f - Mathf.Clamp01(attackT / Mathf.Max(0.01f, attackDur));
                if (!attackDidHit && progress > 0.35f)
                {
                    attackDidHit = true;
                    DoMeleeHit(attackIndex);
                }
                if (attackT <= 0f) { attackT = 0f; }
            }

            if (dodgeT > 0f) dodgeT -= dt;
        }

        void UpdateMove(float dt)
        {
            // 世界边界
            Vector3 p = transform.position;
            p = WorldData.Clamp(p);
            if (p != transform.position)
            {
                CC.enabled = false; transform.position = p; CC.enabled = true;
            }

            // ALS 攀爬进行中：按时间插值抬升（CC 关闭，直接改位置）
            if (_mantling)
            {
                _mantleT += dt;
                float mp = Mathf.Clamp01(_mantleT / _mantleDur);
                _mantleP = mp;
                float s = mp * mp * (3f - 2f * mp);
                CC.enabled = false;
                transform.position = Vector3.Lerp(_mantleFrom, _mantleTo, s);
                CC.enabled = true;
                if (mp >= 1f)
                {
                    _mantling = false;
                    _mantleP = 0f;
                    velocity.y = 0f;
                    _hVel = Vector3.zero;
                }
                UpdateSpeedNow(dt);
                return;
            }

            Vector3 camF = CameraRig.Instance != null ? CameraRig.Instance.FlatForward : Vector3.forward;
            Vector3 camR = CameraRig.Instance != null ? CameraRig.Instance.FlatRight : Vector3.right;

            Vector3 dir = camF * moveInput.y + camR * moveInput.x;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            bool hasInput = dir.sqrMagnitude > 0.02f;

            bool grounded = CC.isGrounded;
            float groundH = WorldData.HeightAt(transform.position.x, transform.position.z);
            InWater = grounded && groundH < WorldData.WaterLevel - 0.2f && transform.position.y < WorldData.WaterLevel + 0.6f;
            bool deepWater = groundH < WorldData.WaterLevel - 1.6f;

            // 水中自动往浅处推开一点，避免淹死卡住
            if (deepWater && transform.position.y < WorldData.WaterLevel + 0.4f)
            {
                Vector3 away = new Vector3(transform.position.x, 0f, transform.position.z);
                if (away.sqrMagnitude > 0.01f) dir += away.normalized * 0.8f;
            }

            // 攻击中减速，闪避中锁定方向
            float speed = walkSpeed;
            bool sprinting = InputHub.SprintHeld && stamina > 1f && hasInput;
            if (sprinting) speed = sprintSpeed;
            if (Attacking) speed *= 0.28f;
            if (InWater) speed *= 0.62f;
            if (Gliding) speed *= 0.9f;

            // 消耗体力
            if (sprinting && grounded) stamina = Mathf.Max(0f, stamina - 18f * dt);
            if (Gliding) stamina = Mathf.Max(0f, stamina - 10f * dt);

            // 闪避冲刺
            Vector3 dodgeDir = transform.forward;
            if (dodgeT > 0f)
            {
                speed = 17f;
                dir = dodgeDirXZ;
            }

            // ALS 攀爬：着地 + 朝障碍移动时探测台面（0.5–1.25 m），命中则抬升上去。
            _mantleTimer -= dt;
            if (grounded && hasInput && !Attacking && !Dodging && _rollT <= 0f && _mantleTimer <= 0f)
            {
                if (GalgameOnline.Core.ALSLocomotion.TryFindLedge(transform, dir, 1.25f, 0.5f, 0.75f, out Vector3 ledgeTop))
                {
                    _mantling = true;
                    _mantleT = 0f;
                    _mantleDur = 0.55f;
                    _mantleFrom = transform.position;
                    _mantleTo = ledgeTop + Vector3.up * 0.05f;
                    _mantleTimer = 0.6f;
                    _hVel = Vector3.zero;
                    UpdateSpeedNow(dt);
                    return;
                }
            }

            if (dir.sqrMagnitude > 0.001f)
            {
                float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-16f * dt));
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            // 重力 / 跳跃 / 滑翔
            if (grounded && velocity.y < 0f) velocity.y = -3f;
            velocity.y -= 24f * dt;

            if (InputHub.ConsumeJump())
            {
                if (grounded)
                {
                    velocity.y = 9.2f;
                    Sfx.Play(SfxId.Jump, transform.position);
                }
                else if (velocity.y < 0.5f && stamina > 8f)
                {
                    Gliding = true;
                    Sfx.Play(SfxId.Glide, transform.position);
                }
            }
            if (!InputHub.JumpHeld) Gliding = false;
            if (Gliding && velocity.y < -glideFallSpeed) velocity.y = -glideFallSpeed;
            if (grounded) Gliding = false;
            if (stamina <= 0f) Gliding = false;

            // ALS：水平速度按加速度/摩擦曲线趋近（闪避与翻滚直接给速度，保持干脆）。
            Vector3 motion;
            if (_rollT > 0f)
            {
                _rollT -= dt;
                _hVel = _rollDir * 8f;
            }
            else if (dodgeT > 0f)
            {
                _hVel = dir * speed;
            }
            else
            {
                _hVel = GalgameOnline.Core.ALSLocomotion.Accelerate(_hVel, dir * speed, dt);
            }
            motion = _hVel;
            motion.y = velocity.y;
            CC.Move(motion * dt);

            // ALS 落地：下落速度 > 7 m/s 且有移动输入 → 翻滚卸力（带 0.3 s 无敌）。
            if (!grounded && CC.isGrounded && _rollT <= 0f && !Attacking)
            {
                float fallSpeed = -velocity.y;
                if (GalgameOnline.Core.ALSLocomotion.ShouldRoll(fallSpeed, hasInput))
                {
                    _rollT = 0.45f;
                    _rollDir = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
                    invulnerable = true;
                    CancelInvoke("EndInvuln");
                    Invoke("EndInvuln", 0.30f);
                }
            }

            // 地形安全网：运行时生成的 MeshCollider 烘焙可能滞后于第一帧，
            // 角色控制器会穿透地形无限坠落。用高度函数（地形唯一权威）
            // 把玩家拉回地面。只在玩家已低于地表时触发，不影响跳跃/滑翔。
            float groundSnap = WorldData.HeightAt(transform.position.x, transform.position.z);
            if (transform.position.y < groundSnap - 0.1f)
            {
                Vector3 fixedPos = transform.position;
                fixedPos.y = groundSnap;
                CC.enabled = false;
                transform.position = fixedPos;
                CC.enabled = true;
                if (velocity.y < 0f) velocity.y = 0f;
            }

            UpdateSpeedNow(dt);
            if (CC.isGrounded) lastGroundY = transform.position.y;
        }

        // ALS 速度匹配：用真实位移速度（被挡住时腿停摆，不按输入空摆）。
        void UpdateSpeedNow(float dt)
        {
            Vector3 disp = transform.position - _prevPos;
            SpeedNow = Mathf.Clamp(new Vector3(disp.x, 0f, disp.z).magnitude / Mathf.Max(dt, 0.001f), 0f, 20f);
            _prevPos = transform.position;
        }

        Vector3 dodgeDirXZ = Vector3.forward;

        void UpdateAnimOnly(float dt)
        {
            animTime += dt * 0.4f;
            if (Rig != null && Rig.body != null)
                Rig.body.localPosition = new Vector3(0f, 1.06f + Mathf.Sin(animTime * 3f) * 0.012f, 0f);
        }

        void UpdateAnim(float dt)
        {
            if (Rig == null) return;
            animTime += dt;

            // ALS 落地翻滚：视觉前滚 360°，腿收起。
            if (_rollT > 0f)
            {
                float rp = 1f - _rollT / 0.45f;
                if (Rig.visual != null) Rig.visual.localRotation = Quaternion.Euler(rp * 360f, 0f, 0f);
                if (Rig.legL != null) Rig.legL.localRotation = Quaternion.Euler(-70f, 0f, 0f);
                if (Rig.legR != null) Rig.legR.localRotation = Quaternion.Euler(-70f, 0f, 0f);
                return;
            }
            // ALS 攀爬：身体前倾。
            if (_mantling)
            {
                if (Rig.visual != null) Rig.visual.localRotation = Quaternion.Euler(-25f * Mathf.Sin(_mantleP * Mathf.PI), 0f, 0f);
                return;
            }

            float moveAmt = Mathf.Clamp01(SpeedNow / walkSpeed);
            float cycle = animTime * (7f + SpeedNow * 0.55f);

            // ALS 足部贴地（单骨骼近似）：按脚下前后地形高差算坡度，身体顺坡前倾、
            // 前摆腿多伸，让脚尽量落在斜坡地面上（无 Humanoid 骨架，做不了真 IK）。
            float hAhead = WorldData.HeightAt(transform.position.x + transform.forward.x * 0.45f, transform.position.z + transform.forward.z * 0.45f);
            float hBehind = WorldData.HeightAt(transform.position.x - transform.forward.x * 0.45f, transform.position.z - transform.forward.z * 0.45f);
            float slopeDeg = Mathf.Clamp(Mathf.Atan2(hAhead - hBehind, 0.9f) * Mathf.Rad2Deg, -18f, 18f);

            // 呼吸
            float bob = Mathf.Sin(animTime * 2.2f) * 0.015f;
            float walkBob = Mathf.Abs(Mathf.Sin(cycle)) * 0.06f * moveAmt;
            if (Rig.body != null) Rig.body.localPosition = new Vector3(0f, 1.06f + bob + walkBob, 0f);

            // 腿摆动
            float sinCycle = Mathf.Sin(cycle);
            float legSwingL = sinCycle * 32f * moveAmt + (sinCycle > 0f ? slopeDeg * 0.4f * moveAmt : 0f);
            float legSwingR = -sinCycle * 32f * moveAmt + (sinCycle < 0f ? slopeDeg * 0.4f * moveAmt : 0f);
            if (Rig.legL != null) Rig.legL.localRotation = Quaternion.Euler(legSwingL, 0f, 0f);
            if (Rig.legR != null) Rig.legR.localRotation = Quaternion.Euler(legSwingR, 0f, 0f);

            // 手臂
            float armSwing = -Mathf.Sin(cycle) * 26f * moveAmt;
            if (Rig.armL != null) Rig.armL.localRotation = Quaternion.Euler(armSwing, 0f, 8f);

            // 攻击姿态覆盖右臂
            if (Attacking && Rig.armR != null)
            {
                float p = 1f - Mathf.Clamp01(attackT / Mathf.Max(0.01f, attackDur));
                float swing = attackIndex == 1 ? Mathf.Lerp(-110f, 60f, p) : Mathf.Lerp(70f, -95f, p);
                Rig.armR.localRotation = Quaternion.Euler(swing, attackIndex == 2 ? 30f : 0f, -20f);
                if (Rig.visual != null)
                    Rig.visual.localRotation = Quaternion.Euler(0f, attackIndex == 2 ? 12f : -8f, 0f);
            }
            else if (Rig.armR != null)
            {
                Rig.armR.localRotation = Quaternion.Euler(-armSwing + (Gliding ? -40f : 0f), 0f, -8f);
                if (Rig.visual != null)
                {
                    // 顺坡前倾（移动中才倾，站定时回正）。
                    Quaternion lean = moveAmt > 0.05f
                        ? Quaternion.Euler(slopeDeg * 0.5f, 0f, 0f)
                        : Quaternion.identity;
                    Rig.visual.localRotation = Quaternion.Slerp(Rig.visual.localRotation, lean, dt * 8f);
                }
            }

            // 滑翔时手臂张开、身体前倾
            if (Gliding)
            {
                if (Rig.armL != null) Rig.armL.localRotation = Quaternion.Euler(-30f, 0f, 62f);
                if (Rig.armR != null) Rig.armR.localRotation = Quaternion.Euler(-30f, 0f, -62f);
                if (Rig.visual != null) Rig.visual.localRotation = Quaternion.Euler(12f, 0f, 0f);
            }

            // 受击闪红
            if (hurtFlashT > 0f && Rig.body != null)
            {
                var r = Rig.body.GetComponent<Renderer>();
                if (r != null) r.material.color = Color.Lerp(new Color(0.14f, 0.30f, 0.67f), Color.red, hurtFlashT * 0.7f);
            }

            // 星鸣状态光环
            if (burstActive && Rig.visual != null)
            {
                float s = 1f + Mathf.Sin(Time.time * 6f) * 0.02f;
                Rig.visual.localScale = new Vector3(s, s, s);
            }
            else if (Rig.visual != null)
            {
                Rig.visual.localScale = Vector3.one;
            }

            // 双马尾甩动
            if (Rig.hairL != null) Rig.hairL.localRotation = Quaternion.Euler(Mathf.Sin(animTime * 4f) * 8f + moveAmt * 12f, 0f, 14f);
            if (Rig.hairR != null) Rig.hairR.localRotation = Quaternion.Euler(-Mathf.Sin(animTime * 4f) * 8f + moveAmt * 12f, 0f, -14f);
        }

        // ---------------- 战斗 ----------------

        public void OnAttackPressed()
        {
            if (GameManager.Instance != null && GameManager.Instance.Paused) return;
            if (Dodging) return;
            if (attackT > 0.12f) return;  // 前摇内不接受
            attackIndex = (attackIndex % 3) + 1;
            attackDur = attackIndex == 3 ? 0.52f : 0.34f;
            attackT = attackDur;
            attackDidHit = false;
            comboCount++;
            comboTimer = 2.6f;
            comboCount = Mathf.Clamp(comboCount, 1, 99);
            Sfx.Play(SfxId.Swing, transform.position);
            var col = burstActive ? new Color(0.55f, 0.95f, 1f) : new Color(0.86f, 0.95f, 1f);
            FX.SlashArc(transform, col, 2.4f, 130f, 0.2f);
        }

        void DoMeleeHit(int index)
        {
            float radius = index == 3 ? 4.2f : 2.9f;
            float dmg = (index == 3 ? 46f : 26f) * (burstActive ? 1.65f : 1f) * DamageMultiplier;
            Vector3 center = transform.position + transform.forward * (index == 3 ? 2.0f : 1.5f) + Vector3.up * 1f;

            if (index == 3)
            {
                FX.Shockwave(transform.position + transform.forward * 1.2f, new Color(0.6f, 0.95f, 1f), 4.6f, 0.35f);
                Sfx.Play(SfxId.Heavy, transform.position);
            }

            var hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
            var seen = new HashSet<EnemyAI>();
            foreach (var h in hits)
            {
                var e = h.GetComponentInParent<EnemyAI>();
                if (e == null || e.Dead || seen.Contains(e)) continue;
                seen.Add(e);
                Vector3 push = (e.transform.position - transform.position).normalized;
                e.TakeDamage(dmg, push * (index == 3 ? 9f : 4f));
                GameManager.Instance?.HitStop(index == 3 ? 0.07f : 0.035f);
            }
            if (seen.Count > 0)
            {
                burstEnergy = Mathf.Min(100f, burstEnergy + seen.Count * 5f);
                OnMeleeHit?.Invoke(seen.Count);
            }
        }

        public void Dodge()
        {
            if (dodgeT > 0f || dodgeCooldown > 0f) return;
            if (stamina < 18f) return;
            var camRig = CameraRig.Instance;
            dodgeDirXZ = (moveInput.sqrMagnitude > 0.05f && camRig != null)
                ? (camRig.FlatForward * moveInput.y + camRig.FlatRight * moveInput.x).normalized
                : transform.forward;
            dodgeT = 0.26f;
            dodgeCooldown = 0.55f;
            stamina -= 18f;
            invulnerable = true;
            CancelInvoke("EndInvuln");
            Invoke("EndInvuln", 0.30f);
            Sfx.Play(SfxId.Dodge, transform.position);
            FX.Burst(transform.position + Vector3.up * 0.9f, new Color(0.75f, 0.95f, 1f), 14, 5f, 0.25f, 0.4f);
        }

        void EndInvuln() { invulnerable = false; }

        public void UseSkill()
        {
            if (skillCooldown > 0f) return;
            if (stamina < 20f) return;
            skillCooldown = 7.5f;
            stamina -= 20f;
            float dmg = 62f * (burstActive ? 1.7f : 1f);
            Vector3 c = transform.position + transform.forward * 2.2f;
            FX.Shockwave(transform.position, new Color(0.45f, 0.9f, 1f), 8f, 0.55f);
            FX.Burst(c + Vector3.up, new Color(0.5f, 0.95f, 1f), 60, 9f, 0.5f, 0.9f);
            Sfx.Play(SfxId.Skill, transform.position);

            var hits = Physics.OverlapSphere(transform.position, 7.5f, ~0, QueryTriggerInteraction.Collide);
            foreach (var h in hits)
            {
                var e = h.GetComponentInParent<EnemyAI>();
                if (e == null || e.Dead) continue;
                Vector3 push = (e.transform.position - transform.position).normalized;
                e.TakeDamage(dmg, push * 14f);
                e.Stagger(0.7f);
            }
            GameManager.Instance?.HitStop(0.09f);
            burstEnergy = Mathf.Min(100f, burstEnergy + 10f);
            OnSkillUsed?.Invoke();
        }

        public void TryBurst()
        {
            if (burstActive || burstEnergy < 100f) return;
            burstActive = true;
            burstTime = 9f;
            burstEnergy = 0f;
            hp = Mathf.Min(maxHP, hp + 25f);
            FX.Shockwave(transform.position, new Color(0.65f, 0.98f, 1f), 9f, 0.8f);
            FX.Burst(transform.position + Vector3.up, new Color(0.7f, 1f, 1f), 70, 8f, 0.55f, 1.2f);
            Sfx.Play(SfxId.Burst, transform.position);
            // 小队接管觉醒文案时由 SquadManager 按角色区分显示，否则用通用文案
            if (OnBurstUsed != null) OnBurstUsed.Invoke();
            else GameHUD.Instance?.Toast("星鸣 · 觉醒");
        }

        public void TakeDamage(float dmg, Vector3 from)
        {
            if (invulnerable || Dodging) return;
            if (GameManager.Instance != null && GameManager.Instance.GodMode) return;
            hp -= dmg * (1f - DamageReduction);
            hurtFlashT = 1f;
            invulnerable = true;
            Invoke("EndInvuln", 0.55f);
            Sfx.Play(SfxId.Hurt, transform.position);
            FX.Burst(transform.position + Vector3.up * 1f, new Color(1f, 0.4f, 0.4f), 16, 4f, 0.3f, 0.5f);
            CameraRig.Instance?.Shake(0.35f);
            if (hp <= 0f) Die();
        }

        void Die()
        {
            hp = maxHP;
            stamina = maxStamina;
            GameManager.Instance?.OnPlayerDied();
        }

        public void Heal(float v) { hp = Mathf.Min(maxHP, hp + v); }
        public void SetEnergy(float v) { burstEnergy = Mathf.Clamp(v, 0f, 100f); }

        /// <summary>切换队员时换配色（几何体占位）。</summary>
        public void ApplyMemberVisual(Color clothColor)
        {
            if (Rig == null || Rig.body == null) return;
            var r = Rig.body.GetComponent<Renderer>();
            if (r != null) r.material.color = clothColor;
        }
        public void AddStamina(float v) { stamina = Mathf.Min(maxStamina, stamina + v); }
        public void AddEnergy(float v) { burstEnergy = Mathf.Min(100f, burstEnergy + v); }
        public void AddExp(int v)
        {
            Exp += v;
            while (Exp >= Level * 60)
            {
                Exp -= Level * 60;
                Level++;
                maxHP += 12f;
                hp = maxHP;
                GameHUD.Instance?.Toast("等级提升 · Lv." + Level);
            }
        }

        // ---------------- 交互 ----------------

        Interactable nearest;
        public Interactable Nearest { get { return nearest; } }

        void UpdateInteractPrompt()
        {
            nearest = GameManager.Instance != null ? GameManager.Instance.FindNearestInteractable(transform.position) : null;
            if (nearest != null && InputHub.ConsumeInteract())
            {
                nearest.Interact(this);
                CameraRig.Instance?.Shake(0.12f);
            }
        }
    }
}
