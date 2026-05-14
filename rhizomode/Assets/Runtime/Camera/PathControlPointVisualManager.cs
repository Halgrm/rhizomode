#nullable enable

using UnityEngine;
using UnityEngine.Splines;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// 編集モード中に PathCameraController の Spline Knot に対応する VR ハンドル
    /// (PathControlPointVisual) を生成・破棄する MonoBehaviour facade。
    /// Miniature モード: 実空間 Knot 群の bbox を anchor 周囲の固定サイズ立方に縮小表示。
    /// Direct モード: 実空間にそのままハンドルを置く (旧挙動)。
    /// 実生成は <see cref="MiniaturePathMapper"/> / <see cref="PathHandleFactory"/> /
    /// <see cref="CoordinateReferenceRenderer"/> に委譲し、編集中の state は
    /// <see cref="PathEditSession"/> に集約する。
    /// </summary>
    public class PathControlPointVisualManager : MonoBehaviour
    {
        public enum HandleMode
        {
            Miniature = 0,
            Direct = 1
        }

        private const float DefaultMiniatureBoxSize = 1.0f;
        private const float DefaultMiniatureHandleRadius = 0.03f;
        private const float DefaultDirectHandleRadius = 0.06f;
        private const float MiniatureLineWidth = 0.012f;
        private const float MinBboxExtent = 0.001f;

        private static readonly Color RealPathColor = new Color(0.2f, 0.7f, 1f, 1f);
        private static readonly Color MiniaturePathColor = new Color(1f, 0.5f, 0.1f, 1f);

        [SerializeField] private HandleMode mode = HandleMode.Miniature;
        [Tooltip("Miniature の中心となる world Transform。未指定なら自身の Transform")]
        [SerializeField] private Transform? miniatureAnchor;
        [SerializeField] private float miniatureBoxSize = DefaultMiniatureBoxSize;
        [SerializeField] private float miniatureHandleRadius = DefaultMiniatureHandleRadius;
        [SerializeField] private float directHandleRadius = DefaultDirectHandleRadius;
        [SerializeField] private Material? handleMaterial;
        [SerializeField] private Material? miniatureLineMaterial;
        [SerializeField] private GameObject? visualizerPrefab;
        [Tooltip("Miniature 編集中、実空間の原点と XYZ 軸を縮小座標として表示する")]
        [SerializeField] private bool showCoordinateReference = true;

        private PathEditSession? _session;

        /// <summary>編集中かどうか。</summary>
        public bool IsEditing => _session != null;

        /// <summary>編集中のターゲット (なければ null)。</summary>
        public PathCameraController? Target => _session?.Target;

        /// <summary>
        /// 指定したパスカメラの編集モードを開始する。
        /// </summary>
        public void BeginEdit(PathCameraController target)
        {
            EndEdit();
            if (target.Spline == null || target.Spline.Spline == null) return;
            if (target.Spline.Spline.Count == 0) return;

            bool isMini = mode == HandleMode.Miniature;
            MiniaturePathMapper? mapper = isMini
                ? new MiniaturePathMapper(
                    target.Spline,
                    GetAnchorPosition(),
                    miniatureBoxSize,
                    showCoordinateReference,
                    MinBboxExtent)
                : null;

            var radius = isMini ? miniatureHandleRadius : directHandleRadius;
            var handleFactory = new PathHandleFactory(handleMaterial);
            var visuals = handleFactory.Create(target.Spline, mapper, radius);

            var visualizerInstance = CreateVisualizer(target.Spline);
            var (miniLineGo, miniLineRenderer) = isMini ? CreateMiniatureLine() : (null, null);
            GameObject? coordRoot = (isMini && showCoordinateReference)
                ? new CoordinateReferenceRenderer().Render(mapper!, miniatureHandleRadius)
                : null;

            _session = new PathEditSession(
                target, visuals, visualizerInstance, miniLineGo, miniLineRenderer, coordRoot);
        }

        /// <summary>編集モードを終了し、ハンドル・LineRenderer を破棄する。</summary>
        public void EndEdit()
        {
            _session?.Dispose();
            _session = null;
        }

        /// <summary>当該 Collider が編集中のハンドルなら Visual を返す。</summary>
        public PathControlPointVisual? GetVisualByCollider(Collider collider)
        {
            return _session?.GetVisualByCollider(collider);
        }

        private Vector3 GetAnchorPosition()
        {
            return miniatureAnchor != null ? miniatureAnchor.position : transform.position;
        }

        private GameObject CreateVisualizer(SplineContainer container)
        {
            GameObject instance;
            if (visualizerPrefab != null)
            {
                instance = Instantiate(visualizerPrefab);
            }
            else
            {
                instance = new GameObject("PathVisualizer", typeof(LineRenderer));
                var lr = instance.GetComponent<LineRenderer>();
                lr.material = CoordinateReferenceRenderer.CreateLineMaterial(RealPathColor);
                lr.startColor = RealPathColor;
                lr.endColor = RealPathColor;
            }
            var visualizer = instance.GetComponent<PathVisualizer>();
            if (visualizer == null) visualizer = instance.AddComponent<PathVisualizer>();
            visualizer.SetTarget(container);
            return instance;
        }

        private (GameObject go, LineRenderer lr) CreateMiniatureLine()
        {
            var go = new GameObject("MiniaturePath", typeof(LineRenderer));
            var lr = go.GetComponent<LineRenderer>();
            lr.material = miniatureLineMaterial != null
                ? miniatureLineMaterial
                : CoordinateReferenceRenderer.CreateLineMaterial(MiniaturePathColor);
            lr.startColor = MiniaturePathColor;
            lr.endColor = MiniaturePathColor;
            lr.startWidth = MiniatureLineWidth;
            lr.endWidth = MiniatureLineWidth;
            lr.useWorldSpace = true;
            lr.positionCount = 0; // session.UpdateMiniatureLine() で都度合わせる
            return (go, lr);
        }

        private void LateUpdate()
        {
            _session?.UpdateMiniatureLine();
        }

        private void OnDestroy()
        {
            EndEdit();
        }
    }
}
