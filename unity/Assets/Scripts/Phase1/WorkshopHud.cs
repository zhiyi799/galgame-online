// WorkshopHud — 星律工坊 HUD（Phase 1 / P1-C + P1-E 建造 HUD）。
// IMGUI 绘制：右下角工坊面板（设施列表、电力/产量、收取按钮）、
// 设施 3D 标注（投影到屏幕）、建造模式提示条。
// 1280×720 与 1920×1080 均按 uiScale 缩放。
using UnityEngine;
using TideSong;

namespace AstralBloom
{
    public class WorkshopHud : MonoBehaviour
    {
        private Font _font;
        private bool _fontTried;

        private float Scale { get { return Mathf.Clamp(Screen.height / 900f, 0.68f, 2.4f); } }

        private void OnGUI()
        {
            var wm = WorkshopManager.Instance;
            if (wm == null) return;
            if (_font == null && !_fontTried)
            {
                _fontTried = true;
                _font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            }
            if (_font != null && GUI.skin.font != _font) GUI.skin.font = _font;

            float s = Scale;
            DrawPanel(s);
            DrawFacilityLabels(s);
            if (wm.BuildMode) DrawBuildHints(s);
        }

        private void DrawPanel(float s)
        {
            var wm = WorkshopManager.Instance;
            float w = 340f * s, h = 260f * s;
            var r = new Rect(Screen.width - w - 16f * s, Screen.height - h - 16f * s, w, h);
            GUI.color = new Color(0.04f, 0.08f, 0.14f, 0.82f);
            GUI.DrawTexture(r, ArtUtil.White);
            GUI.color = new Color(0.55f, 0.9f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(r.x, r.y, 3f * s, r.height), ArtUtil.White);

            float x = r.x + 12f * s;
            float y = r.y + 8f * s;
            GUI.color = new Color(0.94f, 0.97f, 1f);
            GUI.Label(new Rect(x, y, w - 24f * s, 24f * s), "星律工坊 · 苍穹工业区",
                MakeStyle(Mathf.RoundToInt(17f * s), TextAnchor.MiddleLeft));
            y += 28f * s;

            // 电力条（容量 = 核心 100 + 电力塔 25/座）
            float pw = w - 24f * s;
            GUI.color = new Color(0.02f, 0.04f, 0.07f, 0.85f);
            GUI.DrawTexture(new Rect(x, y, pw, 10f * s), ArtUtil.White);
            float pk = Mathf.Clamp01((float)wm.PowerUsed / wm.PowerCapacity);
            GUI.color = wm.Overloaded ? new Color(0.95f, 0.3f, 0.3f) : new Color(0.45f, 0.75f, 1f);
            GUI.DrawTexture(new Rect(x + 1f, y + 1f, (pw - 2f) * pk, 10f * s - 2f), ArtUtil.White);
            GUI.color = new Color(0.94f, 0.97f, 1f);
            GUI.Label(new Rect(x, y + 12f * s, pw, 20f * s),
                "电力 " + wm.PowerUsed + " / " + wm.PowerCapacity,
                MakeStyle(Mathf.RoundToInt(14f * s), TextAnchor.MiddleLeft));
            y += 34f * s;

            // 状态
            string status;
            Color statusColor;
            if (wm.Overloaded)
            {
                status = "电力不足，降速 45% 运行（加电力塔或拆设施）";
                statusColor = new Color(1f, 0.85f, 0.4f);
            }
            else if (!wm.LineComplete)
            {
                status = "生产线不完整（需采集器+输送带+加工站+仓库）";
                statusColor = new Color(1f, 0.85f, 0.4f);
            }
            else if (wm.WarehousePlates >= WorkshopManager.WarehouseCap)
            {
                status = "运行中 · 仓库已满（请收取）";
                statusColor = new Color(1f, 0.85f, 0.4f);
            }
            else
            {
                status = "运行中 · 1 板 / 6 秒";
                statusColor = new Color(0.55f, 0.95f, 0.7f);
            }
            GUI.color = statusColor;
            GUI.Label(new Rect(x, y, pw, 20f * s), status,
                MakeStyle(Mathf.RoundToInt(14f * s), TextAnchor.MiddleLeft));
            y += 24f * s;

            // 产量（聚合：在途矿石 / 加工站输入矿石，上限按设施数折算）
            GUI.color = new Color(0.94f, 0.97f, 1f);
            GUI.Label(new Rect(x, y, pw, 20f * s),
                "输送带矿石 " + Mathf.FloorToInt(wm.BeltOre) + "/" + wm.BeltOreCap +
                "　加工站矿石 " + Mathf.FloorToInt(wm.ProcOre) + "/" + wm.ProcOreCap,
                MakeStyle(Mathf.RoundToInt(14f * s), TextAnchor.MiddleLeft));
            y += 22f * s;
            GUI.Label(new Rect(x, y, pw, 20f * s),
                "仓库星律板 " + wm.WarehousePlates + "/" + WorkshopManager.WarehouseCap +
                "　星律板（已收取）×" + wm.StarPlates,
                MakeStyle(Mathf.RoundToInt(14f * s), TextAnchor.MiddleLeft));
            y += 24f * s;

            // 设施列表（两行：单行会超出面板宽度被裁切，2026-11-18 1080p
            // 截图证实「仓库(0)」的右括号被切掉；720p 按比例同样超长）
            var parts = new System.Collections.Generic.List<string>();
            foreach (var f in wm.Facilities)
                parts.Add(WorkshopManager.TypeName(f.type) + "(" + WorkshopManager.PowerText(f.type) + ")");
            GUI.color = new Color(0.79f, 0.83f, 0.91f);
            int half = (parts.Count + 1) / 2;
            GUI.Label(new Rect(x, y, pw, 20f * s), "设施：" + string.Join(" · ", parts.GetRange(0, half).ToArray()),
                MakeStyle(Mathf.RoundToInt(13f * s), TextAnchor.MiddleLeft));
            y += 22f * s;
            if (parts.Count > half)
            {
                GUI.Label(new Rect(x, y, pw, 20f * s), string.Join(" · ", parts.GetRange(half, parts.Count - half).ToArray()),
                    MakeStyle(Mathf.RoundToInt(13f * s), TextAnchor.MiddleLeft));
                y += 22f * s;
            }
            else y += 2f * s;

            // 收取按钮
            GUI.color = Color.white;
            if (GUI.Button(new Rect(x, y, pw, 32f * s),
                wm.WarehousePlates > 0 ? "收取星律板 ×" + wm.WarehousePlates : "收取星律板（仓库为空）"))
            {
                wm.CollectPlates();
            }
            GUI.color = Color.white;

            // 建造模式入口提示
            if (!wm.BuildMode)
            {
                GUI.color = new Color(0.79f, 0.83f, 0.91f);
                GUI.Label(new Rect(x, y + 36f * s, pw, 18f * s), "按 B 进入建造模式（放置/旋转/拆除）",
                    MakeStyle(Mathf.RoundToInt(12f * s), TextAnchor.MiddleLeft));
            }
            GUI.color = Color.white;
        }

