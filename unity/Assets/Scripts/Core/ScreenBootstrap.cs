// ScreenBootstrap — attached to every scene's root GameObject (before the
// screen controller). Ensures the scene Canvas has a CanvasScaler +
// GraphicRaycaster and that an EventSystem exists, so the hand-written scene
// files only need Camera + Canvas + root (no built-in script GUIDs in YAML).
using GalgameOnline.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GalgameOnline.Core
{
    public class ScreenBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            // The scene Canvas (named "Canvas"), NOT the ToastCanvas — FindObjectOfType
            // would return the ToastCanvas (created first by the GameManager).
            var canvas = UiFactory.SceneCanvas();
            if (canvas != null)
            {
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                if (canvas.GetComponent<GraphicRaycaster>() == null)
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }
    }
}
