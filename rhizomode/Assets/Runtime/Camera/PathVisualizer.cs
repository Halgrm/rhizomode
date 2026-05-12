#nullable enable

using UnityEngine;
using UnityEngine.Splines;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// Spline を LineRenderer で 3D 空間に可視化する。
    /// 編集中は毎フレ Knot をサンプリングし直して LineRenderer を更新する。
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PathVisualizer : MonoBehaviour
    {
        private const int SampleCount = 64;
        private const float DefaultLineWidth = 0.015f;

        [SerializeField] private SplineContainer? splineContainer;

        private LineRenderer? _lineRenderer;
        private readonly Vector3[] _positions = new Vector3[SampleCount];

        public void SetTarget(SplineContainer? container)
        {
            splineContainer = container;
            Rebuild();
        }

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.positionCount = SampleCount;
            _lineRenderer.startWidth = DefaultLineWidth;
            _lineRenderer.endWidth = DefaultLineWidth;
        }

        private void LateUpdate()
        {
            // ノット移動中に追従する。コストは 64 サンプリング程度で軽い。
            Rebuild();
        }

        private void Rebuild()
        {
            if (_lineRenderer == null) return;
            if (splineContainer == null || splineContainer.Spline == null
                || splineContainer.Spline.Count < 2)
            {
                _lineRenderer.positionCount = 0;
                return;
            }

            var spline = splineContainer.Spline;
            var xform = splineContainer.transform;

            for (int i = 0; i < SampleCount; i++)
            {
                float t = i / (float)(SampleCount - 1);
                var local = spline.EvaluatePosition(t);
                _positions[i] = xform.TransformPoint(local);
            }

            _lineRenderer.positionCount = SampleCount;
            _lineRenderer.SetPositions(_positions);
        }
    }
}
