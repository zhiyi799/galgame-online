// SquadHud — 星律小队战斗 HUD（Phase 1 / P1-E）。
// IMGUI 绘制（与 TideSong GameHUD 同风格），独立 OnGUI，不改 GameHUD。
// 内容：左上 4 人小队条（头像/HP/能量，当前操作者高亮）、底部中央
// 星律共鸣值条 + SP、最近敌人的失衡条、连携技就绪提示、回响技能格。
// 1280×720 与 1920×1080 均按 uiScale 缩放，无溢出。
using UnityEngine;
using TideSong;

namespace AstralBloom
{
    public class SquadHud : MonoBehaviour
    {
        private Font _font;
        private bool _fontTried;
        private Texture2D[] _portraits = new Texture2D[4];
        private bool _portraitsTried;

        private float Scale { get { return Mathf.Clamp(Screen.height / 900f, 0.68f, 2.4f); } }

        private void OnGUI()
        {
            var sq = SquadManager.Instance;
            if (sq == null) return;
            if (_font == null && !_fontTried)
            {
                _fontTried = true;
                _font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            }
            if (_font != null && GUI.skin.font != _font) GUI.skin.font = _font;
            if (!_portraitsTried)
            {
                _portraitsTried = true;
                for (int i = 0; i < 4; i++)
                {
                    string art = SquadData.Roster[i].artName;
                    if (!string.IsNullOrEmpty(art))
                        _portraits[i] = Resources.Load<Texture2D>("Characters/" + art);
                }
            }

            float s = Scale;
            DrawSquadChips(s);
            DrawResonance(s);
            DrawStagger(s);
            DrawComboReady(s);
            DrawEchoSkill(s);
        }

