// InventoryScreen — items grouped by kind (character / item), name + count +
// level for characters, total count header, empty state.
using UnityEngine;
using TMPro;
using GalgameOnline.Api;
using GalgameOnline.Core;
using GalgameOnline.UI;

namespace GalgameOnline.Screens
{
    public class InventoryScreen : MonoBehaviour
    {
        private RectTransform _content;
        private TextMeshProUGUI _totalLabel;

        private void Start()
        {
            var canvas = FindObjectOfType<Canvas>();
            var (root, _) = UiFactory.ScreenBase(canvas.transform, "背包",
                () => GameManager.Instance.GoTo(GameManager.SceneMainMenu));

            _totalLabel = UiFactory.Label(root.transform, "Total", "加载中…", 20,
                TextAlignmentOptions.Left, new Color(1f, 0.85f, 0.4f, 1f));
            _totalLabel.rectTransform.anchorMin = new Vector2(0, 1);
            _totalLabel.rectTransform.anchorMax = new Vector2(0, 1);
            UiFactory.Place(_totalLabel.rectTransform, 20, -76, 800, 30);

            var (scroll, content) = UiFactory.ScrollView(root.transform, "List",
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0));
            var sRt = scroll.GetComponent<RectTransform>();
            sRt.offsetMin = new Vector2(16, 16);
            sRt.offsetMax = new Vector2(-16, -116);
            _content = content;

            StartCoroutine(GameManager.Instance.Api.GetInventory(r =>
            {
                if (!r.Success)
                {
                    if (r.Failure == ApiFailureKind.Unauthorized) GameManager.Instance.HandleUnauthorized();
                    else Toast.Show(r.ErrorMessage);
                    return;
                }
                var items = r.Data.items ?? new InventoryItemDto[0];
                int totalKinds = items.Length;
                int totalPieces = 0;
                foreach (var it in items) totalPieces += it.count;
                _totalLabel.text = $"共 {totalKinds} 种 · {totalPieces} 件";

                if (items.Length == 0)
                {
                    var e = UiFactory.Label(_content, "Empty", "背包是空的，去抽卡吧。", 20,
                        TextAlignmentOptions.Center, new Color(0.7f, 0.75f, 0.85f, 1f));
                    e.rectTransform.sizeDelta = new Vector2(0, 30);
                    return;
                }

                AddGroup("角色", items, "character");
                AddGroup("道具", items, "item");
                // Any other kinds fall under their own header.
                var seen = new System.Collections.Generic.HashSet<string> { "character", "item" };
                foreach (var it in items)
                {
                    if (!seen.Contains(it.kind))
                    {
                        AddGroup(it.kind, items, it.kind);
                        seen.Add(it.kind);
                    }
                }
            }));
        }

        private void AddGroup(string title, InventoryItemDto[] items, string kind)
        {
            bool any = false;
            foreach (var it in items) if (it.kind == kind) { any = true; break; }
            if (!any) return;

            var header = UiFactory.Label(_content, "Group", "—— " + title + " ——", 20,
                TextAlignmentOptions.Left, new Color(0.8f, 0.85f, 1f, 1f), true);
            header.rectTransform.sizeDelta = new Vector2(0, 32);

            foreach (var it in items)
            {
                if (it.kind != kind) continue;
                string line = ItemNames.Get(it.itemId) + "　x" + it.count +
                    (kind == "character" ? "　Lv." + it.level : "");
                var l = UiFactory.Label(_content, "Item", line, 20,
                    TextAlignmentOptions.Left, Color.white);
                l.rectTransform.sizeDelta = new Vector2(0, 30);
            }
        }
    }
}
