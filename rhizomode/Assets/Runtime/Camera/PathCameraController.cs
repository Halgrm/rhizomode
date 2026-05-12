#nullable enable

using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Splines;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// Spline 上を Progress(0..1) で走るパスカメラ。
    /// CameraManagerPanel から typed API 経由でパラメータを受け取る。
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public class PathCameraController : MonoBehaviour
    {
        [SerializeField] private CinemachineSplineDolly? splineDolly;
        [SerializeField] private SplineContainer? splineContainer;
        [SerializeField] private string displayName = "Path Camera";

        private CinemachineCamera? _camera;

        public string DisplayName => displayName;
        public SplineContainer? Spline => splineContainer;
        public CinemachineCamera? CinemachineCamera => _camera;

        /// <summary>現在の Progress 値 (0..1)。SplineDolly.CameraPosition を読む。</summary>
        public float Progress => splineDolly != null ? splineDolly.CameraPosition : 0f;

        private void Awake()
        {
            _camera = GetComponent<CinemachineCamera>();
            if (splineDolly == null)
                splineDolly = GetComponent<CinemachineSplineDolly>();
        }

        public void SetProgress(float value)
        {
            if (splineDolly == null) return;
            splineDolly.CameraPosition = Mathf.Clamp01(value);
        }

        public void SetFov(float value)
        {
            if (_camera == null) return;
            var lens = _camera.Lens;
            lens.FieldOfView = Mathf.Clamp(value, 1f, 179f);
            _camera.Lens = lens;
        }

        public void SetDutch(float degrees)
        {
            if (_camera == null) return;
            var lens = _camera.Lens;
            lens.Dutch = degrees;
            _camera.Lens = lens;
        }

        public void SetPriority(int priority)
        {
            if (_camera == null) return;
            _camera.Priority = priority;
        }
    }
}
