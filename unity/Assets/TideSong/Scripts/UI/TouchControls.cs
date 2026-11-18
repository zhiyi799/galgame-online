using UnityEngine;

namespace TideSong
{
    /// <summary>手机端的半透明摇杆与技能键（IMGUI 手绘，支持多点触控）。</summary>
    public class TouchControls : MonoBehaviour
    {
        public bool ShowButtons;
        public float uiScale = 1f;

        GameManager gm;
        int moveId = -1;
        int lookId = -1;
        Vector2 joyCenter;
        Vector2 joyHandle;
        float joyRadius = 92f;
        bool joyActive;

        struct Btn
        {
            public Vector2 center;
            public float radius;
            public string label;
            public string sub;
            public Color color;
            public float flash;
        }

        Btn attack, dodge, jump, skill, burst;

        void Awake()
        {
            InputHub.Mobile = Application.isMobilePlatform;
            ShowButtons = InputHub.Mobile;
        }

        public void Init(GameManager manager)
        {
            gm = manager;
            Layout();
        }

        void Layout()
        {
            float s = uiScale;
            float w = Screen.width;
            float h = Screen.height;
            joyRadius = 92f * s;
            joyCenter = new Vector2(150f * s, h - 165f * s);

            attack = new Btn { center = new Vector2(w - 122f * s, h - 150f * s), radius = 74f * s, label = "攻", sub = "击", color = new Color(0.85f, 0.42f, 0.32f) };
            dodge = new Btn { center = new Vector2(w - 118f * s, h - 312f * s), radius = 52f * s, label = "闪", sub = "避", color = new Color(0.32f, 0.72f, 0.55f) };
            jump = new Btn { center = new Vector2(w - 250f * s, h - 250f * s), radius = 50f * s, label = "跳", sub = "跃", color = new Color(0.32f, 0.55f, 0.85f) };
            skill = new Btn { center = new Vector2(w - 250f * s, h - 130f * s), radius = 54f * s, label = "技", sub = "能", color = new Color(0.36f, 0.62f, 0.90f) };
            burst = new Btn { center = new Vector2(150f * s, h - 372f * s), radius = 46f * s, label = "星", sub = "鸣", color = new Color(0.30f, 0.80f, 0.95f) };
        }

        void Update()
        {
            uiScale = Mathf.Clamp(Screen.height / 900f, 0.68f, 2.4f);
            if (ShowButtons) Layout();
            if (!ShowButtons) return;
            if (GameManager.Instance != null && GameManager.Instance.Paused) { InputHub.Move = Vector2.zero; return; }

            HandleTouches();
            HandleMouse();
        }

        float flashAttack, flashDodge, flashJump, flashSkill, flashBurst;

        void HandleTouches()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.touches[i];
                Vector2 p = new Vector2(t.position.x, Screen.height - t.position.y);

