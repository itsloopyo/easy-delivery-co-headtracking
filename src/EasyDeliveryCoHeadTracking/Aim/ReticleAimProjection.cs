using UnityEngine;

namespace EasyDeliveryCoHeadTracking.Aim
{
    /// <summary>
    /// Projects the player's clean aim direction into screen-space pixel offsets,
    /// given the head-tracked camera orientation. The reticle is then drawn at the
    /// returned offset so projectiles fired along clean aim land on the crosshair
    /// even when the view is rotated by head tracking.
    /// </summary>
    internal static class ReticleAimProjection
    {
        // Floor on aim depth (camera-Z) to keep the perspective divide finite when
        // the player turns nearly 90 degrees and the aim vector lies in the screen plane.
        private const float MinAimDepth = 0.01f;

        public static Vector2 Compute(
            float yaw, float pitch, float roll,
            float verticalFovDegrees, float aspect,
            int screenWidth, int screenHeight)
        {
            float tanHalfVFov = Mathf.Tan(verticalFovDegrees * Mathf.Deg2Rad * 0.5f);
            float tanHalfHFov = tanHalfVFov * aspect;

            float halfWidth = screenWidth * 0.5f;
            float halfHeight = screenHeight * 0.5f;

            var headLocal = Quaternion.Euler(-pitch, yaw, roll);
            Vector3 aimInTracked = Quaternion.Inverse(headLocal) * Vector3.forward;

            float depth = aimInTracked.z;
            if (depth < MinAimDepth) depth = MinAimDepth;

            float offsetX = (aimInTracked.x / depth) / tanHalfHFov * halfWidth;
            float offsetY = (aimInTracked.y / depth) / tanHalfVFov * halfHeight;

            if (float.IsNaN(offsetX) || float.IsNaN(offsetY))
                return Vector2.zero;

            return new Vector2(offsetX, offsetY);
        }
    }
}