        private void DrawSquadChips(float s)
        {
            var sq = SquadManager.Instance;
            float x = 22f * s;
            float y = 124f * s;
            float w = 252f * s;
            float h = 44f * s;
            float gap = 6f * s;
            for (int i = 0; i < 4; i++)
            {
                var def = SquadData.Roster[i];
                bool active = i == sq.ActiveIndex;
                var r = new Rect(x, y + i * (h + gap), w, h);
                // 底
                GUI.color = active
                    ? new Color(0.10f, 0.22f, 0.48f, 0.92f)
                    : new Color(0.04f, 0.08f, 0.14f, 0.78f);
                GUI.DrawTexture(r, ArtUtil.White);
                if (active)
                {
                    GUI.color = new Color(0.55f, 0.9f, 1f, 0.9f);
                    GUI.DrawTexture(new Rect(r.x, r.y, 3f * s, r.height), ArtUtil.White);
                }
                // 头像：有立绘用立绘，否则用属性色块 + 字
                float av = h - 8f * s;
                var avRect = new Rect(r.x + 4f * s, r.y + 4f * s, av, av);
                if (_portraits[i] != null)
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(avRect, _portraits[i], ScaleMode.ScaleToFit);
                }
                else
                {
                    GUI.color = def.clothColor;
                    GUI.DrawTexture(avRect, ArtUtil.Dot);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(avRect.x, avRect.y + av * 0.2f, av, av * 0.6f),
                        "无", MakeStyle(Mathf.RoundToInt(16f * s), TextAnchor.MiddleCenter));
                }
                // 名字 + 数字键
                GUI.color = new Color(0.94f, 0.97f, 1f);
                GUI.Label(new Rect(r.x + av + 10f * s, r.y + 2f * s, 120f * s, 20f * s),
                    (i + 1) + " · " + def.name, MakeStyle(Mathf.RoundToInt(15f * s), TextAnchor.MiddleLeft));
                // HP 条
                float hp = GetMemberHp(i);
                float hpMax = def.maxHP;
                var hpBg = new Rect(r.x + av + 10f * s, r.y + 24f * s, 150f * s, 8f * s);
                GUI.color = new Color(0.02f, 0.04f, 0.07f, 0.85f);
                GUI.DrawTexture(hpBg, ArtUtil.White);
                GUI.color = new Color(0.90f, 0.34f, 0.36f);
                GUI.DrawTexture(new Rect(hpBg.x + 1f, hpBg.y + 1f, (hpBg.width - 2f) * Mathf.Clamp01(hp / hpMax), hpBg.height - 2f), ArtUtil.White);
                // 能量条
                float en = GetMemberEnergy(i);
                var enBg = new Rect(r.x + av + 10f * s, r.y + 34f * s, 150f * s, 5f * s);
                GUI.color = new Color(0.02f, 0.04f, 0.07f, 0.85f);
                GUI.DrawTexture(enBg, ArtUtil.White);
                GUI.color = en >= 100f ? new Color(0.75f, 1f, 1f) : new Color(0.35f, 0.72f, 0.95f);
                GUI.DrawTexture(new Rect(enBg.x + 1f, enBg.y + 1f, (enBg.width - 2f) * Mathf.Clamp01(en / 100f), enBg.height - 2f), ArtUtil.White);
                GUI.color = Color.white;
            }
        }

        private float GetMemberHp(int i)
        {
            var sq = SquadManager.Instance;
            if (i == sq.ActiveIndex)
            {
                var p = TideSong.GameManager.Instance?.Player;
                return p != null ? p.hp : SquadData.Roster[i].maxHP;
            }
            return sq.GetMemberHp(i);
        }

        private float GetMemberEnergy(int i)
        {
            var sq = SquadManager.Instance;
            if (i == sq.ActiveIndex)
            {
                var p = TideSong.GameManager.Instance?.Player;
                return p != null ? p.BurstEnergy : 0f;
            }
            return sq.GetMemberEnergy(i);
        }

        private void DrawResonance(float s)
        {
            var sq = SquadManager.Instance;
            float w = 300f * s, h = 12f * s;
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height - 132f * s, w, h);
            GUI.color = new Color(0.02f, 0.04f, 0.07f, 0.85f);
            GUI.DrawTexture(r, ArtUtil.White);
            float k = Mathf.Clamp01(sq.Resonance / SquadData.ResonanceMax);
            GUI.color = k >= 1f ? new Color(0.85f, 0.75f, 0.35f) : new Color(0.45f, 0.75f, 1f);
            GUI.DrawTexture(new Rect(r.x + 1f, r.y + 1f, (r.width - 2f) * k, r.height - 2f), ArtUtil.White);
            GUI.color = new Color(0.94f, 0.97f, 1f);
            GUI.Label(new Rect(r.x, r.y - 20f * s, r.width, 20f * s),
                "星律共鸣 " + Mathf.FloorToInt(sq.Resonance) + " / 100　SP " + Mathf.FloorToInt(sq.SP) + " / 10",
                MakeStyle(Mathf.RoundToInt(14f * s), TextAnchor.MiddleCenter));
            GUI.color = Color.white;
        }

        private void DrawStagger(float s)
        {
            var player = TideSong.GameManager.Instance?.Player;
            if (player == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            EnemyAI best = null;
            float bestD = 46f;
            foreach (var e in Object.FindObjectsOfType<EnemyAI>())
            {
                if (e == null || e.Dead) continue;
                float d = Vector3.Distance(cam.transform.position, e.transform.position);
                if (d < bestD) { bestD = d; best = e; }
            }
            if (best == null) return;
            Vector3 sp = cam.WorldToScreenPoint(best.transform.position + Vector3.up * 1.9f);
            if (sp.z < 0f) return;
            float w = Mathf.Lerp(84f, 42f, Mathf.Clamp01(bestD / 46f)) * s;
            float x = sp.x - w * 0.5f;
            float y = Screen.height - sp.y + 9f * s;
            GUI.color = new Color(0.03f, 0.03f, 0.06f, 0.85f);
            GUI.DrawTexture(new Rect(x, y, w, 5f * s), ArtUtil.White);
            GUI.color = best.Downed ? new Color(1f, 0.8f, 0.3f) : new Color(0.55f, 0.85f, 1f);
            GUI.DrawTexture(new Rect(x + 1f, y + 1f, (w - 2f) * best.StaggerRatio, 5f * s - 2f), ArtUtil.White);
            if (best.Downed)
            {
                GUI.color = new Color(1f, 0.85f, 0.4f);
                GUI.Label(new Rect(x - 20f, y + 6f * s, w + 40f, 18f * s), "失衡",
                    MakeStyle(Mathf.RoundToInt(13f * s), TextAnchor.MiddleCenter));
            }
            GUI.color = Color.white;
        }

        private void DrawComboReady(float s)
        {
            var sq = SquadManager.Instance;
            if (!sq.ComboReady) return;
            float a = 0.65f + 0.35f * Mathf.Sin(Time.time * 6f);
            GUI.color = new Color(1f, 0.85f, 0.4f, a);
            GUI.Label(new Rect(0, 150f * s, Screen.width, 40f * s), "连携技就绪  [ R ]",
                MakeStyle(Mathf.RoundToInt(26f * s), TextAnchor.MiddleCenter));
            GUI.color = Color.white;
        }

        private void DrawEchoSkill(float s)
        {
            var em = EchoManager.Instance;
            float w = 220f * s, h = 52f * s;
            var r = new Rect(Screen.width - w - 22f * s, Screen.height - 132f * s, w, h);
            GUI.color = new Color(0.04f, 0.08f, 0.14f, 0.78f);
            GUI.DrawTexture(r, ArtUtil.White);
            GUI.color = new Color(0.55f, 0.9f, 1f);
            GUI.DrawTexture(new Rect(r.x, r.y, 3f * s, r.height), ArtUtil.White);
            GUI.color = new Color(0.94f, 0.97f, 1f);
            string name = em != null ? em.SlotOneEchoName() : "未装备回响";
            string cd = em != null ? em.EchoSkillCooldownText() : "";
            GUI.Label(new Rect(r.x + 10f * s, r.y + 4f * s, r.width - 14f * s, 22f * s),
                "回响技能 [F]", MakeStyle(Mathf.RoundToInt(15f * s), TextAnchor.MiddleLeft));
            GUI.Label(new Rect(r.x + 10f * s, r.y + 26f * s, r.width - 14f * s, 22f * s),
                name + cd, MakeStyle(Mathf.RoundToInt(13f * s), TextAnchor.MiddleLeft));
            GUI.color = Color.white;
        }

        private GUIStyle MakeStyle(int size, TextAnchor anchor)
        {
            var st = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                alignment = anchor,
                richText = false
            };
            st.normal.textColor = Color.white;
            st.wordWrap = false;
            return st;
        }
    }
}
