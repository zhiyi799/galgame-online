// WorldMapPlayer — simple third-person controller for the WorldMap scene.
// WASD to move (camera-relative), Shift to run, Space to jump, drag with the
// mouse to orbit the camera, wheel to zoom. Ground height comes from
// WorldMapTerrain.SampleHeight (Unity Terrain), never from TideSong's
// WorldData. No combat system — walking / jumping / camera follow only.
using UnityEngine;

namespace GalgameOnline.WorldMap
{
    public class WorldMapPlayer : MonoBehaviour
    {
        public float walkSpeed = 8f;
        public float runSpeed = 16f;
        public float jumpSpeed = 8.5f;
        public float gravity = 22f;

        private float _vy;
        private bool _grounded = true;

        // ALS 手感：水平速度不再瞬切，按加速度/摩擦曲线趋近目标速度。
        private Vector3 _vel;
        // 落地镜头下沉（硬着陆时俯角临时 -3°，0.25 s 恢复）。
        private float _landDipT;

        // Third-person orbit camera.
        private float _yaw = 180f;   // start looking north from the plaza
        private float _pitch = 18f;
        private float _distance = 10f;
        private Vector3 _lastMousePos;
        private bool _dragging;

        // Screenshot / smoke mode: the runner drives the camera directly.
        private bool _externalCamera;
        private Vector3 _externalLookAt;

        public bool IsGrounded => _grounded;

        private void Start()
        {
            var cam = Camera.main;
            if (cam != null) cam.transform.position = transform.position + CameraOffset();
        }

        private Vector3 CameraOffset()
        {
            float dip = _landDipT > 0f ? 3f * (_landDipT / 0.25f) : 0f;
            Quaternion rot = Quaternion.Euler(_pitch - dip, _yaw, 0f);
            return rot * new Vector3(0f, 0f, -_distance);
        }

        /// <summary>Smoke-test / screenshot mode: park the camera at an
        /// absolute viewpoint and stop following the player.</summary>
        public void SetExternalView(Vector3 position, Vector3 lookAt)
        {
            _externalCamera = true;
            _externalLookAt = lookAt;
            var cam = Camera.main;
            if (cam == null) return;
            cam.transform.position = position;
            cam.transform.LookAt(lookAt);
        }

        public void ClearExternalView()
        {
            _externalCamera = false;
        }

        private void Update()
        {
            if (_landDipT > 0f) _landDipT = Mathf.Max(0f, _landDipT - Time.deltaTime);
            HandleCameraInput();
            if (_externalCamera) return;

            float ix = Input.GetAxis("Horizontal");
            float iz = Input.GetAxis("Vertical");
            bool run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // Camera-relative movement on the ground plane.
            Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 move = yawRot * new Vector3(ix, 0f, iz);
            if (move.sqrMagnitude > 1f) move.Normalize();
            float speed = run ? runSpeed : walkSpeed;
            // ALS：速度按加速度/摩擦曲线趋近目标值（起步/刹车有惯性，不再瞬切）。
            _vel = GalgameOnline.Core.ALSLocomotion.Accelerate(_vel, move * speed, Time.deltaTime);
            Vector3 pos = transform.position;
            pos += _vel * Time.deltaTime;

            // Keep the player inside the continent bounds (with a margin so
            // they cannot walk off the world edge into the void).
            pos.x = Mathf.Clamp(pos.x, -980f, 3980f);
            pos.z = Mathf.Clamp(pos.z, -1118f, 2256f);

            float ground = WorldMapTerrain.SampleHeight(pos.x, pos.z);
            bool onTerrain = WorldMapTerrain.IsOnTerrain(pos.x, pos.z);

            if (_grounded && Input.GetKeyDown(KeyCode.Space) && onTerrain)
            {
                _vy = jumpSpeed;
                _grounded = false;
            }

            if (!_grounded)
            {
                _vy -= gravity * Time.deltaTime;
                pos.y += _vy * Time.deltaTime;
                if (onTerrain && pos.y <= ground + 0.05f)
                {
                    // ALS 落地：下落速度 > 6 m/s 时镜头下沉一下（无身体，不做假翻滚）。
                    if (-_vy > 6f) _landDipT = 0.25f;
                    pos.y = ground;
                    _vy = 0f;
                    _grounded = true;
                }
            }
            else
            {
                // Stick to the terrain while walking (covers slopes).
                pos.y = onTerrain ? ground : pos.y;
            }

            // Safety net: never fall through the world (mirrors the TideSong
            // fix — terrain colliders can lag a frame behind the player).
            if (onTerrain && pos.y < ground - 2f) pos.y = ground;

            transform.position = pos;

            // Face the movement direction.
            if (move.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * 10f);
            }
        }

        private void HandleCameraInput()
        {
            // Mouse wheel zoom.
            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) > 0.001f)
                _distance = Mathf.Clamp(_distance - wheel * 6f, 5f, 24f);

            // Left-drag orbit.
            if (Input.GetMouseButtonDown(0))
            {
                _dragging = true;
                _lastMousePos = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(0)) _dragging = false;
            if (_dragging)
            {
                Vector3 d = Input.mousePosition - _lastMousePos;
                _lastMousePos = Input.mousePosition;
                _yaw += d.x * 0.35f;
                _pitch = Mathf.Clamp(_pitch - d.y * 0.25f, -10f, 60f);
            }
        }

        private void LateUpdate()
        {
            if (_externalCamera) return;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 target = transform.position + new Vector3(0f, 1.8f, 0f);
            cam.transform.position = target + CameraOffset();
            cam.transform.LookAt(target);
        }
    }
}
