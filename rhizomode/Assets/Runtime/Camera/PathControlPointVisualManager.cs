#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// 編集モード中に PathCameraController の Spline Knot に対応する VR ハンドル
    /// (PathControlPointVisual) を生成・破棄する。Visual の移動を Spline に書き戻す。
    /// Miniature モード: 実空間 Knot 群の bbox を anchor 周囲の固定サイズ立方に縮小表示。
    /// Direct モード: 実空間にそのままハンドルを置く (旧挙動)。
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
        private const float CoordAxisRealLength = 1.5f; // 実空間で 1.5m 分の軸 (miniature で目立たせる)
        private const float CoordAxisLineWidth = 0.020f; // 2cm
        private const float CoordOriginRadiusMultiplier = 1.2f; // ハンドル半径 × 倍率 (やや大きめ)
        private const float GridRealHalfExtent = 3f; // 実空間 ±3m
        private const float GridRealCellSize = 1f;
        private const float GridLineWidth = 0.012f; // 1.2cm
        private const float GridMajorLineWidth = 0.018f; // 1.8cm

        private static readonly Color RealPathColor = new Color(0.2f, 0.7f, 1f, 1f);
        private static readonly Color MiniaturePathColor = new Color(1f, 0.5f, 0.1f, 1f);
        private static readonly Color CoordOriginColor = new Color(1f, 1f, 0.4f, 1f);
        private static readonly Color CoordXColor = new Color(1f, 0.3f, 0.3f, 1f);
        private static readonly Color CoordYColor = new Color(0.3f, 1f, 0.3f, 1f);
        private static readonly Color CoordZColor = new Color(0.3f, 0.55f, 1f, 1f);
        private static readonly Color GridColor = new Color(0.7f, 0.75f, 0.8f, 0.85f);
        private static readonly Color GridMajorColor = new Color(0.95f, 0.95f, 1f, 1f);

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

        private readonly List<PathControlPointVisual> _visuals = new();
        private PathCameraController? _target;
        private GameObject? _visualizerInstance;
        private PathVisualizer? _visualizer;

        // Miniature 用
        private LineRenderer? _miniLineRenderer;
        private GameObject? _miniLineGo;
        private Matrix4x4 _realToMini = Matrix4x4.identity;
        private Matrix4x4 _miniToReal = Matrix4x4.identity;

        // 縮小座標 (実空間の原点 + XYZ 軸を miniature 内に表示)
        private GameObject? _coordRoot;

        /// <summary>編集中かどうか。</summary>
        public bool IsEditing => _target != null;

        /// <summary>編集中のターゲット (なければ null)。</summary>
        public PathCameraController? Target => _target;

        /// <summary>
        /// 指定したパスカメラの編集モードを開始する。
        /// </summary>
        public void BeginEdit(PathCameraController target)
        {
            EndEdit();
            if (target.Spline == null || target.Spline.Spline == null) return;
            if (target.Spline.Spline.Count == 0) return;

            _target = target;
            if (mode == HandleMode.Miniature)
                ComputeMiniatureMapping(target.Spline);

            CreateVisualizer(target.Spline);
            CreateHandles(target.Spline);

            if (mode == HandleMode.Miniature)
            {
                CreateMiniatureLine();
                if (showCoordinateReference) CreateCoordinateReference();
            }
        }

        /// <summary>編集モードを終了し、ハンドル・LineRenderer を破棄する。</summary>
        public void EndEdit()
        {
            foreach (var visual in _visuals)
            {
                if (visual != null) Destroy(visual.gameObject);
            }
            _visuals.Clear();

            if (_visualizerInstance != null)
            {
                Destroy(_visualizerInstance);
                _visualizerInstance = null;
                _visualizer = null;
            }
            if (_miniLineGo != null)
            {
                Destroy(_miniLineGo);
                _miniLineGo = null;
                _miniLineRenderer = null;
            }
            if (_coordRoot != null)
            {
                Destroy(_coordRoot);
                _coordRoot = null;
            }
            _target = null;
        }

        /// <summary>当該 Collider が編集中のハンドルなら Visual を返す。</summary>
        public PathControlPointVisual? GetVisualByCollider(Collider collider)
        {
            foreach (var v in _visuals)
            {
                if (v == null) continue;
                if (v.GetComponent<Collider>() == collider) return v;
            }
            return null;
        }

        private Vector3 GetAnchorPosition()
        {
            return miniatureAnchor != null ? miniatureAnchor.position : transform.position;
        }

        private void ComputeMiniatureMapping(SplineContainer container)
        {
            var spline = container.Spline;
            var xform = container.transform;

            // 実空間 bbox を Knot から計算 (Bezier 制御点まで含めない簡易版)
            Vector3 min = Vector3.positiveInfinity;
            Vector3 max = Vector3.negativeInfinity;
            for (int i = 0; i < spline.Count; i++)
            {
                var w = xform.TransformPoint((Vector3)spline[i].Position);
                min = Vector3.Min(min, w);
                max = Vector3.Max(max, w);
            }
            // 実空間原点も bbox に含めて miniature 内に常に見えるようにする
            if (showCoordinateReference)
            {
                min = Vector3.Min(min, Vector3.zero);
                max = Vector3.Max(max, Vector3.zero);
            }
            var center = (min + max) * 0.5f;
            var size = max - min;
            var maxExtent = Mathf.Max(size.x, size.y, size.z, MinBboxExtent);
            var scale = miniatureBoxSize / maxExtent;

            var anchor = GetAnchorPosition();
            // realToMini = T(anchor) * S(scale) * T(-center)
            var t1 = Matrix4x4.Translate(-center);
            var s = Matrix4x4.Scale(Vector3.one * scale);
            var t2 = Matrix4x4.Translate(anchor);
            _realToMini = t2 * s * t1;
            _miniToReal = _realToMini.inverse;
        }

        private void CreateHandles(SplineContainer container)
        {
            var spline = container.Spline;
            var xform = container.transform;
            bool isMini = mode == HandleMode.Miniature;
            var radius = isMini ? miniatureHandleRadius : directHandleRadius;

            for (int i = 0; i < spline.Count; i++)
            {
                var knot = spline[i];
                var realWorldPos = xform.TransformPoint((Vector3)knot.Position);
                var visualPos = isMini
                    ? _realToMini.MultiplyPoint3x4(realWorldPos)
                    : realWorldPos;

                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = isMini ? $"MiniPathKnot_{i}" : $"PathKnot_{i}";
                go.transform.position = visualPos;
                go.transform.localScale = Vector3.one * (radius * 2f);

                if (handleMaterial != null)
                {
                    var renderer = go.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = handleMaterial;
                }

                var visual = go.AddComponent<PathControlPointVisual>();
                if (isMini)
                {
                    var capturedMatrix = _miniToReal;
                    visual.Initialize(i, mini => capturedMatrix.MultiplyPoint3x4(mini));
                }
                else
                {
                    visual.Initialize(i);
                }
                visual.OnPositionChanged += OnKnotMoved;
                _visuals.Add(visual);
            }
        }

        private void CreateVisualizer(SplineContainer container)
        {
            if (visualizerPrefab != null)
            {
                _visualizerInstance = Instantiate(visualizerPrefab);
            }
            else
            {
                _visualizerInstance = new GameObject("PathVisualizer", typeof(LineRenderer));
                var lr = _visualizerInstance.GetComponent<LineRenderer>();
                lr.material = CreateLineMaterial(RealPathColor);
                lr.startColor = RealPathColor;
                lr.endColor = RealPathColor;
            }
            _visualizer = _visualizerInstance.GetComponent<PathVisualizer>();
            if (_visualizer == null)
                _visualizer = _visualizerInstance.AddComponent<PathVisualizer>();
            _visualizer.SetTarget(container);
        }

        private void CreateMiniatureLine()
        {
            _miniLineGo = new GameObject("MiniaturePath", typeof(LineRenderer));
            _miniLineRenderer = _miniLineGo.GetComponent<LineRenderer>();
            _miniLineRenderer.material = miniatureLineMaterial != null
                ? miniatureLineMaterial
                : CreateLineMaterial(MiniaturePathColor);
            _miniLineRenderer.startColor = MiniaturePathColor;
            _miniLineRenderer.endColor = MiniaturePathColor;
            _miniLineRenderer.startWidth = MiniatureLineWidth;
            _miniLineRenderer.endWidth = MiniatureLineWidth;
            _miniLineRenderer.useWorldSpace = true;
            _miniLineRenderer.positionCount = 0; // LateUpdate で都度 _visuals.Count に合わせる
        }

        /// <summary>
        /// 実空間の原点と XYZ 軸を miniature 内に縮小表示する。
        /// 「この小球は実空間で原点からどのくらい離れているか」を視覚化する基準。
        /// </summary>
        private void CreateCoordinateReference()
        {
            _coordRoot = new GameObject("MiniatureCoordRef");

            // 実空間の原点 (0,0,0) を miniature 空間に写像した位置
            var miniOrigin = _realToMini.MultiplyPoint3x4(Vector3.zero);

            // 原点マーカー (黄色い球、URP/Unlit で常に明るく)
            var originGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            originGo.name = "CoordOrigin";
            originGo.transform.SetParent(_coordRoot.transform, false);
            originGo.transform.position = miniOrigin;
            originGo.transform.localScale =
                Vector3.one * (miniatureHandleRadius * CoordOriginRadiusMultiplier * 2f);
            var collider = originGo.GetComponent<Collider>();
            if (collider != null) Destroy(collider); // ハンドルと誤グラブしないように
            var renderer = originGo.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateLineMaterial(CoordOriginColor);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // 3 軸 (実空間 1m 分を miniature サイズに縮小)
            CreateCoordAxis(_coordRoot.transform, "AxisX",
                miniOrigin, Vector3.right, CoordXColor);
            CreateCoordAxis(_coordRoot.transform, "AxisY",
                miniOrigin, Vector3.up, CoordYColor);
            CreateCoordAxis(_coordRoot.transform, "AxisZ",
                miniOrigin, Vector3.forward, CoordZColor);

            // 実空間 Y=0 平面 (床) のグリッドを縮小表示
            CreateMiniatureGrid(_coordRoot.transform);
        }

        private void CreateMiniatureGrid(Transform parent)
        {
            int halfCount = Mathf.Max(1, Mathf.RoundToInt(GridRealHalfExtent / GridRealCellSize));
            for (int i = -halfCount; i <= halfCount; i++)
            {
                bool isMajor = i == 0; // 原点を通る線だけ少し強調
                var color = isMajor ? GridMajorColor : GridColor;
                var width = isMajor ? GridMajorLineWidth : GridLineWidth;
                float c = i * GridRealCellSize;
                // 実空間 Z 軸方向の線 (real X=c で固定)
                CreateGridLine(parent, $"GridZ_{i}",
                    new Vector3(c, 0, -GridRealHalfExtent),
                    new Vector3(c, 0, GridRealHalfExtent),
                    color, width);
                // 実空間 X 軸方向の線 (real Z=c で固定)
                CreateGridLine(parent, $"GridX_{i}",
                    new Vector3(-GridRealHalfExtent, 0, c),
                    new Vector3(GridRealHalfExtent, 0, c),
                    color, width);
            }
        }

        private void CreateGridLine(Transform parent, string name,
            Vector3 startReal, Vector3 endReal, Color color, float width)
        {
            var startMini = _realToMini.MultiplyPoint3x4(startReal);
            var endMini = _realToMini.MultiplyPoint3x4(endReal);

            var go = new GameObject(name, typeof(LineRenderer));
            go.transform.SetParent(parent, false);
            var lr = go.GetComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, startMini);
            lr.SetPosition(1, endMini);
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = color;
            lr.endColor = color;
            lr.material = CreateLineMaterial(color);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
        }

        private void CreateCoordAxis(Transform parent, string name,
            Vector3 miniOrigin, Vector3 realDirection, Color color)
        {
            var realEnd = realDirection * CoordAxisRealLength;
            var miniEnd = _realToMini.MultiplyPoint3x4(realEnd);

            var go = new GameObject(name, typeof(LineRenderer));
            go.transform.SetParent(parent, false);
            var lr = go.GetComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, miniOrigin);
            lr.SetPosition(1, miniEnd);
            lr.startWidth = CoordAxisLineWidth;
            lr.endWidth = CoordAxisLineWidth;
            lr.startColor = color;
            lr.endColor = color;
            lr.material = CreateLineMaterial(color);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
        }

        /// <summary>
        /// URP/Unlit シェーダは LineRenderer の頂点カラーを無視するので、
        /// マテリアルの _BaseColor / _Color を明示的に塗っておく。
        /// </summary>
        private static Material CreateLineMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            return mat;
        }

        private void OnKnotMoved(int index, Vector3 realWorldPosition)
        {
            if (_target?.Spline == null) return;
            var container = _target.Spline;
            var spline = container.Spline;
            if (index < 0 || index >= spline.Count) return;

            var localPos = container.transform.InverseTransformPoint(realWorldPosition);
            var knot = spline[index];
            knot.Position = localPos;
            spline.SetKnot(index, knot);
        }

        private void LateUpdate()
        {
            if (mode != HandleMode.Miniature) return;
            if (_miniLineRenderer == null) return;

            int n = _visuals.Count;
            if (n < 2)
            {
                _miniLineRenderer.positionCount = 0;
                return;
            }

            // 小球を順に直接つなぐ。Bezier カーブの形は無視 (実空間の青線で確認)。
            if (_miniLineRenderer.positionCount != n)
                _miniLineRenderer.positionCount = n;

            for (int i = 0; i < n; i++)
            {
                if (_visuals[i] == null) continue;
                _miniLineRenderer.SetPosition(i, _visuals[i].transform.position);
            }
        }

        private void OnDestroy()
        {
            EndEdit();
        }
    }
}
