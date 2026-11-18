// WorldMapHud — minimal in-world overlay for the WorldMap scene.
// Top bar: scene title + current region + player position / ground height.
// Bottom hint: controls. Back button returns to the main menu.
// Built at runtime with UiFactory, like the other screens.
using UnityEngine;
using TMPro;
using GalgameOnline.Core;
using GalgameOnline.UI;

namespace GalgameOnline.WorldMap
{
    public class WorldMapHud : MonoBehaviour
    {
        private TextMeshProUGUI _regionLabel;
        private TextMeshProUGUI _posLabel;
        private Transform _player;
        private float _timer;

        private void Start()
        {
            var canvas = UiFactory.SceneCanvas();
            if (canvas == null) return;

            var top = UiFactory.Panel(canvas.transform, "WorldMapTopBar",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 56),
                UiFactory.Glass(0.85f));
            var topRt = top.GetComponent<RectTransform>();
            topRt.pivot = new Vector2(0.5f, 1);
            topRt.anchoredPosition = Vector2.zero;

            var title = UiFactory.Label(top.transform, "Title", "世界地图 · ASTRAL BLOOM", 22,
                TextAlignmentOptions.Left, UiFactory.MoonWhite, true);
            UiFactory.Place(title.rectTransform, 16, -8, 420, 40);

            _regionLabel = UiFactory.Label(top.transform, "Region", "星环城", 20,
                TextAlignmentOptions.Center, UiFactory.PaleGold, true);
            UiFactory.Place(_regionLabel.rectTransform, 440, -8, 400, 40);
            _regionLabel.rectTransform.anchorMin = new Vector2(0.5f, 1);
            _regionLabel.rectTransform.anchorMax = new Vector2(0.5f, 1);
            _regionLabel.rectTransform.pivot = new Vector2(0.5f, 1);

            var back = UiFactory.Button(top.transform, "BackBtn", "返回主菜单",
                () => GameManager.Instance.GoTo(GameManager.SceneMainMenu),
                new Vector2(160, 40), UiFactory.DeepUltramarine);
            var bRt = back.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(1, 1);
            bRt.anchorMax = new Vector2(1, 1);
            bRt.pivot = new Vector2(1, 1);
            bRt.anchoredPosition = new Vector2(-12, -8);

            _posLabel = UiFactory.Label(canvas.transform, "PosLabel", "", 16,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            UiFactory.Place(_posLabel.rectTransform, 16, -72, 500, 28);

            var hint = UiFactory.Label(canvas.transform, "Hint",
                "WASD 移动 · Shift 疾跑 · 空格跳跃 · 拖动鼠标旋转视角 · 滚轮缩放", 16,
                TextAlignmentOptions.Left, UiFactory.SilverWhite);
            UiFactory.Place(hint.rectTransform, 16, -104, 700, 28);

            var playerObj = GameObject.Find("Player");
            if (playerObj != null) _player = playerObj.transform;
            UpdateLabels();
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= 0.25f)
            {
                _timer = 0f;
                UpdateLabels();
            }
        }

        private void UpdateLabels()
        {
            if (_player == null || _regionLabel == null) return;
            Vector3 p = _player.position;
            _regionLabel.text = WorldMapTerrain.RegionAt(p.x, p.z);
            float ground = WorldMapTerrain.SampleHeight(p.x, p.z);
            _posLabel.text = string.Format("位置 X {0:F0} · Z {1:F0} · 地面 {2:F0} m",
                p.x, p.z, ground);
        }
    }
}
