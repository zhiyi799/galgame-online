// EchoPanel — 星律回响面板（Phase 1 / P1-B）。
// Inventory 场景内的一页：队员选择（4 人）、2 个回响槽（强化/卸下）、
// 回响背包（装备到槽位 1/2、强化）。UGUI via UiFactory。
// 打开时若无 EchoManager 则新建一个（加载本地存档）。
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GalgameOnline.Core;
using GalgameOnline.UI;

namespace AstralBloom
{
    public class EchoPanel : MonoBehaviour
    {
        private GameObject _root;
        private RectTransform _listContent;
        private TextMeshProUGUI _platesLabel;
        private TextMeshProUGUI[] _slotLabels = new TextMeshProUGUI[2];
        private Button[] _memberButtons = new Button[4];
        private int _selectedMember;

        public static EchoPanel Open()
        {
            EnsureManager();
            var go = new GameObject("EchoPanel");
            var p = go.AddComponent<EchoPanel>();
            p.Build();
            return p;
        }

        private static void EnsureManager()
        {
            if (EchoManager.Instance == null)
            {
                var go = new GameObject("EchoManager");
                go.AddComponent<EchoManager>();
            }
        }

        private void Build()
        {
            var canvas = UiFactory.SceneCanvas();
            _root = UiFactory.Panel(canvas.transform, "EchoPanelRoot",
                Vector2.zero, Vector2.one, Vector2.zero,
                new Color(0.05f, 0.10f, 0.25f, 0.97f));

            var title = UiFactory.Label(_root.transform, "Title", "星律回响", 28,
                TextAlignmentOptions.Left, UiFactory.MoonWhite, true);
            UiFactory.Place(title.rectTransform, 20, -16, 400, 40);

            _platesLabel = UiFactory.Label(_root.transform, "Plates", "", 20,
                TextAlignmentOptions.Left, UiFactory.PaleGold);
            UiFactory.Place(_platesLabel.rectTransform, 20, -60, 500, 30);

            var closeBtn = UiFactory.Button(_root.transform, "Close", "× 关闭",
                () => { Destroy(_root); Destroy(gameObject); }, new Vector2(140, 44));
            var cRt = closeBtn.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(1, 1);
            cRt.anchorMax = new Vector2(1, 1);
            cRt.pivot = new Vector2(1, 1);
            cRt.anchoredPosition = new Vector2(-20, -16);

            // 队员选择
            for (int i = 0; i < 4; i++)
            {
                int m = i;
                var b = UiFactory.Button(_root.transform, "Member" + i, SquadData.Roster[i].name,
                    () => { _selectedMember = m; Refresh(); }, new Vector2(180, 44));
                var rt = b.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(20 + i * 192, -116);
                _memberButtons[i] = b;
            }

            // 2 个回响槽
            for (int slot = 0; slot < 2; slot++)
            {
                int s = slot;
                var row = UiFactory.Panel(_root.transform, "SlotRow" + slot,
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(900, 44),
                    UiFactory.Glass(0.55f));
                var rRt = row.GetComponent<RectTransform>();
                rRt.pivot = new Vector2(0, 1);
                rRt.anchoredPosition = new Vector2(20, -176 - slot * 52);

                _slotLabels[slot] = UiFactory.Label(row.transform, "SlotText", "", 20,
                    TextAlignmentOptions.Left, UiFactory.MoonWhite);
                UiFactory.Place(_slotLabels[slot].rectTransform, 8, -4, 560, 36);

                var upBtn = UiFactory.Button(row.transform, "Upgrade", "强化 +1 · 星律板×2",
                    () => OnUpgradeSlot(s), new Vector2(220, 36));
                PlaceRight(upBtn, -140, -4);
                var offBtn = UiFactory.Button(row.transform, "Unequip", "卸下",
                    () => OnUnequipSlot(s), new Vector2(110, 36));
                PlaceRight(offBtn, -16, -4);
            }

            var listTitle = UiFactory.Label(_root.transform, "ListTitle",
                "回响背包（装备到当前队员）", 20,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            UiFactory.Place(listTitle.rectTransform, 20, -292, 600, 30);

            var (scroll, content) = UiFactory.ScrollView(_root.transform, "EchoList",
                new Vector2(0, 0), new Vector2(1, 1), Vector2.zero);
            var sRt = scroll.GetComponent<RectTransform>();
            sRt.offsetMin = new Vector2(16, 16);
            sRt.offsetMax = new Vector2(-16, -330);
            _listContent = content;

            Refresh();
        }

        private static void PlaceRight(Button btn, float x, float y)
        {
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(x, y);
        }

        public void Refresh()
        {
            var em = EchoManager.Instance;
            if (em == null || _root == null) return;

            _platesLabel.text = "星律板 ×" + em.StarPlates +
                "　（工坊生产，强化 +1 级消耗 ×2）";

            for (int i = 0; i < 4; i++)
            {
                var img = _memberButtons[i].GetComponent<Image>();
                if (img != null)
                    img.color = i == _selectedMember ? UiFactory.StarryBlue : UiFactory.ClassicUltramarine;
            }

            for (int slot = 0; slot < 2; slot++)
            {
                var e = em.EquippedEcho(_selectedMember, slot);
                _slotLabels[slot].text = e == null
                    ? "槽位 " + (slot + 1) + "：空"
                    : "槽位 " + (slot + 1) + "：" + EchoManager.DefName(e.defId) +
                      " Lv." + e.level + "（" + EchoManager.DefStatText(e.defId, e.level) + "）";
            }

            UiFactory.ClearChildren(_listContent);
            var data = em.Data;
            if (data.inventory.Count == 0)
            {
                var e = UiFactory.Label(_listContent, "Empty",
                    "暂无回响：去探索场景击败暗星之影，有概率掉落。", 20,
                    TextAlignmentOptions.Left, UiFactory.SilverWhite);
                e.rectTransform.sizeDelta = new Vector2(0, 30);
                return;
            }

            foreach (var echo in data.inventory)
            {
                var inst = echo;
                var row = UiFactory.Panel(_listContent, "EchoRow" + inst.uid,
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 44),
                    UiFactory.Glass(0.55f));
                var rRt = row.GetComponent<RectTransform>();
                rRt.pivot = new Vector2(0, 1);

                var label = UiFactory.Label(row.transform, "EchoText",
                    EchoManager.DefName(inst.defId) + " Lv." + inst.level +
                    "（" + EchoManager.DefStatText(inst.defId, inst.level) + "）", 20,
                    TextAlignmentOptions.Left, UiFactory.MoonWhite);
                UiFactory.Place(label.rectTransform, 8, -4, 560, 36);

                var eq1 = UiFactory.Button(row.transform, "Equip1", "装备到槽位 1",
                    () => { em.Equip(inst.uid, _selectedMember, 0); Refresh(); },
                    new Vector2(170, 36));
                PlaceRight(eq1, -356, -4);
                var eq2 = UiFactory.Button(row.transform, "Equip2", "装备到槽位 2",
                    () => { em.Equip(inst.uid, _selectedMember, 1); Refresh(); },
                    new Vector2(170, 36));
                PlaceRight(eq2, -178, -4);
                var up = UiFactory.Button(row.transform, "Upgrade", "强化",
                    () => OnUpgrade(inst.uid), new Vector2(110, 36));
                PlaceRight(up, -56, -4);
                // 按钮宽度由 PlaceRight 的 anchoredPosition 定位；补齐 sizeDelta
                eq1.GetComponent<RectTransform>().sizeDelta = new Vector2(170, 36);
                eq2.GetComponent<RectTransform>().sizeDelta = new Vector2(170, 36);
                up.GetComponent<RectTransform>().sizeDelta = new Vector2(110, 36);
            }
        }

        private void OnUpgradeSlot(int slot)
        {
            var em = EchoManager.Instance;
            if (em == null) return;
            var e = em.EquippedEcho(_selectedMember, slot);
            if (e == null)
            {
                Toast.Show("该槽位未装备回响");
                return;
            }
            OnUpgrade(e.uid);
        }

        private void OnUpgrade(int uid)
        {
            var em = EchoManager.Instance;
            if (em == null) return;
            var e = em.FindEcho(uid);
            if (e == null) return;
            if (e.level >= EchoManager.MaxLevel)
            {
                Toast.Show("已达最高等级 Lv." + EchoManager.MaxLevel);
                return;
            }
            if (em.StarPlates < EchoManager.UpgradePlateCost)
            {
                Toast.Show("星律板不足（需要 ×" + EchoManager.UpgradePlateCost +
                    "，当前 ×" + em.StarPlates + "）");
                return;
            }
            if (em.Upgrade(uid))
                Toast.Show(EchoManager.DefName(e.defId) + " 强化至 Lv." + e.level);
            Refresh();
        }

        private void OnUnequipSlot(int slot)
        {
            var em = EchoManager.Instance;
            if (em == null) return;
            if (em.Unequip(_selectedMember, slot)) Refresh();
        }
    }
}
