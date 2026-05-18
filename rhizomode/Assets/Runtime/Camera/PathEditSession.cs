#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace Rhizomode.Cameras
{
    /// <summary>
    /// パス編集セッション。BeginEdit で生成された全 GameObject (ハンドル群、visualizer、
    /// miniature line、coord ref) を 1 オブジェクトに束ね、Dispose で一括破棄する。
    /// Visual の OnPositionChanged を Spline 書き戻しに繋ぐ責務もここに集約。
    /// </summary>
    public sealed class PathEditSession : IDisposable
    {
        public PathCameraController Target { get; }
        public IReadOnlyList<PathControlPointVisual> Visuals => _visuals;

        private readonly List<PathControlPointVisual> _visuals;
        private readonly GameObject _visualizerInstance;
        private readonly GameObject? _miniLineGo;
        private readonly LineRenderer? _miniLineRenderer;
        private readonly GameObject? _coordRoot;

        public PathEditSession(
            PathCameraController target,
            List<PathControlPointVisual> visuals,
            GameObject visualizerInstance,
            GameObject? miniLineGo,
            LineRenderer? miniLineRenderer,
            GameObject? coordRoot)
        {
            Target = target;
            _visuals = visuals;
            _visualizerInstance = visualizerInstance;
            _miniLineGo = miniLineGo;
            _miniLineRenderer = miniLineRenderer;
            _coordRoot = coordRoot;

            foreach (var v in _visuals)
                v.OnPositionChanged += OnKnotMoved;
        }

        /// <summary>
        /// Miniature line を visual 位置に追従させる。Direct モード時は何もしない。
        /// </summary>
        public void UpdateMiniatureLine()
        {
            if (_miniLineRenderer == null) return;
            int n = _visuals.Count;
            if (n < 2)
            {
                _miniLineRenderer.positionCount = 0;
                return;
            }
            if (_miniLineRenderer.positionCount != n)
                _miniLineRenderer.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                if (_visuals[i] == null) continue;
                _miniLineRenderer.SetPosition(i, _visuals[i].transform.position);
            }
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

        private void OnKnotMoved(int index, Vector3 realWorldPosition)
        {
            if (Target?.Spline == null) return;
            var container = Target.Spline;
            var spline = container.Spline;
            if (index < 0 || index >= spline.Count) return;

            var localPos = container.transform.InverseTransformPoint(realWorldPosition);
            var knot = spline[index];
            knot.Position = localPos;
            spline.SetKnot(index, knot);

            // F-camera-path-1 (2026-05-18): 診断ログ + Cinemachine cache invalidate。
            // Spline.SetKnot 自体は内部で Changed event を発火するはずだが、CinemachineSplineDolly が
            // ランタイム変更を pickup していない症状があり、SplineSettings を一度 default にして戻すことで
            // Spline 参照の hook を張り直す (cache の強制 refresh)。
            Debug.Log($"[PathEdit] SetKnot index={index} local={localPos} count={spline.Count}");
            Target.NotifySplineMutated();
        }

        public void Dispose()
        {
            foreach (var visual in _visuals)
            {
                if (visual == null) continue;
                visual.OnPositionChanged -= OnKnotMoved;
                UnityEngine.Object.Destroy(visual.gameObject);
            }
            _visuals.Clear();

            if (_visualizerInstance != null) UnityEngine.Object.Destroy(_visualizerInstance);
            if (_miniLineGo != null) UnityEngine.Object.Destroy(_miniLineGo);
            if (_coordRoot != null) UnityEngine.Object.Destroy(_coordRoot);
        }
    }
}
