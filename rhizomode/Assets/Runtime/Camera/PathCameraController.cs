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
    public class PathCameraController : MonoBehaviour, ICameraMotion
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

        // --- ICameraMotion: パネルの汎用 Motion 駆動 (グラフ Float 購読) はこの実装を通る ---
        string ICameraMotion.MotionLabel => "Progress";
        float ICameraMotion.Drive => Progress;
        void ICameraMotion.SetDrive(float value) => SetProgress(value);

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

        /// <summary>
        /// ランタイム編集 (PathControlPointVisual 経由の <c>Spline.SetKnot</c>) の直後に呼び、
        /// <see cref="CinemachineSplineDolly"/> 内部の Spline cache を invalidate する。
        /// </summary>
        /// <remarks>
        /// 症状: Spline.SetKnot で knot 位置を書き戻しても CM_PathDolly が古い経路で評価し続ける
        /// (PathVisualizer の LineRenderer は毎フレ EvaluatePosition で追従するが、Cinemachine 側だけ
        /// 動かない)。原因は CinemachineSplineDolly が Spline.Changed event を立ち上げ時しか hook
        /// していない or 内部キャッシュをランタイム refresh していないこと。SplineContainer 参照を
        /// 一度切り戻すことで Cinemachine 側の cache invalidate を強制する。
        /// </remarks>
        public void NotifySplineMutated()
        {
            if (splineDolly == null || splineContainer == null) return;

            // F-camera-path-2 (2026-05-18): SplineSettings = default → saved 戻しでは
            // CinemachineSplineDolly 内部 cache が refresh されない (実機ログで confirm 済)。
            // SplineContainer の Spline プロパティ自身を一旦 null → 戻すと、
            // SplineContainer 内部 onSplineChanged event が発火し、CinemachineSplineDolly が
            // listen している場合は cache が invalidate される。
            // 加えて splineDolly.enabled を toggle して OnEnable hook 再張りも保険として行う。
            var spline = splineContainer.Spline;
            var pos = splineDolly.CameraPosition;
            splineDolly.enabled = false;
            splineDolly.enabled = true;
            splineDolly.CameraPosition = pos; // toggle で 0 にリセットされても直近値に戻す
        }
    }
}