        private void DrawFacilityLabels(float s)
        {
            var wm = WorkshopManager.Instance;
            var cam = Camera.main;
            if (cam == null || wm.Facilities == null) return;
            foreach (var f in wm.Facilities)
            {
                float dist = Vector3.Distance(cam.transform.position, f.pos);
                if (dist > 500f) continue;
                Vector3 sp = cam.WorldToScreenPoint(f.pos + Vector3.up * 4.5f);
                if (sp.z < 0f) continue;
                string text = WorkshopManager.TypeName(f.type) +
                    "（" + WorkshopManager.PowerText(f.type) + "）";
                var style = MakeStyle(Mathf.RoundToInt(14f * s), TextAnchor.MiddleCenter);
                float w = 130f * s, h = 20f * s;
                var r = new Rect(sp.x - w * 0.5f, Screen.height - sp.y - h * 0.5f, w, h);
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(r, ArtUtil.White);
                GUI.color = f == wm.Selected
                    ? new Color(0.85f, 0.80f, 0.67f)
                    : new Color(0.94f, 0.97f, 1f);
                GUI.Label(r, text, style);
            }
            GUI.color = Color.white;
        }

        private void DrawBuildHints(float s)
        {
            var wm = WorkshopManager.Instance;
            float w = 760f * s, h = 30f * s;
            var r = new Rect(Screen.width * 0.5f - w * 0.5f, 66f * s, w, h);
            GUI.color = new Color(0.04f, 0.08f, 0.14f, 0.85f);
            GUI.DrawTexture(r, ArtUtil.White);
            string ghost = wm.GhostType >= 0
                ? "待放置：" + WorkshopManager.TypeName(wm.GhostType) +
                  (wm.GhostValid ? "（可放置）" : "（不可放置：超出用地或重叠）")
                : (wm.Selected != null
                    ? "已选中：" + WorkshopManager.TypeName(wm.Selected.type) + "（R 旋转，X 拆除）"
                    : "点空地选择设施，或按数字键选类型放置");
            GUI.color = new Color(0.94f, 0.97f, 1f);
            GUI.Label(r, "建造模式 [B 退出]　1 采集器(20)　2 输送带(5)　3 加工站(40)　4 仓库(0)　5 电力塔(+25)　" + ghost,
                MakeStyle(Mathf.RoundToInt(14f * s), TextAnchor.MiddleCenter));
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
