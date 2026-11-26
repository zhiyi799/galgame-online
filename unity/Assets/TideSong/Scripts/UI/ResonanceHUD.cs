using UnityEngine;

namespace TideSong
{
    /// <summary>
    /// 共鸣 HUD：队伍切换条、协奏能量、技能状态、面板入口。
    /// 独立于 GameHUD，避免动原有 HUD 的布局。
    /// </summary>
    public class ResonanceHUD : MonoBehaviour
    {
        GameManager gm;
        TeamPanel teamPanel;
        EchoPanel echoPanel;
        float s = 1f;

        float panelCooldown;
        public static bool PanelOpenStatic;

        public void Init(GameManager manager, TeamPanel team, EchoPanel echo)
        {
            gm = manager;
            teamPanel = team;
            echoPanel = echo;
        }

        bool PanelOpen { get { return (teamPanel != null && teamPanel.open) || (echoPanel != null && echoPanel.open); } }

        void Update()
        {
            if (gm == null) gm = GameManager.Instance;
            if (gm == null) return;

            PanelOpenStatic = PanelOpen;
            if (panelCooldown > 0f) panelCooldown -= Time.unscaledDeltaTime;

            // 面板打开时 Esc 只关面板
            if (PanelOpen && Input.GetKeyDown(KeyCode.Escape) && panelCooldown <= 0f)
            {
                if (teamPanel != null) teamPanel.open = false;
                if (echoPanel != null) echoPanel.open = false;
                ApplyPause(false);
                panelCooldown = 0.18f;
                return;
            }

            if (Input.GetKeyDown(KeyCode.C) && !(echoPanel != null && echoPanel.open) && panelCooldown <= 0f)
            {
                ToggleTeam();
            }
            if (Input.GetKeyDown(KeyCode.B) && !(teamPanel != null && teamPanel.open) && panelCooldown <= 0f)
            {
                ToggleEcho();
            }

            if (Input.GetKeyDown(KeyCode.R) && !gm.Paused && EchoSystem.Instance != null)
                EchoSystem.Instance.CastEchoSkill();
        }

        void ToggleTeam()
        {
            if (teamPanel == null) return;
            if (gm != null && gm.Paused && !teamPanel.open && !PanelOpen) return;
            bool willOpen = !teamPanel.open;
            teamPanel.Toggle();
            if (willOpen) { if (echoPanel != null) echoPanel.open = false; }
            ApplyPause(willOpen);
            panelCooldown = 0.18f;
        }

        void ToggleEcho()
        {
            if (echoPanel == null) return;
            if (gm != null && gm.Paused && !echoPanel.open && !PanelOpen) return;
            bool willOpen = !echoPanel.open;
            echoPanel.Toggle();
            if (willOpen) { if (teamPanel != null) teamPanel.open = false; }
            ApplyPause(willOpen);
            panelCooldown = 0.18f;
        }

        void ApplyPause(bool p)
        {
            if (gm == null) return;
            if (p) gm.SetPaused(true);
            else gm.SetPaused(false);
        }

        void OnGUI()
        {
            if (gm == null || gm.Paused || PanelOpen) return;
            if (GameHUD.Instance == null) return;
            var team = TeamManager.Instance;
            var echo = EchoSystem.Instance;
            if (team == null || team.Roster.Count == 0) return;

            s = Mathf.Clamp(Screen.height / 900f, 0.68f, 2.2f);
            bool mobile = InputHub.Mobile;

            float panelW = 200f * s;
            float x = mobile ? 24f * s : Screen.width - panelW - 26f * s;
            float y = mobile ? 128f * s : 208f * s;

            DrawTeamStrip(team, x, y, panelW);

            // 协奏能量
            var cyber = new Rect(x, y + 74f * s, panelW, 10f * s);
            GUI.color = new Color(0.03f, 0.06f, 0.10f, 0.85f);
            GUI.DrawTexture(cyber, ArtUtil.White);
            GUI.color = team.concerto >= TeamManager.IntroCost ? new Color(1f, 0.82f, 0.38f) : new Color(0.45f, 0.7f, 0.95f);
            GUI.DrawTexture(new Rect(cyber.x + 1f, cyber.y + 1f, (cyber.width - 2f) * Mathf.Clamp01(team.concerto / 100f), cyber.height - 2f), ArtUtil.White);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 86f * s, panelW, 20f * s),
                team.concerto >= TeamManager.IntroCost ? "协奏 " + Mathf.RoundToInt(team.concerto) + " · 可变奏" : "协奏 " + Mathf.RoundToInt(team.concerto),
                UIStyle.Label(12f * s, team.concerto >= TeamManager.IntroCost ? new Color(1f, 0.86f, 0.5f) : new Color(0.66f, 0.8f, 0.95f)));

