#nullable enable

using UnityEngine;

namespace Rhizomode.UI.Presentation.Character
{
    /// <summary>
    /// Follows a target (typically the avatar head bone) from behind at a fixed
    /// yaw-relative offset and renders it to a RenderTexture (3rd-person view).
    /// </summary>
    /// <remarks>
    /// <para>The offset is applied in the target's yaw frame, so the camera stays
    /// behind the avatar regardless of which way it turns instead of being pinned to
    /// a world direction.</para>
    ///
    /// <para><see cref="ExecuteAlways"/> keeps the camera positioned in Edit mode too,
    /// so it never sits at its spawn origin embedded inside the avatar mesh.</para>
    /// </remarks>
    [ExecuteAlways]
    public sealed class CharacterCameraController : MonoBehaviour
    {
        [SerializeField] private Camera? cam;
        [SerializeField] private Transform? target;
        [Tooltip("Offset in the target's yaw frame: x=side, y=up, z=back(-)/front(+).")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 0.3f, -2.0f);
        [Tooltip("Height above the target the camera aims at.")]
        [SerializeField] private float lookHeightOffset = 0f;
        [SerializeField, Min(0.1f)] private float damping = 8f;
        [Tooltip("Camera never gets closer to the target than this (m), preventing mesh clipping.")]
        [SerializeField, Min(0.2f)] private float minDistance = 0.6f;

        private Vector3 _smoothedPos;
        private bool _initialized;

        private void LateUpdate()
        {
            if (cam == null || target == null)
            {
                return;
            }

            Vector3 desired = ComputeDesiredPosition();

            // Snap on first frame (and in Edit mode every frame so it never lags into the mesh).
            bool instant = !_initialized || !Application.isPlaying;
            if (instant)
            {
                _smoothedPos = desired;
                _initialized = true;
            }
            else
            {
                float coefficient = 1f - Mathf.Exp(-damping * Time.deltaTime);
                _smoothedPos = Vector3.Lerp(_smoothedPos, desired, coefficient);
            }

            cam.transform.position = _smoothedPos;
            cam.transform.LookAt(target.position + Vector3.up * lookHeightOffset);
        }

        private Vector3 ComputeDesiredPosition()
        {
            // Yaw-only frame so the camera orbits with the avatar but stays level.
            float yaw = target!.eulerAngles.y;
            Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 desired = target.position + yawRotation * offset;

            // Enforce a minimum distance so the camera can't dive into the mesh.
            Vector3 toCamera = desired - target.position;
            if (toCamera.sqrMagnitude < minDistance * minDistance)
            {
                toCamera = toCamera.sqrMagnitude > 1e-4f
                    ? toCamera.normalized * minDistance
                    : -target.forward * minDistance;
                desired = target.position + toCamera;
            }

            return desired;
        }
    }
}
