using UnityEngine;

namespace TideSong
{
    /// <summary>面板共用的字体与样式。</summary>
    public static class UIStyle
    {
        static Font _font;
        static bool _tried;

        public static Font Font
        {
            get
            {
                if (!_tried)
                {
                    _tried = true;
                    _font = Load();
                }
                return _font;
            }
        }

        static Font Load()
        {
            string[] candidates = new string[]
            {
                "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "SimSun",
                "PingFang SC", "Hiragino Sans GB", "Heiti SC", "STHeiti",
                "Noto Sans CJK SC", "Noto Sans SC", "Source Han Sans SC",
                "Droid Sans Fallback", "WenQuanYi Micro Hei", "Arial Unicode MS"
            };
            var installed = Font.GetOSInstalledFontNames();
            if (installed == null) return null;
            for (int c = 0; c < candidates.Length; c++)
                for (int i = 0; i < installed.Length; i++)
                    if (string.Equals(installed[i], candidates[c], System.StringComparison.OrdinalIgnoreCase))
                        return Font.CreateDynamicFontFromOSFont(installed[i], 28);
            for (int c = 0; c < candidates.Length; c++)
                for (int i = 0; i < installed.Length; i++)
                    if (installed[i] != null && installed[i].IndexOf(candidates[c], System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return Font.CreateDynamicFontFromOSFont(installed[i], 28);
            return null;
        }

        static readonly System.Collections.Generic.Dictionary<string, GUIStyle> cache =
            new System.Collections.Generic.Dictionary<string, GUIStyle>();

        public static GUIStyle Label(float size, Color c, TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = false)
        {
            int px = Mathf.RoundToInt(size);
            string key = "L" + px + "_" + (int)anchor + "_" + (wrap ? 1 : 0) + "_" + ColorUtility.ToHtmlStringRGBA(c);
            GUIStyle st;
            if (cache.TryGetValue(key, out st) && st != null) return st;

            st = new GUIStyle(GUI.skin.label);
            st.font = Font;
            st.fontSize = px;
            st.alignment = anchor;
            st.wordWrap = wrap;
            st.richText = false;
            st.normal.textColor = c;
            cache[key] = st;
            return st;
        }

        public static GUIStyle Button(float size)
        {
            int px = Mathf.RoundToInt(size);
            string key = "B" + px;
            GUIStyle st;
            if (cache.TryGetValue(key, out st) && st != null) return st;

            st = new GUIStyle(GUI.skin.button);
            st.font = Font;
            st.fontSize = px;
            st.alignment = TextAnchor.MiddleCenter;
            st.normal.textColor = new Color(0.92f, 0.96f, 1f);
            st.hover.textColor = Color.white;
            cache[key] = st;
            return st;
        }
    }

    /// <summary>角色 / 队伍面板（C 键）。</summary>
    public class TeamPanel : MonoBehaviour
    {
        public bool open;
        GameManager gm;
        int hoverIndex = -1;

        public void Init(GameManager manager) { gm = manager; }
        public void Toggle() { open = !open; Sfx.Play2D(SfxId.UI); }

        void OnGUI()
        {
            if (!open || TeamManager.Instance == null || PlayerCharacter.Instance == null) return;

            float s = Mathf.Clamp(Screen.height / 900f, 0.7f, 2f);
            var white = ArtUtil.White;
            var roster = TeamManager.Instance.Roster;

            GUI.color = new Color(0.01f, 0.03f, 0.06f, 0.90f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);

            float w = Mathf.Min(Screen.width * 0.90f, 1000f * s);
            float h = Mathf.Min(Screen.height * 0.86f, 720f * s);
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            GUI.color = new Color(0.05f, 0.10f, 0.17f, 0.98f);
            GUI.DrawTexture(r, white);
            GUI.color = new Color(0.4f, 0.85f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f * s), white);

            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 26f * s, r.y + 14f * s, r.width - 52f * s, 40f * s), "队伍 · 共鸣者", UIStyle.Label(30f * s, Color.white));
            if (GUI.Button(new Rect(r.x + r.width - 100f * s, r.y + 14f * s, 76f * s, 34f * s), "关闭", UIStyle.Button(15f * s)))
            {
                open = false;
                if (gm != null) gm.SetPaused(false);
                return;
            }
            GUI.Label(new Rect(r.x + 26f * s, r.y + 52f * s, r.width - 52f * s, 26f * s),
                "按 1 / 2 / 3 / 4 切换 · 切人消耗 20 点协奏能量触发「变奏」· C 关闭", UIStyle.Label(15f * s, new Color(0.7f, 0.85f, 1f)));

