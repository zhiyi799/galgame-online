// Toast — a small transient message bar, always on top (own Canvas, sortOrder 999).
// Lives on the GameManager GameObject, so it works from any scene.
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GalgameOnline.UI;

namespace GalgameOnline.Core
{
    public class Toast : MonoBehaviour
    {
        private static Toast _instance;
        private GameObject _panel;
        private TextMeshProUGUI _label;
        private Coroutine _hideRoutine;

        public static void Show(string message) => Show(message, 2.5f);

        public static void Show(string message, float seconds)
        {
            if (_instance == null) return;
            _instance.ShowInternal(message, seconds);
        }

        private void Awake()
        {
            _instance = this;
            var canvasGo = new GameObject("ToastCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            _panel = UiFactory.Panel(canvasGo.transform, "ToastPanel",
                new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.05f), new Vector2(700, 44),
                UiFactory.Glass(0.92f));
            _label = UiFactory.Label(_panel.transform, "ToastText", "",
                20, TextAlignmentOptions.Center, UiFactory.MoonWhite);
            UiFactory.Stretch(_label.rectTransform);
            _panel.SetActive(false);
        }

        private void ShowInternal(string message, float seconds)
        {
            _label.text = message;
            _panel.SetActive(true);
            if (_hideRoutine != null) StopCoroutine(_hideRoutine);
            _hideRoutine = StartCoroutine(HideAfter(seconds));
        }

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _panel.SetActive(false);
        }
    }
}
