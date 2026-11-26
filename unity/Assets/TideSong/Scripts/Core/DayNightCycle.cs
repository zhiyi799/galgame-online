using UnityEngine;

namespace TideSong
{
    /// <summary>昼夜：太阳角度、天空颜色、雾、以及影响光照强度的环境变化。</summary>
    public class DayNightCycle : MonoBehaviour
    {
        public float dayLength = 360f;     // 一天多少秒
        public float timeOfDay = 0.32f;    // 0=午夜 0.25=日出 0.5=正午 0.75=日落

        Light sun;
        Light fill;
        Material sky;
        Transform sunT;

        static readonly Color DayTop = new Color(0.08f, 0.17f, 0.40f);
        static readonly Color DayHorizon = new Color(0.65f, 0.75f, 0.90f);
        static readonly Color DuskTop = new Color(0.28f, 0.22f, 0.45f);
        static readonly Color DuskHorizon = new Color(0.90f, 0.70f, 0.45f);
        static readonly Color NightTop = new Color(0.02f, 0.04f, 0.10f);
        static readonly Color NightHorizon = new Color(0.08f, 0.12f, 0.24f);

        public float SunHeight { get; private set; }

        public void Setup(Light sunLight, Light fillLight, Material skyMat, Transform sunTransform)
        {
            sun = sunLight;
            fill = fillLight;
            sky = skyMat;
            sunT = sunTransform;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            Apply();
        }