            int n = roster.Count;
            float cardH = (h - 140f * s) / Mathf.Max(1, n);
            float cardW = w - 52f * s;

            for (int i = 0; i < n; i++)
            {
                var st = roster[i];
                var def = st.def;
                float cy = r.y + 92f * s + i * cardH;
                var card = new Rect(r.x + 26f * s, cy, cardW, cardH - 10f * s);

                bool active = TeamManager.Instance.activeIndex == i;
                bool locked = !st.unlocked;

                GUI.color = active ? new Color(0.12f, 0.24f, 0.36f, 0.95f) : new Color(0.07f, 0.13f, 0.20f, 0.85f);
                GUI.DrawTexture(card, white);
                GUI.color = locked ? new Color(0.35f, 0.35f, 0.40f) : def.elementColor;
                GUI.DrawTexture(new Rect(card.x, card.y, 4f * s, card.height), white);

                // 头像
                var av = new Rect(card.x + 18f * s, card.y + 18f * s, 62f * s, 62f * s);
                GUI.color = new Color(0.10f, 0.16f, 0.24f, 1f);
                GUI.DrawTexture(av, ArtUtil.Dot);
                GUI.color = locked ? new Color(0.3f, 0.3f, 0.34f) : def.elementColor;
                GUI.DrawTexture(new Rect(av.x + 4f * s, av.y + 4f * s, av.width - 8f * s, av.height - 8f * s), ArtUtil.Dot);
                GUI.color = Color.black;
                GUI.Label(new Rect(av.x, av.y + 18f * s, av.width, 30f * s), def.name.Substring(0, 1), UIStyle.Label(28f * s, new Color(0.06f, 0.09f, 0.14f), TextAnchor.MiddleCenter));

                // 名字 / 定位
                GUI.color = Color.white;
                GUI.Label(new Rect(card.x + 96f * s, card.y + 10f * s, 300f * s, 30f * s),
                    def.name + " · " + def.title + (active ? "   [ 出战中 ]" : ""),
                    UIStyle.Label(22f * s, active ? new Color(0.75f, 1f, 1f) : new Color(0.95f, 0.97f, 1f)));

                GUI.Label(new Rect(card.x + 96f * s, card.y + 38f * s, 460f * s, 24f * s),
                    def.ElementName + " · " + def.WeaponName + "  |  Lv." + PlayerCharacter.Instance.Level + "  |  生命 " + Mathf.RoundToInt(st.maxHp),
                    UIStyle.Label(15f * s, new Color(0.72f, 0.84f, 1f)));

                // 血条 / 能量
                float barX = card.x + 96f * s;
                float barY = card.y + 66f * s;
                DrawBar(new Rect(barX, barY, 190f * s, 10f * s), st.hp / Mathf.Max(1f, st.maxHp), new Color(0.9f, 0.35f, 0.38f));
                DrawBar(new Rect(barX + 200f * s, barY, 120f * s, 10f * s), st.energy / 100f, new Color(0.45f, 0.8f, 1f));

                // 技能说明（右侧）
                float tx = card.x + 560f * s;
                float tw = cardW - 580f * s;
                if (tw > 120f * s)
                {
                    GUI.Label(new Rect(tx, card.y + 8f * s, tw, 22f * s), "E  " + def.skillName, UIStyle.Label(15f * s, new Color(0.85f, 0.95f, 1f)));
                    GUI.Label(new Rect(tx, card.y + 30f * s, tw, 36f * s), def.skillDesc, UIStyle.Label(12f * s, new Color(0.68f, 0.8f, 0.92f), TextAnchor.UpperLeft, true));
                    GUI.Label(new Rect(tx, card.y + 68f * s, tw, 22f * s), "Q  " + def.burstName, UIStyle.Label(15f * s, new Color(1f, 0.9f, 0.6f)));
                    GUI.Label(new Rect(tx, card.y + 90f * s, tw, 36f * s), def.burstDesc, UIStyle.Label(12f * s, new Color(0.85f, 0.78f, 0.6f), TextAnchor.UpperLeft, true));
                }

                // 锁定
                if (locked)
                {
                    GUI.color = new Color(0f, 0f, 0f, 0.55f);
                    GUI.DrawTexture(card, white);
                    GUI.color = new Color(0.9f, 0.9f, 1f);
                    GUI.Label(new Rect(card.x, card.y + card.height * 0.5f - 20f * s, card.width, 40f * s),
                        "尚未共鸣 · 收集 " + ResonatorLibrary.YuanUnlockEchoes + " 件声骸解锁", UIStyle.Label(20f * s, new Color(1f, 0.85f, 0.5f), TextAnchor.MiddleCenter));
                }
                else if (!active)
                {
                    if (GUI.Button(new Rect(card.x + cardW - 110f * s, card.y + card.height - 40f * s, 96f * s, 30f * s), "上 场", UIStyle.Button(15f * s)))
                    {
                        TeamManager.Instance.SwitchTo(i);
                        Sfx.Play2D(SfxId.UI);
                    }
                }

                GUI.color = Color.white;
            }

