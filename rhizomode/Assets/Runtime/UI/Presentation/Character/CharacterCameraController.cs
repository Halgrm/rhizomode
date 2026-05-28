#nullable enable

using UnityEngine;

namespace Rhizomode.UI.Presentation.Character
{
    public sealed class CharacterCameraController : MonoBehaviour
    {
        [SerializeField] private Camera? cam;
        [SerializeField] private Transform? target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.6f, -2.5f);
        [SerializeField, Min(0.1f)] private float damping = 5f;

        private Vector3 _smoothedPos;
        private bool _initialized;

        private void LateUpdate()
        {
            if (cam == null || target == null)
            {
                return;
            }

            Vector3 desired = target.position + offset;
            if (!_initialized)
            {
                _smoothedPos = desired;
                _initialized = true;
            }

            float coefficient = 1f - Mathf.Exp(-damping * Time.deltaTime);
            _smoothedPos = Vector3.Lerp(_smoothedPos, desired, coefficient);
            cam.transform.position = _smoothedPos;
            cam.transform.LookAt(target.position + Vector3.up);
        }
    }
}