        void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.Paused) return;
            timeOfDay += Time.deltaTime / Mathf.Max(30f, dayLength);
            if (timeOfDay >= 1f) timeOfDay -= 1f;
            Apply();
        }

        void Apply()
        {
            float ang = (timeOfDay - 0.25f) * 360f;
            if (sunT != null) sunT.rotation = Quaternion.Euler(ang, 34f, 0f);

            float rad = ang * Mathf.Deg2Rad;
            float h = Mathf.Sin(rad);              // 太阳高度 -1..1
            SunHeight = h;

            float day = Mathf.Clamp01(h * 2.2f);            // 白天强度
            float dusk = Mathf.Clamp01(1f - Mathf.Abs(h) * 4.5f); // 地平线附近
            float night = Mathf.Clamp01(-h * 2.5f);

            if (sun != null)
            {
                sun.intensity = Mathf.Lerp(0.02f, 1.22f, day);
                Color warm = new Color(1f, 0.55f, 0.32f);
                Color noon = new Color(1f, 0.97f, 0.90f);
                sun.color = Color.Lerp(warm, noon, Mathf.Clamp01(h * 1.8f));
            }
            if (fill != null)
            {
                fill.intensity = Mathf.Lerp(0.55f, 0.18f, day);
                fill.color = Color.Lerp(new Color(0.45f, 0.62f, 1f), new Color(0.62f, 0.74f, 1f), day);
            }

            Color top = Color.Lerp(NightTop, DayTop, day);
            Color hor = Color.Lerp(NightHorizon, DayHorizon, day);
            top = Color.Lerp(top, DuskTop, dusk * 0.75f);
            hor = Color.Lerp(hor, DuskHorizon, dusk * 0.9f);

            if (sky != null)
            {
                if (sky.HasProperty("_TopColor")) sky.SetColor("_TopColor", top);
                if (sky.HasProperty("_HorizonColor")) sky.SetColor("_HorizonColor", hor);
                if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", Color.Lerp(hor * 0.5f, hor, 0.4f));
                if (sky.HasProperty("_SunColor")) sky.SetColor("_SunColor", Color.Lerp(new Color(1f, 0.7f, 0.45f), new Color(1f, 0.97f, 0.86f), day));
                if (sunT != null && sky.HasProperty("_SunDir")) sky.SetVector("_SunDir", -sunT.forward);
            }

            RenderSettings.fogColor = hor;
            // 地图修复（2026-11-26）：雾只做远景分层——近处/中景保持饱和清晰，
            // 只有远山才被雾盖住。旧密度（白天 0.0022）100 m 外就开始发灰，整个
            // 近景都蒙了一层；现在白天 0.0014（100 m 处 98% 清晰，420 m 处仍有
            // 71%），夜里 0.0032（100 m 处 90% 清晰）。
            RenderSettings.fogDensity = Mathf.Lerp(0.0032f, 0.0014f, day);
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.12f, 0.16f, 0.28f), new Color(0.45f, 0.58f, 0.85f), day);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.08f, 0.10f, 0.16f), new Color(0.44f, 0.50f, 0.52f), day);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.04f, 0.05f, 0.07f), new Color(0.26f, 0.30f, 0.24f), day);
            RenderSettings.ambientIntensity = 1f;

            if (CameraCached != null) CameraCached.backgroundColor = hor;
        }

        static Camera CameraCached
        {
            get { return Camera.main; }
        }

        public string ClockText()
        {
            int totalMinutes = Mathf.FloorToInt(timeOfDay * 24f * 60f);
            int hh = (totalMinutes / 60) % 24;
            int mm = totalMinutes % 60;
            return hh.ToString("00") + ":" + mm.ToString("00");
        }
    }

    /// <summary>第三人称跟随相机：鼠标 / 触屏双端，带地形避让与屏震。</summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }

        public Transform target;
        public float distance = 5.6f;
        public float heightOffset = 1.55f;
        public float yaw = 0f;
        public float pitch = 16f;
        public float sensitivity = 3.2f;
        public float touchSensitivity = 0.16f;

        public Vector2 LookDelta;      // 触屏拖动写入
        public bool TouchLookEnabled;

        Camera cam;
        float shake;
        float currentDistance;
        float smoothYaw;
        float smoothPitch;

        public Vector3 FlatForward
        {
            get
            {
                Vector3 f = transform.forward;
                f.y = 0f;
                return f.sqrMagnitude < 0.001f ? Vector3.forward : f.normalized;
            }
        }

        public Vector3 FlatRight
        {
            get
            {
                Vector3 r = transform.right;
                r.y = 0f;
                return r.sqrMagnitude < 0.001f ? Vector3.right : r.normalized;
            }
        }

        public void Setup(Transform t)
        {
            Instance = this;
            target = t;
            cam = GetComponent<Camera>();
            currentDistance = distance;
            smoothYaw = yaw;
            smoothPitch = pitch;
            Cursor.lockState = InputHub.Mobile ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = InputHub.Mobile;
        }

        public void Shake(float amount) { shake = Mathf.Min(1f, shake + amount); }

        void LateUpdate()
        {
            if (target == null) return;
            bool paused = GameManager.Instance != null && GameManager.Instance.Paused;

            if (!paused)
            {
                if (!InputHub.Mobile && Cursor.lockState == CursorLockMode.Locked)
                {
                    yaw += Input.GetAxis("Mouse X") * sensitivity;
                    pitch -= Input.GetAxis("Mouse Y") * sensitivity;
                }
                if (TouchLookEnabled && LookDelta.sqrMagnitude > 0f)
                {
                    yaw += LookDelta.x * touchSensitivity;
                    pitch -= LookDelta.y * touchSensitivity;
                    LookDelta = Vector2.zero;
                }
            }

            pitch = Mathf.Clamp(pitch, -28f, 72f);
            smoothYaw = Mathf.LerpAngle(smoothYaw, yaw, 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime));
            smoothPitch = Mathf.Lerp(smoothPitch, pitch, 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime));

            Quaternion rot = Quaternion.Euler(smoothPitch, smoothYaw, 0f);
            Vector3 pivot = target.position + Vector3.up * heightOffset;

            float want = distance;
            Vector3 dir = rot * Vector3.back;
            RaycastHit hit;
            if (Physics.SphereCast(pivot, 0.32f, dir, out hit, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform != target && !hit.transform.IsChildOf(target))
                    want = Mathf.Max(1.4f, hit.distance - 0.15f);
            }
            currentDistance = Mathf.Lerp(currentDistance, want, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));

            Vector3 pos = pivot + dir * currentDistance;

            // 别钻到地下
            float groundY = WorldData.HeightAt(pos.x, pos.z) + 0.6f;
            if (pos.y < groundY) pos.y = groundY;
            if (pos.y < WorldData.WaterLevel + 0.4f && WorldData.HeightAt(pos.x, pos.z) < WorldData.WaterLevel)
                pos.y = Mathf.Max(pos.y, WorldData.WaterLevel + 0.5f);

            transform.position = pos;

            if (shake > 0f)
            {
                float s = shake * 0.22f;
                transform.position += new Vector3(
                    (Mathf.PerlinNoise(Time.unscaledTime * 40f, 0f) - 0.5f) * s,
                    (Mathf.PerlinNoise(0f, Time.unscaledTime * 40f) - 0.5f) * s,
                    (Mathf.PerlinNoise(Time.unscaledTime * 33f, 3f) - 0.5f) * s);
                shake = Mathf.Max(0f, shake - Time.unscaledDeltaTime * 2.2f);
            }

            transform.rotation = rot;
        }
    }
}
