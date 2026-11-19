// ALSLocomotion — ALS 移动手感的纯数学 helper（只学方法，不抄 ALS 源码）。
// ALS 模型：速度按加速度/摩擦曲线趋近目标速度，而不是瞬切；无输入时摩擦刹车。
// 曲线取 ALS MovementModel 的形状简化：加速 20→7.5 m/s²，摩擦 5→0.5，
// 反向时用刹车减速度 15 m/s²。WorldMapPlayer 与 TideSong PlayerCharacter 共用。
using UnityEngine;

namespace GalgameOnline.Core
{
    public static class ALSLocomotion
    {
        // 最大加速度随速度下降（起步猛、高速缓），与 ALS maxAcceleration 曲线同形。
        public static float MaxAcceleration(float speed)
        {
            return Mathf.Lerp(20f, 7.5f, Mathf.Clamp01(speed / 10f));
        }

        // 地面摩擦随速度下降（低速停得快、高速滑得远），与 ALS groundFriction 曲线同形。
        public static float GroundFriction(float speed)
        {
            return Mathf.Lerp(5f, 0.5f, Mathf.Clamp01(speed / 10f));
        }

        // 把当前水平速度推向目标速度：有输入时按加速度趋近（反向按刹车减速度），
        // 无输入时按摩擦减速到零。y 分量原样保留（重力/跳跃由调用方处理）。
        public static Vector3 Accelerate(Vector3 current, Vector3 desired, float dt)
        {
            Vector3 curH = new Vector3(current.x, 0f, current.z);
            Vector3 desH = new Vector3(desired.x, 0f, desired.z);
            float speed = curH.magnitude;
            if (desH.sqrMagnitude > 0.001f)
            {
                float maxDelta = Vector3.Dot(curH, desH) < 0f
                    ? 15f * dt          // 反向：刹车减速度
                    : MaxAcceleration(speed) * dt;
                curH = Vector3.MoveTowards(curH, desH, maxDelta);
            }
            else
            {
                curH = Vector3.MoveTowards(curH, Vector3.zero, GroundFriction(speed) * dt);
            }
            return new Vector3(curH.x, current.y, curH.z);
        }

        // 落地判定：ALS breakfall（下落速度 > 7 m/s 且有移动输入 → 翻滚卸力）。
        public static bool ShouldRoll(float fallSpeed, bool hasInput)
        {
            return fallSpeed > 7f && hasInput;
        }

        // 攀爬探测（ALS ALSMantle 的探测法简化）：胸高前向球形探测找障碍，
        // 再从障碍上方往下找台面。台面高度在 [minLedge, maxLedge] 内且上方
        // 可站立时返回台面位置；否则 false。调用方做抬升动画。
        // 注意：从胸前 0.45 m 外起测，并排除自身的碰撞体，避免探到自己。
        public static bool TryFindLedge(Transform actor, Vector3 moveDir,
            float maxLedge, float minLedge, float reach, out Vector3 ledgeTop)
        {
            ledgeTop = Vector3.zero;
            if (moveDir.sqrMagnitude < 0.001f) return false;
            Vector3 fwd = moveDir.normalized;
            Vector3 chest = actor.position + Vector3.up * 1.2f + fwd * 0.45f;
            if (!Physics.SphereCast(chest, 0.3f, fwd, out RaycastHit hit, reach)) return false;
            if (hit.collider != null && hit.collider.transform.IsChildOf(actor)) return false;
            // 障碍面必须是陡面（法线接近水平），否则只是在上陡坡，不触发攀爬。
            if (hit.normal.y > 0.5f) return false;
            Vector3 above = hit.point + Vector3.up * (maxLedge + 0.5f);
            if (!Physics.Raycast(above, Vector3.down, out RaycastHit down, maxLedge + 1.0f)) return false;
            if (down.collider != null && down.collider.transform.IsChildOf(actor)) return false;
            // 台面必须够平才能站。
            if (down.normal.y < 0.7f) return false;
            float ledgeH = down.point.y - actor.position.y;
            if (ledgeH < minLedge || ledgeH > maxLedge) return false;
            if (Physics.CheckSphere(down.point + Vector3.up * 0.9f, 0.4f)) return false;
            ledgeTop = down.point;
            return true;
        }
    }
}