            GUI.Label(new Rect(r.x + 26f * s, r.y + h - 40f * s, r.width - 52f * s, 26f * s),
                "共鸣回路：" + (TeamManager.Instance.Active != null ? TeamManager.Instance.Active.def.passiveName + " — " + TeamManager.Instance.Active.def.passiveDesc : ""),
                UIStyle.Label(13f * s, new Color(0.6f, 0.75f, 0.9f)));
        }

        void DrawBar(Rect rr, float k, Color c)
        {
            GUI.color = new Color(0.02f, 0.04f, 0.07f, 0.9f);
            GUI.DrawTexture(rr, ArtUtil.White);
            GUI.color = c;
            GUI.DrawTexture(new Rect(rr.x + 1f, rr.y + 1f, (rr.width - 2f) * Mathf.Clamp01(k), rr.height - 2f), ArtUtil.White);
            GUI.color = Color.white;
        }
    }

    /// <summary>声骸面板（B 键）：装备、升级、分解。</summary>
    public class EchoPanel : MonoBehaviour
    {
        public bool open;
        GameManager gm;
        int slot = 0;
        EchoInstance selected;
        Vector2 scroll;
        string hint = "";

        public void Init(GameManager manager) { gm = manager; }
        public void Toggle() { open = !open; Sfx.Play2D(SfxId.UI); }

        void OnGUI()
        {
            if (!open || EchoSystem.Instance == null) return;
            var sys = EchoSystem.Instance;

            float s = Mathf.Clamp(Screen.height / 900f, 0.7f, 2f);
            var white = ArtUtil.White;

            GUI.color = new Color(0.01f, 0.03f, 0.06f, 0.92f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);

            float w = Mathf.Min(Screen.width * 0.92f, 1100f * s);
            float h = Mathf.Min(Screen.height * 0.88f, 760f * s);
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            GUI.color = new Color(0.05f, 0.10f, 0.17f, 0.98f);
            GUI.DrawTexture(r, white);
            GUI.color = new Color(0.55f, 0.75f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f * s), white);

            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 24f * s, r.y + 12f * s, 600f * s, 40f * s), "声骸 · 回响收藏", UIStyle.Label(28f * s, Color.white));
            if (GUI.Button(new Rect(r.x + r.width - 100f * s, r.y + 12f * s, 76f * s, 34f * s), "关闭", UIStyle.Button(15f * s)))
            {
                open = false;
                if (gm != null) gm.SetPaused(false);
                return;
            }
            GUI.Label(new Rect(r.x + 24f * s, r.y + 50f * s, r.width - 48f * s, 26f * s),
                "cost " + sys.UsedCost + " / " + EchoSystem.MaxCost + "   粉尘 " + sys.dust + "   B 关闭",
                UIStyle.Label(15f * s, new Color(0.72f, 0.86f, 1f)));

            // -------- 左侧五个槽 --------
            float leftW = w * 0.42f;
            float slotH = (h - 170f * s) / EchoSystem.SlotCount;
            for (int i = 0; i < EchoSystem.SlotCount; i++)
            {
                var e = sys.equipped[i];
                var rr = new Rect(r.x + 24f * s, r.y + 90f * s + i * slotH, leftW - 20f * s, slotH - 8f * s);
                bool sel = slot == i;

                GUI.color = sel ? new Color(0.15f, 0.28f, 0.42f, 0.95f) : new Color(0.08f, 0.14f, 0.21f, 0.9f);
                GUI.DrawTexture(rr, white);
                GUI.color = e != null ? EchoLibrary.SetColor(e.Def.set) : new Color(0.3f, 0.35f, 0.42f);
                GUI.DrawTexture(new Rect(rr.x, rr.y, 4f * s, rr.height), white);

                if (GUI.Button(new Rect(rr.x, rr.y, rr.width, rr.height), GUIContent.none, GUIStyle.none)) { slot = i; Sfx.Play2D(SfxId.UI); }

                GUI.color = Color.white;
                string head = (i + 1) + " 号槽";
                if (e == null)
                {
                    GUI.Label(new Rect(rr.x + 14f * s, rr.y + rr.height * 0.5f - 14f * s, rr.width - 20f * s, 28f * s),
                        head + "    空", UIStyle.Label(16f * s, new Color(0.55f, 0.65f, 0.75f)));
                }
                else
                {
                    GUI.Label(new Rect(rr.x + 14f * s, rr.y + 6f * s, rr.width - 24f * s, 26f * s),
                        head + "    " + e.Def.name, UIStyle.Label(16f * s, Color.white));
                    GUI.Label(new Rect(rr.x + 14f * s, rr.y + 30f * s, rr.width - 24f * s, 22f * s),
                        "cost " + e.Cost + "   " + e.star + "★  +" + e.level + "   " + EchoLibrary.SetName(e.Def.set),
                        UIStyle.Label(13f * s, new Color(0.7f, 0.85f, 1f)));
                    GUI.Label(new Rect(rr.x + 14f * s, rr.y + 52f * s, rr.width - 24f * s, 22f * s),
                        EchoLibrary.MainName((EchoMainStat)e.mainType) + " +" + e.MainValue.ToString("0.0") + "%",
                        UIStyle.Label(13f * s, new Color(1f, 0.88f, 0.55f)));
                }
                GUI.color = Color.white;
            }

            // -------- 右侧背包 --------
            float rx = r.x + leftW + 16f * s;
            float rw = w - leftW - 40f * s;

            GUI.Label(new Rect(rx, r.y + 88f * s, rw, 26f * s), "背包（" + sys.bag.Count + "）", UIStyle.Label(17f * s, new Color(0.85f, 0.95f, 1f)));

            var listRect = new Rect(rx, r.y + 116f * s, rw, h - 116f * s - 230f * s);
            float rowH = 46f * s;
            scroll = GUI.BeginScrollView(listRect, scroll, new Rect(0, 0, rw - 20f * s, Mathf.Max(listRect.height, sys.bag.Count * rowH + 6f)));
            for (int i = 0; i < sys.bag.Count; i++)
            {
                var e = sys.bag[i];
                var row = new Rect(0, i * rowH, rw - 24f * s, rowH - 5f * s);
                bool sel = selected == e;
                GUI.color = sel ? new Color(0.16f, 0.28f, 0.4f, 0.95f) : new Color(0.09f, 0.14f, 0.21f, 0.9f);
                GUI.DrawTexture(row, ArtUtil.White);
                GUI.color = EchoLibrary.SetColor(e.Def.set);
                GUI.DrawTexture(new Rect(row.x, row.y, 3f * s, row.height), ArtUtil.White);

                GUI.color = Color.white;
                GUI.Label(new Rect(row.x + 10f * s, row.y + 2f * s, row.width - 20f * s, 22f * s),
                    e.Def.name + "   cost " + e.Cost + "   " + e.star + "★ +" + e.level,
                    UIStyle.Label(14f * s, Color.white));
                GUI.Label(new Rect(row.x + 10f * s, row.y + 22f * s, row.width - 20f * s, 20f * s),
                    EchoLibrary.SetName(e.Def.set) + "  ·  " + EchoLibrary.MainName((EchoMainStat)e.mainType) + " +" + e.MainValue.ToString("0.0") + "%",
                    UIStyle.Label(11f * s, new Color(0.68f, 0.82f, 0.95f)));

                if (GUI.Button(row, GUIContent.none, GUIStyle.none)) { selected = e; Sfx.Play2D(SfxId.UI); }
            }
            GUI.EndScrollView();

            // -------- 底部详情与操作 --------
            float by = r.y + h - 220f * s;
            var box = new Rect(rx, by, rw, 210f * s);
            GUI.color = new Color(0.07f, 0.12f, 0.19f, 0.95f);
            GUI.DrawTexture(box, white);

            GUI.color = Color.white;
            if (selected == null)
            {
                GUI.Label(new Rect(box.x + 14f * s, box.y + 12f * s, box.width - 28f * s, 26f * s), "选一件声骸看详情", UIStyle.Label(15f * s, new Color(0.6f, 0.72f, 0.85f)));
            }
            else
            {
                var e = selected;
                GUI.Label(new Rect(box.x + 14f * s, box.y + 8f * s, box.width - 28f * s, 26f * s),
                    e.Def.name + "   " + e.star + "★ +" + e.level + " / " + e.MaxLevel,
                    UIStyle.Label(17f * s, Color.white));
                GUI.Label(new Rect(box.x + 14f * s, box.y + 34f * s, box.width - 28f * s, 22f * s),
                    EchoLibrary.SetName(e.Def.set) + " · " + e.Def.skillName,
                    UIStyle.Label(13f * s, EchoLibrary.SetColor(e.Def.set)));

                string subs = "";
                for (int k = 0; k < e.subTypes.Count && k < e.subValues.Count; k++)
                {
                    if (k > 0) subs += "   ";
                    subs += EchoLibrary.MainName((EchoMainStat)e.subTypes[k]) + " +" + e.subValues[k].ToString("0.0") + "%";
                }
                GUI.Label(new Rect(box.x + 14f * s, box.y + 58f * s, box.width - 28f * s, 40f * s), subs,
                    UIStyle.Label(11f * s, new Color(0.72f, 0.86f, 1f), TextAnchor.UpperLeft, true));

                float bw = (box.width - 48f * s) / 3f;
                float bx = box.x + 14f * s;
                float byy = box.y + 118f * s;
                if (GUI.Button(new Rect(bx, byy, bw, 40f * s), "装备到 " + (slot + 1) + " 号槽", UIStyle.Button(14f * s)))
                {
                    if (sys.Equip(e, slot)) { selected = sys.equipped[slot]; hint = "已装备"; }
                }
                if (GUI.Button(new Rect(bx + bw + 10f * s, byy, bw, 40f * s), "升级 (" + sys.UpgradeCost(e) + " 粉尘)", UIStyle.Button(14f * s)))
                {
                    sys.Upgrade(e);
                }
                if (GUI.Button(new Rect(bx + (bw + 10f * s) * 2f, byy, bw, 40f * s), "分解", UIStyle.Button(14f * s)))
                {
                    sys.Salvage(e);
                    selected = null;
                }

                GUI.Label(new Rect(box.x + 14f * s, box.y + 164f * s, box.width - 28f * s, 40f * s),
                    "声骸技能：装备后按 R 释放，冷却 " + EchoSystem.EchoSkillCooldown + " 秒",
                    UIStyle.Label(12f * s, new Color(0.6f, 0.74f, 0.88f), TextAnchor.UpperLeft, true));
            }

            // 套装汇总
            GUI.Label(new Rect(r.x + 24f * s, r.y + h - 34f * s, w - 48f * s, 26f * s), hint, UIStyle.Label(13f * s, new Color(1f, 0.9f, 0.6f)));
            GUI.color = Color.white;
        }
    }
}