                if (t.phase == TouchPhase.Began)
                {
                    if (Vector2.Distance(p, joyCenter) < joyRadius * 1.25f && moveId < 0)
                    {
                        moveId = t.fingerId;
                        joyActive = true;
                        UpdateJoy(p);
                        continue;
                    }
                    if (TryPressButton(p)) continue;
                    if (p.x > Screen.width * 0.42f && lookId < 0)
                    {
                        lookId = t.fingerId;
                    }
                }
                else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                {
                    if (t.fingerId == moveId) { UpdateJoy(p); continue; }
                    if (t.fingerId == lookId && CameraRig.Instance != null)
                    {
                        CameraRig.Instance.LookDelta = t.deltaPosition;
                    }
                }
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    if (t.fingerId == moveId)
                    {
                        moveId = -1;
                        joyActive = false;
                        InputHub.Move = Vector2.zero;
                    }
                    if (t.fingerId == lookId) lookId = -1;
                }
            }

            if (InputHub.Mobile && CameraRig.Instance != null)
            {
                // 跳跃键按住判定（滑翔要用）
                bool held = false;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Vector2 p = new Vector2(Input.touches[i].position.x, Screen.height - Input.touches[i].position.y);
                    if (Vector2.Distance(p, jump.center) < jump.radius * 1.3f) { held = true; break; }
                }
                InputHub.JumpHeld = held;
            }
        }

        void UpdateJoy(Vector2 p)
        {
            Vector2 d = p - joyCenter;
            if (d.magnitude > joyRadius) d = d.normalized * joyRadius;
            joyHandle = d;
            InputHub.Move = d / joyRadius;
            InputHub.SprintHeld = d.magnitude > joyRadius * 0.92f;
        }

        bool TryPressButton(Vector2 p)
        {
            if (Vector2.Distance(p, attack.center) < attack.radius * 1.15f) { InputHub.PressAttack(); flashAttack = 1f; return true; }
            if (Vector2.Distance(p, dodge.center) < dodge.radius * 1.2f) { InputHub.PressDodge(); flashDodge = 1f; return true; }
            if (Vector2.Distance(p, jump.center) < jump.radius * 1.2f) { InputHub.PressJump(); InputHub.JumpHeld = true; flashJump = 1f; return true; }
            if (Vector2.Distance(p, skill.center) < skill.radius * 1.2f) { InputHub.PressSkill(); flashSkill = 1f; return true; }
            if (Vector2.Distance(p, burst.center) < burst.radius * 1.2f) { InputHub.PressBurst(); flashBurst = 1f; return true; }
            return false;
        }

        void HandleMouse()
        {
            // 编辑器里用鼠标也能试玩按钮
            if (Input.GetMouseButtonDown(0) && !Application.isMobilePlatform)
            {
                Vector2 p = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                if (p.x < Screen.width * 0.3f && p.y > Screen.height * 0.6f)
                {
                    moveId = 0; joyActive = true; UpdateJoy(p);
                }
            }
            if (Input.GetMouseButton(0) && joyActive && moveId == 0 && !Application.isMobilePlatform)
            {
                Vector2 p = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                UpdateJoy(p);
            }
            if (Input.GetMouseButtonUp(0) && moveId == 0 && !Application.isMobilePlatform)
            {
                moveId = -1; joyActive = false; InputHub.Move = Vector2.zero;
            }
        }

        // ---------------- 绘制 ----------------

        public void DrawGUI(float scale)
        {
            float s = scale;
            var white = ArtUtil.White;

            // 摇杆底盘
            GUI.color = new Color(0.05f, 0.10f, 0.16f, joyActive ? 0.55f : 0.34f);
            GUI.DrawTexture(new Rect(joyCenter.x - joyRadius, joyCenter.y - joyRadius, joyRadius * 2f, joyRadius * 2f), ArtUtil.Dot);
            GUI.color = new Color(0.45f, 0.78f, 1f, 0.28f);
            GUI.DrawTexture(new Rect(joyCenter.x - joyRadius, joyCenter.y - joyRadius, joyRadius * 2f, joyRadius * 2f), ArtUtil.Ring);

            // 摇杆头
            Vector2 hp = joyCenter + joyHandle;
            float hr = 38f * s;
            GUI.color = new Color(0.55f, 0.85f, 1f, joyActive ? 0.9f : 0.6f);
            GUI.DrawTexture(new Rect(hp.x - hr, hp.y - hr, hr * 2f, hr * 2f), ArtUtil.Dot);

            DrawBtn(attack, ref flashAttack, s);
            DrawBtn(dodge, ref flashDodge, s);
            DrawBtn(jump, ref flashJump, s);
            DrawBtn(skill, ref flashSkill, s);
            DrawBtn(burst, ref flashBurst, s);

            // 冷却
            var p = PlayerCharacter.Instance;
            if (p != null)
            {
                DrawCooldown(skill.center, skill.radius, p.SkillCooldown, s);
                DrawCooldown(dodge.center, dodge.radius, p.DodgeCooldown, s);
            }

            // 交互按钮
            var near = p != null ? p.Nearest : null;
            if (near != null)
            {
                var c = new Vector2(Screen.width * 0.5f, Screen.height - 210f * s);
                float r = 54f * s;
                GUI.color = new Color(0.95f, 0.82f, 0.35f, 0.22f);
                GUI.DrawTexture(new Rect(c.x - r, c.y - r, r * 2f, r * 2f), ArtUtil.Dot);
                GUI.color = new Color(0.95f, 0.85f, 0.4f);
                GUI.Label(new Rect(c.x - r, c.y - 18f * s, r * 2f, 36f * s), "互动", CenterStyle(20f * s));
            }

            GUI.color = Color.white;
        }

        void DrawBtn(Btn b, ref float flash, float s)
        {
            float f = flash;
            flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime * 3.5f);
            GUI.color = new Color(b.color.r, b.color.g, b.color.b, 0.30f + f * 0.45f);
            GUI.DrawTexture(new Rect(b.center.x - b.radius, b.center.y - b.radius, b.radius * 2f, b.radius * 2f), ArtUtil.Dot);
            GUI.color = new Color(b.color.r, b.color.g, b.color.b, 0.95f);
            GUI.DrawTexture(new Rect(b.center.x - b.radius, b.center.y - b.radius, b.radius * 2f, b.radius * 2f), ArtUtil.Ring);
            GUI.color = new Color(1f, 1f, 1f, 0.92f);
            GUI.Label(new Rect(b.center.x - b.radius, b.center.y - b.radius * 0.62f, b.radius * 2f, b.radius * 1.3f), b.label, CenterStyle(b.radius * 0.62f));
        }

        void DrawCooldown(Vector2 c, float r, float remain, float s)
        {
            if (remain <= 0.01f) return;
            GUI.color = new Color(0.02f, 0.04f, 0.07f, 0.6f);
            GUI.DrawTexture(new Rect(c.x - r, c.y - r, r * 2f, r * 2f), ArtUtil.Dot);
            GUI.color = new Color(0.75f, 0.9f, 1f, 0.85f);
            GUI.Label(new Rect(c.x - r, c.y - 14f * s, r * 2f, 30f * s), remain.ToString("0.0"), CenterStyle(20f * s));
        }

        GUIStyle _cs;
        int _csSize;

        GUIStyle CenterStyle(float size)
        {
            int px = Mathf.RoundToInt(size);
            if (_cs == null || _csSize != px)
            {
                _cs = new GUIStyle(GUI.skin.label) { fontSize = px, alignment = TextAnchor.MiddleCenter };
                _cs.normal.textColor = Color.white;
                _csSize = px;
            }
            return _cs;
        }
    }
}