            // 技能状态
            var st = team.Active;
            if (st != null)
            {
                string skill = "E " + st.def.skillName.Replace("共鸣技能 · ", "") + (st.skillCd > 0.05f ? "  " + st.skillCd.ToString("0.0") + "s" : "  就绪");
                GUI.Label(new Rect(x, y + 106f * s, panelW + 60f * s, 18f * s), skill,
                    UIStyle.Label(11f * s, st.skillCd > 0.05f ? new Color(0.6f, 0.68f, 0.78f) : new Color(0.7f, 1f, 0.9f)));

                string burst = "Q " + st.def.burstName.Replace("共鸣解放 · ", "") + "  " + Mathf.RoundToInt(st.energy) + "%";
                GUI.Label(new Rect(x, y + 124f * s, panelW + 60f * s, 18f * s), burst,
                    UIStyle.Label(11f * s, st.energy >= st.def.burstCost ? new Color(1f, 0.88f, 0.5f) : new Color(0.62f, 0.7f, 0.8f)));

                if (echo != null)
                {
                    var main = echo.MainEcho;
                    string rTxt = main != null
                        ? "R " + main.Def.skillName.Replace("声骸 · ", "") + (echo.EchoSkillCd > 0.05f ? "  " + echo.EchoSkillCd.ToString("0.0") + "s" : "  就绪")
                        : "R 声骸技能（未装备）";
                    GUI.Label(new Rect(x, y + 142f * s, panelW + 60f * s, 18f * s), rTxt,
                        UIStyle.Label(11f * s, echo.EchoSkillCd > 0.05f ? new Color(0.6f, 0.68f, 0.78f) : new Color(0.8f, 0.85f, 1f)));
                }
            }

            // 手机上的面板入口
            if (mobile)
            {
                float bw = 62f * s, bh = 30f * s;
                if (GUI.Button(new Rect(Screen.width - bw * 2f - 24f * s, 12f * s, bw, bh), "角色", UIStyle.Button(13f * s))) ToggleTeam();
                if (GUI.Button(new Rect(Screen.width - bw - 14f * s, 12f * s, bw, bh), "声骸", UIStyle.Button(13f * s))) ToggleEcho();
            }
            else
            {
                GUI.Label(new Rect(x, y + 164f * s, 260f * s, 18f * s), "C 角色   B 声骸   1-4 切换", UIStyle.Label(11f * s, new Color(0.52f, 0.62f, 0.74f)));
            }

            DrawStateHint();
            GUI.color = Color.white;
        }

        void DrawTeamStrip(TeamManager team, float x, float y, float panelW)
        {
            var roster = team.Roster;
            int n = roster.Count;
            float gap = 6f * s;
            float size = Mathf.Min(44f * s, (panelW - gap * (n - 1)) / Mathf.Max(1, n));

            for (int i = 0; i < n; i++)
            {
                var st = roster[i];
                var def = st.def;
                bool active = team.activeIndex == i;
                var rr = new Rect(x + i * (size + gap), y, size, size);

                GUI.color = new Color(0.03f, 0.06f, 0.10f, 0.9f);
                GUI.DrawTexture(rr, ArtUtil.Dot);
                GUI.color = st.unlocked ? def.elementColor : new Color(0.28f, 0.3f, 0.34f);
                GUI.DrawTexture(new Rect(rr.x + 2f, rr.y + 2f, rr.width - 4f, rr.height - 4f), ArtUtil.Dot);

                GUI.color = Color.black;
                GUI.Label(new Rect(rr.x, rr.y + size * 0.16f, size, size * 0.5f),
                    st.unlocked ? def.name.Substring(0, 1) : "?", UIStyle.Label(size * 0.46f, new Color(0.05f, 0.08f, 0.12f), TextAnchor.MiddleCenter));

                // 血量
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(rr.x, rr.y + size + 2f, size, 3f * s), ArtUtil.White);
                GUI.color = new Color(0.9f, 0.35f, 0.38f);
                GUI.DrawTexture(new Rect(rr.x, rr.y + size + 2f, size * Mathf.Clamp01(st.hp / Mathf.Max(1f, st.maxHp)), 3f * s), ArtUtil.White);

                // 能量
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(rr.x, rr.y + size + 6f * s, size, 2f * s), ArtUtil.White);
                GUI.color = st.energy >= st.def.burstCost ? new Color(1f, 0.85f, 0.4f) : new Color(0.4f, 0.72f, 0.95f);
                GUI.DrawTexture(new Rect(rr.x, rr.y + size + 6f * s, size * Mathf.Clamp01(st.energy / 100f), 2f * s), ArtUtil.White);

                if (active)
                {
                    GUI.color = new Color(0.6f, 1f, 1f, 0.9f);
                    GUI.DrawTexture(new Rect(rr.x - 1f, rr.y - 1f, rr.width + 2f, 2f * s), ArtUtil.White);
                    GUI.DrawTexture(new Rect(rr.x - 1f, rr.y + rr.height, rr.width + 2f, 2f * s), ArtUtil.White);
                    GUI.DrawTexture(new Rect(rr.x - 1f, rr.y, 2f * s, rr.height), ArtUtil.White);
                    GUI.DrawTexture(new Rect(rr.x + rr.width - 1f, rr.y, 2f * s, rr.height), ArtUtil.White);
                }

                // 点击切换（手机和 PC 都能点）
                if (GUI.Button(rr, GUIContent.none, GUIStyle.none))
                    team.SwitchTo(i);

                GUI.color = Color.white;
                GUI.Label(new Rect(rr.x, rr.y + size + 9f * s, size + 6f * s, 14f * s), (i + 1).ToString(),
                    UIStyle.Label(10f * s, new Color(0.55f, 0.65f, 0.78f), TextAnchor.MiddleCenter));
            }
        }

        void DrawStateHint()
        {
            var p = PlayerCharacter.Instance;
            if (p == null) return;

            string hint = p.ParkourHint;
            if (string.IsNullOrEmpty(hint)) return;

            float w = 320f * s;
            var rr = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.68f, w, 30f * s);
            GUI.color = new Color(0.03f, 0.07f, 0.12f, 0.6f);
            GUI.DrawTexture(rr, ArtUtil.White);
            GUI.color = Color.white;
            GUI.Label(rr, hint, UIStyle.Label(14f * s, new Color(0.72f, 0.94f, 1f), TextAnchor.MiddleCenter));
        }
    }
}
